using Ymm4DanmakuPlugin.Core.Audio;
using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Parameters;

namespace Ymm4DanmakuPlugin.Audio;

/// <summary>
/// 弾幕アイテムの登録情報（音声側へ返すスナップショット）。
/// </summary>
public sealed class DanmakuChannelRegistration
{
    public object SourceKey { get; }
    public WeakReference<DanmakuShapeParameter> ParameterRef { get; }
    public int Fps { get; }
    public int TotalFrame { get; }
    public double TimelineStartSeconds { get; }
    public double TimelineDurationSeconds { get; }
    public int Layer { get; }
    public long TouchTick { get; }
    public int LastItemFrame { get; }

    internal DanmakuChannelRegistration(
        object sourceKey,
        WeakReference<DanmakuShapeParameter> parameterRef,
        int fps,
        int totalFrame,
        double timelineStartSeconds,
        double timelineDurationSeconds,
        int layer,
        long touchTick,
        int lastItemFrame)
    {
        SourceKey = sourceKey;
        ParameterRef = parameterRef;
        Fps = fps;
        TotalFrame = totalFrame;
        TimelineStartSeconds = timelineStartSeconds;
        TimelineDurationSeconds = timelineDurationSeconds;
        Layer = layer;
        TouchTick = touchTick;
        LastItemFrame = lastItemFrame;
    }
}

/// <summary>
/// 映像側の弾幕アイテムと音声側の効果音エフェクトを結び付けるための連絡簿。
/// パラメータは図形ソースの生成より先に存在するので、プレビュー先読みでも次の弾幕を予約できる。
/// キーは <see cref="DanmakuShapeParameter"/>（タイムラインアイテムと寿命が同じ）であり、
/// 描画ソースの破棄では消さない。
/// </summary>
public static class DanmakuChannelBus
{
    private static readonly object Gate = new();
    private static readonly List<Entry> Entries = [];
    private static readonly List<AudioClaim> AudioClaims = [];
    private static long nextTouchTick;
    private static long nextRegistrationOrder;

    /// <summary>登録状態の変更バージョン番号。登録・更新・削除のたびにインクリメントされる。</summary>
    public static int Version { get; private set; }

    /// <summary>
    /// タイムライン上の弾幕パラメータを、映像ソース生成前に覚えておく。
    /// プレビューが次の音声を先に作り始めても、弾幕2を弾幕1と取り違えないようにする。
    /// </summary>
    public static void Remember(DanmakuShapeParameter parameter)
    {
        if (parameter is null) return;

        lock (Gate)
        {
            if (FindByParameter(parameter) is not null) return;
            Entries.Add(new Entry(
                parameter,
                fps: 60,
                totalFrame: 1,
                timelineStartSeconds: 0,
                timelineDurationSeconds: 0,
                layer: 0,
                registrationOrder: ++nextRegistrationOrder));
            Version++;
        }
    }

    /// <summary>弾幕アイテムを登録・更新する。存在の連絡であり、再生位置の担当切替は <see cref="Touch"/> が行う。</summary>
    public static void Register(
        DanmakuShapeParameter parameter,
        object? sourceKey = null,
        int fps = 60,
        int totalFrame = 1,
        double timelineStartSeconds = 0,
        double timelineDurationSeconds = 0,
        int layer = 0,
        int itemFrame = 0)
    {
        lock (Gate)
        {
            _ = sourceKey;
            fps = fps > 0 ? fps : 60;
            totalFrame = Math.Max(1, totalFrame);
            itemFrame = Math.Clamp(itemFrame, 0, Math.Max(0, totalFrame - 1));

            var entry = FindByParameter(parameter);
            if (entry is not null)
            {
                var changed = entry.Fps != fps || entry.TotalFrame != totalFrame;
                entry.Fps = fps;
                entry.TotalFrame = totalFrame;
                entry.LastItemFrame = itemFrame;
                if (timelineDurationSeconds > 0)
                {
                    changed |= Math.Abs(entry.TimelineStartSeconds - timelineStartSeconds) > 1e-9
                        || Math.Abs(entry.TimelineDurationSeconds - timelineDurationSeconds) > 1e-9
                        || entry.Layer != layer;
                    entry.TimelineStartSeconds = timelineStartSeconds;
                    entry.TimelineDurationSeconds = timelineDurationSeconds;
                    entry.Layer = layer;
                }

                if (changed) Version++;
            }
            else
            {
                Entries.Add(new Entry(
                    parameter,
                    fps,
                    totalFrame,
                    timelineStartSeconds,
                    timelineDurationSeconds,
                    layer,
                    ++nextRegistrationOrder,
                    itemFrame));
                Version++;
            }
        }
    }

