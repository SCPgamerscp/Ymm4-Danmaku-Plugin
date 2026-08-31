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
    public DanmakuSimulator? Simulator { get; set; }

    public DanmakuLayerRegistration(object sourceKey, DanmakuShapeParameter parameter, int fps, int totalFrame, DanmakuSimulator? simulator)
    {
        SourceKey = sourceKey;
        Parameter = parameter;
        Fps = fps > 0 ? fps : 60;
        TotalFrame = Math.Max(1, totalFrame);
        Simulator = simulator;
    }
}

/// <summary>
/// 異なるタイムラインレイヤーに配置された弾幕図形アイテム同士を
/// チャンネル番号で結び付け、エネミー位置、自機位置、被弾ダメージ、弾相殺を時系列で完全に同期・共有するための連絡バス。
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
    /// レイヤーのパラメータとシミュレータを登録・更新する。
    /// </summary>
    public static void RegisterLayer(object sourceKey, DanmakuShapeParameter parameter, int fps, int totalFrame, DanmakuSimulator? simulator)
    {
        lock (Gate)
        {
            if (Registrations.TryGetValue(sourceKey, out var reg))
            {
                reg.Fps = fps > 0 ? fps : 60;
                reg.TotalFrame = Math.Max(1, totalFrame);
                reg.Simulator = simulator;
            }
            else
            {
                Registrations[sourceKey] = new DanmakuLayerRegistration(sourceKey, parameter, fps, totalFrame, simulator);
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
    /// 指定した時刻 <paramref name="timeSeconds"/> における相手方エネミー (ボス) の位置と判定半径を取得する。
    /// </summary>
    public static bool TryGetEnemyAt(int targetChannel, object callerKey, double timeSeconds, int fps, int totalFrame, out Vec2 position, out double radius)
    {
        lock (Gate)
        {
            foreach (var (key, reg) in Registrations)
            {
                if (ReferenceEquals(key, callerKey)) continue;

                var frame = TimeToFrame(timeSeconds, reg.Fps, reg.TotalFrame);
                var ch = (int)Math.Round(reg.Parameter.Channel.GetValue(frame, reg.TotalFrame, reg.Fps));
                if (targetChannel >= 0 && ch != targetChannel) continue;

                var hasEnemy = reg.Parameter.Emitters.Count > 0 &&
                               reg.Parameter.Emitters.Any(e => e.IsEnabled.GetValue(frame, reg.TotalFrame, reg.Fps) >= 0.5);
                if (!hasEnemy) continue;

                var emitter = reg.Parameter.Emitters[0];
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

                position = pos;
                radius = reg.Parameter.EnemyRadius.GetValue(frame, reg.TotalFrame, reg.Fps);
                return true;
            }
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
        lock (Gate)
        {
            foreach (var (key, reg) in Registrations)
            {
                if (ReferenceEquals(key, callerKey)) continue;

                var frame = TimeToFrame(timeSeconds, reg.Fps, reg.TotalFrame);
                var ch = (int)Math.Round(reg.Parameter.Channel.GetValue(frame, reg.TotalFrame, reg.Fps));
                if (targetChannel >= 0 && ch != targetChannel) continue;

                var col = reg.Parameter.CollisionEnabled.GetValue(frame, reg.TotalFrame, reg.Fps) >= 0.5;
                var show = reg.Parameter.ShowTargetMarker.GetValue(frame, reg.TotalFrame, reg.Fps) >= 0.5;
                var hasTarget = col && (show || reg.Parameter.HasCustomTargetImage);
                if (!hasTarget) continue;

                position = new Vec2(
                    reg.Parameter.TargetX.GetValue(frame, reg.TotalFrame, reg.Fps),
                    reg.Parameter.TargetY.GetValue(frame, reg.TotalFrame, reg.Fps));
                radius = reg.Parameter.TargetRadius.GetValue(frame, reg.TotalFrame, reg.Fps);
                return true;
            }
        }

        position = Vec2.Zero;
        radius = 0;
        return false;
    }

    /// <summary>
    /// 指定した時刻 <paramref name="timeSeconds"/> までに他レイヤーの自機ショットから受けた累積ダメージを取得する。
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

                if (reg.Simulator is { } sim)
                {
                    // シミュレータがまだ現在時刻まで進んでいなければ進める
                    if (sim.CurrentTime < timeSeconds)
                    {
                        sim.SeekTo(timeSeconds);
                    }

                    // 履歴から timeSeconds までの被弾ダメージを合算
                    foreach (var (hitTime, dmg, hitTargetCh) in sim.Engine.DamageHistory)
                    {
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
    /// </summary>
    public static bool TryCancelEnemyBulletAt(int channel, object callerKey, Vec2 bulletPos, double bulletRadius)
    {
        lock (Gate)
        {
            foreach (var (key, reg) in Registrations)
            {
                if (ReferenceEquals(key, callerKey)) continue;

                if (reg.Simulator is not { } sim) continue;

                var targetCh = reg.Parameter.PlayerShotTargetChannel;
                if (channel >= 0 && targetCh >= 0 && targetCh != channel) continue;

                foreach (var bullet in sim.Bullets)
                {
                    if (bullet.IsPlayerShot && bullet.IsAlive && bullet.CancelEnemyBullets)
                    {
                        var r = bullet.HitRadius * Math.Abs(bullet.Scale) + bulletRadius;
                        if (bulletPos.DistanceSquaredTo(bullet.Position) <= r * r)
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
