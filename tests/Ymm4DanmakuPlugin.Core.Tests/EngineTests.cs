using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Engine;
using Ymm4DanmakuPlugin.Core.Mathematics;
using Ymm4DanmakuPlugin.Core.Model;

namespace Ymm4DanmakuPlugin.Core.Tests;

/// <summary>
/// 弾の発生数に関するテスト。
/// 開発計画書の検証項目「弾の発生数」に対応する。
/// </summary>
public class BulletCountTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    public void 一回の発射でWay発ちょうど生成される(int way)
    {
        var settings = TestFactory.Settings(TestFactory.Emitter(TestFactory.SingleShot(way)));
        var engine = TestFactory.Engine(settings);

        engine.Advance(0.1);

        Assert.Equal(way, engine.AliveBullets().Length);
        Assert.Equal(way, engine.TotalSpawned);
    }

    [Theory]
    [InlineData(4, 1, 4)]
    [InlineData(8, 3, 24)]
    [InlineData(12, 5, 60)]
    public void Stackを重ねるとWay掛けるStack発になる(int way, int stack, int expected)
    {
        var pattern = TestFactory.SingleShot(way) with { Stack = stack, StackSpeedStep = 20 };
        var engine = TestFactory.Engine(TestFactory.Settings(TestFactory.Emitter(pattern)));

        engine.Advance(0.1);

        Assert.Equal(expected, engine.AliveBullets().Length);
    }

    [Fact]
    public void 発射間隔ごとに繰り返し発射される()
    {
        // 0.1 秒間隔で 4way → 1 秒間で 10 回発射 = 40 発
        var pattern = TestFactory.SingleShot(4) with { FireInterval = 0.1 };
        var engine = TestFactory.Engine(TestFactory.Settings(TestFactory.Emitter(pattern)));

        engine.Advance(1.0);

        // 端点の扱いによる 1 回ぶんの差を許容する
        Assert.InRange(engine.TotalSpawned, 36, 44);
    }

    [Fact]
    public void バースト設定で連射される()
    {
        // 1 バースト = 3 連射、バースト間隔 0.01 秒、次のバーストまで 10 秒
        var pattern = TestFactory.SingleShot(2) with
        {
            FireInterval = 10.0,
            BurstCount = 3,
            BurstInterval = 0.01,
        };
        var engine = TestFactory.Engine(TestFactory.Settings(TestFactory.Emitter(pattern)));

        engine.Advance(0.5);

        // 3 連射 × 2way = 6 発
        Assert.Equal(6, engine.TotalSpawned);
    }

    [Fact]
    public void StartTime前は発射されない()
    {
        var pattern = TestFactory.SingleShot(4) with { StartTime = 1.0 };
        var engine = TestFactory.Engine(TestFactory.Settings(TestFactory.Emitter(pattern)));

        engine.Advance(0.5);
        Assert.Equal(0, engine.TotalSpawned);

        engine.Advance(1.0); // 累計 1.5 秒 → 発射済み
        Assert.Equal(4, engine.TotalSpawned);
    }

    [Fact]
    public void EndTime後は発射されない()
    {
        var pattern = TestFactory.SingleShot(2) with { FireInterval = 0.1, EndTime = 0.5 };
        var engine = TestFactory.Engine(TestFactory.Settings(TestFactory.Emitter(pattern)));

        engine.Advance(0.6);
        var spawnedAtCutoff = engine.TotalSpawned;

        engine.Advance(3.0);

        Assert.Equal(spawnedAtCutoff, engine.TotalSpawned);
    }

    [Fact]
    public void 無効なエミッターからは発射されない()
    {
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(8)) with { IsEnabled = false };
        var engine = TestFactory.Engine(TestFactory.Settings(emitter));

        engine.Advance(1.0);

        Assert.Equal(0, engine.TotalSpawned);
    }

    [Fact]
    public void MaxBulletsを超えると生成が却下される()
    {
        var pattern = TestFactory.SingleShot(64) with { FireInterval = 0.05 };
        var settings = TestFactory.Settings(TestFactory.Emitter(pattern), maxBullets: 100);
        var engine = TestFactory.Engine(settings);

        engine.Advance(2.0);

        Assert.True(engine.AliveBullets().Length <= 100);
        Assert.True(engine.Pool.RejectedCount > 0, "容量超過が発生していない");
    }

    [Fact]
    public void マルチエミッターは全エミッターぶん発射する()
    {
        var settings = TestFactory.Settings() with
        {
            Emitters =
            [
                TestFactory.Emitter(TestFactory.SingleShot(4), x: -200),
                TestFactory.Emitter(TestFactory.SingleShot(6), x: 200),
                TestFactory.Emitter(TestFactory.SingleShot(2), y: 300),
            ],
        };
        var engine = TestFactory.Engine(settings);

        engine.Advance(0.1);

        Assert.Equal(12, engine.AliveBullets().Length);
        Assert.Equal(3, engine.AliveBullets().Select(b => b.EmitterIndex).Distinct().Count());
    }

    [Fact]
    public void 寿命が尽きた弾は消滅する()
    {
        var physics = TestFactory.Straight(lifetime: 0.5);
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(8), physics);
        var engine = TestFactory.Engine(TestFactory.Settings(emitter));

        engine.Advance(0.1);
        Assert.Equal(8, engine.AliveBullets().Length);

        engine.Advance(1.0);
        Assert.Empty(engine.AliveBullets());
    }
}