    /// <summary>
    /// 映像側が「今この瞬間、自分が再生位置にある」と連絡簿へ伝える。
    /// Version は増やさない（連続再生中の音声再構築を避ける）。
    /// </summary>
    public static void Touch(object sourceKey, int itemFrame = 0, int totalFrame = 0)
    {
        if (sourceKey is null) return;

        lock (Gate)
        {
            var entry = FindByKey(sourceKey);
            if (entry is null) return;
            if (!entry.ParameterRef.TryGetTarget(out _)) return;

            if (totalFrame > 0) entry.TotalFrame = totalFrame;
            if (itemFrame >= 0) entry.LastItemFrame = itemFrame;
            entry.TouchTick = ++nextTouchTick;
        }
    }

    /// <summary>
    /// 登録を解除する。描画ソースの破棄では呼ばないこと（パラメータはアイテムが生きている間残す）。
    /// </summary>
    public static void Unregister(object sourceKey)
    {
        if (sourceKey is null) return;

        lock (Gate)
        {
            var removed = Entries.RemoveAll(entry =>
            {
                if (!entry.ParameterRef.TryGetTarget(out var parameter)) return true;
                return ReferenceEquals(parameter, sourceKey);
            });
            if (removed > 0) Version++;
        }
    }

    /// <summary>
    /// 個別の音声プロセッサへ、同じチャンネルの弾幕を重複しないよう割り当てる。
    /// 映像側の Touch が音声先読みより後になっても、終盤の弾幕を避けて次の弾幕を選べる。
    /// </summary>
    public static DanmakuChannelRegistration? ClaimRegistration(
        int channel,
        DanmakuSoundKind soundKind,
        object owner,
        double audioDurationSeconds,
        object? currentSourceKey = null)
    {
        lock (Gate)
        {
            Prune();
            PruneAudioClaims();

            var live = new List<(Entry Entry, DanmakuShapeParameter Parameter)>();
            foreach (var entry in Entries)
            {
                if (!entry.ParameterRef.TryGetTarget(out var parameter)) continue;
                if (!MatchesChannel(channel, parameter)) continue;
                live.Add((entry, parameter));
            }

            var candidates = live
                .Select(item => item.Entry.ToCandidate(item.Parameter))
                .ToArray();
            var claimed = AudioClaims
                .Where(claim =>
                    claim.Channel == channel &&
                    claim.SoundKind == soundKind &&
                    (!claim.OwnerRef.TryGetTarget(out var claimOwner) || !ReferenceEquals(claimOwner, owner)))
                .Select(claim => claim.SourceKey)
                .ToHashSet();

            var selectedKey = DanmakuAudioAssignment.Select(
                candidates,
                currentSourceKey,
                claimed,
                audioDurationSeconds);
            if (selectedKey is null) return null;

            var existingClaim = AudioClaims.FirstOrDefault(
                claim => claim.OwnerRef.TryGetTarget(out var claimOwner) && ReferenceEquals(claimOwner, owner));
            if (existingClaim is null)
            {
                AudioClaims.Add(new AudioClaim(owner, selectedKey, channel, soundKind));
            }
            else
            {
                existingClaim.SourceKey = selectedKey;
            }

            var selected = live.FirstOrDefault(item => ReferenceEquals(item.Parameter, selectedKey));
            return selected.Entry?.ToPublic(selected.Parameter);
        }
    }

    /// <summary>音声プロセッサが保持している弾幕の割り当てを解放する。</summary>
    public static void ReleaseRegistration(object owner)
    {
        lock (Gate)
        {
            AudioClaims.RemoveAll(
                claim => !claim.OwnerRef.TryGetTarget(out var claimOwner) || ReferenceEquals(claimOwner, owner));
        }
    }

    /// <summary>
    /// 指定チャンネルに登録されているすべての有効な登録情報を取得する（タイムライン開始時刻順）。
    /// </summary>
    public static List<DanmakuChannelRegistration> GetRegistrations(int channel)
    {
        lock (Gate)
        {
            Prune();
            var list = new List<DanmakuChannelRegistration>();
            foreach (var entry in Entries)
            {
                if (!entry.ParameterRef.TryGetTarget(out var parameter)) continue;
                if (!MatchesChannel(channel, parameter)) continue;
                list.Add(entry.ToPublic(parameter));
            }
            list.Sort(static (a, b) =>
            {
                var start = a.TimelineStartSeconds.CompareTo(b.TimelineStartSeconds);
                return start != 0 ? start : a.TouchTick.CompareTo(b.TouchTick);
            });
            return list;
        }
    }

    /// <summary>
    /// 指定チャンネルで直近に Touch された弾幕設定を取得する。
    /// 同じチャンネルに複数ある場合は、現在の再生位置で描画されたものを優先する。
    /// </summary>
    public static DanmakuSettings? TryGetSettings(int channel)
    {
        var parameter = TryGetParameter(channel);
        return parameter?.ToSettings(parameter.LastCanvasWidth, parameter.LastCanvasHeight);
    }

    /// <summary>
    /// 指定チャンネルで直近に Touch された弾幕パラメータインスタンスを取得する。
    /// </summary>
    public static DanmakuShapeParameter? TryGetParameter(int channel)
    {
        lock (Gate)
        {
            Prune();
            var best = FindLatest(channel);
            if (best is not null && best.ParameterRef.TryGetTarget(out var parameter))
            {
                return parameter;
            }

            return null;
        }
    }

