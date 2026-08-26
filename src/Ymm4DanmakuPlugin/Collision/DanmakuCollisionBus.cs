using Ymm4DanmakuPlugin.Core.Mathematics;

namespace Ymm4DanmakuPlugin.Collision;

/// <summary>
/// 1 つの弾幕アイテム (レイヤー) が公開する衝突・位置情報。
/// </summary>
public readonly record struct DanmakuLayerState(
    object SourceKey,
    int Channel,
    Vec2 EnemyPosition,
    double EnemyRadius,
    Vec2 TargetPosition,
    double TargetRadius
);

/// <summary>
/// 異なるタイムラインレイヤーに配置された弾幕図形アイテム同士を
/// チャンネル番号で結び付け、エネミー位置や自機位置を共有するための連絡バス。
/// </summary>
public static class DanmakuCollisionBus
{
    private static readonly object Gate = new();
    private static readonly Dictionary<object, DanmakuLayerState> States = new();

    /// <summary>
    /// 現在のフレームにおけるアイテムの座標・判定情報を登録・更新する。
    /// </summary>
    public static void Publish(in DanmakuLayerState state)
    {
        lock (Gate)
        {
            States[state.SourceKey] = state;
        }
    }

    /// <summary>
    /// アイテムが破棄された際に登録を削除する。
    /// </summary>
    public static void Remove(object sourceKey)
    {
        lock (Gate)
        {
            States.Remove(sourceKey);
        }
    }

    /// <summary>
    /// 指定したチャンネルの相手方エネミー (ボス) の位置と判定半径を取得する。
    /// <paramref name="targetChannel"/> が負 (-1) の場合は最初に見つかった別レイヤーのエネミーを返す。
    /// </summary>
    public static bool TryGetEnemy(int targetChannel, object callerKey, out Vec2 position, out double radius)
    {
        lock (Gate)
        {
            foreach (var (key, state) in States)
            {
                if (ReferenceEquals(key, callerKey)) continue;

                if (targetChannel >= 0 && state.Channel != targetChannel) continue;

                position = state.EnemyPosition;
                radius = state.EnemyRadius;
                return true;
            }
        }

        position = Vec2.Zero;
        radius = 0;
        return false;
    }

    /// <summary>
    /// 指定したチャンネルの相手方ターゲット (自機) の位置と判定半径を取得する。
    /// <paramref name="targetChannel"/> が負 (-1) の場合は最初に見つかった別レイヤーの自機を返す。
    /// </summary>
    public static bool TryGetTarget(int targetChannel, object callerKey, out Vec2 position, out double radius)
    {
        lock (Gate)
        {
            foreach (var (key, state) in States)
            {
                if (ReferenceEquals(key, callerKey)) continue;

                if (targetChannel >= 0 && state.Channel != targetChannel) continue;

                position = state.TargetPosition;
                radius = state.TargetRadius;
                return true;
            }
        }

        position = Vec2.Zero;
        radius = 0;
        return false;
    }
}