/// <summary>
/// 軌道計算のテスト。
/// 開発計画書の検証項目「軌道計算」に対応する。
/// </summary>
public class TrajectoryTests
{
    /// <summary>1 発だけ指定角へ撃つエンジンを作る。</summary>
    private static DanmakuEngine SingleBulletEngine(BulletPhysics physics, double angle = 0)
    {
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1, angle), physics);
        return TestFactory.Engine(TestFactory.Settings(emitter));
    }

    [Fact]
    public void 等速直線運動は距離が速度掛ける時間になる()
    {
        var engine = SingleBulletEngine(TestFactory.Straight(speed: 100));

        engine.Advance(1.0);

        var bullet = Assert.Single(engine.AliveBullets());
        // 発射は 0 秒付近、1 秒経過 → およそ 100px 進む
        Assert.InRange(bullet.Position.X, 98, 100.1);
        Assert.Equal(0.0, bullet.Position.Y, 6);
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(90, 0, 1)]
    [InlineData(180, -1, 0)]
    [InlineData(-90, 0, -1)]
    public void 発射角どおりの向きに進む(double angle, double signX, double signY)
    {
        var engine = SingleBulletEngine(TestFactory.Straight(speed: 100), angle);

        engine.Advance(1.0);

        var bullet = Assert.Single(engine.AliveBullets());
        Assert.InRange(bullet.Position.X * signX + bullet.Position.Y * signY, 98, 100.1);
        // 直交成分はゼロ
        Assert.Equal(0.0, bullet.Position.X * signY - bullet.Position.Y * signX, 4);
    }

    [Fact]
    public void 加速度で速度が増える()
    {
        var physics = TestFactory.Straight(speed: 0) with { Acceleration = 100 };
        var engine = SingleBulletEngine(physics);

        engine.Advance(2.0);

        var bullet = Assert.Single(engine.AliveBullets());
        // v = a*t ≒ 200
        Assert.InRange(bullet.Speed, 195, 200.5);
        // 距離は概ね 0.5*a*t^2 = 200 (離散積分の誤差を許容)
        Assert.InRange(bullet.Position.X, 190, 205);
    }

    [Fact]
    public void 最大速度でクランプされる()
    {
        var physics = TestFactory.Straight(speed: 100) with { Acceleration = 1000, MaxSpeed = 300 };
        var engine = SingleBulletEngine(physics);

        engine.Advance(2.0);

        var bullet = Assert.Single(engine.AliveBullets());
        Assert.Equal(300.0, bullet.Speed, 6);
    }

    [Fact]
    public void 旋回速度で進行方向が回る()
    {
        var physics = TestFactory.Straight(speed: 100) with { AngularVelocity = 90 };
        var engine = SingleBulletEngine(physics);

        engine.Advance(1.0);

        var bullet = Assert.Single(engine.AliveBullets());
        // 90 度/秒 × 約 1 秒 → 0 度から 90 度付近へ
        Assert.InRange(bullet.Direction, 88, 90.5);
    }

    [Fact]
    public void 重力で下方向へ曲がる()
    {
        var physics = TestFactory.Straight(speed: 100) with { Gravity = 200 };
        var engine = SingleBulletEngine(physics);

        engine.Advance(1.0);

        var bullet = Assert.Single(engine.AliveBullets());
        Assert.True(bullet.Position.Y > 50, $"重力で落下していない (Y={bullet.Position.Y})");
        Assert.True(bullet.Position.X > 50, $"横方向にも進んでいるはず (X={bullet.Position.X})");
    }

    [Fact]
    public void 減衰で速度が下がる()
    {
        var physics = TestFactory.Straight(speed: 400) with { Damping = 0.25, MinSpeed = 0 };
        var engine = SingleBulletEngine(physics);

        engine.Advance(1.0);

        var bullet = Assert.Single(engine.AliveBullets());
        // Damping は「1 秒後に残る割合」なので 400 * 0.25 = 100 付近
        Assert.InRange(bullet.Speed, 90, 110);
    }

    [Fact]
    public void ホーミング弾はターゲットへ向きを変える()
    {
        var physics = TestFactory.Straight(speed: 100) with
        {
            HomingEnabled = true,
            HomingTurnRate = 180,
            HomingDuration = 5,
        };
        // 真右(0度)へ撃つが、ターゲットは真下にある
        var collision = new CollisionSettings { TargetX = 0, TargetY = 500 };
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1), physics);
        var engine = TestFactory.Engine(TestFactory.Settings(emitter, collision: collision));

        engine.Advance(1.0);
        var bullet = Assert.Single(engine.AliveBullets());

        // 下方向 (+90 度) へ寄っているはず
        Assert.True(bullet.Direction > 30, $"ターゲット方向へ旋回していない (Direction={bullet.Direction})");
    }

    [Fact]
    public void ホーミング遅延中は向きが変わらない()
    {
        var physics = TestFactory.Straight(speed: 100) with
        {
            HomingEnabled = true,
            HomingTurnRate = 180,
            HomingDuration = 5,
            HomingDelay = 1.0,
        };
        var collision = new CollisionSettings { TargetX = 0, TargetY = 500 };
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1), physics);
        var engine = TestFactory.Engine(TestFactory.Settings(emitter, collision: collision));

        engine.Advance(0.5); // まだ遅延中

        var bullet = Assert.Single(engine.AliveBullets());
        Assert.Equal(0.0, bullet.Direction, 3);
    }

    [Fact]
    public void ホーミング時間が切れると追尾をやめる()
    {
        var physics = TestFactory.Straight(speed: 100) with
        {
            HomingEnabled = true,
            HomingTurnRate = 10,
            HomingDuration = 0.2,
        };
        var collision = new CollisionSettings { TargetX = 0, TargetY = 500 };
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1), physics);
        var engine = TestFactory.Engine(TestFactory.Settings(emitter, collision: collision));

        engine.Advance(1.0);

        var bullet = Assert.Single(engine.AliveBullets());
        Assert.False(bullet.HomingEnabled);
    }

    [Fact]
    public void 画面外に出た弾はDestroyで消える()
    {
        var physics = TestFactory.Straight(speed: 5000);
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1), physics);
        var settings = TestFactory.Settings(emitter, outOfBounds: OutOfBoundsBehavior.Destroy);
        var engine = TestFactory.Engine(settings);

        engine.Advance(1.0);

        Assert.Empty(engine.AliveBullets());
    }

    [Fact]
    public void Bounceでは画面内に留まる()
    {
        var physics = TestFactory.Straight(speed: 3000);
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1), physics);
        var settings = TestFactory.Settings(emitter, outOfBounds: OutOfBoundsBehavior.Bounce);
        var engine = TestFactory.Engine(settings);

        engine.Advance(2.0);

        var bullet = Assert.Single(engine.AliveBullets());
        var halfWidth = settings.CanvasWidth / 2.0 + settings.BoundsMargin;
        Assert.InRange(bullet.Position.X, -halfWidth, halfWidth);
    }

    [Fact]
    public void Bounceでは進行方向が反転する()
    {
        var physics = TestFactory.Straight(speed: 3000);
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1), physics);
        var settings = TestFactory.Settings(emitter, outOfBounds: OutOfBoundsBehavior.Bounce);
        var engine = TestFactory.Engine(settings);

        engine.Advance(0.5);

        var bullet = Assert.Single(engine.AliveBullets());
        // 右へ撃った弾が右端で跳ね返り、左向き (|Direction| > 90) になっている
        Assert.True(Math.Abs(bullet.Direction) > 90, $"反射していない (Direction={bullet.Direction})");
    }

    [Fact]
    public void Wrapでは反対側へ回り込む()
    {
        var physics = TestFactory.Straight(speed: 3000);
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1), physics);
        var settings = TestFactory.Settings(emitter, outOfBounds: OutOfBoundsBehavior.Wrap);
        var engine = TestFactory.Engine(settings);

        engine.Advance(2.0);

        var bullet = Assert.Single(engine.AliveBullets());
        var halfWidth = settings.CanvasWidth / 2.0 + settings.BoundsMargin;
        // ワープしているので、直進した場合の距離 (6000px) よりずっと内側にいる
        Assert.InRange(bullet.Position.X, -halfWidth - 100, halfWidth + 100);
    }

    [Fact]
    public void 全方位弾は角度が均等に配置される()
    {
        var engine = TestFactory.Engine(TestFactory.Settings(
            TestFactory.Emitter(TestFactory.SingleShot(way: 8, baseAngle: 0))));

        engine.Advance(0.05);

        var angles = engine.AliveBullets()
            .Select(b => DanmakuMath.NormalizeAngle360(b.Direction))
            .OrderBy(a => a)
            .ToArray();

        Assert.Equal(8, angles.Length);
        for (var i = 0; i < 8; i++)
            Assert.Equal(i * 45.0, angles[i], 3);
    }

    [Fact]
    public void 扇弾は中心角を挟んで広がる()
    {
        var pattern = TestFactory.SingleShot(5) with
        {
            Kind = PatternKind.Fan,
            BaseAngle = 0,
            SpreadAngle = 80,
        };
        var engine = TestFactory.Engine(TestFactory.Settings(TestFactory.Emitter(pattern)));

        engine.Advance(0.05);

        var angles = engine.AliveBullets().Select(b => b.Direction).OrderBy(a => a).ToArray();

        Assert.Equal(5, angles.Length);
        Assert.Equal(-40.0, angles[0], 3);
        Assert.Equal(0.0, angles[2], 3);
        Assert.Equal(40.0, angles[4], 3);
    }

    [Fact]
    public void 自機狙い弾はターゲット方向へ飛ぶ()
    {
        var pattern = TestFactory.SingleShot(1) with { Kind = PatternKind.Aimed, SpreadAngle = 0 };
        var collision = new CollisionSettings { TargetX = 300, TargetY = 300 };
        var emitter = TestFactory.Emitter(pattern);
        var engine = TestFactory.Engine(TestFactory.Settings(emitter, collision: collision));

        engine.Advance(0.05);

        var bullet = Assert.Single(engine.AliveBullets());
        Assert.Equal(45.0, bullet.Direction, 1);
    }

    [Fact]
    public void 螺旋弾は発射ごとに基準角がずれる()
    {
        var pattern = TestFactory.SingleShot(1) with
        {
            Kind = PatternKind.Spiral,
            BaseAngle = 0,
            SpreadAngle = 360,
            AngleStepPerShot = 15,
            FireInterval = 0.1,
        };
        var engine = TestFactory.Engine(TestFactory.Settings(TestFactory.Emitter(pattern)));

        engine.Advance(0.35);

        var directions = engine.AliveBullets()
            .OrderBy(b => b.Id)
            .Select(b => DanmakuMath.NormalizeAngle360(b.Direction))
            .ToArray();

        Assert.True(directions.Length >= 3, "螺旋弾が 3 発以上発射されていない");
        for (var i = 1; i < directions.Length; i++)
        {
            var delta = DanmakuMath.NormalizeAngle(directions[i] - directions[i - 1]);
            Assert.Equal(15.0, delta, 3);
        }
    }

    [Fact]
    public void 全方位弾でもAngleStepPerShotで発射ごとに回転する()
    {
        var pattern = TestFactory.SingleShot(4) with
        {
            Kind = PatternKind.Circle,
            BaseAngle = 0,
            SpreadAngle = 360,
            AngleStepPerShot = 10,
            FireInterval = 0.1,
        };
        var engine = TestFactory.Engine(TestFactory.Settings(TestFactory.Emitter(pattern)));

        engine.Advance(0.15); // 2 shots: 0.0s (shot 0, 4 bullets) and 0.1s (shot 1, 4 bullets)

        var bullets = engine.AliveBullets().OrderBy(b => b.Id).ToArray();
        Assert.Equal(8, bullets.Length);

        // Shot 0 is bullets[0..4], Shot 1 is bullets[4..8]
        var diff = DanmakuMath.NormalizeAngle(bullets[4].Direction - bullets[0].Direction);
        Assert.Equal(10.0, diff, 3);
    }

    [Fact]
    public void 壁弾は発射方向と直交する向きに並ぶ()
    {
        var pattern = TestFactory.SingleShot(5) with
        {
            Kind = PatternKind.Wall,
            BaseAngle = 90, // 真下へ撃つ
            WallWidth = 400,
        };
        var engine = TestFactory.Engine(TestFactory.Settings(TestFactory.Emitter(pattern)));

        engine.Advance(0.05);

        var bullets = engine.AliveBullets();
        Assert.Equal(5, bullets.Length);
        // 全弾が同じ向き (真下) を向いている
        Assert.All(bullets, b => Assert.Equal(90.0, b.Direction, 3));
        // X 座標が横に散っている (幅 400px ぶん)
        var xs = bullets.Select(b => b.Position.X).OrderBy(x => x).ToArray();
        Assert.True(xs[^1] - xs[0] > 300, $"横に広がっていない (幅={xs[^1] - xs[0]})");
    }

    [Fact]
    public void 疑似レーザーは同方向へ距離を空けて並ぶ()
    {
        var pattern = TestFactory.SingleShot(4) with
        {
            Kind = PatternKind.Laser,
            BaseAngle = 0,
            AngleStepPerShot = 0,
            LaserSpacing = 30,
            SpawnRadius = 0,
        };
        var engine = TestFactory.Engine(TestFactory.Settings(TestFactory.Emitter(pattern)));

        // Advance は固定ステップ格子でしか進まないため、
        // 1 ステップ未満の時間を渡しても発射されない。発射直後を見たいので 1 ステップ進める。
        // (弾はすべて同方向・同速度なので、1 ステップ進んでも間隔は変わらない)
        engine.Step(1.0 / 120.0);

        var bullets = engine.AliveBullets();
        Assert.Equal(4, bullets.Length);
        Assert.All(bullets, b => Assert.Equal(0.0, b.Direction, 3));

        var xs = bullets.Select(b => b.Position.X).OrderBy(x => x).ToArray();
        for (var i = 1; i < xs.Length; i++)
            Assert.Equal(30.0, xs[i] - xs[i - 1], 1);
    }
}