    /// <summary>現在登録されているチャンネル番号の一覧。</summary>
    public static IReadOnlyList<int> GetChannels()
    {
        lock (Gate)
        {
            Prune();
            var list = new List<int>();
            foreach (var entry in Entries)
            {
                if (!entry.ParameterRef.TryGetTarget(out var p)) continue;
                var ch = ReadChannel(p);
                if (ch >= 0 && !list.Contains(ch)) list.Add(ch);
            }
            list.Sort();
            return list;
        }
    }

    /// <summary>呼び出し側で <see cref="Gate"/> をロックしていること。</summary>
    private static Entry? FindLatest(int channel)
    {
        Entry? best = null;
        var bestTick = long.MinValue;
        foreach (var entry in Entries)
        {
            if (!entry.ParameterRef.TryGetTarget(out var parameter)) continue;
            if (!MatchesChannel(channel, parameter)) continue;
            if (entry.TouchTick > bestTick)
            {
                bestTick = entry.TouchTick;
                best = entry;
            }
        }

        return best;
    }

    private static Entry? FindByParameter(DanmakuShapeParameter parameter)
    {
        foreach (var entry in Entries)
        {
            if (entry.ParameterRef.TryGetTarget(out var existing) && ReferenceEquals(existing, parameter))
            {
                return entry;
            }
        }

        return null;
    }

    private static Entry? FindByKey(object sourceKey)
    {
        foreach (var entry in Entries)
        {
            if (!entry.ParameterRef.TryGetTarget(out var parameter)) continue;
            if (ReferenceEquals(parameter, sourceKey)) return entry;
        }

        return null;
    }

    private static bool MatchesChannel(int channel, DanmakuShapeParameter parameter)
    {
        var paramChannel = ReadChannel(parameter);
        return channel == -1 || paramChannel == -1 || paramChannel == channel;
    }

    private static int ReadChannel(DanmakuShapeParameter parameter) =>
        (int)Math.Round(parameter.Channel.GetFirstValue());

    /// <summary>回収済みの参照を取り除く。呼び出し側で <see cref="Gate"/> をロックしていること。</summary>
    private static void Prune()
    {
        var removed = Entries.RemoveAll(entry => !entry.ParameterRef.TryGetTarget(out _));
        if (removed > 0) PruneAudioClaims();
    }

    /// <summary>破棄済みプロセッサまたは削除済み弾幕の割り当てを除く。呼び出し側でロック済みであること。</summary>
    private static void PruneAudioClaims()
    {
        AudioClaims.RemoveAll(claim =>
            !claim.OwnerRef.TryGetTarget(out _) || FindByKey(claim.SourceKey) is null);
    }

    private sealed class AudioClaim(object owner, object sourceKey, int channel, DanmakuSoundKind soundKind)
    {
        public WeakReference<object> OwnerRef { get; } = new(owner);
        public object SourceKey { get; set; } = sourceKey;
        public int Channel { get; } = channel;
        public DanmakuSoundKind SoundKind { get; } = soundKind;
    }

    /// <summary>連絡簿の内部エントリ。公開 API には出さない。パラメータは弱参照のみ保持する。</summary>
    private sealed class Entry
    {
        public WeakReference<DanmakuShapeParameter> ParameterRef { get; }
        public int Fps { get; set; }
        public int TotalFrame { get; set; }
        public double TimelineStartSeconds { get; set; }
        public double TimelineDurationSeconds { get; set; }
        public int Layer { get; set; }
        public long TouchTick { get; set; }
        public long RegistrationOrder { get; }
        public int LastItemFrame { get; set; }

        public Entry(
            DanmakuShapeParameter parameter,
            int fps,
            int totalFrame,
            double timelineStartSeconds,
            double timelineDurationSeconds,
            int layer,
            long registrationOrder,
            int lastItemFrame = 0)
        {
            ParameterRef = new WeakReference<DanmakuShapeParameter>(parameter);
            Fps = fps;
            TotalFrame = totalFrame;
            TimelineStartSeconds = timelineStartSeconds;
            TimelineDurationSeconds = timelineDurationSeconds;
            Layer = layer;
            RegistrationOrder = registrationOrder;
            LastItemFrame = lastItemFrame;
        }

        public DanmakuAudioCandidate ToCandidate(DanmakuShapeParameter parameter) => new(
            parameter,
            TimelineStartSeconds,
            TimelineDurationSeconds,
            TouchTick,
            RegistrationOrder,
            LastItemFrame,
            TotalFrame);

        public DanmakuChannelRegistration ToPublic(DanmakuShapeParameter parameter) => new(
            parameter,
            ParameterRef,
            Fps,
            TotalFrame,
            TimelineStartSeconds,
            TimelineDurationSeconds,
            Layer,
            TouchTick,
            LastItemFrame);
    }
}
