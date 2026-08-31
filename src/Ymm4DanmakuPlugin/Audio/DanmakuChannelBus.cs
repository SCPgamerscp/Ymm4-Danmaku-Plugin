using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Parameters;

namespace Ymm4DanmakuPlugin.Audio;

/// <summary>
/// 映像側の弾幕アイテムと音声側の効果音エフェクトを結び付けるための連絡簿。
/// <para>
/// <b>なぜ必要か:</b> YMM4 では図形アイテム (映像) と音声エフェクト (音声) は
/// 別々のアイテムとして独立に処理される。音声側から映像側の設定を直接読む手段が無いため、
/// 「チャンネル番号」で紐付けるこの仕組みを用意している。
/// </para>
/// <para>
/// <b>決定論との関係:</b> 音声側は映像側の計算結果を受け取るのではなく、
/// <b>同じ設定・同じシードで自前にシミュレーションをやり直す</b>。
/// コアエンジンは決定論的なので、両者は必ず同一の効果音イベント列を得る。
/// これにより「音声だけ先に書き出す」ような順序でも音ズレが起きない。
/// </para>
/// <para>
/// 参照は <see cref="WeakReference{T}"/> で保持するため、
/// アイテムを削除してもここが原因でメモリが残ることはない。
/// </para>
/// </summary>
public static class DanmakuChannelBus
{
    private static readonly object Gate = new();
    private static readonly List<WeakReference<DanmakuShapeParameter>> Entries = [];

    /// <summary>弾幕アイテムを登録する。同じインスタンスの二重登録は行われない。</summary>
    public static void Register(DanmakuShapeParameter parameter)
    {
        lock (Gate)
        {
            Prune();
            foreach (var entry in Entries)
            {
                if (entry.TryGetTarget(out var existing) && ReferenceEquals(existing, parameter)) return;
            }

            Entries.Add(new WeakReference<DanmakuShapeParameter>(parameter));
        }
    }

    /// <summary>登録を解除する。</summary>
    public static void Unregister(DanmakuShapeParameter parameter)
    {
        lock (Gate)
        {
            Entries.RemoveAll(e => !e.TryGetTarget(out var t) || ReferenceEquals(t, parameter));
        }
    }

    /// <summary>
    /// 指定チャンネルの弾幕設定を取得する。
    /// 同じチャンネルが複数ある場合は最初に見つかったものを返す。
    /// </summary>
    public static DanmakuSettings? TryGetSettings(int channel)
    {
        var parameter = TryGetParameter(channel);
        return parameter?.ToSettings(parameter.LastCanvasWidth, parameter.LastCanvasHeight);
    }

    /// <summary>
    /// 指定チャンネルの弾幕パラメータインスタンスを取得する。
    /// </summary>
    public static DanmakuShapeParameter? TryGetParameter(int channel)
    {
        lock (Gate)
        {
            Prune();
            foreach (var entry in Entries)
            {
                if (!entry.TryGetTarget(out var parameter)) continue;
                var paramChannel = (int)Math.Round(parameter.Channel.GetFirstValue());
                if (channel != -1 && paramChannel != -1 && paramChannel != channel) continue;

                return parameter;
            }
        }

        return null;
    }

    /// <summary>現在登録されているチャンネル番号の一覧。</summary>
    public static IReadOnlyList<int> GetChannels()
    {
        lock (Gate)
        {
            Prune();
            return Entries
                .Select(e => e.TryGetTarget(out var p) ? (int)Math.Round(p.Channel.GetFirstValue()) : -1)
                .Where(c => c >= 0)
                .Distinct()
                .Order()
                .ToArray();
        }
    }

    /// <summary>回収済みの参照を取り除く。呼び出し側で <see cref="Gate"/> をロックしていること。</summary>
    private static void Prune() => Entries.RemoveAll(e => !e.TryGetTarget(out _));
}