/// <summary>分裂 (多段弾幕) のテスト。</summary>
public class SplitTests
{
    [Fact]
    public void 分裂すると子弾が生成される()
    {
        var split = new SplitSpec
        {
            Count = 6,
            SpreadDegrees = 360,
            Speed = 150,
            DestroyParent = true,
            MaxGeneration = 3,
        };
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1)) with
        {
            Split = split,
            SplitDelay = 0.2,
        };
        var engine = TestFactory.Engine(TestFactory.Settings(emitter));

        engine.Advance(0.1);
        Assert.Single(engine.AliveBullets());

        engine.Advance(0.3); // 分裂タイミングを跨ぐ

        var bullets = engine.AliveBullets();
        Assert.Equal(6, bullets.Length);
        Assert.All(bullets, b => Assert.Equal(1, b.Generation));
    }

    [Fact]
    public void 分裂子弾は全方位へ均等に散る()
    {
        var split = new SplitSpec
        {
            Count = 4,
            SpreadDegrees = 360,
            Speed = 100,
            DestroyParent = true,
            MaxGeneration = 3,
        };
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1)) with
        {
            Split = split,
            SplitDelay = 0.1,
        };
        var engine = TestFactory.Engine(TestFactory.Settings(emitter));

        engine.Advance(0.2);

        var angles = engine.AliveBullets()
            .Select(b => DanmakuMath.NormalizeAngle360(b.Direction))
            .OrderBy(a => a)
            .ToArray();

        Assert.Equal(4, angles.Length);
        for (var i = 1; i < angles.Length; i++)
            Assert.Equal(90.0, angles[i] - angles[i - 1], 3);
    }

    [Fact]
    public void DestroyParentがfalseなら親も残る()
    {
        var split = new SplitSpec { Count = 4, DestroyParent = false, MaxGeneration = 3 };
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1)) with
        {
            Split = split,
            SplitDelay = 0.2,
        };
        var engine = TestFactory.Engine(TestFactory.Settings(emitter));

        engine.Advance(0.4);

        // 親 1 + 子 4
        Assert.Equal(5, engine.AliveBullets().Length);
    }

    [Fact]
    public void 多段分裂は世代を重ねる()
    {
        var second = new SplitSpec { Count = 2, DestroyParent = true, MaxGeneration = 3 };
        var first = new SplitSpec
        {
            Count = 3,
            DestroyParent = true,
            MaxGeneration = 3,
            Next = second,
            NextDelay = 0.2,
        };
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1)) with
        {
            Split = first,
            SplitDelay = 0.2,
        };
        var engine = TestFactory.Engine(TestFactory.Settings(emitter));

        engine.Advance(0.3); // 1 段目
        Assert.Equal(3, engine.AliveBullets().Length);

        engine.Advance(0.3); // 2 段目
        var bullets = engine.AliveBullets();
        Assert.Equal(6, bullets.Length);
        Assert.All(bullets, b => Assert.Equal(2, b.Generation));
    }

    [Fact]
    public void MaxGenerationを超えると分裂が止まる()
    {
        // 自分自身を Next に持つ「無限分裂」設定でも世代上限で止まることを確認する
        var recursive = new SplitSpec { Count = 2, DestroyParent = true, MaxGeneration = 2 };
        recursive = recursive with { Next = recursive, NextDelay = 0.1 };

        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1), TestFactory.Straight(lifetime: 60)) with
        {
            Split = recursive,
            SplitDelay = 0.1,
        };
        var engine = TestFactory.Engine(TestFactory.Settings(emitter, maxBullets: 4096));

        engine.Advance(3.0);

        var bullets = engine.AliveBullets();
        Assert.NotEmpty(bullets);
        Assert.All(bullets, b => Assert.True(b.Generation <= 2, $"世代 {b.Generation} が上限 2 を超えている"));
    }

    [Fact]
    public void 分裂子弾のスケールが倍率で縮む()
    {
        var split = new SplitSpec { Count = 2, ScaleFactor = 0.5, DestroyParent = true, MaxGeneration = 3 };
        var appearance = TestFactory.PlainAppearance() with { Scale = 2.0 };
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1), appearance: appearance) with
        {
            Split = split,
            SplitDelay = 0.1,
        };
        var engine = TestFactory.Engine(TestFactory.Settings(emitter));

        engine.Advance(0.3);

        Assert.All(engine.AliveBullets(), b => Assert.Equal(1.0, b.Scale, 5));
    }

    [Fact]
    public void 相対速度指定なら親の速度に加算される()
    {
        var split = new SplitSpec
        {
            Count = 1,
            SpreadDegrees = 0,
            Speed = 50,
            SpeedIsRelative = true,
            DestroyParent = true,
            MaxGeneration = 3,
        };
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1), TestFactory.Straight(speed: 200)) with
        {
            Split = split,
            SplitDelay = 0.1,
        };
        var engine = TestFactory.Engine(TestFactory.Settings(emitter));

        engine.Advance(0.2);

        var child = Assert.Single(engine.AliveBullets());
        Assert.Equal(250.0, child.Speed, 3);
    }
}

