using System.Collections.Concurrent;
using Ymm4DanmakuPlugin.Core.Engine;
using Ymm4DanmakuPlugin.Core.Mathematics;
using Ymm4DanmakuPlugin.Parameters;

namespace Ymm4DanmakuPlugin.Collision;

/// <summary>
/// 弾相殺判定用の円形領域。
/// </summary>
public readonly record struct BulletCancelArea(Vec2 Position, double Radius);

/// <summary>
/// 1 つの弾幕レイヤーの登録情報。
/// </summary>
public sealed class DanmakuLayerRegistration
{
    public object SourceKey { get; }
    public DanmakuShapeParameter Parameter { get; }
    public int Fps { get; set; }
    public int TotalFrame { get; set; }
    public (double Time, double Damage, int TargetChannel)[]? DamageHistorySnapshot { get; set; }
    public BulletCancelArea[]? CancelersSnapshot { get; set; }
    public EnemyHitbox[]? EnemyPositionsSnapshot { get; set; }
    public TargetHitbox[]? TargetPositionsSnapshot { get; set; }

    public DanmakuLayerRegistration(object sourceKey, DanmakuShapeParameter parameter, int fps, int totalFrame)
    {
        SourceKey = sourceKey;
        Parameter = parameter;
        Fps = fps > 0 ? fps : 60;
        TotalFrame = Math.Max(1, totalFrame);
    }
}

/// <summary>
/// 異なるタイムラインレイヤーに配置された弾幕図形アイテム同士を
/// チャンネル番号で結び付け、エネミー位置、自機位置、被弾ダメージ、弾相殺を時系列で完全に同期・共有するための連絡バス。
/// マルチスレッド並列更新（YMM4 Parallel.ForEach）に対して完全に安全。
/// </summary>
public static class DanmakuCollisionBus
{
    private static readonly object Gate = new();
    private static readonly Dictionary<object, DanmakuLayerRegistration> Registrations = new();

    private static int TimeToFrame(double timeSeconds, int fps, int totalFrame)
    {
        if (fps <= 0) fps = 60;
        var frame = (int)Math.Round(timeSeconds * fps);
        return Math.Clamp(frame, 0, Math.Max(0, totalFrame - 1));
    }

    /// <summary>
    /// レイヤーのパラメータを登録・更新する。
    /// </summary>
    public static void RegisterLayer(object sourceKey, DanmakuShapeParameter parameter, int fps, int totalFrame)
    {
        lock (Gate)
        {
            if (Registrations.TryGetValue(sourceKey, out var reg))
            {
                reg.Fps = fps > 0 ? fps : 60;
                reg.TotalFrame = Math.Max(1, totalFrame);
            }
            else
            {
                Registrations[sourceKey] = new DanmakuLayerRegistration(sourceKey, parameter, fps, totalFrame);
            }
        }
    }

    /// <summary>
    /// シミュレーション完了後にスナップショット (ダメージ履歴・相殺領域・エネミー位置・自機位置) を公開する。
    /// スナップショットは不変配列のため、別スレッドから同時に読まれても安全。
    /// </summary>
    public static void PublishSnapshots(
        object sourceKey,
        (double Time, double Damage, int TargetChannel)[]? damageHistory,
        BulletCancelArea[]? cancelers,
        EnemyHitbox[]? enemyPositions = null,
        TargetHitbox[]? targetPositions = null)
    {
        lock (Gate)
        {
            if (Registrations.TryGetValue(sourceKey, out var reg))
            {
                reg.DamageHistorySnapshot = damageHistory;
                reg.CancelersSnapshot = cancelers;
                if (enemyPositions is not null) reg.EnemyPositionsSnapshot = enemyPositions;
                if (targetPositions is not null) reg.TargetPositionsSnapshot = targetPositions;
            }
        }
    }

    /// <summary>
    /// アイテムが破棄された際に登録を削除する。
    /// </summary>
    public static void UnregisterLayer(object sourceKey)
    {
        lock (Gate)
        {
            Registrations.Remove(sourceKey);
        }
    }

