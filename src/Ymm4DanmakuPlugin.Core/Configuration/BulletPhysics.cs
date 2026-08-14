using Ymm4DanmakuPlugin.Core.Mathematics;
using Ymm4DanmakuPlugin.Core.Model;

namespace Ymm4DanmakuPlugin.Core.Configuration;

/// <summary>
/// 第 2 階層「高度パラメータ物理計算」に相当する弾の挙動設定。
/// </summary>
public sealed record BulletPhysics
{
    /// <summary>初速 (px/秒)。</summary>
    public double Speed { get; init; } = 220;

    /// <summary>初速のランダム幅 (±)。</summary>
    public double SpeedJitter { get; init; }

    /// <summary>弾ごとの初速の増分 (n-way の内側/外側で速度差をつける)。</summary>
    public double SpeedStep { get; init; }

    /// <summary>進行方向の加速度 (px/秒^2)。</summary>
    public double Acceleration { get; init; }

    /// <summary>旋回速度 (度/秒)。正で時計回り。</summary>
    public double AngularVelocity { get; init; }

    /// <summary>旋回速度のランダム幅 (±)。</summary>
    public double AngularVelocityJitter { get; init; }

    /// <summary>速度減衰 (1 秒後に残る割合)。1 で減衰なし。</summary>
    public double Damping { get; init; } = 1.0;

    /// <summary>速度下限 (px/秒)。</summary>
    public double MinSpeed { get; init; } = 0;

    /// <summary>速度上限 (px/秒)。</summary>
    public double MaxSpeed { get; init; } = 2000;

    /// <summary>重力加速度 (px/秒^2)。正で下向き。</summary>
    public double Gravity { get; init; }

    /// <summary>風 (横方向の一様加速度、px/秒^2)。</summary>
    public double Wind { get; init; }

    /// <summary>弾の寿命 (秒)。0 以下で無限。</summary>
    public double Lifetime { get; init; } = 6.0;

    /// <summary>寿命のランダム幅 (±秒)。</summary>
    public double LifetimeJitter { get; init; }

    // ---- ホーミング ----
    /// <summary>ホーミングを有効にする。</summary>
    public bool HomingEnabled { get; init; }

    /// <summary>ホーミングの旋回力 (度/秒)。</summary>
    public double HomingTurnRate { get; init; } = 90;

    /// <summary>ホーミングを行う時間 (秒)。0 以下で寿命いっぱい。</summary>
    public double HomingDuration { get; init; } = 1.5;

    /// <summary>発射後、ホーミングを開始するまでの遅延 (秒)。</summary>
    public double HomingDelay { get; init; }

    // ---- 当たり判定 ----
    /// <summary>当たり判定半径 (px)。0 以下で判定なし。</summary>
    public double HitRadius { get; init; }

    /// <summary>衝突時に弾を消滅させる。</summary>
    public bool DestroyOnHit { get; init; } = true;

    /// <summary>プールから弾を確保した際に物理設定を適用する。</summary>
    public void Apply(Bullet bullet, DeterministicRandom random, int indexInBurst)
    {
        var speed = Speed + SpeedStep * indexInBurst;
        if (SpeedJitter > 0) speed += random.NextSymmetric(SpeedJitter);

        bullet.Speed = speed;
        bullet.Acceleration = Acceleration;
        bullet.AngularVelocity = AngularVelocity +
                                 (AngularVelocityJitter > 0 ? random.NextSymmetric(AngularVelocityJitter) : 0);
        bullet.Damping = DanmakuMath.Clamp(Damping, 0.0, 1.0);
        bullet.MinSpeed = MinSpeed;
        bullet.MaxSpeed = MaxSpeed <= 0 ? double.PositiveInfinity : MaxSpeed;
        bullet.ExternalAcceleration = new Vec2(Wind, Gravity);

        var lifetime = Lifetime;
        if (lifetime <= 0)
        {
            bullet.Lifetime = double.PositiveInfinity;
        }
        else
        {
            if (LifetimeJitter > 0) lifetime += random.NextSymmetric(LifetimeJitter);
            bullet.Lifetime = Math.Max(0.05, lifetime);
        }

        bullet.HomingEnabled = HomingEnabled;
        bullet.HomingTurnRate = HomingTurnRate;
        bullet.HomingDelay = Math.Max(0, HomingDelay);
        bullet.HomingRemaining = HomingEnabled
            ? (HomingDuration > 0 ? HomingDuration : double.PositiveInfinity)
            : 0;

        bullet.HitRadius = HitRadius;
        bullet.DestroyOnHit = DestroyOnHit;
    }
}