/// <summary>衝突判定とヒットエフェクトのテスト。</summary>
public class CollisionTests
{
    [Fact]
    public void ターゲットに当たるとHitCountが増える()
    {
        var physics = TestFactory.Straight(speed: 200) with { HitRadius = 8, DestroyOnHit = true };
        var collision = new CollisionSettings
        {
            IsEnabled = true,
            TargetX = 200,
            TargetY = 0,
            TargetRadius = 16,
            SpawnHitEffect = false,
        };
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1), physics);
        var engine = TestFactory.Engine(TestFactory.Settings(emitter, collision: collision));

        engine.Advance(1.5);

        Assert.Equal(1, engine.HitCount);
    }

    [Fact]
    public void 衝突判定が無効なら当たらない()
    {
        var physics = TestFactory.Straight(speed: 200) with { HitRadius = 8 };
        var collision = new CollisionSettings { IsEnabled = false, TargetX = 200, TargetY = 0 };
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1), physics);
        var engine = TestFactory.Engine(TestFactory.Settings(emitter, collision: collision));

        engine.Advance(1.5);

        Assert.Equal(0, engine.HitCount);
    }

    [Fact]
    public void HitRadiusが0の弾は判定されない()
    {
        var physics = TestFactory.Straight(speed: 200) with { HitRadius = 0 };
        var collision = new CollisionSettings { IsEnabled = true, TargetX = 200, TargetY = 0, TargetRadius = 32 };
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1), physics);
        var engine = TestFactory.Engine(TestFactory.Settings(emitter, collision: collision));

        engine.Advance(1.5);

        Assert.Equal(0, engine.HitCount);
    }

    [Fact]
    public void ターゲットから外れた弾は当たらない()
    {
        var physics = TestFactory.Straight(speed: 200) with { HitRadius = 8 };
        // ターゲットは真下、弾は真右へ飛ぶ
        var collision = new CollisionSettings { IsEnabled = true, TargetX = 0, TargetY = 400, TargetRadius = 16 };
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1), physics);
        var engine = TestFactory.Engine(TestFactory.Settings(emitter, collision: collision));

        engine.Advance(2.0);

        Assert.Equal(0, engine.HitCount);
    }

    [Fact]
    public void ヒットエフェクトが生成される()
    {
        var physics = TestFactory.Straight(speed: 200) with { HitRadius = 8, DestroyOnHit = true };
        var collision = new CollisionSettings
        {
            IsEnabled = true,
            TargetX = 200,
            TargetY = 0,
            TargetRadius = 16,
            SpawnHitEffect = true,
            HitEffectCount = 8,
            HitEffectLifetime = 1.0,
        };
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1), physics);
        var engine = TestFactory.Engine(TestFactory.Settings(emitter, collision: collision));

        engine.Advance(1.1);

        // 元の弾は消え、飛沫が 8 個残る
        Assert.Equal(8, engine.AliveBullets().Length);
        Assert.All(engine.AliveBullets(), b => Assert.True(b.Additive));
    }

    [Fact]
    public void ヒットエフェクト自身は当たり判定を持たない()
    {
        var physics = TestFactory.Straight(speed: 200) with { HitRadius = 8, DestroyOnHit = true };
        var collision = new CollisionSettings
        {
            IsEnabled = true,
            TargetX = 200,
            TargetY = 0,
            TargetRadius = 16,
            SpawnHitEffect = true,
            HitEffectCount = 4,
            HitEffectLifetime = 1.0,
        };
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1), physics);
        var engine = TestFactory.Engine(TestFactory.Settings(emitter, collision: collision));

        engine.Advance(1.1);

        // 飛沫が連鎖してヒットを増やさない
        Assert.Equal(1, engine.HitCount);
        Assert.All(engine.AliveBullets(), b => Assert.Equal(0.0, b.HitRadius));
    }

    [Fact]
    public void DestroyOnHitがfalseなら弾は残り多重ヒットしない()
    {
        var physics = TestFactory.Straight(speed: 100) with { HitRadius = 8, DestroyOnHit = false };
        var collision = new CollisionSettings
        {
            IsEnabled = true,
            TargetX = 200,
            TargetY = 0,
            TargetRadius = 64,
            SpawnHitEffect = false,
        };
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1), physics);
        var engine = TestFactory.Engine(TestFactory.Settings(emitter, collision: collision));

        engine.Advance(4.0);

        // 判定域に長く留まっても HasHit で 1 回に抑えられる
        Assert.Equal(1, engine.HitCount);
        Assert.Contains(engine.AliveBullets(), b => b.HasHit);
    }
}

