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
