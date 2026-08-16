using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Mathematics;

namespace Ymm4DanmakuPlugin.Core.Engine;

/// <summary>
/// 第 1 階層「GUI プリセットスライダー」による弾幕生成。
/// <see cref="PatternSettings"/> の内容に従って発射タイミングと弾の配置を決定する。
/// </summary>
public sealed class PatternEmitterBehavior(EmitterSettings settings) : IEmitterBehavior
{
    /// <summary>1 ステップで処理する発射回数の上限 (無限ループ防止)。</summary>
    private const int MaxShotsPerStep = 64;

    /// <summary>発射間隔の下限 (秒)。</summary>
    private const double MinInterval = 1.0 / 480.0;

    private readonly EmitterSettings settings = settings;

    private double nextShotTime;
    private int shotIndex;
    private int burstIndex;

    public void Reset()
    {
        nextShotTime = settings.Pattern.StartTime;
        shotIndex = 0;
        burstIndex = 0;
    }

    public void Update(EmitterContext context, double deltaTime)
    {
        var pattern = settings.Pattern;
        var start = pattern.StartTime;
        var end = pattern.EndTime > 0 ? pattern.EndTime : double.PositiveInfinity;

        var stepStart = context.Time;
        var stepEnd = stepStart + deltaTime;

        if (nextShotTime < start) nextShotTime = start;

        var guard = 0;
        while (nextShotTime < stepEnd && nextShotTime <= end && guard++ < MaxShotsPerStep)
        {
            if (nextShotTime >= stepStart)
                FireOnce(context, nextShotTime);

            shotIndex++;
            burstIndex++;

            if (pattern.BurstCount > 1 && burstIndex < pattern.BurstCount)
            {
                nextShotTime += Math.Max(MinInterval, pattern.BurstInterval);
            }
            else
            {
                burstIndex = 0;
                var interval = context.EmitterFireInterval(nextShotTime) ?? pattern.FireInterval;
                nextShotTime += Math.Max(MinInterval, interval + pattern.BurstCooldown);
            }
        }

        // タイムラインを大きく飛ばした場合に while が回りきらないケースを補正する
        if (nextShotTime < stepStart)
            nextShotTime = stepStart;
    }

    private void FireOnce(EmitterContext context, double fireTime)
    {
        var pattern = settings.Pattern;
        var physics = settings.Physics;
        var appearance = settings.Appearance;

        var way = Math.Max(1, pattern.Way);
        var stack = Math.Max(1, pattern.Stack);

        var baseAngle = context.EmitterAngle(fireTime) ?? pattern.BaseAngle;
        if (pattern.AimAtTarget || pattern.Kind == PatternKind.Aimed)
            baseAngle = context.AngleToTarget() + baseAngle;

        var angleStepPerShot = context.EmitterAngleStepPerShot(fireTime) ?? pattern.AngleStepPerShot;
        if (angleStepPerShot != 0)
            baseAngle += angleStepPerShot * shotIndex;

        if (pattern.AngleJitter > 0)
            baseAngle += context.Random.NextSymmetric(pattern.AngleJitter);

        if (pattern.Kind == PatternKind.Whip)
        {
            var period = Math.Max(0.05, pattern.WhipPeriod);
            baseAngle += pattern.WhipAmplitude * Math.Sin(DanmakuMath.Tau * fireTime / period);
        }

        var spreadAngle = context.EmitterSpreadAngle(fireTime) ?? pattern.SpreadAngle;
        var spawnRadius = context.EmitterSpawnRadius(fireTime) ?? pattern.SpawnRadius;
        var baseSpeed = context.EmitterSpeed(fireTime) ?? physics.Speed;
        var angularVelocity = context.EmitterAngularVelocity(fireTime) ?? physics.AngularVelocity;
        var gravity = context.EmitterGravity(fireTime) ?? physics.Gravity;
        var wind = context.EmitterWind(fireTime) ?? physics.Wind;
        var scale = context.EmitterScale(fireTime) ?? appearance.Scale;
        var rotationVelocity = context.EmitterRotationVelocity(fireTime) ?? appearance.RotationVelocity;

        var indexInBurst = 0;
        var fired = false;

        for (var s = 0; s < stack; s++)
        {
            var stackAngle = baseAngle + pattern.StackAngleStep * s;

            // Bloom は段ごとに半ステップずらして花弁状にする
            if (pattern.Kind == PatternKind.Bloom && stack > 1)
                stackAngle += 360.0 / way / stack * s;

            var stackSpeed = pattern.StackSpeedStep * s;

            for (var i = 0; i < way; i++)
            {
                var request = BulletSpawnRequest.Create(physics, appearance);
                request.EmitterIndex = context.EmitterIndex;
                request.ShotIndex = shotIndex;
                request.IndexInBurst = indexInBurst;
                request.PlayFireSound = !fired; // 1 回の発射につき 1 音のみ
                request.Split = settings.Split;
                request.SplitDelay = settings.SplitDelay;

                var (direction, offset, extraSpeed) = ResolveBullet(context, pattern, way, i, stackAngle, spreadAngle, spawnRadius);

                request.Direction = direction;
                request.Position = context.Position + offset;
                request.SpeedOverride = baseSpeed + stackSpeed + extraSpeed;
                request.AngularVelocityOverride = angularVelocity;
                request.GravityOverride = gravity;
                request.WindOverride = wind;
                request.ScaleOverride = scale;
                request.RotationVelocityOverride = rotationVelocity;

                if (spawnRadius > 0 && pattern.Kind != PatternKind.Laser && pattern.Kind != PatternKind.Wall)
                    request.Position += Vec2.FromDegrees(direction, spawnRadius);

                if (pattern.SpawnJitter > 0)
                {
                    request.Position += new Vec2(
                        context.Random.NextSymmetric(pattern.SpawnJitter),
                        context.Random.NextSymmetric(pattern.SpawnJitter));
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
        double spawnRadius)
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
                var width = pattern.WallWidth;
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
                var extraSpeed = pattern.StackSpeedStep * Math.Sqrt(index);
                return (angle, Vec2.Zero, extraSpeed);
            }

            case PatternKind.Laser:
            {
                var distance = spawnRadius + pattern.LaserSpacing * index;
                return (baseAngle, Vec2.FromDegrees(baseAngle, distance), 0);
            }

            case PatternKind.Whip:
            {
                var spread = spreadAngle;
                var step = way > 1 ? spread / (way - 1) : 0;
                var offsetAngle = way > 1 ? -spread / 2 + step * index : 0;
                return (baseAngle + offsetAngle, Vec2.Zero, 0);
            }

            default:
                return (baseAngle, Vec2.Zero, 0);
        }
    }
}
