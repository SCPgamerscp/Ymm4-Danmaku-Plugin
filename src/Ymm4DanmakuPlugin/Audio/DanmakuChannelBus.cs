using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Parameters;

namespace Ymm4DanmakuPlugin.Audio;

/// <summary>
/// 弾幕アイテムの登録情報。
/// </summary>
public sealed class DanmakuChannelRegistration
{
    public object SourceKey { get; }
    public WeakReference<DanmakuShapeParameter> ParameterRef { get; }
    public int Fps { get; set; } = 60;
    public int TotalFrame { get; set; } = 1;
    public double TimelineStartSeconds { get; set; }
    public double TimelineDurationSeconds { get; set; }
    public int Layer { get; set; }
    public DateTime LastActiveUtc { get; set; } = DateTime.UtcNow;

    public DanmakuChannelRegistration(
        object sourceKey,
        DanmakuShapeParameter parameter,
        int fps,
        int totalFrame,
        double timelineStartSeconds = 0,
        double timelineDurationSeconds = 0,
        int layer = 0)
    {
        SourceKey = sourceKey;
        ParameterRef = new WeakReference<DanmakuShapeParameter>(parameter);
        Fps = fps > 0 ? fps : 60;
        TotalFrame = Math.Max(1, totalFrame);
        TimelineStartSeconds = timelineStartSeconds;
        TimelineDurationSeconds = timelineDurationSeconds;
        Layer = layer;
        LastActiveUtc = DateTime.UtcNow;
    }
}

/// <summary>
/// 映像側の弾幕アイテムと音声側の効果音エフェクトを結び付けるための連絡簿。
/// タイムライン上の各弾幕アイテムの開始位置（秒）を保持し、
/// 単一の長い音声アイテムや連続配置された音声アイテムに対して正確に音響を配置する。
/// </summary>
public static class DanmakuChannelBus
{
    private static readonly object Gate = new();
    private static readonly Dictionary<object, DanmakuChannelRegistration> Registrations = new();

    /// <summary>弾幕アイテムを登録・更新する。</summary>
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
            if (Registrations.TryGetValue(key, out var reg))
            {
                reg.Fps = fps > 0 ? fps : 60;
                reg.TotalFrame = Math.Max(1, totalFrame);
                if (timelineDurationSeconds > 0)
                {
                    reg.TimelineStartSeconds = timelineStartSeconds;
                    reg.TimelineDurationSeconds = timelineDurationSeconds;
                    reg.Layer = layer;
                }
                reg.LastActiveUtc = DateTime.UtcNow;
            }
            else
            {
                Registrations[key] = new DanmakuChannelRegistration(
                    key, parameter, fps, totalFrame, timelineStartSeconds, timelineDurationSeconds, layer);
            }
        }
    }

    /// <summary>登録を解除する。</summary>
    public static void Unregister(object sourceKey)
    {
        lock (Gate)
        {
            Registrations.Remove(sourceKey);
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
            foreach (var (_, reg) in Registrations)
            {
                if (!reg.ParameterRef.TryGetTarget(out var parameter)) continue;
                var paramChannel = (int)Math.Round(parameter.Channel.GetFirstValue());
                if (channel != -1 && paramChannel != -1 && paramChannel != channel) continue;

                list.Add(reg);
            }
            list.Sort(static (a, b) => a.TimelineStartSeconds.CompareTo(b.TimelineStartSeconds));
            return list;
        }
    }

    /// <summary>
    /// 指定チャンネルで直近に最もアクティブだった弾幕設定を取得する。
    /// </summary>
    public static DanmakuSettings? TryGetSettings(int channel)
    {
        var parameter = TryGetParameter(channel);
        return parameter?.ToSettings(parameter.LastCanvasWidth, parameter.LastCanvasHeight);
    }

    /// <summary>
    /// 指定チャンネルで直近に最もアクティブだった弾幕パラメータインスタンスを取得する。
    /// </summary>
    public static DanmakuShapeParameter? TryGetParameter(int channel)
    {
        lock (Gate)
        {
            Prune();
            DanmakuShapeParameter? best = null;
            var bestTime = DateTime.MinValue;

            foreach (var (_, reg) in Registrations)
            {
                if (!reg.ParameterRef.TryGetTarget(out var parameter)) continue;
                var paramChannel = (int)Math.Round(parameter.Channel.GetFirstValue());
                if (channel != -1 && paramChannel != -1 && paramChannel != channel) continue;

                if (reg.LastActiveUtc > bestTime)
                {
                    bestTime = reg.LastActiveUtc;
                    best = parameter;
                }
            }

            return best;
        }
    }

    /// <summary>現在登録されているチャンネル番号の一覧。</summary>
    public static IReadOnlyList<int> GetChannels()
    {
        lock (Gate)
        {
            Prune();
            var list = new List<int>();
            foreach (var (_, reg) in Registrations)
            {
                if (reg.ParameterRef.TryGetTarget(out var p))
                {
                    var ch = (int)Math.Round(p.Channel.GetFirstValue());
                    if (ch >= 0 && !list.Contains(ch)) list.Add(ch);
                }
            }
            list.Sort();
            return list;
        }
    }

    /// <summary>回収済みの参照を取り除く。呼び出し側で <see cref="Gate"/> をロックしていること。</summary>
    private static void Prune()
    {
        var deadKeys = new List<object>();
        foreach (var (key, reg) in Registrations)
        {
            if (!reg.ParameterRef.TryGetTarget(out _)) deadKeys.Add(key);
        }
        foreach (var k in deadKeys) Registrations.Remove(k);
    }
}