/// <summary>
/// 決定論性 (シード再現性) のテスト。
/// タイムラインをシークしても弾幕が変わらないことを保証する。
/// </summary>
public class DeterminismTests
{
    /// <summary>ばらつき要素をふんだんに含んだ設定を作る。</summary>
    private static DanmakuSettings JitterySettings(int seed) => TestFactory.Settings(
        TestFactory.Emitter(
            TestFactory.SingleShot(8) with
            {
                Kind = PatternKind.Scatter,
                FireInterval = 0.08,
                AngleJitter = 20,
                SpawnJitter = 12,
                SpreadAngle = 120,
            },
            TestFactory.Straight(speed: 180) with
            {
                SpeedJitter = 40,
                AngularVelocityJitter = 15,
                LifetimeJitter = 0.5,
            },
            TestFactory.PlainAppearance() with { ColorMode = ColorMode.Random, ScaleJitter = 0.3 }),
        seed: seed);

    /// <summary>状態を比較しやすい文字列にまとめる。</summary>
    private static string Snapshot(DanmakuEngine engine) =>
        string.Join('|', engine.AliveBullets()
            .OrderBy(b => b.Id)
            .Select(b => $"{b.Id}:{b.Position.X:F6},{b.Position.Y:F6},{b.Direction:F6},{b.Speed:F6},{b.Scale:F6}"));

