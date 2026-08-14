using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Mathematics;
using Ymm4DanmakuPlugin.Core.Model;
using Ymm4DanmakuPlugin.Core.Scripting;

namespace Ymm4DanmakuPlugin.Core.Engine;

/// <summary>エミッター本体を BulletML の実行主体として見せるアダプター。</summary>
internal sealed class EmitterBulletMlHost(DanmakuEngine engine, int emitterIndex) : IBulletMlHost
{
    private readonly DanmakuEngine engine = engine;
    private readonly int emitterIndex = emitterIndex;

    public EmitterContext Context { get; set; } = null!;

    public Vec2 SelfPosition => Context.Position;

    /// <summary>エミッター自身は移動しないが、sequence 指定のために方向を保持する。</summary>
    public double SelfDirection { get; set; }

    public double SelfSpeed { get; set; } = 1.0;

    public Vec2 TargetPosition => engine.TargetPosition;

    public double Rank => engine.Settings.Emitters[emitterIndex].ScriptRank;

    public DeterministicRandom Random => engine.Random;

    public void Fire(double direction, double speed, BulletMlBullet? definition, BulletMlRunner? runner)
    {
        var settings = engine.Settings.Emitters[emitterIndex];
        var request = BulletSpawnRequest.Create(settings.Physics, settings.Appearance);
        request.EmitterIndex = emitterIndex;
        request.Position = Context.Position;
        request.Direction = direction;
        request.SpeedOverride = speed * settings.ScriptSpeedScale;
        request.Script = runner;
        request.PlayFireSound = true;
        engine.Spawn(in request);
    }

    public void Vanish()
    {
        // エミッター本体は消滅しない
    }

    public void ApplyVelocityDelta(double deltaVx, double deltaVy)
    {
        var velocity = Vec2.FromDegrees(SelfDirection, SelfSpeed) + new Vec2(deltaVx, deltaVy);
        SelfDirection = velocity.Degrees;
        SelfSpeed = velocity.Length;
    }

    public void NotifyChange() => engine.EmitSound(DanmakuSoundKind.Change, emitterIndex);
}

/// <summary>個々の弾を BulletML の実行主体として見せるアダプター。</summary>
internal sealed class BulletBulletMlHost(DanmakuEngine engine) : IBulletMlHost
{
    private readonly DanmakuEngine engine = engine;

    public Bullet Bullet { get; set; } = null!;

    /// <summary>BulletML 単位 → px/秒 の換算係数。</summary>
    public double SpeedScale { get; set; } = 60;

    public Vec2 SelfPosition => Bullet.Position;

    public double SelfDirection
    {
        get => Bullet.Direction;
        set => Bullet.Direction = value;
    }

    public double SelfSpeed
    {
        get => Bullet.Speed / SpeedScale;
        set => Bullet.Speed = value * SpeedScale;
    }

    public Vec2 TargetPosition => engine.TargetPosition;

    public double Rank { get; set; } = 0.5;

    public DeterministicRandom Random => engine.Random;

    public void Fire(double direction, double speed, BulletMlBullet? definition, BulletMlRunner? runner)
    {
        var settings = engine.Settings.Emitters[Bullet.EmitterIndex];
        var request = BulletSpawnRequest.Create(settings.Physics, settings.Appearance);
        request.EmitterIndex = Bullet.EmitterIndex;
        request.Generation = Bullet.Generation + 1;
        request.Position = Bullet.Position;
        request.Direction = direction;
        request.SpeedOverride = speed * SpeedScale;
        request.Script = runner;
        request.PlayFireSound = false;
        engine.Spawn(in request);
    }

    public void Vanish() => engine.Kill(Bullet, BulletDeathReason.Vanished);

    public void ApplyVelocityDelta(double deltaVx, double deltaVy)
    {
        var velocity = Vec2.FromDegrees(Bullet.Direction, SelfSpeed) + new Vec2(deltaVx, deltaVy);
        Bullet.Direction = velocity.Degrees;
        SelfSpeed = velocity.Length;
    }

    public void NotifyChange() => engine.EmitSound(DanmakuSoundKind.Change, Bullet.EmitterIndex);
}