    /// <summary>
    /// 指定した時刻 <paramref name="timeSeconds"/> におけるすべての相手方ターゲット (自機) のリストを取得する。
    /// </summary>
    public static List<TargetHitbox> GetTargetsAt(int targetChannel, object? callerKey, double timeSeconds)
    {
        var list = new List<TargetHitbox>();
        lock (Gate)
        {
            var targetId = 0;
            foreach (var (key, reg) in Registrations)
            {
                if (callerKey is not null && ReferenceEquals(key, callerKey)) continue;

                var frame = TimeToFrame(timeSeconds, reg.Fps, reg.TotalFrame);
                var ch = (int)Math.Round(reg.Parameter.Channel.GetValue(frame, reg.TotalFrame, reg.Fps));
                if (targetChannel >= 0 && ch != targetChannel) continue;

                if (reg.TargetPositionsSnapshot is { Length: > 0 } snap)
                {
                    list.AddRange(snap);
                    continue;
                }

                var col = reg.Parameter.CollisionEnabled.GetValue(frame, reg.TotalFrame, reg.Fps) >= 0.5;
                var show = reg.Parameter.ShowTargetMarker.GetValue(frame, reg.TotalFrame, reg.Fps) >= 0.5;
                var hasTarget = col && (show || reg.Parameter.HasCustomTargetImage);
                if (!hasTarget) continue;

                var radius = reg.Parameter.TargetRadius.GetValue(frame, reg.TotalFrame, reg.Fps);
                if (radius <= 0) continue;

                var pos = new Vec2(
                    reg.Parameter.TargetX.GetValue(frame, reg.TotalFrame, reg.Fps),
                    reg.Parameter.TargetY.GetValue(frame, reg.TotalFrame, reg.Fps));
                list.Add(new TargetHitbox(pos, radius, targetId++));
            }
        }
        return list;
    }

    /// <summary>
    /// 指定した時刻 <paramref name="timeSeconds"/> におけるすべての相手方エネミー (ボス) のリストを取得する。
    /// </summary>
    public static List<EnemyHitbox> GetEnemiesAt(int targetChannel, object? callerKey, double timeSeconds)
    {
        var list = new List<EnemyHitbox>();
        lock (Gate)
        {
            foreach (var (key, reg) in Registrations)
            {
                if (callerKey is not null && ReferenceEquals(key, callerKey)) continue;

                var frame = TimeToFrame(timeSeconds, reg.Fps, reg.TotalFrame);
                var ch = (int)Math.Round(reg.Parameter.Channel.GetValue(frame, reg.TotalFrame, reg.Fps));
                if (targetChannel >= 0 && ch != targetChannel) continue;

                if (reg.EnemyPositionsSnapshot is { Length: > 0 } snap)
                {
                    for (var s = 0; s < snap.Length; s++)
                    {
                        var e = snap[s];
                        if (targetChannel < 0 || e.Channel < 0 || e.Channel == targetChannel)
                        {
                            list.Add(e);
                        }
                    }
                    continue;
                }

                var enemyRadius = reg.Parameter.EnemyRadius.GetValue(frame, reg.TotalFrame, reg.Fps);
                if (enemyRadius <= 0) continue;

                for (var i = 0; i < reg.Parameter.Emitters.Count && i < DanmakuShapeParameter.MaxEmitters; i++)
                {
                    var emitter = reg.Parameter.Emitters[i];
                    var isEnabled = emitter.IsEnabled.GetValue(frame, reg.TotalFrame, reg.Fps) >= 0.5;
                    if (!isEnabled) continue;

                    var baseX = emitter.X.GetValue(frame, reg.TotalFrame, reg.Fps);
                    var baseY = emitter.Y.GetValue(frame, reg.TotalFrame, reg.Fps);
                    var orbitRadius = emitter.OrbitRadius.GetValue(frame, reg.TotalFrame, reg.Fps);
                    var orbitSpeed = emitter.OrbitSpeed.GetValue(frame, reg.TotalFrame, reg.Fps);
                    var orbitPhase = emitter.OrbitPhase.GetValue(frame, reg.TotalFrame, reg.Fps);
                    var orbitAngle = orbitPhase + orbitSpeed * timeSeconds;

                    var pos = new Vec2(baseX, baseY);
                    if (orbitRadius != 0)
                    {
                        pos += Vec2.FromDegrees(orbitAngle, orbitRadius);
                    }

                    list.Add(new EnemyHitbox(pos, enemyRadius, i, ch));
                }
            }
        }
        return list;
    }