    [Fact]
    public void 同一シードなら同一の弾幕になる()
    {
        var a = TestFactory.Engine(JitterySettings(777));
        var b = TestFactory.Engine(JitterySettings(777));

        a.Advance(2.0);
        b.Advance(2.0);

        Assert.NotEmpty(a.AliveBullets());
        Assert.Equal(Snapshot(a), Snapshot(b));
    }

    [Fact]
    public void 異なるシードなら別の弾幕になる()
    {
        var a = TestFactory.Engine(JitterySettings(1));
        var b = TestFactory.Engine(JitterySettings(2));

        a.Advance(2.0);
        b.Advance(2.0);

        Assert.NotEqual(Snapshot(a), Snapshot(b));
    }

    [Fact]
    public void Resetして再計算すると同じ状態に戻る()
    {
        var engine = TestFactory.Engine(JitterySettings(4242));

        engine.Advance(1.5);
        var first = Snapshot(engine);

        engine.Reset();
        engine.Advance(1.5);
        var second = Snapshot(engine);

        Assert.Equal(first, second);
    }

    [Fact]
    public void 分割して進めても一括で進めても同じ結果になる()
    {
        // FixedTimeStep (1/120 秒) の倍数で刻めば、分割の仕方によらず同じ状態になる
        var settings = JitterySettings(31337);
        var whole = TestFactory.Engine(settings);
        var pieces = TestFactory.Engine(settings);

        whole.Advance(1.0);
        for (var i = 0; i < 120; i++) pieces.Advance(1.0 / 120.0);

        Assert.Equal(Snapshot(whole), Snapshot(pieces));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(12)]
    [InlineData(120)]
    public void 何分割して進めても一括と同じ結果になる(int divisions)
    {
        // 回帰テスト:
        // 以前は CurrentTime を += で積み上げていたため、分割数によって
        // 浮動小数点誤差の累積が変わり、発射タイミングの比較結果がずれていた。
        // 現在は「完了ステップ数 × ステップ幅」で時刻を算出し、
        // 格子に乗らない微小残差を 0 に丸めることで一致を保証している。
        var settings = JitterySettings(24680);
        var whole = TestFactory.Engine(settings);
        var pieces = TestFactory.Engine(settings);

        const double total = 1.0;
        whole.Advance(total);
        for (var i = 0; i < divisions; i++) pieces.Advance(total / divisions);

        Assert.Equal(whole.StepCount, pieces.StepCount);
        Assert.Equal(whole.CurrentTime, pieces.CurrentTime, 9);
        Assert.NotEmpty(whole.AliveBullets());
        Assert.Equal(Snapshot(whole), Snapshot(pieces));
    }

    [Fact]
    public void 固定ステップ未満のAdvanceでは進まない()
    {
        // 中途半端な幅のステップを実行しないことの確認。
        // 端数は繰り越され、合計が 1 ステップに達した時点でまとめて処理される。
        var engine = TestFactory.Engine(JitterySettings(13579));
        var step = engine.StepSize;

        engine.Advance(step / 4);
        Assert.Equal(0, engine.StepCount);

        // 端数が積み上がって 1 ステップぶんに達すると実行される
        engine.Advance(step / 4);
        engine.Advance(step / 4);
        engine.Advance(step / 4);
        Assert.Equal(1, engine.StepCount);
    }

    [Fact]
    public void 効果音イベントも決定論的に再現される()
    {
        var sound = new SoundSettings
        {
            IsEnabled = true,
            Volume = 0.7,
            PitchJitterSemitones = 3.0,
            MaxVoicesPerSecond = 30,
            CoalesceSimultaneous = false,
        };
        var settings = JitterySettings(555) with { FireSound = sound };

        var a = TestFactory.Engine(settings);
        var b = TestFactory.Engine(settings);

        a.Advance(2.0);
        b.Advance(2.0);

        Assert.NotEmpty(a.SoundLog.Events);
        Assert.Equal(a.SoundLog.Events, b.SoundLog.Events);
    }
}

/// <summary>シーク動作 (DanmakuSimulator) のテスト。</summary>
public class SimulatorTests
{
    private static DanmakuSettings SeekSettings() => TestFactory.Settings(
        TestFactory.Emitter(
            TestFactory.SingleShot(6) with { FireInterval = 0.1, AngleJitter = 10 },
            TestFactory.Straight(speed: 150, lifetime: 3.0) with { SpeedJitter = 20 }),
        seed: 8888);

    private static string Snapshot(DanmakuSimulator simulator) =>
        string.Join('|', simulator.Bullets
            .Where(b => b.IsAlive)
            .OrderBy(b => b.Id)
            .Select(b => $"{b.Id}:{b.Position.X:F6},{b.Position.Y:F6}"));

    [Fact]
    public void 前進シークは巻き戻さない()
    {
        var simulator = new DanmakuSimulator(SeekSettings());

        simulator.SeekTo(1.0);
        simulator.SeekTo(2.0);

        Assert.Equal(2.0, simulator.CurrentTime, 3);
        Assert.Equal(0, simulator.RewindCount);
    }

    [Fact]
    public void 後退シークは巻き戻して再計算する()
    {
        var simulator = new DanmakuSimulator(SeekSettings());

        simulator.SeekTo(2.0);
        simulator.SeekTo(1.0);

        Assert.Equal(1.0, simulator.CurrentTime, 3);
        Assert.Equal(1, simulator.RewindCount);
    }

    [Fact]
    public void どの順序でシークしても同じ時刻なら同じ状態になる()
    {
        var forward = new DanmakuSimulator(SeekSettings());
        var jumping = new DanmakuSimulator(SeekSettings());

        // 素直に前進
        forward.SeekTo(1.5);

        // 行ったり来たりしてから同じ時刻へ
        jumping.SeekTo(2.5);
        jumping.SeekTo(0.3);
        jumping.SeekTo(2.0);
        jumping.SeekTo(1.5);

        Assert.NotEmpty(forward.Bullets);
        Assert.Equal(Snapshot(forward), Snapshot(jumping));
    }

