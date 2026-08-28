using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Mathematics;
using Ymm4DanmakuPlugin.Core.Model;

namespace Ymm4DanmakuPlugin.Core.Engine;

/// <summary>
/// 第 1 階層「GUI プリセットスライダー」による弾幕生成。
/// <see cref="PatternSettings"/> の内容に従って発射タイミングと弾の配置を決定する。
/// </summary>
public sealed class PatternEmitterBehavior(EmitterSettings settings) : IEmitterBehavior
{
    /// <summary>1 ステップで処理する発射回数の上限 (無限ループ防止)。</summary>
    private const int MaxShotsPerStep = 4096;

    /// <summary>発射間隔の下限 (秒)。完全無制限。</summary>
    private const double MinInterval = 0.0;

    private readonly EmitterSettings settings = settings;

    private double nextShotTime;
    private int shotIndex;
    private int burstIndex;
    private double accumulatedAngleStep;
    private double whipPhase;

    public void Reset()
    {
        nextShotTime = settings.Pattern.StartTime;
        shotIndex = 0;
        burstIndex = 0;
        accumulatedAngleStep = 0.0;
        whipPhase = 0.0;
    }

    public void Update(EmitterContext context, double deltaTime)
    {
        var pattern = settings.Pattern;
        var stepStart = context.Time;
        var stepEnd = stepStart + deltaTime;

        if (pattern.Kind == PatternKind.Whip)
        {
            var period = Math.Max(0.05, context.EmitterWhipPeriod(stepStart) ?? pattern.WhipPeriod);
            whipPhase += (DanmakuMath.Tau / period) * deltaTime;
        }

        var start = context.EmitterStartTime(stepStart) ?? pattern.StartTime;
        var rawEnd = context.EmitterEndTime(stepStart) ?? pattern.EndTime;
        var end = rawEnd > 0 ? rawEnd : double.PositiveInfinity;

        if (nextShotTime < start) nextShotTime = start;

        var guard = 0;
        while (nextShotTime < stepEnd && nextShotTime <= end && guard++ < MaxShotsPerStep)
        {
            FireOnce(context, nextShotTime);

            shotIndex++;
            burstIndex++;

            var burstCount = Math.Max(1, context.EmitterBurstCount(nextShotTime) ?? pattern.BurstCount);
            var burstInterval = Math.Max(MinInterval, context.EmitterBurstInterval(nextShotTime) ?? pattern.BurstInterval);
            var burstCooldown = Math.Max(0.0, context.EmitterBurstCooldown(nextShotTime) ?? pattern.BurstCooldown);

            if (burstCount > 1 && burstIndex < burstCount)
            {
                nextShotTime += burstInterval;
            }
            else
            {
                burstIndex = 0;
                var interval = context.EmitterFireInterval(nextShotTime) ?? pattern.FireInterval;
                nextShotTime += Math.Max(MinInterval, interval + burstCooldown);
            }
        }
    }

