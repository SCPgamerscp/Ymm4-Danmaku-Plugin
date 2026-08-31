using Ymm4DanmakuPlugin.Collision;
using Ymm4DanmakuPlugin.Core.Engine;
using Ymm4DanmakuPlugin.Core.Mathematics;
using Ymm4DanmakuPlugin.Parameters;

namespace Ymm4DanmakuPlugin;

/// <summary>
/// 映像側 (DanmakuShapeSource) と 音声側 (DanmakuSingleSoundProcessor) で
/// シミュレーターへタイムラインのキーフレーム値を同期するための共通配線処理。
/// </summary>
public static class DanmakuLiveWiring
{
    public static int TimeToFrame(double timeSeconds, int fps, int totalFrame)
    {
        if (fps <= 0) fps = 60;
        var frame = (int)Math.Round(timeSeconds * fps);
        return Math.Clamp(frame, 0, Math.Max(0, totalFrame - 1));
    }

    public static void WireLiveValues(
        DanmakuShapeParameter parameter,
        DanmakuSimulator sim,
        int fps,
        int totalFrame,
        object? sourceKey = null)
    {
        var emitters = parameter.Emitters;

        sim.Live.EmitterPosition = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return new Vec2(
                emitter.X.GetValue(frame, totalFrame, fps),
                emitter.Y.GetValue(frame, totalFrame, fps));
        };