    [Fact]
    public void フレーム指定シークは秒指定と一致する()
    {
        var byFrame = new DanmakuSimulator(SeekSettings());
        var bySecond = new DanmakuSimulator(SeekSettings());

        byFrame.SeekToFrame(90, 60.0);
        bySecond.SeekTo(1.5);

        Assert.Equal(Snapshot(bySecond), Snapshot(byFrame));
    }

    [Fact]
    public void 負の時刻へのシークは0に丸められる()
    {
        var simulator = new DanmakuSimulator(SeekSettings());

        simulator.SeekTo(1.0);
        simulator.SeekTo(-5.0);

        Assert.Equal(0.0, simulator.CurrentTime, 6);
        Assert.DoesNotContain(simulator.Bullets, b => b.IsAlive);
    }

    [Fact]
    public void MaxSimulationSecondsで打ち切られる()
    {
        var simulator = new DanmakuSimulator(SeekSettings()) { MaxSimulationSeconds = 2.0 };

        simulator.SeekTo(1000.0);

        Assert.Equal(2.0, simulator.CurrentTime, 3);
    }

    [Fact]
    public void ターゲット位置の変更では作り直さない()
    {
        // プレビュー上のマウスドラッグで頻繁に動く値なので、
        // 再シミュレーションが発生しないことがパフォーマンス上重要。
        var simulator = new DanmakuSimulator(SeekSettings());
        simulator.SeekTo(1.0);
        var timeBefore = simulator.CurrentTime;

        simulator.Configure(simulator.Settings with
        {
            Collision = simulator.Settings.Collision with { TargetX = 123, TargetY = 45 },
        });

        Assert.Equal(timeBefore, simulator.CurrentTime, 6);
        Assert.Equal(123.0, simulator.Engine.TargetPosition.X, 6);
        Assert.Equal(45.0, simulator.Engine.TargetPosition.Y, 6);
    }

    [Fact]
    public void シードを変えると作り直される()
    {
        var simulator = new DanmakuSimulator(SeekSettings());
        simulator.SeekTo(1.0);

        simulator.Configure(simulator.Settings with { Seed = 999 });

        Assert.Equal(0.0, simulator.CurrentTime, 6);
    }

    [Fact]
    public void パターン設定を変えると作り直される()
    {
        var simulator = new DanmakuSimulator(SeekSettings());
        simulator.SeekTo(1.0);

        var emitter = simulator.Settings.Emitters[0];
        simulator.Configure(simulator.Settings with
        {
            Emitters = [emitter with { Pattern = emitter.Pattern with { Way = 32 } }],
        });

        Assert.Equal(0.0, simulator.CurrentTime, 6);
    }
}

/// <summary>
/// キーフレーム連携 (<see cref="LiveValueSource"/>) のテスト。
/// <para>
/// 開発計画書の「キーフレーム対応」に対応する。エミッター位置やターゲット位置を
/// タイムラインでアニメーションさせても、
/// ・シミュレーションが作り直されない (プレビューが重くならない)
/// ・シークの再現性 (決定論) が壊れない
/// ことを保証する。
/// </para>
/// </summary>
public class KeyframeLiveValueTests
{
    private static DanmakuSettings LiveSettings(double fireInterval = 0.1) => TestFactory.Settings(
        TestFactory.Emitter(
            TestFactory.SingleShot(1, baseAngle: 0) with { FireInterval = fireInterval },
            TestFactory.Straight(speed: 100, lifetime: 30)),
        seed: 4242);

    [Fact]
    public void エミッター位置の供給関数が発射位置に反映される()
    {
        var settings = LiveSettings(fireInterval: TestFactory.SingleShotInterval);
        var engine = TestFactory.Engine(settings);

        // 時刻に比例して右へ移動するエミッター
        engine.Live.EmitterPosition = (_, t) => new Vec2(1000 * t, 0);

        // 最初の 1 ステップ (t=0) で発射されるので、発射位置は原点付近
        engine.Step(1.0 / 120.0);
        var first = engine.AliveBullets().Single();

        Assert.Equal(0.0, first.PreviousPosition.X, 6);
        Assert.Equal(0.0, first.PreviousPosition.Y, 6);
    }

    [Fact]
    public void エミッター位置の供給関数は時間経過で移動する()
    {
        var settings = LiveSettings(fireInterval: 0.5);
        var engine = TestFactory.Engine(settings);
        engine.Live.EmitterPosition = (_, t) => new Vec2(1000 * t, 0);

        engine.Advance(0.6);

        var bullets = engine.AliveBullets().OrderBy(b => b.Id).ToArray();

        // t=0 と t=0.5 の 2 回発射され、2 発目は右に約 500px ずれた位置から出る
        Assert.Equal(2, bullets.Length);
        Assert.True(bullets[1].Position.X > bullets[0].Position.X + 300,
            $"2発目({bullets[1].Position.X:F1})は1発目({bullets[0].Position.X:F1})より右から発射されるべき");
    }

    [Fact]
    public void 供給関数がnullを返すと設定値が使われる()
    {
        var settings = TestFactory.Settings(
            TestFactory.Emitter(
                TestFactory.SingleShot(1) with { FireInterval = TestFactory.SingleShotInterval },
                TestFactory.Straight(speed: 0, lifetime: 30),
                x: 250,
                y: -75),
            seed: 1);
        var engine = TestFactory.Engine(settings);
        engine.Live.EmitterPosition = (_, _) => null;

        engine.Step(1.0 / 120.0);
        var bullet = engine.AliveBullets().Single();

        Assert.Equal(250.0, bullet.Position.X, 6);
        Assert.Equal(-75.0, bullet.Position.Y, 6);
    }