    private void FireOnce(EmitterContext context, double fireTime)
    {
        var pattern = settings.Pattern;
        var physics = settings.Physics;
        var appearance = settings.Appearance;

        var way = Math.Max(0, context.EmitterWay(fireTime) ?? pattern.Way);
        var stack = Math.Max(0, context.EmitterStack(fireTime) ?? pattern.Stack);
        if (way <= 0 || stack <= 0) return;

        var baseAngle = context.EmitterAngle(fireTime) ?? pattern.BaseAngle;
        var defaultAimRate = pattern.Kind == PatternKind.Aimed ? 100.0 : (pattern.AimRate != 0 ? pattern.AimRate : (pattern.AimAtTarget ? 100.0 : 0.0));
        var rawAimRate = context.EmitterAimRate(fireTime) ?? defaultAimRate;
        var aimRate = DanmakuMath.Clamp(rawAimRate / 100.0, -1.0, 1.0);
        if (aimRate > 0)
        {
            baseAngle += context.AngleToTarget() * aimRate;
        }
        else if (aimRate < 0)
        {
            baseAngle += (context.AngleToTarget() + 180.0) * (-aimRate);
        }

        var angleStepPerShot = context.EmitterAngleStepPerShot(fireTime) ?? pattern.AngleStepPerShot;
        if (accumulatedAngleStep != 0)
            baseAngle += accumulatedAngleStep;
        accumulatedAngleStep += angleStepPerShot;

        var angleJitter = context.EmitterAngleJitter(fireTime) ?? pattern.AngleJitter;
        if (angleJitter != 0)
            baseAngle += context.Random.NextSymmetric(Math.Abs(angleJitter));

        if (pattern.Kind == PatternKind.Whip)
        {
            var amplitude = context.EmitterWhipAmplitude(fireTime) ?? pattern.WhipAmplitude;
            baseAngle += amplitude * Math.Sin(whipPhase);
        }

        var spreadAngle = context.EmitterSpreadAngle(fireTime) ?? pattern.SpreadAngle;
        var spawnRadius = context.EmitterSpawnRadius(fireTime) ?? pattern.SpawnRadius;
        var spawnJitter = context.EmitterSpawnJitter(fireTime) ?? pattern.SpawnJitter;
        var stackSpeedStep = context.EmitterStackSpeedStep(fireTime) ?? pattern.StackSpeedStep;
        var stackAngleStep = context.EmitterStackAngleStep(fireTime) ?? pattern.StackAngleStep;
        var wallWidth = context.EmitterWallWidth(fireTime) ?? pattern.WallWidth;
        var laserSpacing = context.EmitterLaserSpacing(fireTime) ?? pattern.LaserSpacing;

        var baseSpeed = context.EmitterSpeed(fireTime) ?? physics.Speed;
        var speedJitter = context.EmitterSpeedJitter(fireTime) ?? physics.SpeedJitter;
        var speedStep = context.EmitterSpeedStep(fireTime) ?? physics.SpeedStep;
        var acceleration = context.EmitterAcceleration(fireTime);
        var angularVelocity = context.EmitterAngularVelocity(fireTime) ?? physics.AngularVelocity;
        var angVelJitter = context.EmitterAngularVelocityJitter(fireTime) ?? physics.AngularVelocityJitter;
        var damping = context.EmitterDamping(fireTime);
        var minSpeed = context.EmitterMinSpeed(fireTime);
        var maxSpeed = context.EmitterMaxSpeed(fireTime);
        var gravity = context.EmitterGravity(fireTime) ?? physics.Gravity;
        var wind = context.EmitterWind(fireTime) ?? physics.Wind;
        var lifetime = context.EmitterLifetime(fireTime) ?? 0;
        var lifetimeJitter = context.EmitterLifetimeJitter(fireTime) ?? physics.LifetimeJitter;
        var homingTurnRate = context.EmitterHomingTurnRate(fireTime);
        var homingDuration = context.EmitterHomingDuration(fireTime);
        var homingDelay = context.EmitterHomingDelay(fireTime);
        var hitRadius = context.EmitterHitRadius(fireTime);

        var scale = context.EmitterScale(fireTime) ?? appearance.Scale;
        var scaleJitter = context.EmitterScaleJitter(fireTime) ?? appearance.ScaleJitter;
        var scaleVelocity = context.EmitterScaleVelocity(fireTime);
        var opacity = context.EmitterOpacity(fireTime);
        var rotationVelocity = context.EmitterRotationVelocity(fireTime) ?? appearance.RotationVelocity;
        var hueVelocity = context.EmitterHueVelocity(fireTime) ?? appearance.HueVelocity;
        var hueStep = context.EmitterHueStep(fireTime) ?? appearance.HueStep;
        var fadeIn = context.EmitterFadeInDuration(fireTime);
        var fadeOut = context.EmitterFadeOutDuration(fireTime);
        var trailLength = context.EmitterTrailLength(fireTime);
        var trailInterval = context.EmitterTrailInterval(fireTime);
        var trailFade = context.EmitterTrailFade(fireTime) ?? appearance.TrailFade;
        var trailScale = context.EmitterTrailScale(fireTime) ?? appearance.TrailScale;

        // 分裂の動的評価
        SplitSpec? split = settings.Split;
        var splitDelay = context.EmitterSplitDelay(fireTime) ?? settings.SplitDelay;
        if (split is not null)
        {
            var splitCount = Math.Max(0, context.EmitterSplitCount(fireTime) ?? split.Count);
            var splitSpread = context.EmitterSplitSpread(fireTime) ?? split.SpreadDegrees;
            var splitSpeed = context.EmitterSplitSpeed(fireTime) ?? split.Speed;
            var splitScaleFactor = context.EmitterSplitScaleFactor(fireTime) ?? split.ScaleFactor;
            var splitMaxGen = Math.Max(0, context.EmitterSplitMaxGeneration(fireTime) ?? split.MaxGeneration);
            split = split with
            {
                Count = splitCount,
                SpreadDegrees = splitSpread,
                Speed = splitSpeed,
                ScaleFactor = splitScaleFactor,
                MaxGeneration = splitMaxGen,
            };
        }

        var indexInBurst = 0;
        var fired = false;

        for (var s = 0; s < stack; s++)
        {
            var stackAngle = baseAngle + stackAngleStep * s;

            // Bloom は段ごとに半ステップずらして花弁状にする
            if (pattern.Kind == PatternKind.Bloom && stack > 1)
                stackAngle += 360.0 / way / stack * s;

            var stackSpeed = stackSpeedStep * s;

            for (var i = 0; i < way; i++)
            {
                var curPhysics = physics with
                {
                    SpeedJitter = speedJitter,
                    AngularVelocityJitter = angVelJitter,
                    LifetimeJitter = lifetimeJitter,
                };
                var curAppearance = appearance with
                {
                    ScaleJitter = scaleJitter,
                    HueVelocity = hueVelocity,
                    HueStep = hueStep,
                    TrailFade = trailFade,
                    TrailScale = trailScale,
                };

                var request = BulletSpawnRequest.Create(curPhysics, curAppearance);
                request.EmitterIndex = context.EmitterIndex;
                request.ShotIndex = shotIndex;
                request.IndexInBurst = indexInBurst;
                request.PlayFireSound = !fired; // 1 回の発射につき 1 音のみ
                request.Split = split;
                request.SplitDelay = splitDelay;

                var (direction, offset, extraSpeed) = ResolveBullet(context, pattern, way, i, stackAngle, spreadAngle, spawnRadius, wallWidth, laserSpacing, stackSpeedStep);

                // 弾ごとの速度差 (SpeedStep)
                var speedStepOffset = (i - (way - 1) / 2.0) * speedStep;

                request.Direction = direction;
                request.Position = context.Position + offset;
                request.SpeedOverride = baseSpeed + stackSpeed + extraSpeed + speedStepOffset;
                request.AngularVelocityOverride = angularVelocity;
                request.AccelerationOverride = acceleration;
                request.DampingOverride = damping;
                request.MinSpeedOverride = minSpeed;
                request.MaxSpeedOverride = maxSpeed;
                request.GravityOverride = gravity;
                request.WindOverride = wind;
                request.LifetimeOverride = lifetime;
                request.HomingTurnRateOverride = homingTurnRate;
                request.HomingDurationOverride = homingDuration;
                request.HomingDelayOverride = homingDelay;
                request.HitRadiusOverride = hitRadius;
                request.ScaleOverride = scale;
                request.ScaleVelocityOverride = scaleVelocity;
                request.OpacityOverride = opacity;
                request.RotationVelocityOverride = rotationVelocity;
                request.FadeInDurationOverride = fadeIn;
                request.FadeOutDurationOverride = fadeOut;
                request.TrailLengthOverride = trailLength;
                request.TrailIntervalOverride = trailInterval;

                if (spawnRadius != 0 && pattern.Kind != PatternKind.Laser && pattern.Kind != PatternKind.Wall)
                    request.Position += Vec2.FromDegrees(direction, spawnRadius);

                if (spawnJitter != 0)
                {
                    request.Position += new Vec2(
                        context.Random.NextSymmetric(Math.Abs(spawnJitter)),
                        context.Random.NextSymmetric(Math.Abs(spawnJitter)));
                }

                if (context.Spawn(in request) is not null)
                    fired = true;

                indexInBurst++;
            }
        }
    }

