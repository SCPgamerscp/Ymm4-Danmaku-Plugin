using System.Collections.Immutable;
using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Engine;
using Ymm4DanmakuPlugin.Core.Model;

namespace Ymm4DanmakuPlugin.Core.Tests;

/// <summary>
/// テスト用の設定を組み立てるヘルパー。
/// <para>
/// 既定値のままだと「毎秒 10 回発射・16way」で弾数が読みづらいため、
/// 検証しやすいよう「1 回だけ発射する」「等速直線運動」といった素直な設定を作れるようにしている。
/// </para>
/// </summary>
internal static class TestFactory
{
    /// <summary>実質 1 回しか発射しないほど長い発射間隔。</summary>
    public const double SingleShotInterval = 1000.0;

    /// <summary>1 回だけ発射する n-way リングのパターン設定。</summary>
    public static PatternSettings SingleShot(int way = 1, double baseAngle = 0) => new()
    {
        Kind = PatternKind.Circle,
        Way = way,
        Stack = 1,
        BaseAngle = baseAngle,
        SpreadAngle = 360,
        AngleStepPerShot = 0,
        FireInterval = SingleShotInterval,
        BurstCount = 1,
    };

    /// <summary>等速直線運動 (加速も減衰もホーミングもしない) の物理設定。</summary>
    public static BulletPhysics Straight(double speed = 200, double lifetime = 60) => new()
    {
        Speed = speed,
        SpeedJitter = 0,
        SpeedStep = 0,
        Acceleration = 0,
        AngularVelocity = 0,
        AngularVelocityJitter = 0,
        Damping = 1.0,
        MinSpeed = 0,
        MaxSpeed = 100000,
        Gravity = 0,
        Wind = 0,
        Lifetime = lifetime,
        LifetimeJitter = 0,
        HomingEnabled = false,
        HitRadius = 0,
    };

    /// <summary>トレイルもフェードも無い、検証しやすい見た目設定。</summary>
    public static BulletAppearance PlainAppearance() => new()
    {
        SpriteIndex = 0,
        SpriteCycleCount = 1,
        Scale = 1.0,
        ScaleJitter = 0,
        ScaleVelocity = 0,
        ColorMode = ColorMode.Single,
        Opacity = 1.0,
        FadeInDuration = 0,
        FadeOutDuration = 0,
        TrailLength = 0,
        AnimationFps = 0,
    };

    /// <summary>エミッター設定を作る。既定では原点から発射する。</summary>
    public static EmitterSettings Emitter(
        PatternSettings? pattern = null,
        BulletPhysics? physics = null,
        BulletAppearance? appearance = null,
        double x = 0,
        double y = 0) => new()
    {
        Name = "テスト",
        IsEnabled = true,
        X = x,
        Y = y,
        Pattern = pattern ?? SingleShot(),
        Physics = physics ?? Straight(),
        Appearance = appearance ?? PlainAppearance(),
    };

    /// <summary>弾幕設定を作る。画面外消滅を無効にして弾数を数えやすくしてある。</summary>
    public static DanmakuSettings Settings(
        EmitterSettings? emitter = null,
        int seed = 12345,
        int maxBullets = 4096,
        OutOfBoundsBehavior outOfBounds = OutOfBoundsBehavior.None,
        CollisionSettings? collision = null) => new()
    {
        Seed = seed,
        CanvasWidth = 1920,
        CanvasHeight = 1080,
        BoundsMargin = 160,
        MaxBullets = maxBullets,
        TimeScale = 1.0,
        FixedTimeStep = 1.0 / 120.0,
        OutOfBounds = outOfBounds,
        Emitters = [emitter ?? Emitter()],
        Collision = collision ?? new CollisionSettings(),
    };

    /// <summary>設定からエンジンを作る (パターン生成の既定挙動)。</summary>
    public static DanmakuEngine Engine(DanmakuSettings settings) =>
        new(settings, DanmakuBehaviorFactory.CreateAll(settings));

    /// <summary>生存中の弾を配列で取得する (列挙中の変化を避けるためコピーする)。</summary>
    public static Bullet[] AliveBullets(this DanmakuEngine engine) =>
        engine.Pool.ActiveBullets.Where(b => b.IsAlive).ToArray();
}