    [Fact]
    public void ターゲット位置の供給関数がホーミングに反映される()
    {
        var physics = TestFactory.Straight(speed: 100, lifetime: 30) with
        {
            HomingEnabled = true,
            HomingTurnRate = 720,
            HomingDuration = 10,
            HomingDelay = 0,
        };
        var settings = TestFactory.Settings(
            TestFactory.Emitter(
                TestFactory.SingleShot(1, baseAngle: 0) with { FireInterval = TestFactory.SingleShotInterval },
                physics),
            seed: 7);

        var engine = TestFactory.Engine(settings);
        // 真上 (エンジン角 -90 度) にターゲットを固定する
        engine.Live.TargetPosition = _ => new Vec2(0, -1000);

        engine.Advance(0.5);
        var bullet = engine.AliveBullets().Single();

        // 右向き(0度)で発射された弾が上向き(-90度)へ旋回している
        Assert.InRange(DanmakuMath.NormalizeAngle(bullet.Direction), -91.0, -89.0);
        Assert.True(bullet.Position.Y < 0, "ターゲット方向 (上) へ進むべき");
    }

    [Fact]
    public void ターゲット位置の供給関数が衝突判定に反映される()
    {
        var collision = new CollisionSettings
        {
            IsEnabled = true,
            TargetX = 100000,   // 設定値は遥か彼方 = 供給関数が無視されれば当たらない
            TargetY = 100000,
            TargetRadius = 20,
            SpawnHitEffect = false,
        };
        var settings = TestFactory.Settings(
            TestFactory.Emitter(
                TestFactory.SingleShot(1, baseAngle: 0) with { FireInterval = TestFactory.SingleShotInterval },
                TestFactory.Straight(speed: 200, lifetime: 30) with { HitRadius = 8 }),
            seed: 11,
            collision: collision);

        var engine = TestFactory.Engine(settings);
        // 弾は右へ進むので、右方向 200px の位置に置けば約 1 秒で当たる
        engine.Live.TargetPosition = _ => new Vec2(200, 0);

        engine.Advance(1.5);

        Assert.Equal(1, engine.HitCount);
    }

    [Fact]
    public void 供給関数を使ってもシークの再現性が保たれる()
    {
        // 「時刻のみに依存する純粋関数」であれば決定論が壊れないことの確認。
        static Vec2? Emitter(int _, double t) => new(120 * Math.Cos(t), 120 * Math.Sin(t));
        static Vec2? Target(double t) => new(0, 200 + 50 * Math.Sin(t * 2));

        var settings = LiveSettings();

        var a = new DanmakuSimulator(settings);
        a.Live.EmitterPosition = Emitter;
        a.Live.TargetPosition = Target;

        var b = new DanmakuSimulator(settings);
        b.Live.EmitterPosition = Emitter;
        b.Live.TargetPosition = Target;

        // A は一気に進め、B は往復シークしてから同じ時刻へ合わせる
        a.SeekTo(2.0);

        b.SeekTo(1.5);
        b.SeekTo(0.4);
        b.SeekTo(2.0);

        var snapshotA = string.Join('|', a.Bullets.Where(x => x.IsAlive).OrderBy(x => x.Id)
            .Select(x => $"{x.Id}:{x.Position.X:F6},{x.Position.Y:F6}"));
        var snapshotB = string.Join('|', b.Bullets.Where(x => x.IsAlive).OrderBy(x => x.Id)
            .Select(x => $"{x.Id}:{x.Position.X:F6},{x.Position.Y:F6}"));

        Assert.NotEmpty(snapshotA);
        Assert.Equal(snapshotA, snapshotB);
    }

    [Fact]
    public void 供給関数の設定はシミュレーションを作り直さない()
    {
        // キーフレームを動かすたびに作り直されるとプレビューが実用にならない。
        var simulator = new DanmakuSimulator(LiveSettings());
        simulator.SeekTo(1.0);
        var timeBefore = simulator.CurrentTime;

        simulator.Live.EmitterPosition = (_, t) => new Vec2(t * 10, 0);
        simulator.Live.TargetPosition = _ => new Vec2(0, 300);

        Assert.Equal(timeBefore, simulator.CurrentTime, 6);
        Assert.Equal(0, simulator.RewindCount);
    }

    [Fact]
    public void 供給関数は設定を差し替えても保持される()
    {
        var simulator = new DanmakuSimulator(LiveSettings());
        simulator.Live.TargetPosition = _ => new Vec2(0, -500);

        // 構造を変える設定変更 (作り直しが起きる) を行う
        simulator.Configure(simulator.Settings with { Seed = 31337 });
        simulator.SeekTo(0.3);

        Assert.NotNull(simulator.Live.TargetPosition);
        Assert.Equal(-500.0, simulator.Engine.TargetPosition.Y, 6);
    }

    [Fact]
    public void Clearで供給関数が解除され設定値に戻る()
    {
        var settings = TestFactory.Settings(
            TestFactory.Emitter(TestFactory.SingleShot(1)),
            collision: new CollisionSettings { TargetX = 42, TargetY = 84 });

        var engine = TestFactory.Engine(settings);
        engine.Live.TargetPosition = _ => new Vec2(-1, -2);
        engine.Step(1.0 / 120.0);
        Assert.Equal(-1.0, engine.TargetPosition.X, 6);

        engine.Live.Clear();
        engine.Step(1.0 / 120.0);

        Assert.False(engine.Live.HasAny);
        Assert.Equal(42.0, engine.TargetPosition.X, 6);
        Assert.Equal(84.0, engine.TargetPosition.Y, 6);
    }

    [Fact]
    public void EmitterAngle供給関数によって発射角度が動的に変化する()
    {
        var pattern = new PatternSettings
        {
            Kind = PatternKind.Circle,
            Way = 1,
            FireInterval = 0.1,
            BaseAngle = 0,
        };

        var settings = TestFactory.Settings(TestFactory.Emitter(pattern));
        var engine = TestFactory.Engine(settings);

        // 時刻 t に応じて発射角度を 90度 * t にする
        engine.Live.EmitterAngle = (idx, t) => 90.0 * t;

        engine.Advance(0.05); // 1発目: t=0, angle = 0度
        Assert.Single(engine.AliveBullets());
        Assert.Equal(0.0, engine.AliveBullets()[0].Velocity.Degrees, 1);

        engine.Advance(0.1); // 2発目: t=0.1, angle = 9度
        Assert.Equal(2, engine.AliveBullets().Length);
        Assert.Equal(9.0, engine.AliveBullets()[1].Velocity.Degrees, 1);
    }
}