        sim.Live.TargetPosition = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            var channel = (int)Math.Round(parameter.Channel.GetValue(frame, totalFrame, fps));
            if (sourceKey is not null && DanmakuCollisionBus.TryGetTargetAt(channel, sourceKey, timeSeconds, fps, totalFrame, out var extTargetPos, out _))
            {
                return extTargetPos;
            }
            return new Vec2(
                parameter.TargetX.GetValue(frame, totalFrame, fps),
                parameter.TargetY.GetValue(frame, totalFrame, fps));
        };

        sim.Live.EmitterOrbitRadius = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.OrbitRadius.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterOrbitSpeed = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.OrbitSpeed.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterOrbitPhase = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.OrbitPhase.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterMagicCircleRotationSpeed = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.MagicCircleRotationSpeed.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSeedOffset = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return (int)Math.Round(emitter.SeedOffset.GetValue(frame, totalFrame, fps));
        };

        sim.Live.EmitterScriptSpeedScale = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.ScriptSpeedScale.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterScriptRank = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.ScriptRank.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterAngle = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.BaseAngle.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterAimRate = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.AimRate.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterWay = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return Math.Max(0, (int)Math.Round(emitter.Way.GetValue(frame, totalFrame, fps)));
        };

        sim.Live.EmitterStack = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return Math.Max(0, (int)Math.Round(emitter.Stack.GetValue(frame, totalFrame, fps)));
        };

        sim.Live.EmitterStackSpeedStep = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.StackSpeedStep.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterStackAngleStep = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.StackAngleStep.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSpreadAngle = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.SpreadAngle.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterAngleStepPerShot = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.AngleStepPerShot.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterAngleJitter = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.AngleJitter.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterFireInterval = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.FireInterval.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterBurstCount = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return Math.Max(0, (int)Math.Round(emitter.BurstCount.GetValue(frame, totalFrame, fps)));
        };

        sim.Live.EmitterBurstInterval = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.BurstInterval.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterBurstCooldown = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.BurstCooldown.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterStartTime = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.StartTime.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterEndTime = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.EndTime.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSpawnRadius = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.SpawnRadius.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSpawnJitter = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.SpawnJitter.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterWallWidth = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.WallWidth.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterLaserSpacing = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.LaserSpacing.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterWhipAmplitude = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.WhipAmplitude.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterWhipPeriod = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.WhipPeriod.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSpeed = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.Speed.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSpeedJitter = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.SpeedJitter.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSpeedStep = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.SpeedStep.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterAcceleration = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.Acceleration.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterAngularVelocity = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.AngularVelocity.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterAngularVelocityJitter = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.AngularVelocityJitter.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterDamping = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.Damping.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterMinSpeed = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.MinSpeed.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterMaxSpeed = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.MaxSpeed.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterGravity = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.Gravity.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterWind = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.Wind.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterLifetime = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.Lifetime.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterLifetimeJitter = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.LifetimeJitter.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterHomingTurnRate = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.HomingTurnRate.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterHomingDuration = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.HomingDuration.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterHomingDelay = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.HomingDelay.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterHitRadius = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.HitRadius.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterScale = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.Scale.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterScaleJitter = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.ScaleJitter.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterScaleVelocity = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.ScaleVelocity.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterRotationVelocity = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.RotationVelocity.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterHueVelocity = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.HueVelocity.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterHueStep = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.HueStep.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterOpacity = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.Opacity.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterFadeInDuration = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.FadeInDuration.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterFadeOutDuration = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.FadeOutDuration.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterTrailLength = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return (int)Math.Round(emitter.TrailLength.GetValue(frame, totalFrame, fps));
        };

        sim.Live.EmitterTrailInterval = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.TrailInterval.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterTrailFade = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.TrailFade.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterTrailScale = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.TrailScale.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSplitDelay = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.SplitDelay.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSplitCount = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return (int)Math.Round(emitter.SplitCount.GetValue(frame, totalFrame, fps));
        };

        sim.Live.EmitterSplitSpread = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.SplitSpread.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSplitSpeed = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.SplitSpeed.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSplitScaleFactor = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.SplitScaleFactor.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSplitMaxGeneration = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return (int)Math.Round(emitter.SplitMaxGeneration.GetValue(frame, totalFrame, fps));
        };

        // 全体設定
        sim.Live.Seed = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return Math.Max(0, (int)Math.Round(parameter.Seed.GetValue(frame, totalFrame, fps)));
        };

        sim.Live.MaxBullets = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return Math.Max(0, (int)Math.Round(parameter.MaxBullets.GetValue(frame, totalFrame, fps)));
        };

        sim.Live.TimeScale = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.TimeScale.GetValue(frame, totalFrame, fps);
        };

        sim.Live.Channel = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return Math.Max(0, (int)Math.Round(parameter.Channel.GetValue(frame, totalFrame, fps)));
        };

        sim.Live.BoundsMargin = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.BoundsMargin.GetValue(frame, totalFrame, fps);
        };

        sim.Live.TargetRadius = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            var channel = (int)Math.Round(parameter.Channel.GetValue(frame, totalFrame, fps));
            if (sourceKey is not null && DanmakuCollisionBus.TryGetTargetAt(channel, sourceKey, timeSeconds, fps, totalFrame, out _, out var extTargetRadius))
            {
                return extTargetRadius;
            }
            return parameter.TargetRadius.GetValue(frame, totalFrame, fps);
        };

        sim.Live.TargetScale = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.TargetScale.GetValue(frame, totalFrame, fps);
        };

        sim.Live.TargetRotation = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.TargetRotation.GetValue(frame, totalFrame, fps);
        };

        sim.Live.TargetOpacity = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.TargetOpacity.GetValue(frame, totalFrame, fps);
        };

        sim.Live.HitEffectCount = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return Math.Max(0, (int)Math.Round(parameter.HitEffectCount.GetValue(frame, totalFrame, fps)));
        };

        sim.Live.HitEffectSpeed = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.HitEffectSpeed.GetValue(frame, totalFrame, fps);
        };

        sim.Live.HitEffectLifetime = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.HitEffectLifetime.GetValue(frame, totalFrame, fps);
        };

        // 自機ショット
        sim.Live.PlayerShotWay = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return (int)Math.Round(parameter.PlayerShotWay.GetValue(frame, totalFrame, fps));
        };

        sim.Live.PlayerShotInterval = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.PlayerShotInterval.GetValue(frame, totalFrame, fps);
        };

        sim.Live.PlayerShotSpeed = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.PlayerShotSpeed.GetValue(frame, totalFrame, fps);
        };

        sim.Live.PlayerShotSpread = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.PlayerShotSpread.GetValue(frame, totalFrame, fps);
        };

        sim.Live.PlayerShotScale = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.PlayerShotScale.GetValue(frame, totalFrame, fps);
        };

        sim.Live.PlayerShotHitRadius = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.PlayerShotHitRadius.GetValue(frame, totalFrame, fps);
        };

        sim.Live.Targets = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            var channel = (int)Math.Round(parameter.Channel.GetValue(frame, totalFrame, fps));
            var externalTargets = sourceKey is not null
                ? DanmakuCollisionBus.GetTargetsAt(channel, sourceKey, timeSeconds)
                : null;
            if (externalTargets is { Count: > 0 })
            {
                return externalTargets;
            }
            return null;
        };

        sim.Live.Enemies = timeSeconds =>
        {
            var externalEnemies = sourceKey is not null
                ? DanmakuCollisionBus.GetEnemiesAt(parameter.PlayerShotTargetChannel, sourceKey, timeSeconds)
                : null;
            if (externalEnemies is { Count: > 0 })
            {
                return externalEnemies;
            }
            return null;
        };

        sim.Live.EnemyPosition = timeSeconds =>
        {
            if (sourceKey is not null && DanmakuCollisionBus.TryGetEnemyAt(parameter.PlayerShotTargetChannel, sourceKey, timeSeconds, fps, totalFrame, out var pos, out _))
            {
                return pos;
            }
            // 自レイヤーにエミッターが存在する場合はエンジン内部で正確に積分された contexts[0].Position を優先するため null を返す
            return null;
        };

        sim.Live.EnemyRadius = timeSeconds =>
        {
            if (sourceKey is not null && DanmakuCollisionBus.TryGetEnemyAt(parameter.PlayerShotTargetChannel, sourceKey, timeSeconds, fps, totalFrame, out _, out var radius))
            {
                return radius;
            }
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.EnemyRadius.GetValue(frame, totalFrame, fps);
        };

        sim.Live.ExternalDamage = timeSeconds =>
        {
            if (sourceKey is null) return 0.0;
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            var channel = (int)Math.Round(parameter.Channel.GetValue(frame, totalFrame, fps));
            return DanmakuCollisionBus.GetExternalDamageAt(channel, sourceKey, timeSeconds, fps, totalFrame);
        };

        sim.Live.OnDamageDealt = (damage, emitterIndex) =>
        {
            // エンジン内部で DamageHistory に記録されるためここでは何もしない
        };

        sim.Live.IsBulletCancelledByExternalShot = (bulletPos, bulletRadius) =>
        {
            if (sourceKey is null) return false;
            var frame = TimeToFrame(sim.CurrentTime, fps, totalFrame);
            var channel = (int)Math.Round(parameter.Channel.GetValue(frame, totalFrame, fps));
            return DanmakuCollisionBus.TryCancelEnemyBulletAt(channel, sourceKey, bulletPos, bulletRadius);
        };

        // ボス体力バー (HP ゲージ)
        sim.Live.BossHp = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.BossHp.GetValue(frame, totalFrame, fps);
        };

        sim.Live.HpBarRadius = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.HpBarRadius.GetValue(frame, totalFrame, fps);
        };

        sim.Live.HpBarWidth = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.HpBarWidth.GetValue(frame, totalFrame, fps);
        };

        sim.Live.HpBarHeight = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.HpBarHeight.GetValue(frame, totalFrame, fps);
        };

        sim.Live.HpBarX = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.HpBarX.GetValue(frame, totalFrame, fps);
        };

        sim.Live.HpBarY = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.HpBarY.GetValue(frame, totalFrame, fps);
        };

        sim.Live.HpBarOpacity = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.HpBarOpacity.GetValue(frame, totalFrame, fps);
        };

        // ---- トグル・スイッチ系 (キーフレーム 0/1 切替) ----
        sim.Live.EmitterIsEnabled = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitters[index].IsEnabled.GetValue(frame, totalFrame, fps) >= 0.5;
        };

        sim.Live.EmitterHomingEnabled = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitters[index].HomingEnabled.GetValue(frame, totalFrame, fps) >= 0.5;
        };

        sim.Live.EmitterAdditive = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitters[index].Additive.GetValue(frame, totalFrame, fps) >= 0.5;
        };

        sim.Live.EmitterAlignToDirection = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitters[index].AlignToDirection.GetValue(frame, totalFrame, fps) >= 0.5;
        };

        sim.Live.EmitterSplitEnabled = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitters[index].SplitEnabled.GetValue(frame, totalFrame, fps) >= 0.5;
        };

        sim.Live.EmitterAuraEnabled = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitters[index].AuraEnabled.GetValue(frame, totalFrame, fps) >= 0.5;
        };

        sim.Live.EmitterMagicCircleEnabled = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitters[index].MagicCircleEnabled.GetValue(frame, totalFrame, fps) >= 0.5;
        };

        sim.Live.EmitterEnemyBehindBullets = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitters[index].EnemyBehindBullets.GetValue(frame, totalFrame, fps) >= 0.5;
        };

        sim.Live.EmitterScriptLoop = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitters[index].ScriptLoop.GetValue(frame, totalFrame, fps) >= 0.5;
        };

        sim.Live.CollisionEnabled = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.CollisionEnabled.GetValue(frame, totalFrame, fps) >= 0.5;
        };

        sim.Live.EnemyHitEnabled = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.EnemyHitEnabled.GetValue(frame, totalFrame, fps) >= 0.5;
        };

        sim.Live.SpawnHitEffect = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.SpawnHitEffect.GetValue(frame, totalFrame, fps) >= 0.5;
        };

        sim.Live.ShowTargetMarker = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.ShowTargetMarker.GetValue(frame, totalFrame, fps) >= 0.5;
        };

        sim.Live.PlayerShotEnabled = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.PlayerShotEnabled.GetValue(frame, totalFrame, fps) >= 0.5;
        };

        sim.Live.PlayerShotAlignToDirection = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.PlayerShotAlignToDirection.GetValue(frame, totalFrame, fps) >= 0.5;
        };

        sim.Live.PlayerShotAdditive = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.PlayerShotAdditive.GetValue(frame, totalFrame, fps) >= 0.5;
        };

        sim.Live.PlayerShotAutoAim = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.PlayerShotAutoAim.GetValue(frame, totalFrame, fps) >= 0.5;
        };

        sim.Live.PlayerShotCancelEnemyBullets = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.PlayerShotCancelEnemyBullets.GetValue(frame, totalFrame, fps) >= 0.5;
        };

        sim.Live.PlayerShotDestroyOnHit = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.PlayerShotDestroyOnHit.GetValue(frame, totalFrame, fps) >= 0.5;
        };

        sim.Live.EmitterDestroyOnHit = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitters[index].DestroyOnHit.GetValue(frame, totalFrame, fps) >= 0.5;
        };

        sim.Live.HpBarEnabled = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.HpBarEnabled.GetValue(frame, totalFrame, fps) >= 0.5;
        };

        sim.Live.HpBarDamagePerHit = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.DamagePerHit.GetValue(frame, totalFrame, fps);
        };
    }
}
