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

    internal DanmakuChannelRegistration(
        object sourceKey,
        WeakReference<DanmakuShapeParameter> parameterRef,
        int fps,
        int totalFrame,
        double timelineStartSeconds,
        double timelineDurationSeconds,
        int layer,
        long touchTick)
    {
        SourceKey = sourceKey;
        ParameterRef = parameterRef;
        Fps = fps;
        TotalFrame = totalFrame;
        TimelineStartSeconds = timelineStartSeconds;
        TimelineDurationSeconds = timelineDurationSeconds;
        Layer = layer;
        TouchTick = touchTick;
    }
}

/// <summary>
/// 映像側の弾幕アイテムと音声側の効果音エフェクトを結び付けるための連絡簿。
/// タイムライン上の各弾幕アイテムの開始位置（秒）を保持し、
/// 単一の長い音声アイテムや連続配置された音声アイテムに対して正確に音響を配置する。
/// <para>
/// 同じチャンネルに複数の弾幕が登録されたままになることがあるため、
/// 映像側は描画のたびに <see cref="Touch"/> し、音声側は直近に Touch された項目を優先する。
/// </para>
/// </summary>
public static class DanmakuChannelBus
{
    private static readonly object Gate = new();
    private static readonly Dictionary<object, Entry> Registrations = new();
    private static long nextTouchTick;

    /// <summary>登録状態の変更バージョン番号。登録・更新・削除のたびにインクリメントされる。</summary>
    public static int Version { get; private set; }

    /// <summary>弾幕アイテムを登録・更新する。存在の連絡であり、再生位置の担当切替は <see cref="Touch"/> が行う。</summary>
    public static void Register(
        DanmakuShapeParameter parameter,
        object? sourceKey = null,
        int fps = 60,
        int totalFrame = 1,
        double timelineStartSeconds = 0,
        double timelineDurationSeconds = 0,
        int layer = 0)
    {
        lock (Gate)
        {
            var key = sourceKey ?? parameter;
            fps = fps > 0 ? fps : 60;
            totalFrame = Math.Max(1, totalFrame);

            if (Registrations.TryGetValue(key, out var entry))
            {
                var changed = entry.Fps != fps || entry.TotalFrame != totalFrame;
                entry.Fps = fps;
                entry.TotalFrame = totalFrame;
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
                Registrations[key] = new Entry(key, parameter, fps, totalFrame, timelineStartSeconds, timelineDurationSeconds, layer);
                Version++;
            }
        }
    }

    /// <summary>
    /// 映像側が「今この瞬間、自分が再生位置にある」と連絡簿へ伝える。
    /// 同じチャンネルの候補が複数あるとき、音声側は直近に Touch された項目を使う。
    /// </summary>
    public static void Touch(object sourceKey)
    {
        if (sourceKey is null) return;

        lock (Gate)
        {
            if (!Registrations.TryGetValue(sourceKey, out var entry)) return;
            if (!entry.ParameterRef.TryGetTarget(out var parameter)) return;

            // Touch は再生位置の担当を示すだけで、音声プロセッサの構造変更ではない。
            // ここで Version を増やすと、連続再生中に既に準備済みの音声が再構築され、
            // 次の弾幕の先頭へ切り替わる前後で音が欠落・途中再生になる。
            var channel = ReadChannel(parameter);
            _ = FindLatest(channel);
            entry.TouchTick = ++nextTouchTick;
        }
    }

    /// <summary>登録を解除する。</summary>
    public static void Unregister(object sourceKey)
    {
        lock (Gate)
        {
            if (Registrations.Remove(sourceKey))
            {
                Version++;
            }
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
            foreach (var entry in Registrations.Values)
            {
                if (!entry.ParameterRef.TryGetTarget(out var parameter)) continue;
                if (!MatchesChannel(channel, parameter)) continue;
                list.Add(entry.ToPublic());
            }
            list.Sort(static (a, b) => a.TimelineStartSeconds.CompareTo(b.TimelineStartSeconds));
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
            foreach (var entry in Registrations.Values)
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
        foreach (var entry in Registrations.Values)
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
        List<object>? deadKeys = null;
        foreach (var (key, entry) in Registrations)
        {
            if (entry.ParameterRef.TryGetTarget(out _)) continue;
            deadKeys ??= [];
            deadKeys.Add(key);
        }

        if (deadKeys is null) return;
        foreach (var k in deadKeys) Registrations.Remove(k);
    }

    /// <summary>連絡簿の内部エントリ。公開 API には出さない。</summary>
    private sealed class Entry
    {
        public object SourceKey { get; }
        public WeakReference<DanmakuShapeParameter> ParameterRef { get; }
        public int Fps { get; set; }
        public int TotalFrame { get; set; }
        public double TimelineStartSeconds { get; set; }
        public double TimelineDurationSeconds { get; set; }
        public int Layer { get; set; }
        public long TouchTick { get; set; }

        public Entry(
            object sourceKey,
            DanmakuShapeParameter parameter,
            int fps,
            int totalFrame,
            double timelineStartSeconds,
            double timelineDurationSeconds,
            int layer)
        {
            SourceKey = sourceKey;
            ParameterRef = new WeakReference<DanmakuShapeParameter>(parameter);
            Fps = fps;
            TotalFrame = totalFrame;
            TimelineStartSeconds = timelineStartSeconds;
            TimelineDurationSeconds = timelineDurationSeconds;
            Layer = layer;
        }

        public DanmakuChannelRegistration ToPublic() => new(
            SourceKey,
            ParameterRef,
            Fps,
            TotalFrame,
            TimelineStartSeconds,
            TimelineDurationSeconds,
            Layer,
            TouchTick);
    }
}
