using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Mathematics;

namespace Ymm4DanmakuPlugin.Core.Engine;

/// <summary>
/// 第 3 階層「外部データ読み込み」のうち、JSON / Lua から得られた
/// フラットな発射命令列 (<see cref="ScriptedShotProgram"/>) を再生するエミッター。
/// </summary>
public sealed class ScriptedShotEmitterBehavior(EmitterSettings settings, ScriptedShotProgram program)
    : IEmitterBehavior
{
    private const int MaxShotsPerStep = 512;

    private readonly EmitterSettings settings = settings;
    private readonly ScriptedShotProgram program = program;

    private int cursor;
    private int loopCount;

    public void Reset()
    {
        cursor = 0;
        loopCount = 0;
    }

    public void Update(EmitterContext context, double deltaTime)
    {
        if (program.Shots.Count == 0) return;

        var start = settings.Pattern.StartTime;
        var end = settings.Pattern.EndTime > 0 ? settings.Pattern.EndTime : double.PositiveInfinity;

        var stepStart = context.Time;
        var stepEnd = stepStart + deltaTime;

        var guard = 0;
        while (guard++ < MaxShotsPerStep)
        {
            if (cursor >= program.Shots.Count)
            {
                if (program.LoopDuration <= 0) return;
                cursor = 0;
                loopCount++;
                continue;
            }

            var shot = program.Shots[cursor];
            var shotTime = start + shot.Time + program.LoopDuration * loopCount;

            if (shotTime >= stepEnd) return;
            if (shotTime > end) return;

            cursor++;

            if (shotTime >= stepStart)
                Fire(context, shot);
        }
    }

    private void Fire(EmitterContext context, ScriptedShot shot)
    {
        var way = Math.Max(1, shot.Way);
        var baseAngle = shot.AimAtTarget ? context.AngleToTarget() + shot.Angle : shot.Angle;

        var spread = shot.Spread;
        var isFullCircle = Math.Abs(Math.Abs(spread) - 360.0) < 1e-6;
        var step = way > 1 ? (isFullCircle ? spread / way : spread / (way - 1)) : 0;
        var startAngle = isFullCircle || way <= 1 ? baseAngle : baseAngle - spread / 2;

        var physics = settings.Physics with
        {
            Speed = shot.Speed,
            Acceleration = shot.Acceleration != 0 ? shot.Acceleration : settings.Physics.Acceleration,
            AngularVelocity = shot.AngularVelocity != 0 ? shot.AngularVelocity : settings.Physics.AngularVelocity,
            Lifetime = shot.Lifetime > 0 ? shot.Lifetime : settings.Physics.Lifetime,
            HomingEnabled = shot.Homing ?? settings.Physics.HomingEnabled,
        };

        for (var i = 0; i < way; i++)
        {
            var request = BulletSpawnRequest.Create(physics, settings.Appearance);
            request.EmitterIndex = context.EmitterIndex;
            request.IndexInBurst = i;
            request.Direction = startAngle + step * i;
            request.Position = context.Position + new Vec2(shot.OffsetX, shot.OffsetY);
            request.SpeedOverride = shot.Speed;
            request.SpriteIndexOverride = shot.SpriteIndex;
            request.ColorOverride = shot.Color;
            request.ScaleFactor = shot.ScaleFactor;
            request.LifetimeOverride = shot.Lifetime;
            request.Split = shot.Split ?? settings.Split;
            request.SplitDelay = shot.Split is not null ? shot.SplitDelay : settings.SplitDelay;
            request.PlayFireSound = shot.PlaySound && i == 0;

            context.Spawn(in request);
        }
    }
}
