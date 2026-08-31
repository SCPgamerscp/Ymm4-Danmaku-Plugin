using System.Collections.Concurrent;
using Ymm4DanmakuPlugin.Core.Mathematics;

namespace Ymm4DanmakuPlugin.Collision;

/// <summary>
/// 弾相殺判定用の円形領域。
/// </summary>
public readonly record struct BulletCancelArea(Vec2 Position, double Radius);

/// <summary>
/// 1 つの弾幕アイテム (レイヤー) が公開する衝突・位置・相殺情報。
/// </summary>
public readonly record struct DanmakuLayerState(
    object SourceKey,
    int Channel,
    Vec2 EnemyPosition,
    double EnemyRadius,
    bool HasEnemy,
    Vec2 TargetPosition,
    double TargetRadius,
    bool HasTarget,
    IReadOnlyList<BulletCancelArea>? Cancelers = null
);

/// <summary>
/// 異なるタイムラインレイヤーに配置された弾幕図形アイテム同士を
/// チャンネル番号で結び付け、エネミー位置、自機位置、被弾ダメージ、弾相殺を共有するための連絡バス。
/// </summary>
public static class DanmakuCollisionBus
{
    private static readonly object Gate = new();
    private static readonly Dictionary<object, DanmakuLayerState> States = new();
    private static readonly Dictionary<(int Channel, object SourceKey), double> LayerDamage = new();
    private static readonly Dictionary<(int Channel, object SourceKey), IReadOnlyList<BulletCancelArea>> LayerCancelers = new();

    /// <summary>
    /// 現在のフレームにおけるアイテムの座標・判定情報を登録・更新する。
    /// </summary>
    public static void Publish(in DanmakuLayerState state)
    {
        lock (Gate)
        {
            States[state.SourceKey] = state;
            if (state.Cancelers is { Count: > 0 } cancelers)
            {
                LayerCancelers[(state.Channel, state.SourceKey)] = cancelers;
            }
            else
            {
                LayerCancelers.Remove((state.Channel, state.SourceKey));
            }
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
            var damageKeysToRemove = LayerDamage.Keys.Where(k => ReferenceEquals(k.SourceKey, sourceKey)).ToList();
            foreach (var k in damageKeysToRemove)
            {
                LayerDamage.Remove(k);
            }
            var cancelKeysToRemove = LayerCancelers.Keys.Where(k => ReferenceEquals(k.SourceKey, sourceKey)).ToList();
            foreach (var k in cancelKeysToRemove)
            {
                LayerCancelers.Remove(k);
            }
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
                if (!state.HasEnemy) continue;
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
                if (!state.HasTarget) continue;
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

    /// <summary>指定したチャンネルのエネミーへ被弾ダメージを報告する。</summary>
    public static void ReportDamage(int channel, object sourceKey, double damage)
    {
        if (damage <= 0) return;
        lock (Gate)
        {
            var key = (channel, sourceKey);
            LayerDamage[key] = LayerDamage.GetValueOrDefault(key) + damage;
        }
    }

    /// <summary>指定したチャンネルのエネミーが他レイヤーから受けた累積ダメージを取得する。</summary>
    public static double GetExternalDamage(int channel, object callerKey)
    {
        lock (Gate)
        {
            var total = 0.0;
            foreach (var ((ch, srcKey), dmg) in LayerDamage)
            {
                if (ReferenceEquals(srcKey, callerKey)) continue;
                if (channel >= 0 && ch != channel) continue;
                total += dmg;
            }
            return total;
        }
    }

    /// <summary>
    /// 敵弾が他レイヤーの自機ショット（相殺有効）によって相殺されるか判定する。
    /// </summary>
    public static bool TryCancelEnemyBullet(int channel, object callerKey, Vec2 bulletPos, double bulletRadius)
    {
        lock (Gate)
        {
            foreach (var ((ch, srcKey), cancelers) in LayerCancelers)
            {
                if (ReferenceEquals(srcKey, callerKey)) continue;
                if (channel >= 0 && ch != channel) continue;

                for (var i = 0; i < cancelers.Count; i++)
                {
                    var c = cancelers[i];
                    var r = c.Radius + bulletRadius;
                    if (bulletPos.DistanceSquaredTo(c.Position) <= r * r)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>全状態および累積ダメージを初期化する。</summary>
    public static void Clear()
    {
        lock (Gate)
        {
            States.Clear();
            LayerDamage.Clear();
            LayerCancelers.Clear();
        }
    }

    /// <summary>累積ダメージをクリアする。</summary>
    public static void ClearDamage()
    {
        lock (Gate)
        {
            LayerDamage.Clear();
        }
    }
}