    /// <summary>
    /// 指定した時刻 <paramref name="timeSeconds"/> における相手方エネミー (ボス) の位置と判定半径を取得する。
    /// </summary>
    public static bool TryGetEnemyAt(int targetChannel, object callerKey, double timeSeconds, int fps, int totalFrame, out Vec2 position, out double radius)
    {
        var enemies = GetEnemiesAt(targetChannel, callerKey, timeSeconds);
        if (enemies.Count > 0)
        {
            position = enemies[0].Position;
            radius = enemies[0].Radius;
            return true;
        }

        position = Vec2.Zero;
        radius = 0;
        return false;
    }

    /// <summary>
    /// 指定した時刻 <paramref name="timeSeconds"/> における相手方ターゲット (自機) の位置と判定半径を取得する。
    /// </summary>
    public static bool TryGetTargetAt(int targetChannel, object callerKey, double timeSeconds, int fps, int totalFrame, out Vec2 position, out double radius)
    {
        var targets = GetTargetsAt(targetChannel, callerKey, timeSeconds);
        if (targets.Count > 0)
        {
            position = targets[0].Position;
            radius = targets[0].Radius;
            return true;
        }

        position = Vec2.Zero;
        radius = 0;
        return false;
    }

    /// <summary>
    /// 指定した時刻 <paramref name="timeSeconds"/> までに他レイヤーの自機ショットから受けた累積ダメージを取得する。
    /// 不変スナップショット配列から読み取るため、マルチスレッド並列実行でも完全安全。
    /// </summary>
    public static double GetExternalDamageAt(int channel, object callerKey, double timeSeconds, int fps, int totalFrame)
    {
        lock (Gate)
        {
            var total = 0.0;
            foreach (var (key, reg) in Registrations)
            {
                if (ReferenceEquals(key, callerKey)) continue;

                var frame = TimeToFrame(timeSeconds, reg.Fps, reg.TotalFrame);
                var shotEnabled = reg.Parameter.PlayerShotEnabled.GetValue(frame, reg.TotalFrame, reg.Fps) >= 0.5;
                if (!shotEnabled) continue;

                var targetCh = reg.Parameter.PlayerShotTargetChannel;
                if (channel >= 0 && targetCh >= 0 && targetCh != channel) continue;

                var snapshot = reg.DamageHistorySnapshot;
                if (snapshot is not null)
                {
                    for (var i = 0; i < snapshot.Length; i++)
                    {
                        var (hitTime, dmg, hitTargetCh) = snapshot[i];
                        if (hitTime <= timeSeconds && (channel < 0 || hitTargetCh < 0 || hitTargetCh == channel))
                        {
                            total += dmg;
                        }
                    }
                }
            }
            return total;
        }
    }

    /// <summary>
    /// 敵弾が他レイヤーの自機ショット（相殺有効）によって相殺されるか判定する。
    /// 不変スナップショット配列から読み取るため、コレクション変更例外は起きない。
    /// </summary>
    public static bool TryCancelEnemyBulletAt(int channel, object callerKey, Vec2 bulletPos, double bulletRadius)
    {
        lock (Gate)
        {
            foreach (var (key, reg) in Registrations)
            {
                if (ReferenceEquals(key, callerKey)) continue;

                var targetCh = reg.Parameter.PlayerShotTargetChannel;
                if (channel >= 0 && targetCh >= 0 && targetCh != channel) continue;

                var cancelers = reg.CancelersSnapshot;
                if (cancelers is not null)
                {
                    for (var i = 0; i < cancelers.Length; i++)
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
        }
        return false;
    }

    /// <summary>全レイヤー登録を初期化する。</summary>
    public static void Clear()
    {
        lock (Gate)
        {
            Registrations.Clear();
        }
    }
}