    /// <summary>パターン種別ごとに 1 発分の方向・位置オフセット・追加速度を求める。</summary>
    private (double Direction, Vec2 Offset, double ExtraSpeed) ResolveBullet(
        EmitterContext context,
        PatternSettings pattern,
        int way,
        int index,
        double baseAngle,
        double spreadAngle,
        double spawnRadius,
        double wallWidth,
        double laserSpacing,
        double stackSpeedStep)
    {
        switch (pattern.Kind)
        {
            case PatternKind.Circle:
            case PatternKind.Bloom:
            case PatternKind.Spiral:
            {
                var spread = Math.Abs(spreadAngle) < 1e-6 ? 360.0 : spreadAngle;
                var step = Math.Abs(spread - 360.0) < 1e-6 ? spread / way : spread / Math.Max(1, way - 1);
                var offsetAngle = Math.Abs(spread - 360.0) < 1e-6
                    ? step * index
                    : -spread / 2 + step * index;
                return (baseAngle + offsetAngle, Vec2.Zero, 0);
            }

            case PatternKind.Fan:
            case PatternKind.Aimed:
            case PatternKind.Whip:
            {
                var spread = spreadAngle;
                var step = way > 1 ? spread / (way - 1) : 0;
                var offsetAngle = way > 1 ? -spread / 2 + step * index : 0;
                return (baseAngle + offsetAngle, Vec2.Zero, 0);
            }

            case PatternKind.Scatter:
            {
                var spread = spreadAngle;
                var angle = baseAngle + context.Random.NextSymmetric(spread / 2);
                return (angle, Vec2.Zero, 0);
            }

            case PatternKind.Wall:
            {
                var width = wallWidth;
                var step = way > 1 ? width / (way - 1) : 0;
                // 1 発ごとに半ステップずらして隙間を互い違いにする
                var stagger = shotIndex % 2 == 0 ? 0 : step / 2;
                var x = -width / 2 + step * index + stagger;
                var perpendicular = Vec2.FromDegrees(baseAngle + 90, x);
                var forward = spawnRadius > 0 ? Vec2.FromDegrees(baseAngle, spawnRadius) : Vec2.Zero;

                // 拡散角度が 360 (既定) 以外に指定されていれば、壁の弾を扇状に広げる
                var angleOffset = 0.0;
                if (way > 1 && spreadAngle > 0 && Math.Abs(spreadAngle - 360.0) > 1e-4)
                {
                    angleOffset = -spreadAngle / 2 + (spreadAngle / (way - 1)) * index;
                }

                return (baseAngle + angleOffset, perpendicular + forward, 0);
            }

            case PatternKind.Rose:
            {
                var angle = baseAngle + DanmakuMath.GoldenAngleDegrees * index;
                var extraSpeed = stackSpeedStep * Math.Sqrt(index);
                return (angle, Vec2.Zero, extraSpeed);
            }

            case PatternKind.Laser:
            {
                var distance = spawnRadius + laserSpacing * index;
                return (baseAngle, Vec2.FromDegrees(baseAngle, distance), 0);
            }

            default:
                return (baseAngle, Vec2.Zero, 0);
        }
    }
}
