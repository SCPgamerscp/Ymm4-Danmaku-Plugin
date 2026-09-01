using Ymm4DanmakuPlugin.Core.Configuration;

namespace Ymm4DanmakuPlugin.Core.Audio;

/// <summary>弾幕シミュレーション中に発生した効果音イベント。</summary>
public readonly record struct DanmakuSoundEvent(
    DanmakuSoundKind Kind,
    double TimeSeconds,
    double PitchRatio,
    double Volume,
    int EmitterIndex)
{
    public override string ToString() =>
        $"{Kind}@{TimeSeconds:F3}s pitch={PitchRatio:F3} vol={Volume:F2}";
}

/// <summary>個別の音声アイテムへ割り当て可能な弾幕アイテム。</summary>
public readonly record struct DanmakuAudioCandidate(
    object SourceKey,
    double TimelineStartSeconds,
    double TimelineDurationSeconds,
    long TouchTick);

/// <summary>
/// 複数の個別音声アイテムを、同じチャンネルの連続した弾幕へ重複なく割り当てる。
/// YMM4 のプレビューでは映像と音声の生成順が一定ではないため、再生中の Touch だけには依存しない。
/// </summary>
public static class DanmakuAudioAssignment
{
    /// <summary>
    /// 既存の割り当てを優先し、未割り当て候補の中から現在表示中、次いで時間長とタイムライン順が合う候補を返す。
    /// タイムライン情報がまだ確定していない候補（長さ 0）は、先頭で複数音が混ざる原因になるため除外する。
    /// </summary>
    public static object? Select(
        IReadOnlyList<DanmakuAudioCandidate> candidates,
        object? currentSourceKey,
        IReadOnlySet<object> claimedSourceKeys,
        double audioDurationSeconds,
        double durationToleranceSeconds = 0.05)
    {
        var ready = candidates
            .Where(candidate => candidate.TimelineDurationSeconds > 0)
            .ToArray();
        if (ready.Length == 0) return null;

        if (currentSourceKey is not null &&
            ready.Any(candidate => ReferenceEquals(candidate.SourceKey, currentSourceKey)))
        {
            return currentSourceKey;
        }

        var unclaimed = ready
            .Where(candidate => !claimedSourceKeys.Contains(candidate.SourceKey))
            .ToArray();
        if (unclaimed.Length == 0)
        {
            // 全候補が使用中なら重複再生させず、既存プロセッサの解放を待つ。
            return null;
        }

        var active = unclaimed.MaxBy(candidate => candidate.TouchTick);
        if (active.TouchTick > 0)
        {
            return active.SourceKey;
        }

        var durationMatches = unclaimed
            .Where(candidate => Math.Abs(candidate.TimelineDurationSeconds - audioDurationSeconds) <= durationToleranceSeconds)
            .ToArray();
        var eligible = durationMatches.Length > 0 ? durationMatches : unclaimed;

        return eligible
            .OrderBy(candidate => candidate.TimelineStartSeconds)
            .First()
            .SourceKey;
    }
}

/// <summary>
/// 効果音イベントの記録簿。
/// <para>
/// 映像 (図形プラグイン) と音声 (音声エフェクトプラグイン) は YMM4 内で別々に処理されるが、
/// 同じシード・同じ設定でシミュレーションすれば同一のイベント列が得られるため、
/// このクラスを介して両者を同期させる。
/// </para>
/// </summary>
public sealed class SoundEventLog
{
    private readonly List<DanmakuSoundEvent> events = [];
    private readonly Dictionary<DanmakuSoundKind, double> lastEmitTime = [];
    private readonly Dictionary<DanmakuSoundKind, int> voicesThisSecond = [];
    private readonly Dictionary<DanmakuSoundKind, double> secondBucket = [];

    public IReadOnlyList<DanmakuSoundEvent> Events => events;

    public int Count => events.Count;

    /// <summary>記録を消去する。</summary>
    public void Clear()
    {
        events.Clear();
        lastEmitTime.Clear();
        voicesThisSecond.Clear();
        secondBucket.Clear();
    }

    /// <summary>
    /// 効果音イベントを追加する。
    /// レート制限 (MaxVoicesPerSecond) と同時発音の合成 (CoalesceSimultaneous) を適用する。
    /// </summary>
    public void Emit(
        DanmakuSoundKind kind,
        SoundSettings settings,
        double timeSeconds,
        double pitchRatio,
        int emitterIndex,
        double volumeScale = 1.0)
    {
        if (!settings.IsEnabled) return;

        // 同時発音の合成: 指定時間幅 (CoalesceIntervalSeconds) 内の同種イベントは 1 回にまとめる
        if (settings.CoalesceSimultaneous &&
            settings.CoalesceIntervalSeconds > 0 &&
            lastEmitTime.TryGetValue(kind, out var last) &&
            timeSeconds - last < settings.CoalesceIntervalSeconds)
        {
            return;
        }

        // 1 秒あたりの発音数制限
        var bucket = Math.Floor(timeSeconds);
        if (!secondBucket.TryGetValue(kind, out var currentBucket) || currentBucket != bucket)
        {
            secondBucket[kind] = bucket;
            voicesThisSecond[kind] = 0;
        }

        var used = voicesThisSecond[kind];
        if (settings.MaxVoicesPerSecond > 0 && used >= settings.MaxVoicesPerSecond) return;

        voicesThisSecond[kind] = used + 1;
        lastEmitTime[kind] = timeSeconds;

        events.Add(new DanmakuSoundEvent(
            kind,
            timeSeconds,
            pitchRatio,
            Math.Clamp(settings.Volume * volumeScale, 0.0, 4.0),
            emitterIndex));
    }

    /// <summary>指定した時間範囲 [start, end) のイベントを列挙する。</summary>
    public IEnumerable<DanmakuSoundEvent> GetRange(double startSeconds, double endSeconds)
    {
        foreach (var e in events)
        {
            if (e.TimeSeconds >= startSeconds && e.TimeSeconds < endSeconds)
                yield return e;
        }
    }
}
