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
    long TouchTick,
    long RegistrationOrder = 0);

/// <summary>
/// 複数の個別音声アイテムを、同じチャンネルの連続した弾幕へ重複なく割り当てる。
/// YMM4 のプレビューでは映像と音声の生成順が一定ではないため、再生中の Touch だけには依存しない。
/// </summary>
public static class DanmakuAudioAssignment
{
    /// <summary>
    /// 既存の割り当てを優先し、未割り当て候補の中から時間長、タイムライン順、登録順が合う候補を返す。
    /// タイムライン情報が未確定でも登録順で先行予約し、次の弾幕の開始フレームで準備処理が走るのを防ぐ。
    /// </summary>
    public static object? Select(
        IReadOnlyList<DanmakuAudioCandidate> candidates,
        object? currentSourceKey,
        IReadOnlySet<object> claimedSourceKeys,
        double audioDurationSeconds,
        double durationToleranceSeconds = 0.05)
    {
        if (candidates.Count == 0) return null;

        if (currentSourceKey is not null &&
            candidates.Any(candidate => ReferenceEquals(candidate.SourceKey, currentSourceKey)))
        {
            return currentSourceKey;
        }

        var unclaimed = candidates
            .Where(candidate => !claimedSourceKeys.Contains(candidate.SourceKey))
            .ToArray();
        if (unclaimed.Length == 0)
        {
            // 全候補が使用中なら重複再生させず、既存プロセッサの解放を待つ。
            return null;
        }

        // プレビュー先読みでは後続だけ duration が埋まっていることがある。
        // その場合は長さ一致より登録順を優先し、音声1が弾幕2を奪わないようにする。
        if (unclaimed.Any(candidate => candidate.TimelineDurationSeconds <= 0))
        {
            return unclaimed
                .OrderBy(candidate => candidate.RegistrationOrder)
                .ThenBy(candidate => candidate.TimelineStartSeconds)
                .First()
                .SourceKey;
        }

        var durationMatches = unclaimed
            .Where(candidate => Math.Abs(candidate.TimelineDurationSeconds - audioDurationSeconds) <= durationToleranceSeconds)
            .ToArray();
        var eligible = durationMatches.Length > 0 ? durationMatches : unclaimed;

        return eligible
            .OrderBy(candidate => candidate.TimelineStartSeconds)
            .ThenBy(candidate => candidate.RegistrationOrder)
            .First()
            .SourceKey;
    }
}

/// <summary>
/// 1本の長い音声アイテムが複数の弾幕を跨いでいるかの判定。
/// 個別の短い音声は false のまま、排他割り当てを使う。
/// </summary>
public static class DanmakuSpanningAudio
{
    /// <summary>音声が1本の弾幕よりこれ倍以上長いとき、跨ぎ候補とみなす。</summary>
    public const double DurationSlackFactor = 1.5;

    /// <summary>音声が確定済み弾幕の合計スパンのこれ以上を覆うとき、跨ぎとみなす。</summary>
    public const double SpanCoverageFactor = 0.8;

    /// <summary>音声の長さが、1本の弾幕の長さより明らかに長いか。</summary>
    public static bool IsMuchLongerThanItem(double audioDurationSeconds, double itemDurationSeconds) =>
        itemDurationSeconds > 0 &&
        audioDurationSeconds + 1e-9 >= itemDurationSeconds * DurationSlackFactor;

    /// <summary>
    /// 伸ばした1本の音声が複数の弾幕をまたぐか。
    /// 登録が1件だけのときは、後から2本目が現れても個別音声の割り当てを壊さない。
    /// </summary>
    public static bool LooksSpanning(
        double audioDurationSeconds,
        int registrationCount,
        double longestReadyDurationSeconds,
        double totalReadyTimelineSpanSeconds,
        int readyCount)
    {
        if (audioDurationSeconds <= 0 || registrationCount <= 1)
        {
            return false;
        }

        if (IsMuchLongerThanItem(audioDurationSeconds, longestReadyDurationSeconds))
        {
            return true;
        }

        return readyCount > 1 &&
               totalReadyTimelineSpanSeconds > 0 &&
               audioDurationSeconds + 1e-9 >= totalReadyTimelineSpanSeconds * SpanCoverageFactor;
    }

    /// <summary>
    /// 未確定の長さで組んだ効果音を、本物の長さが分かったときに作り直すか。
    /// 他の弾幕の voices は触らない。
    /// </summary>
    public static bool ShouldResim(
        double preparedDurationSeconds,
        double currentItemDurationSeconds,
        double durationToleranceSeconds = 0.05)
    {
        if (preparedDurationSeconds <= 0 && currentItemDurationSeconds > 0)
        {
            return true;
        }

        return preparedDurationSeconds > 0 &&
               currentItemDurationSeconds > 0 &&
               Math.Abs(preparedDurationSeconds - currentItemDurationSeconds) > durationToleranceSeconds;
    }

    /// <summary>
    /// 跨ぎ再生なのに長さ0のまま全長シミュレートした弾幕は、そのキーの音だけ捨てて待つ。
    /// 個別の短い音声では呼ばない。
    /// </summary>
    public static bool ShouldDropUntimed(
        double preparedDurationSeconds,
        double currentItemDurationSeconds,
        bool spanning) =>
        spanning && preparedDurationSeconds <= 0 && currentItemDurationSeconds <= 0;
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
