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
    public void 壁弾はSpawnRadiusで前方にオフセットされる()
    {
        var pattern = TestFactory.SingleShot(5) with
        {
            Kind = PatternKind.Wall,
            BaseAngle = 90, // 真下へ撃つ (BaseAngle = 90 => +Y 方向)
            WallWidth = 400,
            SpawnRadius = 150, // 150px 前方 (下) に生成
        };
        var emitter = TestFactory.Emitter(pattern, physics: TestFactory.Straight(speed: 0));
        var engine = TestFactory.Engine(TestFactory.Settings(emitter));

        engine.Advance(0.05);

        var bullets = engine.AliveBullets();
        Assert.Equal(5, bullets.Length);
        // 全弾の Y 座標が 150 (中心から下へ 150px) から生成されている
        Assert.All(bullets, b => Assert.Equal(150.0, b.Position.Y, 3));
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

    [Fact]
    public void EmitterSpreadAngleおよびSpeed供給関数によって扇角度と弾速が動的に変化する()
    {
        var pattern = new PatternSettings
        {
            Kind = PatternKind.Fan,
            Way = 3,
            FireInterval = 0.1,
            BaseAngle = 0,
            SpreadAngle = 30,
        };
        var physics = new BulletPhysics { Speed = 100 };

        var settings = TestFactory.Settings(TestFactory.Emitter(pattern, physics: physics));
        var engine = TestFactory.Engine(settings);

        // 時刻 t に応じて拡散角度を 60 + 60*t、速度を 100 + 200*t に変化
        engine.Live.EmitterSpreadAngle = (idx, t) => 60.0 + 60.0 * t;
        engine.Live.EmitterSpeed = (idx, t) => 100.0 + 200.0 * t;

        engine.Advance(0.05); // t=0: spread = 60 (±30度), speed = 100
        var b1 = engine.AliveBullets();
        Assert.Equal(3, b1.Length);
        Assert.Equal(100.0, b1[0].Speed, 1);
        Assert.Equal(-30.0, b1[0].Direction, 1);
        Assert.Equal(0.0, b1[1].Direction, 1);
        Assert.Equal(30.0, b1[2].Direction, 1);

        engine.Advance(0.1); // t=0.1: spread = 66 (±33度), speed = 120
        var b2 = engine.AliveBullets().Skip(3).ToArray();
        Assert.Equal(3, b2.Length);
        Assert.Equal(120.0, b2[0].Speed, 1);
        Assert.Equal(-33.0, b2[0].Direction, 1);
        Assert.Equal(0.0, b2[1].Direction, 1);
        Assert.Equal(33.0, b2[2].Direction, 1);
    }

    [Fact]
    public void EmitterScaleおよびGravity供給関数によって弾の大きさと重力が動的に変化する()
    {
        var pattern = new PatternSettings
        {
            Kind = PatternKind.Circle,
            Way = 1,
            FireInterval = 0.1,
        };
        var appearance = new BulletAppearance { Scale = 1.0 };
        var physics = new BulletPhysics { Speed = 0, Gravity = 0 };

        var settings = TestFactory.Settings(TestFactory.Emitter(pattern, physics: physics, appearance: appearance));
        var engine = TestFactory.Engine(settings);

        engine.Live.EmitterScale = (idx, t) => 1.0 + t * 2.0; // t=0 => scale 1.0, t=0.1 => scale 1.2
        engine.Live.EmitterGravity = (idx, t) => t > 0.05 ? 500.0 : 0.0;

        engine.Advance(0.05); // 1発目: t=0
        var b1 = engine.AliveBullets()[0];
        Assert.Equal(1.0, b1.Scale, 2);
        Assert.Equal(0.0, b1.ExternalAcceleration.Y, 2);

        engine.Advance(0.1); // 2発目: t=0.1
        var b2 = engine.AliveBullets()[1];
        Assert.Equal(1.2, b2.Scale, 2);
        Assert.Equal(500.0, b2.ExternalAcceleration.Y, 2);
    }

    [Fact]
    public void EmitterWayおよびEmitterStack供給関数によって方向数と段数が動的に変化する()
    {
        var pattern = new PatternSettings
        {
            Kind = PatternKind.Circle,
            Way = 4,
            Stack = 1,
            FireInterval = 0.1,
            SpreadAngle = 360,
        };

        var settings = TestFactory.Settings(TestFactory.Emitter(pattern));
        var engine = TestFactory.Engine(settings);

        // 時刻 t=0 では way=4, stack=1 (4発)、時刻 t=0.1 では way=8, stack=2 (16発)
        engine.Live.EmitterWay = (idx, t) => t > 0.05 ? 8 : 4;
        engine.Live.EmitterStack = (idx, t) => t > 0.05 ? 2 : 1;

        engine.Advance(0.05); // 1回目 (t=0): 4 * 1 = 4発
        Assert.Equal(4, engine.AliveBullets().Length);

        engine.Advance(0.1); // 2回目 (t=0.1): 8 * 2 = 16発追加 (合計20発)
        Assert.Equal(20, engine.AliveBullets().Length);
    }

    [Fact]
    public void EmitterAccelerationおよびLifetime供給関数によって加速度と寿命が動的に変化する()
    {
        var pattern = new PatternSettings
        {
            Kind = PatternKind.Circle,
            Way = 1,
            FireInterval = 0.1,
        };
        var physics = new BulletPhysics { Speed = 100, Acceleration = 0, Lifetime = 5.0 };

        var settings = TestFactory.Settings(TestFactory.Emitter(pattern, physics: physics));
        var engine = TestFactory.Engine(settings);

        engine.Live.EmitterAcceleration = (idx, t) => t > 0.05 ? 200.0 : 50.0;
        engine.Live.EmitterLifetime = (idx, t) => t > 0.05 ? 1.0 : 3.0;

        engine.Advance(0.05); // 1発目 (t=0): accel=50, lifetime=3
        var b1 = engine.AliveBullets()[0];
        Assert.Equal(50.0, b1.Acceleration);
        Assert.Equal(3.0, b1.Lifetime);

        engine.Advance(0.1); // 2発目 (t=0.1): accel=200, lifetime=1
        var b2 = engine.AliveBullets()[1];
        Assert.Equal(200.0, b2.Acceleration);
        Assert.Equal(1.0, b2.Lifetime);
    }

    [Fact]
    public void EmitterSplit供給関数によって分裂数と初速が動的に変化する()
    {
        var pattern = new PatternSettings
        {
            Kind = PatternKind.Circle,
            Way = 1,
            FireInterval = 0.5,
        };
        var split = new SplitSpec
        {
            Count = 4,
            SpreadDegrees = 360,
            Speed = 100,
            ScaleFactor = 0.8,
            DestroyParent = true,
            MaxGeneration = 1,
            NextDelay = 0.2,
        };

        var emitter = TestFactory.Emitter(pattern);
        var settings = TestFactory.Settings(emitter with { Split = split, SplitDelay = 0.2 });
        var engine = TestFactory.Engine(settings);

        engine.Live.EmitterSplitCount = (idx, t) => 8;
        engine.Live.EmitterSplitSpeed = (idx, t) => 300.0;

        engine.Advance(0.05); // 発射
        var parent = engine.AliveBullets()[0];
        Assert.NotNull(parent.Split);
        Assert.Equal(8, parent.Split.Count);
        Assert.Equal(300.0, parent.Split.Speed);
    }

    [Fact]
    public void TimeScaleが0のときは弾が空中で完全静止する()
    {
        var settings = TestFactory.Settings(TestFactory.Emitter(TestFactory.SingleShot(4))) with
        {
            TimeScale = 0.0,
        };
        var sim = new DanmakuSimulator(settings);

        sim.SeekTo(1.0);
        Assert.Empty(sim.Bullets); // 時間が進まないため発射されない
    }

    [Fact]
    public void LiveTimeScaleで途中で0にすると時止め演出になり空中で弾がピタッと止まる()
    {
        var pattern = TestFactory.SingleShot(1) with { FireInterval = 10.0 };
        var physics = new BulletPhysics { Speed = 100.0 };
        var settings = TestFactory.Settings(new EmitterSettings { Pattern = pattern, Physics = physics });
        var sim = new DanmakuSimulator(settings);

        // 0s〜1s は通常再生 (TimeScale=1)、1s〜3s は時止め (TimeScale=0)、3s〜4s は通常再生 (TimeScale=1)
        sim.Live.TimeScale = t => t < 1.0 ? 1.0 : (t < 3.0 ? 0.0 : 1.0);

        sim.SeekTo(1.0);
        Assert.Single(sim.Bullets);
        var b1 = sim.Bullets[0];
        var posAt1s = b1.Position;

        sim.SeekTo(2.0); // 時止め中 (2秒目)
        Assert.Single(sim.Bullets);
        var b2 = sim.Bullets[0];
        Assert.Equal(posAt1s.X, b2.Position.X, 1e-3);
        Assert.Equal(posAt1s.Y, b2.Position.Y, 1e-3);

        sim.SeekTo(3.0); // 時止め終了直前 (3秒目)
        Assert.Single(sim.Bullets);
        var b3 = sim.Bullets[0];
        Assert.Equal(posAt1s.X, b3.Position.X, 1e-3);
        Assert.Equal(posAt1s.Y, b3.Position.Y, 1e-3);

        sim.SeekTo(4.0); // 再開後 (4秒目 = 実質シミュレーション 2秒目)
        Assert.Single(sim.Bullets);
        var b4 = sim.Bullets[0];
        Assert.True(Math.Abs(b4.Position.X - posAt1s.X) > 50.0);
    }

    [Fact]
    public void WayやStackが0のときは安全に0発になる()
    {
        var pattern = TestFactory.SingleShot(0) with { Stack = 0 };
        var engine = TestFactory.Engine(TestFactory.Settings(TestFactory.Emitter(pattern)));

        engine.Advance(0.5);
        Assert.Empty(engine.AliveBullets());
    }

    [Fact]
    public void Live供給によるDampingとScaleVelocityとFadeが弾に正しく反映される()
    {
        var pattern = TestFactory.SingleShot(1);
        var emitter = TestFactory.Emitter(pattern);
        var engine = TestFactory.Engine(TestFactory.Settings(emitter));

        engine.Live.EmitterDamping = (idx, t) => 0.5;
        engine.Live.EmitterScaleVelocity = (idx, t) => 1.5;
        engine.Live.EmitterFadeInDuration = (idx, t) => 0.2;
        engine.Live.EmitterFadeOutDuration = (idx, t) => 0.4;
        engine.Live.EmitterMinSpeed = (idx, t) => 10.0;
        engine.Live.EmitterMaxSpeed = (idx, t) => 500.0;

        engine.Advance(0.05);
        var b = engine.AliveBullets()[0];
        Assert.Equal(0.5, b.Damping);
        Assert.Equal(1.5, b.ScaleVelocity);
        Assert.Equal(0.2, b.FadeInDuration);
        Assert.Equal(0.4, b.FadeOutDuration);
        Assert.Equal(10.0, b.MinSpeed);
        Assert.Equal(500.0, b.MaxSpeed);
    }

    [Fact]
    public void AimRateの割合に応じて発射角度が自機方向にブレンドされる()
    {
        // エミッター位置 (0, 0), ターゲット位置 (100, 0) => AngleToTarget = 0度
        // BaseAngle = 90度 (下向き)
        var collision = new CollisionSettings { IsEnabled = true, TargetX = 100, TargetY = 0 };

        // AimRate = 0% => 発射角度 = 90度
        var emitter0 = TestFactory.Emitter(TestFactory.SingleShot(1) with { BaseAngle = 90, AimRate = 0 }) with { X = 0, Y = 0 };
        var engine0 = TestFactory.Engine(TestFactory.Settings(emitter0, collision: collision));
        engine0.Advance(0.05);
        var b0 = Assert.Single(engine0.AliveBullets());
        Assert.Equal(90.0, b0.Direction, 2);
    }

    [Fact]
    public void AimRateによる角度合成テスト()
    {
        // エミッター位置 (0, 0), ターゲット位置 (0, 100) => AngleToTarget = 90度
        // BaseAngle = 0度
        var collision = new CollisionSettings { IsEnabled = true, TargetX = 0, TargetY = 100 };

        // AimRate = 0% => 0度
        var emitter0 = TestFactory.Emitter(TestFactory.SingleShot(1) with { BaseAngle = 0, AimRate = 0 }) with { X = 0, Y = 0 };
        var engine0 = TestFactory.Engine(TestFactory.Settings(emitter0, collision: collision));
        engine0.Advance(0.05);
        Assert.Equal(0.0, Assert.Single(engine0.AliveBullets()).Direction, 2);

        // AimRate = 100% => 0 + 90 * 1.0 = 90度
        var emitter100 = TestFactory.Emitter(TestFactory.SingleShot(1) with { BaseAngle = 0, AimRate = 100 }) with { X = 0, Y = 0 };
        var engine100 = TestFactory.Engine(TestFactory.Settings(emitter100, collision: collision));
        engine100.Advance(0.05);
        Assert.Equal(90.0, Assert.Single(engine100.AliveBullets()).Direction, 2);

        // AimRate = 50% => 0 + 90 * 0.5 = 45度
        var emitter50 = TestFactory.Emitter(TestFactory.SingleShot(1) with { BaseAngle = 0, AimRate = 50 }) with { X = 0, Y = 0 };
        var engine50 = TestFactory.Engine(TestFactory.Settings(emitter50, collision: collision));
        engine50.Advance(0.05);
        Assert.Equal(45.0, Assert.Single(engine50.AliveBullets()).Direction, 2);
    }

    [Fact]
    public void Live供給によるEmitterAimRateが正しく反映される()
    {
        var collision = new CollisionSettings { IsEnabled = true, TargetX = 0, TargetY = 100 }; // 90度
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1) with { BaseAngle = 0, AimRate = 0 }) with { X = 0, Y = 0 };
        var engine = TestFactory.Engine(TestFactory.Settings(emitter, collision: collision));

        engine.Live.EmitterAimRate = (idx, t) => 100.0;
        engine.Advance(0.05);
        Assert.Equal(90.0, Assert.Single(engine.AliveBullets()).Direction, 2);
    }

    [Fact]
    public void マイナスAimRateで自機の真反対方向へ発射される()
    {
        // エミッター (0, 0), ターゲット (100, 0) => AngleToTarget = 0度
        // BaseAngle = 0度
        // AimRate = -100% => 0 + (0 + 180) * 1.0 = 180度 (自機の真後ろ)
        var collision = new CollisionSettings { IsEnabled = true, TargetX = 100, TargetY = 0 };
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1) with { BaseAngle = 0, AimRate = -100 }) with { X = 0, Y = 0 };
        var engine = TestFactory.Engine(TestFactory.Settings(emitter, collision: collision));

        engine.Advance(0.05);
        var b = Assert.Single(engine.AliveBullets());
        Assert.Equal(180.0, DanmakuMath.NormalizeAngle360(b.Direction), 2);
    }

    [Fact]
    public void マイナスHomingTurnRateで自機から逃げるように反発旋回する()
    {
        // エミッター (0, 0), 弾の初期方向 0度 (右向き)
        // ターゲット (100, 50) => ターゲット方向は +26.56度
        // HomingTurnRate = -90度/秒 => ターゲットの反対方向 (206.56度) に向かって旋回する
        var pattern = TestFactory.SingleShot(1) with { BaseAngle = 0 };
        var physics = new BulletPhysics
        {
            Speed = 100,
            HomingEnabled = true,
            HomingTurnRate = -90,
            HomingDuration = 2.0,
            HomingDelay = 0,
        };
        var collision = new CollisionSettings { IsEnabled = true, TargetX = 100, TargetY = 50 };
        var emitter = TestFactory.Emitter(pattern, physics) with { X = 0, Y = 0 };
        var engine = TestFactory.Engine(TestFactory.Settings(emitter, collision: collision));

        engine.Advance(0.2);
        var b = Assert.Single(engine.AliveBullets());
        // ターゲットから逃げるため、角度は負の方向 (時計回りと逆、または離れる向き) へ変化している
        Assert.NotEqual(0.0, b.Direction);
    }

    [Fact]
    public void マイナスSpawnRadiusでエミッターの後方から発生する()
    {
        // 発射角度 0度 (右向き)、SpawnRadius = -50px => 初期位置 X = -50
        var pattern = TestFactory.SingleShot(1) with { BaseAngle = 0, SpawnRadius = -50 };
        var emitter = TestFactory.Emitter(pattern) with { X = 0, Y = 0 };
        var engine = TestFactory.Engine(TestFactory.Settings(emitter));

        engine.Advance(0.05);
        var b = Assert.Single(engine.AliveBullets());
        Assert.True(b.Position.X < 0, "負の生成半径で後方に発生していない");
    }

    [Fact]
    public void マイナスScaleおよび負の拡縮速度が許容される()
    {
        var appearance = new BulletAppearance { Scale = -1.0, ScaleVelocity = -0.5 };
        var emitter = TestFactory.Emitter(TestFactory.SingleShot(1), appearance: appearance);
        var engine = TestFactory.Engine(TestFactory.Settings(emitter));

        engine.Advance(0.1);
        var b = Assert.Single(engine.AliveBullets());
        Assert.True(b.Scale < 0, "負のスケールが保持されていない");
    }

    [Fact]
    public void DanmakuSimulatorがSeekToでシミュレーションを確実に進める()
    {
        var pattern = TestFactory.SingleShot(1) with { BaseAngle = 0 };
        var emitter = TestFactory.Emitter(pattern);
        var settings = TestFactory.Settings(emitter);
        var sim = new DanmakuSimulator(settings);

        sim.SeekTo(1.0);
        var b = Assert.Single(sim.Bullets);
        Assert.True(b.Position.X > 0, "SeekTo(1.0) で弾が進んでいない");
    }

    [Fact]
    public void 自機ショットが正しく上向きに発射される()
    {
        var settings = new DanmakuSettings
        {
            Emitters = [],
            PlayerShot = new PlayerShotSettings
            {
                IsEnabled = true,
                Way = 2,
                Speed = 1000,
                FireInterval = 0.1,
            },
            Collision = new CollisionSettings
            {
                TargetX = 0,
                TargetY = 200,
            }
        };

        var engine = TestFactory.Engine(settings);
        engine.Advance(0.15); // 1 burst fired

        var bullets = engine.AliveBullets();
        Assert.NotEmpty(bullets);
        Assert.All(bullets, b =>
        {
            Assert.True(b.IsPlayerShot);
            Assert.True(b.Position.Y < 200, "自機ショットが上向きに進んでいない");
        });
    }

    [Fact]
    public void 自機ショットがエネミーに命中して被弾判定される()
    {
        var settings = new DanmakuSettings
        {
            Emitters = [],
            PlayerShot = new PlayerShotSettings
            {
                IsEnabled = true,
                Way = 1,
                Speed = 1000,
                HitRadius = 10,
                DestroyOnHit = true,
            },
            Collision = new CollisionSettings
            {
                TargetX = 0,
                TargetY = 200,
                EnemyX = 0,
                EnemyY = 0,
                EnemyRadius = 30,
                EnemyHitEnabled = true,
            }
        };

        var engine = TestFactory.Engine(settings);
        engine.Advance(0.3); // bullets travel 300px from Y=200 up to Y=-100, passing Y=0 (Enemy)

        Assert.True(engine.EnemyHitCount > 0, "エネミーに自機ショットが命中していない");
    }

    [Fact]
    public void 自機ショットによる敵弾相殺が機能する()
    {
        var emitter = TestFactory.Emitter(
            TestFactory.SingleShot(1) with { BaseAngle = 0 },
            physics: TestFactory.Straight(0) with { HitRadius = 20 },
            x: 0,
            y: 50);

        var settings = new DanmakuSettings
        {
            Emitters = [emitter],
            PlayerShot = new PlayerShotSettings
            {
                IsEnabled = true,
                Way = 1,
                Speed = 1000,
                HitRadius = 15,
                CancelEnemyBullets = true,
                DestroyOnHit = true,
            },
            Collision = new CollisionSettings
            {
                TargetX = 0,
                TargetY = 100,
                EnemyHitEnabled = false,
                SpawnHitEffect = false,
            }
        };

        var engine = TestFactory.Engine(settings);
        engine.Advance(0.2); // shot goes through enemy bullet at Y=50

        Assert.DoesNotContain(engine.AliveBullets(), b => !b.IsPlayerShot);
    }

    [Fact]
    public void 誘導札ショットがエネミーに向かって自動追尾する()
    {
        var settings = new DanmakuSettings
        {
            Emitters = [],
            PlayerShot = new PlayerShotSettings
            {
                IsEnabled = true,
                ShotType = PlayerShotType.HomingAmulet,
                Way = 1,
                Speed = 500,
                FireInterval = 0.05,
            },
            Collision = new CollisionSettings
            {
                TargetX = 0,
                TargetY = 200,
                EnemyX = 200,
                EnemyY = 0,
                EnemyHitEnabled = false,
            }
        };

        var engine = TestFactory.Engine(settings);
        engine.Advance(0.3);

        var playerShot = Assert.Single(engine.AliveBullets(), b => b.IsPlayerShot && b.Age > 0.2);
        Assert.True(playerShot.Position.X > 10, "誘導札がエネミーのX座標 (+200) 方向へ曲がっていない");
    }

    [Fact]
    public void エネミー自動狙いショットがエネミー方向へ直接発射される()
    {
        var settings = new DanmakuSettings
        {
            Emitters = [],
            PlayerShot = new PlayerShotSettings
            {
                IsEnabled = true,
                AutoAim = true,
                Way = 1,
                Speed = 1000,
                FireInterval = 0.1,
            },
            Collision = new CollisionSettings
            {
                TargetX = 0,
                TargetY = 200,
                EnemyX = 200,
                EnemyY = 200, // 真右 (角度0度)
                EnemyHitEnabled = false,
            }
        };

        var engine = TestFactory.Engine(settings);
        engine.Advance(0.15);

        var playerShot = Assert.Single(engine.AliveBullets());
        Assert.True(playerShot.Position.X > 50, "自動照準で真右に発射されていない");
        Assert.Equal(200.0, playerShot.Position.Y, 1);
    }

    [Fact]
    public void DanmakuSimulatorがConfigureで自機射撃の有効化を正しく反映する()
    {
        var initialSettings = new DanmakuSettings
        {
            Emitters = [],
            PlayerShot = new PlayerShotSettings { IsEnabled = false },
            Collision = new CollisionSettings { TargetX = 0, TargetY = 200 }
        };

        var sim = new DanmakuSimulator(initialSettings);
        sim.SeekTo(0.2);
        Assert.Empty(sim.Bullets);

        // 途中で自機射撃を有効化する
        var enabledSettings = initialSettings with
        {
            PlayerShot = new PlayerShotSettings
            {
                IsEnabled = true,
                Way = 2,
                Speed = 1000,
                FireInterval = 0.08
            }
        };

        sim.Configure(enabledSettings);
        sim.Reset();
        sim.SeekTo(0.2);

        Assert.NotEmpty(sim.Bullets);
        Assert.All(sim.Bullets, b => Assert.True(b.IsPlayerShot));
    }

    [Fact]
    public void 自機ショットのWay0または連射間隔0以下で射撃が休止する()
    {
        var settings = new DanmakuSettings
        {
            Emitters = [],
            PlayerShot = new PlayerShotSettings
            {
                IsEnabled = true,
                Way = 0,
                Speed = 1000,
                FireInterval = 0.08
            },
            Collision = new CollisionSettings { TargetX = 0, TargetY = 200 }
        };

        var engine = TestFactory.Engine(settings);
        engine.Advance(0.3);
        Assert.Empty(engine.AliveBullets());
    }

    [Fact]
    public void 自機ショットの弾速が負の場合に逆方向へ発射される()
    {
        var settings = new DanmakuSettings
        {
            Emitters = [],
            PlayerShot = new PlayerShotSettings
            {
                IsEnabled = true,
                Way = 1,
                Speed = -1000, // 負の速度
                FireInterval = 0.1
            },
            Collision = new CollisionSettings
            {
                TargetX = 0,
                TargetY = 0,
                EnemyHitEnabled = false,
                SpawnHitEffect = false,
            }
        };

        var engine = TestFactory.Engine(settings);
        engine.Advance(0.15);

        var shot = Assert.Single(engine.AliveBullets());
        Assert.True(shot.Position.Y > 0, "負の速度で下向きに進んでいない");
    }

    [Fact]
    public void 自機ショット発射時にPlayerShot効果音イベントが記録される()
    {
        var settings = new DanmakuSettings
        {
            Emitters = [],
            PlayerShot = new PlayerShotSettings
            {
                IsEnabled = true,
                Way = 2,
                Speed = 1200,
                FireInterval = 0.1
            },
            PlayerShotSound = new SoundSettings { IsEnabled = true, Volume = 0.6 },
            Collision = new CollisionSettings
            {
                TargetX = 0,
                TargetY = 200,
                EnemyHitEnabled = false,
                SpawnHitEffect = false
            }
        };

        var engine = TestFactory.Engine(settings);
        engine.Advance(0.15);

        Assert.NotEmpty(engine.SoundLog.Events);
        Assert.Contains(engine.SoundLog.Events, e => e.Kind == DanmakuSoundKind.PlayerShot);
    }

    [Fact]
    public void 公転速度を時間変化させても連続積分されて角度が滑らかに変化する()
    {
        var settings = TestFactory.Settings(TestFactory.Emitter(
            TestFactory.SingleShot(1) with { FireInterval = 0.5 }) with
        {
            OrbitRadius = 100,
            OrbitSpeed = 60
        });

        var engine = TestFactory.Engine(settings);
        // 最初の 1 秒は 60度/秒、次の 1 秒は 120度/秒
        engine.Live.EmitterOrbitSpeed = (index, time) => time < 1.0 ? 60.0 : 120.0;

        engine.Advance(1.0);
        Assert.Equal(60.0, engine.Contexts[0].OrbitAngle, 1);

        engine.Advance(1.0);
        // 60 + 120 = 180 度
        Assert.Equal(180.0, engine.Contexts[0].OrbitAngle, 1);
    }

    [Fact]
    public void 発射角ステップを動的に変化させても各回のステップが累積加算される()
    {
        var pattern = TestFactory.SingleShot(1) with
        {
            AngleStepPerShot = 10,
            FireInterval = 0.1,
        };
        var engine = TestFactory.Engine(TestFactory.Settings(TestFactory.Emitter(pattern)));
        // 発射 1 回目 (t=0.0): step 10 -> 次回へ 10 累積
        // 発射 2 回目 (t=0.1): step 20 -> 次回へ 10+20=30 累積
        // 発射 3 回目 (t=0.2): step 30
        engine.Live.EmitterAngleStepPerShot = (index, time) => time < 0.05 ? 10.0 : (time < 0.15 ? 20.0 : 30.0);

        engine.Advance(0.25);

        var bullets = engine.AliveBullets().OrderBy(b => b.Id).ToArray();
        Assert.True(bullets.Length >= 3);
        // bullet 0: angle = 0
        // bullet 1: angle = 10 (+10)
        // bullet 2: angle = 30 (+20)
        Assert.Equal(0.0, DanmakuMath.NormalizeAngle(bullets[0].Direction), 1);
        Assert.Equal(10.0, DanmakuMath.NormalizeAngle(bullets[1].Direction), 1);
        Assert.Equal(30.0, DanmakuMath.NormalizeAngle(bullets[2].Direction), 1);
    }

    [Fact]
    public void 魔法陣回転速度が連続積分される()
    {
        var settings = TestFactory.Settings(TestFactory.Emitter(TestFactory.SingleShot(1)));
        var engine = TestFactory.Engine(settings);

        engine.Live.EmitterMagicCircleRotationSpeed = (index, time) => 90.0;
        engine.Advance(2.0);

        Assert.Equal(180.0, engine.Contexts[0].MagicCircleAngle, 1);
    }

    [Fact]
    public void 虹色色相変化速度が連続積分される()
    {
        var settings = TestFactory.Settings(TestFactory.Emitter(TestFactory.SingleShot(1)));
        var engine = TestFactory.Engine(settings);

        engine.Live.EmitterHueVelocity = (index, time) => 45.0;
        engine.Advance(2.0);

        Assert.Equal(90.0, engine.Contexts[0].RainbowBaseHue, 1);
    }

    [Fact]
    public void 効果音イベントのピッチ比率はデフォルトで完全に一定である()
    {
        var settings = TestFactory.Settings(
            TestFactory.Emitter(TestFactory.SingleShot(1) with { FireInterval = 0.05 }),
            seed: 12345);

        var engine = TestFactory.Engine(settings);
        engine.Advance(0.3);

        Assert.NotEmpty(engine.SoundLog.Events);
        foreach (var e in engine.SoundLog.Events)
        {
            Assert.Equal(1.0, e.PitchRatio, 4);
        }
    }

    [Fact]
    public void 全方位リングの発射間隔が途切れることなく一定周期で発射され続ける()
    {
        var pattern = new PatternSettings { Kind = PatternKind.Circle, Way = 16, FireInterval = 0.35 };
        var settings = TestFactory.Settings(TestFactory.Emitter(pattern));

        var engine = TestFactory.Engine(settings);

        var fireTimes = new List<double>();
        long lastCount = 0;
        for (var frame = 0; frame < 600; frame++) // 10 seconds at 60fps
        {
            engine.Advance(1.0 / 60.0);
            if (engine.TotalSpawned > lastCount)
            {
                fireTimes.Add(engine.CurrentTime);
                lastCount = engine.TotalSpawned;
            }
        }

        Assert.True(fireTimes.Count >= 25, "発射回数が不足している");
        for (var i = 1; i < fireTimes.Count; i++)
        {
            var interval = fireTimes[i] - fireTimes[i - 1];
            // interval は常に 0.35 秒 (±1フレーム許容)
            Assert.InRange(interval, 0.33, 0.37);
        }
    }

    [Fact]
    public void 長時間シミュレーションでも弾が途切れることなく発射され続ける()
    {
        var pattern = new PatternSettings { Kind = PatternKind.Circle, Way = 16, FireInterval = 0.35 };
        var settings = TestFactory.Settings(TestFactory.Emitter(pattern));

        var simulator = new DanmakuSimulator(settings);
        simulator.SeekTo(60.0); // 60秒先までシーク

        Assert.True(simulator.Engine.TotalSpawned > 2000, "長時間シークで弾の発射が途切れている");
    }

    [Fact]
    public void 超高速連射0_01秒でも効果音イベントが途切れることなく毎秒100回記録される()
    {
        var pattern = TestFactory.SingleShot(1) with
        {
            FireInterval = 0.01, // 毎秒100発
        };
        var settings = TestFactory.Settings(TestFactory.Emitter(pattern));

        var engine = TestFactory.Engine(settings);
        engine.Advance(1.0); // 1秒進める

        // 1秒間に約100回の効果音イベントが発生していること (途切れ制限でカットされていないこと)
        var fireEvents = engine.SoundLog.Events.Where(e => e.Kind == DanmakuSoundKind.Fire).ToList();
        Assert.InRange(fireEvents.Count, 95, 105);
    }

    [Fact]
    public void 全方位リングのデフォルト発射は歪みゼロの真円である()
    {
        var pattern = TestFactory.SingleShot(8) with
        {
            Kind = PatternKind.Circle,
            SpawnRadius = 100.0,
            BaseAngle = 0.0,
        };
        var engine = TestFactory.Engine(TestFactory.Settings(TestFactory.Emitter(pattern, TestFactory.Straight(0))));
        engine.Advance(0.01);

        var bullets = engine.AliveBullets().OrderBy(b => b.Id).ToList();
        Assert.Equal(8, bullets.Count);

        for (var i = 0; i < 8; i++)
        {
            var expectedAngle = i * 45.0;
            var expectedPos = Vec2.FromDegrees(expectedAngle, 100.0);
            Assert.Equal(DanmakuMath.NormalizeAngle(expectedAngle), DanmakuMath.NormalizeAngle(bullets[i].Direction), 1);
            Assert.Equal(expectedPos.X, bullets[i].Position.X, 1);
            Assert.Equal(expectedPos.Y, bullets[i].Position.Y, 1);
        }
    }

    [Fact]
    public void 全方位リングでWhipやLaserSpacingやWallWidthを動かすと正しく複合変形する()
    {
        // LaserSpacing を動かした場合
        var patternLaser = TestFactory.SingleShot(4) with
        {
            Kind = PatternKind.Circle,
            LaserSpacing = 50.0,
        };
        var engineLaser = TestFactory.Engine(TestFactory.Settings(TestFactory.Emitter(patternLaser, TestFactory.Straight(0))));
        engineLaser.Advance(0.01);
        var bulletsLaser = engineLaser.AliveBullets().OrderBy(b => b.Id).ToList();
        Assert.Equal(0.0, bulletsLaser[0].Position.Length, 1);
        Assert.Equal(50.0, bulletsLaser[1].Position.Length, 1);
        Assert.Equal(100.0, bulletsLaser[2].Position.Length, 1);
        Assert.Equal(150.0, bulletsLaser[3].Position.Length, 1);

        // WallWidth を動かした場合
        var patternWall = TestFactory.SingleShot(3) with
        {
            Kind = PatternKind.Circle,
            BaseAngle = 0.0, // 右向き -> 垂直は下向き (90度)
            WallWidth = 200.0,
        };
        var engineWall = TestFactory.Engine(TestFactory.Settings(TestFactory.Emitter(patternWall, TestFactory.Straight(0))));
        engineWall.Advance(0.01);
        var bulletsWall = engineWall.AliveBullets().OrderBy(b => b.Id).ToList();
        // index 0: y = -100
        // index 1: y = 0
        // index 2: y = +100
        Assert.Equal(-100.0, bulletsWall[0].Position.Y, 1);
        Assert.Equal(0.0, bulletsWall[1].Position.Y, 1);
        Assert.Equal(100.0, bulletsWall[2].Position.Y, 1);
    }

    [Fact]
    public void 自機ショット命中時にボスのHPが自動で減算されラグバーが追従する()
    {
        var settings = new DanmakuSettings
        {
            Collision = new CollisionSettings
            {
                IsEnabled = true,
                EnemyHitEnabled = true,
                EnemyRadius = 40.0,
                TargetX = 0,
                TargetY = 200,
            },
            PlayerShot = new PlayerShotSettings
            {
                IsEnabled = true,
                Way = 1,
                FireInterval = 0.05,
                Speed = 1000,
                HitRadius = 15,
            },
            HpBar = new BossHpBarSettings
            {
                Enabled = true,
                MaxHp = 1000.0,
                InitialHpPercentage = 100.0,
                DamagePerHit = 50.0,
            },
            Emitters = [new EmitterSettings { X = 0, Y = -200 }]
        };

        var engine = new DanmakuEngine(settings);
        engine.EnemyPosition = new Vec2(0, -200);
        engine.TargetPosition = new Vec2(0, 200);

        Assert.Equal(1000.0, engine.CurrentBossHp);
        Assert.Equal(1.0, engine.BossHpRatio);

        // 1秒進めて自機ショットをボスへ命中させる
        engine.Advance(1.0);

        Assert.True(engine.EnemyHitCount > 0, "エネミーに自機弾が命中していること");
        Assert.True(engine.CurrentBossHp < 1000.0, "ボスのHPがダメージを受けて減少していること");
        Assert.True(engine.BossHpRatio < 1.0, "ボスのHP割合が減少していること");
    }

    [Fact]
    public void タイムラインのキーフレーム指定によりHPが直接アニメーション制御される()
    {
        var settings = new DanmakuSettings
        {
            HpBar = new BossHpBarSettings
            {
                Enabled = true,
                MaxHp = 500.0,
                InitialHpPercentage = 100.0,
            }
        };

        var engine = new DanmakuEngine(settings);
        engine.Live.BossHp = time => time switch
        {
            < 1.0 => 100.0,
            < 2.0 => 50.0,
            _ => 10.0
        };

        engine.Advance(0.5);
        Assert.Equal(500.0, engine.CurrentBossHp, 1);

        engine.Advance(1.0); // 1.5秒時点
        Assert.Equal(250.0, engine.CurrentBossHp, 1);
        Assert.Equal(0.5, engine.BossHpRatio, 0.05);

        engine.Advance(1.0); // 2.5秒時点
        Assert.Equal(50.0, engine.CurrentBossHp, 1);
        Assert.Equal(0.1, engine.BossHpRatio, 0.05);
    }

    [Fact]
    public void 自機ショットがボスに命中した時はEnemyHitとHitが発音ログに記録される()
    {
        var settings = new DanmakuSettings
        {
            Collision = new CollisionSettings
            {
                IsEnabled = true,
                EnemyHitEnabled = true,
                EnemyRadius = 50.0,
                SpawnHitEffect = false
            },
            PlayerShot = new PlayerShotSettings
            {
                IsEnabled = true,
                Speed = 1000.0,
                FireInterval = 0.1,
                Way = 1,
                HitRadius = 10.0,
                DestroyOnHit = true
            },
            Emitters = [
                new EmitterSettings
                {
                    IsEnabled = true,
                    Pattern = new PatternSettings { FireInterval = 10.0, BurstCount = 1, Way = 0 }, // 敵弾は撃たない
                }
            ]
        };

        var engine = new DanmakuEngine(settings);
        // 自機位置 Y=100、ボス位置 Y=0、初速1000で上方向に発射
        engine.Live.TargetPosition = _ => new Vec2(0, 100);
        engine.Live.EmitterPosition = (_, _) => new Vec2(0, 0);

        engine.Advance(0.15);

        Assert.Contains(engine.SoundLog.Events, e => e.Kind == DanmakuSoundKind.EnemyHit);
        Assert.Contains(engine.SoundLog.Events, e => e.Kind == DanmakuSoundKind.Hit);
    }

    [Fact]
    public void 敵弾が自機に命中した時はPlayerHitとHitが発音ログに記録される()
    {
        var settings = new DanmakuSettings
        {
            Collision = new CollisionSettings
            {
                IsEnabled = true,
                TargetRadius = 30.0,
                EnemyHitEnabled = false,
                SpawnHitEffect = false
            },
            Emitters = [
                new EmitterSettings
                {
                    IsEnabled = true,
                    Pattern = new PatternSettings { FireInterval = 0.05, BurstCount = 1, Way = 1, BaseAngle = 90 }, // 下向き(90度)に発射
                    Physics = new BulletPhysics { Speed = 500.0, HitRadius = 10.0, DestroyOnHit = true }
                }
            ]
        };

        var engine = new DanmakuEngine(settings);
        // エミッター Y=0、自機 Y=50
        engine.Live.EmitterPosition = (_, _) => new Vec2(0, 0);
        engine.Live.TargetPosition = _ => new Vec2(0, 50);

        engine.Advance(0.15);

        Assert.Contains(engine.SoundLog.Events, e => e.Kind == DanmakuSoundKind.PlayerHit);
        Assert.Contains(engine.SoundLog.Events, e => e.Kind == DanmakuSoundKind.Hit);
    }

    [Fact]
    public void 上向きに発射された弾は下にある自機に命中せずPlayerHitは鳴らない()
    {
        var settings = new DanmakuSettings
        {
            Collision = new CollisionSettings
            {
                IsEnabled = true,
                TargetRadius = 30.0,
                EnemyHitEnabled = false,
                SpawnHitEffect = false
            },
            Emitters = [
                new EmitterSettings
                {
                    IsEnabled = true,
                    Pattern = new PatternSettings { FireInterval = 0.05, BurstCount = 1, Way = 1, BaseAngle = -90 }, // 上向き(-90度)に発射
                    Physics = new BulletPhysics { Speed = 500.0, HitRadius = 10.0, DestroyOnHit = true }
                }
            ]
        };

        var engine = new DanmakuEngine(settings);
        // エミッター Y=0、自機 Y=250 (下側)
        engine.Live.EmitterPosition = (_, _) => new Vec2(0, 0);
        engine.Live.TargetPosition = _ => new Vec2(0, 250);

        engine.Advance(1.0);

        Assert.DoesNotContain(engine.SoundLog.Events, e => e.Kind == DanmakuSoundKind.PlayerHit);
        Assert.DoesNotContain(engine.SoundLog.Events, e => e.Kind == DanmakuSoundKind.Hit);
    }

    [Fact]
    public void 同時発音まとめオフ時は超高頻度発射でもすべての発射音が記録される()
    {
        var settings = new DanmakuSettings
        {
            FireSound = new SoundSettings
            {
                IsEnabled = true,
                CoalesceSimultaneous = false, // まとめオフ
                CoalesceIntervalSeconds = 0.0,
            },
            Emitters = [
                new EmitterSettings
                {
                    IsEnabled = true,
                    Pattern = new PatternSettings { FireInterval = 0.001, BurstCount = 1, Way = 10 },
                    Physics = new BulletPhysics { Speed = 200.0 }
                }
            ]
        };

        var engine = new DanmakuEngine(settings);
        engine.Advance(0.01); // 10ms -> 10 shots

        var fireEvents = engine.SoundLog.Events.Where(e => e.Kind == DanmakuSoundKind.Fire).ToList();
        Assert.InRange(fireEvents.Count, 9, 11);
    }
}

public class DynamicToggleKeyframeTests
{
    [Fact]
    public void 当たり判定をキーフレームで無敵から有効に切り替えられる()
    {
        var settings = new DanmakuSettings
        {
            Collision = new CollisionSettings
            {
                IsEnabled = false, // 初期は無効 (無敵)
                TargetRadius = 20,
            },
            Emitters = [
                new EmitterSettings
                {
                    IsEnabled = true,
                    Pattern = new PatternSettings { FireInterval = 0.05, Way = 1, BaseAngle = 90 },
                    Physics = new BulletPhysics { Speed = 100.0, MinSpeed = 100.0, MaxSpeed = 100.0, HitRadius = 20 }
                }
            ]
        };

        var engine = new DanmakuEngine(settings);
        // エミッター (0, 0)、自機 (0, 50)
        engine.Live.EmitterPosition = (_, _) => new Vec2(0, 0);
        engine.Live.TargetPosition = _ => new Vec2(0, 50);

        // 0.5秒までは無効 (無敵)、0.5秒以降は有効
        engine.Live.CollisionEnabled = t => t >= 0.5;

        engine.Advance(0.4);
        Assert.DoesNotContain(engine.SoundLog.Events, e => e.Kind == DanmakuSoundKind.PlayerHit);

        engine.Advance(0.3); // t=0.7
        Assert.Contains(engine.SoundLog.Events, e => e.Kind == DanmakuSoundKind.PlayerHit);
    }

    [Fact]
    public void 自機射撃をキーフレームで0から1へ動的オンオフできる()
    {
        var settings = new DanmakuSettings
        {
            PlayerShot = new PlayerShotSettings
            {
                IsEnabled = false, // 初期は停止
                Way = 1,
                FireInterval = 0.05,
                Speed = 500,
            },
            Emitters = [
                new EmitterSettings
                {
                    IsEnabled = true,
                    Pattern = new PatternSettings { FireInterval = 10.0, Way = 1 }
                }
            ]
        };

        var engine = new DanmakuEngine(settings);
        engine.Live.PlayerShotEnabled = t => t >= 0.3;

        engine.Advance(0.25);
        Assert.Equal(0, engine.AliveBullets().Count(b => b.IsPlayerShot));

        engine.Advance(0.2); // t=0.45
        Assert.Contains(engine.AliveBullets(), b => b.IsPlayerShot);
    }

    [Fact]
    public void エミッター有効無効をキーフレームで切り替えられる()
    {
        var settings = new DanmakuSettings
        {
            Emitters = [
                new EmitterSettings
                {
                    IsEnabled = false, // 初期は無効
                    Pattern = new PatternSettings { FireInterval = 0.05, Way = 2 },
                    Physics = new BulletPhysics { Speed = 200 }
                }
            ]
        };

        var engine = new DanmakuEngine(settings);
        engine.Live.EmitterIsEnabled = (i, t) => t >= 0.2;

        engine.Advance(0.15);
        Assert.Equal(0, engine.TotalSpawned);

        engine.Advance(0.2); // t=0.35
        Assert.True(engine.TotalSpawned > 0);
    }
}

public class OrbitCollisionTests
{
    [Fact]
    public void 公転設定時にエネミー当たり判定座標が公転位置に追従する()
    {
        var settings = new DanmakuSettings
        {
            Collision = new CollisionSettings
            {
                IsEnabled = true,
                EnemyRadius = 20,
            },
            PlayerShot = new PlayerShotSettings
            {
                IsEnabled = true,
                AutoAim = true,
                Speed = 1000,
                Way = 1,
                FireInterval = 0.05
            },
            Emitters = [
                new EmitterSettings
                {
                    IsEnabled = true,
                    X = 0,
                    Y = 0,
                    OrbitRadius = 150, // 右へ150px公転オフセット
                    OrbitSpeed = 0,
                    OrbitPhase = 0,
                    Pattern = new PatternSettings { FireInterval = 10.0, Way = 1 }
                }
            ]
        };

        var engine = new DanmakuEngine(settings);
        // 自機は (150, 300) に配置 (ボスの公転位置 (150, 0) の真下)
        engine.Live.TargetPosition = _ => new Vec2(150, 300);

        engine.Advance(0.1);

        // ボスの現在位置が公転オフセット (150, 0) になっている
        Assert.Equal(150, engine.EnemyPosition.X, precision: 1);
        Assert.Equal(0, engine.EnemyPosition.Y, precision: 1);

        // 自機の弾が真上 (-90度 = ボス (150, 0) の方向) に発射されている
        var playerShot = engine.AliveBullets().FirstOrDefault(b => b.IsPlayerShot);
        Assert.NotNull(playerShot);
        Assert.Equal(-90, playerShot.Direction, precision: 1);

        // 弾が進んで公転位置のボスに命中する
        engine.Advance(0.35);
        Assert.Contains(engine.SoundLog.Events, e => e.Kind == DanmakuSoundKind.EnemyHit);
    }
}

public class CollisionHitboxFollowUpTests
{
    [Fact]
    public void 自機の当たり判定半径がキーフレームで動的に変化する()
    {
        var settings = new DanmakuSettings
        {
            Collision = new CollisionSettings
            {
                IsEnabled = true,
                TargetRadius = 5.0, // 初期は極小
            },
            Emitters = [
                new EmitterSettings
                {
                    IsEnabled = true,
                    X = 0,
                    Y = 0,
                    Pattern = new PatternSettings { FireInterval = 0.05, Way = 1, BaseAngle = 90 }, // 下向き
                    Physics = new BulletPhysics { Speed = 100.0, HitRadius = 5.0 }
                }
            ]
        };

        var engine = new DanmakuEngine(settings);
        // ターゲットは X=15, Y=50 に配置 (弾の軌道 X=0 との距離は 15px)
        // 初期判定: 弾(5px) + 自機(5px) = 10px <= 15px なのでかすりもせず当たらない
        engine.Live.TargetPosition = _ => new Vec2(15, 50);

        // 0.4秒までは TargetRadius=5px、0.4秒以降は TargetRadius=20px に拡大
        // 拡大後判定: 弾(5px) + 自機(20px) = 25px > 15px で命中する
        engine.Live.TargetRadius = t => t >= 0.4 ? 20.0 : 5.0;

        engine.Advance(0.35); // 弾が Y=50 付近を通過中 (t=0.35s では TargetRadius=5px)
        Assert.DoesNotContain(engine.SoundLog.Events, e => e.Kind == DanmakuSoundKind.PlayerHit);

        engine.Advance(0.3); // t=0.65s (次の弾が通過、TargetRadius=20px)
        Assert.Contains(engine.SoundLog.Events, e => e.Kind == DanmakuSoundKind.PlayerHit);
    }

    [Fact]
    public void 自機弾の命中時消滅と貫通がキーフレームで切り替わる()
    {
        var settings = new DanmakuSettings
        {
            Collision = new CollisionSettings
            {
                IsEnabled = true,
                EnemyRadius = 30.0,
            },
            PlayerShot = new PlayerShotSettings
            {
                IsEnabled = true,
                Way = 1,
                FireInterval = 0.1,
                Speed = 500,
                DestroyOnHit = true, // 初期は消滅
            },
            Emitters = [
                new EmitterSettings
                {
                    IsEnabled = true,
                    X = 0,
                    Y = 0,
                    Pattern = new PatternSettings { FireInterval = 10.0, Way = 1 }
                }
            ]
        };

        var engine = new DanmakuEngine(settings);
        engine.Live.TargetPosition = _ => new Vec2(0, 50); // ボス (0, 0) のすぐ下

        // 0.2秒までは DestroyOnHit=true (消滅)、0.2秒以降は DestroyOnHit=false (貫通)
        engine.Live.PlayerShotDestroyOnHit = t => t < 0.2;

        engine.Advance(0.08); // t=0.08: 1発目発射、命中して消滅
        Assert.DoesNotContain(engine.AliveBullets(), b => b.IsPlayerShot);

        engine.Advance(0.2); // t=0.28: 貫通弾が発射されてボスに命中後も存続
        Assert.Contains(engine.AliveBullets(), b => b.IsPlayerShot && b.HasHit);
    }

    [Fact]
    public void 被弾ダメージ量がキーフレームで動的に変化する()
    {
        var settings = new DanmakuSettings
        {
            Collision = new CollisionSettings { IsEnabled = true, EnemyRadius = 30.0 },
            HpBar = new BossHpBarSettings { MaxHp = 1000, DamagePerHit = 10.0 },
            PlayerShot = new PlayerShotSettings { IsEnabled = true, Way = 1, FireInterval = 0.05, Speed = 1000 },
            Emitters = [
                new EmitterSettings { IsEnabled = true, X = 0, Y = 0, Pattern = new PatternSettings { FireInterval = 10.0, Way = 1 } }
            ]
        };

        var engine = new DanmakuEngine(settings);
        engine.Live.TargetPosition = _ => new Vec2(0, 50);
        // ダメージを 50 に強化
        engine.Live.HpBarDamagePerHit = _ => 50.0;

        engine.Advance(0.08); // 1発命中
        Assert.Equal(50.0, engine.TotalDamageDealt, precision: 1);
    }

    [Fact]
    public void 被弾スパーク数がキーフレームで動的に変化する()
    {
        var settings = new DanmakuSettings
        {
            Collision = new CollisionSettings
            {
                IsEnabled = true,
                EnemyRadius = 30.0,
                SpawnHitEffect = true,
                HitEffectCount = 4
            },
            PlayerShot = new PlayerShotSettings { IsEnabled = true, Way = 1, FireInterval = 0.05, Speed = 1000 },
            Emitters = [
                new EmitterSettings { IsEnabled = true, X = 0, Y = 0, Pattern = new PatternSettings { FireInterval = 10.0, Way = 1 } }
            ]
        };

        var engine = new DanmakuEngine(settings);
        engine.Live.TargetPosition = _ => new Vec2(0, 50);
        // スパーク数を 16 個に設定
        engine.Live.HitEffectCount = _ => 16;

        engine.Advance(0.08); // 1発命中
        var particles = engine.AliveBullets().Where(b => b.Generation > 0).ToList();
        Assert.Equal(16, particles.Count);
    }

    [Fact]
    public void 公転運動中のボスの描画位置とエネミー当たり判定座標が全ステップで完全に一致する()
    {
        var settings = new DanmakuSettings
        {
            FixedTimeStep = 1.0 / 120.0,
            Collision = new CollisionSettings
            {
                IsEnabled = true,
                EnemyRadius = 25.0,
            },
            Emitters = [
                new EmitterSettings
                {
                    IsEnabled = true,
                    X = 0,
                    Y = 0,
                    OrbitRadius = 100.0,
                    OrbitSpeed = 360.0, // 毎秒 360 度
                    OrbitPhase = 0,
                    Pattern = new PatternSettings { FireInterval = 10.0, Way = 1 }
                }
            ]
        };

        var engine = new DanmakuEngine(settings);

        // 120 ステップ (1.0 秒間) にわたり、全ステップで EnemyPosition と Contexts[0].Position が 100% 一致することを検証
        for (var step = 0; step < 120; step++)
        {
            engine.Advance(1.0 / 120.0);
            var expectedAngle = 360.0 * engine.CurrentTime;
            var expectedPos = Vec2.FromDegrees(expectedAngle, 100.0);

            Assert.Equal(expectedPos.X, engine.Contexts[0].Position.X, precision: 2);
            Assert.Equal(expectedPos.Y, engine.Contexts[0].Position.Y, precision: 2);
            Assert.Equal(engine.Contexts[0].Position.X, engine.EnemyPosition.X, precision: 5);
            Assert.Equal(engine.Contexts[0].Position.Y, engine.EnemyPosition.Y, precision: 5);
        }
    }

    [Fact]
    public void 公転運動中のボスへの自機ショット命中判定が遅延なく追従する()
    {
        var settings = new DanmakuSettings
        {
            FixedTimeStep = 1.0 / 120.0,
            Collision = new CollisionSettings
            {
                IsEnabled = true,
                EnemyRadius = 30.0,
            },
            PlayerShot = new PlayerShotSettings
            {
                IsEnabled = true,
                Way = 1,
                FireInterval = 0.05,
                Speed = 1000,
                AutoAim = true,
            },
            Emitters = [
                new EmitterSettings
                {
                    IsEnabled = true,
                    X = 0,
                    Y = 0,
                    OrbitRadius = 80.0,
                    OrbitSpeed = 90.0, // 90 deg/s
                    OrbitPhase = 0,
                    Pattern = new PatternSettings { FireInterval = 10.0, Way = 1 }
                }
            ]
        };

        var engine = new DanmakuEngine(settings);
        // 自機をボスの公転軌道上に配置
        engine.Live.TargetPosition = _ => new Vec2(0, 150);

        engine.Advance(0.5); // 0.5秒進める
        Assert.True(engine.EnemyHitCount > 0);
        Assert.True(engine.CurrentBossHp < engine.BossMaxHp);
    }

    [Fact]
    public void 外部レイヤーからのダメージがボスのHPおよびラグバーに正しく反映される()
    {
        var settings = new DanmakuSettings
        {
            FixedTimeStep = 1.0 / 60.0,
            Collision = new CollisionSettings { IsEnabled = true, EnemyRadius = 30.0 },
            Emitters = [
                new EmitterSettings { IsEnabled = true, X = 0, Y = 0, Pattern = new PatternSettings { FireInterval = 10.0 } }
            ]
        };

        var engine = new DanmakuEngine(settings) { BossMaxHp = 1000.0 };
        engine.Live.ExternalDamage = _ => 300.0; // 他レイヤーから300ダメージ

        engine.Advance(0.1);
        Assert.Equal(700.0, engine.CurrentBossHp, precision: 1);
        Assert.Equal(0.7, engine.BossHpRatio, precision: 2);
    }

    [Fact]
    public void 自機ショット命中時にOnDamageDealtコールバックが発火する()
    {
        var settings = new DanmakuSettings
        {
            Collision = new CollisionSettings { IsEnabled = true, EnemyRadius = 30.0 },
            HpBar = new BossHpBarSettings { DamagePerHit = 25.0 },
            PlayerShot = new PlayerShotSettings { IsEnabled = true, Way = 1, FireInterval = 0.05, Speed = 1000 },
            Emitters = [
                new EmitterSettings { IsEnabled = true, X = 0, Y = 0, Pattern = new PatternSettings { FireInterval = 10.0 } }
            ]
        };

        var engine = new DanmakuEngine(settings);
        engine.Live.TargetPosition = _ => new Vec2(0, 50);

        var reportedDamage = 0.0;
        engine.Live.OnDamageDealt = (dmg, _) => reportedDamage += dmg;

        engine.Advance(0.08); // 1発命中
        Assert.Equal(25.0, reportedDamage, precision: 1);
    }

    [Fact]
    public void 外部ショットによる敵弾相殺判定が機能する()
    {
        var settings = new DanmakuSettings
        {
            Collision = new CollisionSettings { IsEnabled = true, TargetRadius = 10.0 },
            Emitters = [
                new EmitterSettings
                {
                    IsEnabled = true,
                    X = 0,
                    Y = 0,
                    Pattern = new PatternSettings { FireInterval = 10.0, Way = 1, BaseAngle = 90 }, // 1発のみ発射
                    Physics = new BulletPhysics { Speed = 200, HitRadius = 8.0 }
                }
            ]
        };

        var engine = new DanmakuEngine(settings);
        // (0, 50) 付近に外部ショットの相殺領域を設定
        engine.Live.IsBulletCancelledByExternalShot = (pos, r) => pos.DistanceSquaredTo(new Vec2(0, 50)) <= (r + 10) * (r + 10);

        engine.Advance(0.3); // 弾が (0, 50) を通過して相殺消滅
        var mainBulletsAlive = engine.AliveBullets().Count(b => !b.IsPlayerShot && b.Generation == 0);
        Assert.Equal(0, mainBulletsAlive); // 1発発射された弾が相殺されて0発に
        Assert.True(engine.TotalSpawned > 1); // 相殺スパークが生成された
    }

    [Fact]
    public void 複数自機に対して敵弾の当たり判定がそれぞれ機能する()
    {
        var settings = new DanmakuSettings
        {
            Collision = new CollisionSettings { IsEnabled = true },
            Emitters = [
                // 左下 (-100, 100) へ発射するエミッター
                new EmitterSettings
                {
                    IsEnabled = true,
                    X = 0,
                    Y = 0,
                    Pattern = new PatternSettings { FireInterval = 10.0, Way = 1, BaseAngle = 135 },
                    Physics = new BulletPhysics { Speed = 200, HitRadius = 10.0 }
                },
                // 右下 (100, 100) へ発射するエミッター
                new EmitterSettings
                {
                    IsEnabled = true,
                    X = 0,
                    Y = 0,
                    Pattern = new PatternSettings { FireInterval = 10.0, Way = 1, BaseAngle = 45 },
                    Physics = new BulletPhysics { Speed = 200, HitRadius = 10.0 }
                }
            ]
        };

        var engine = new DanmakuEngine(settings);
        // 2機の自機 (1P: -100, 100, 2P: 100, 100)
        engine.Live.Targets = _ => [
            new TargetHitbox(new Vec2(-100, 100), 20.0, 0),
            new TargetHitbox(new Vec2(100, 100), 20.0, 1)
        ];

        engine.Advance(1.0); // 両方の弾がそれぞれの自機に到達
        Assert.True(engine.HitCount >= 2, $"Both targets should be hit (HitCount: {engine.HitCount})");
    }

    [Fact]
    public void 複数ボスに対して自機ショットの当たり判定がそれぞれ機能する()
    {
        var settings = new DanmakuSettings
        {
            Collision = new CollisionSettings { IsEnabled = true, EnemyHitEnabled = true },
            PlayerShot = new PlayerShotSettings
            {
                IsEnabled = true,
                ShotType = PlayerShotType.FocusStraight,
                Way = 2,
                SpreadAngle = 0,
                Speed = 1000,
                HitRadius = 15.0,
                FireInterval = 0.05
            }
        };

        var engine = new DanmakuEngine(settings);
        engine.TargetPosition = new Vec2(0, 200);
        // 2体のボス (Boss A: -8, -100, Boss B: 8, -100) -> 2-way 直進弾 (間隔16px) がそれぞれに命中
        engine.Live.Enemies = _ => [
            new EnemyHitbox(new Vec2(-8, -100), 25.0, 0, 0),
            new EnemyHitbox(new Vec2(8, -100), 25.0, 1, 1)
        ];

        engine.Advance(0.5); // 上向きショットが両方のボスに到達
        Assert.True(engine.EnemyHitCount >= 2, $"Both bosses should take hits (EnemyHitCount: {engine.EnemyHitCount})");
        Assert.True(engine.DamageHistory.Count >= 2);
    }

    [Fact]
    public void 自動照準ショットが最も近いボスを狙う()
    {
        var settings = new DanmakuSettings
        {
            Collision = new CollisionSettings { IsEnabled = true, EnemyHitEnabled = true },
            PlayerShot = new PlayerShotSettings
            {
                IsEnabled = true,
                AutoAim = true,
                Way = 1,
                Speed = 1000,
                FireInterval = 0.05
            }
        };

        var engine = new DanmakuEngine(settings);
        engine.Live.TargetPosition = _ => new Vec2(0, 0);
        // 遠いボス (0, -300) と近いボス (100, 0)
        engine.Live.Enemies = _ => [
            new EnemyHitbox(new Vec2(0, -300), 20.0, 0, 0),
            new EnemyHitbox(new Vec2(100, 0), 20.0, 1, 1)
        ];

        engine.Advance(0.06); // 1バースト発射
        var shots = engine.AliveBullets().Where(b => b.IsPlayerShot).ToList();
        Assert.NotEmpty(shots);
        // 近いボス (100, 0) は右方向 (0度)
        Assert.Equal(0.0, shots[0].Direction, precision: 1);
    }

    [Fact]
    public void 公転運動中のボスの当たり判定座標がエミッター描画座標と完全に一致する()
    {
        var settings = new DanmakuSettings
        {
            FixedTimeStep = 1.0 / 60.0,
            Collision = new CollisionSettings { IsEnabled = true, EnemyRadius = 30.0 },
            Emitters = [
                new EmitterSettings
                {
                    IsEnabled = true,
                    X = 100,
                    Y = -200,
                    OrbitRadius = 150,
                    OrbitSpeed = 360, // 毎秒360度 (1フレームで6度回転)
                    OrbitPhase = 0
                }
            ]
        };

        var engine = new DanmakuEngine(settings);

        for (var step = 1; step <= 60; step++)
        {
            engine.Advance(1.0 / 60.0);
            var expectedContextPos = engine.Contexts[0].Position;
            Assert.NotEmpty(engine.EnemyHitboxes);
            var hitboxPos = engine.EnemyHitboxes[0].Position;

            // 当たり判定座標とエミッターコンテキスト描画座標が完全に一致していること
            Assert.Equal(expectedContextPos.X, hitboxPos.X, precision: 4);
            Assert.Equal(expectedContextPos.Y, hitboxPos.Y, precision: 4);
            Assert.Equal(expectedContextPos.X, engine.EnemyPosition.X, precision: 4);
            Assert.Equal(expectedContextPos.Y, engine.EnemyPosition.Y, precision: 4);
        }
    }

    [Fact]
    public void レイヤー2の敵弾がレイヤー1の外部自機に命中する_自レイヤー当たり判定OFFでも機能する()
    {
        var settings = new DanmakuSettings
        {
            // レイヤー2自体は自機を持たないため CollisionEnabled = false
            Collision = new CollisionSettings { IsEnabled = false },
            Emitters = [
                new EmitterSettings
                {
                    IsEnabled = true,
                    X = 0,
                    Y = 0,
                    Pattern = new PatternSettings { FireInterval = 10.0, Way = 1, BaseAngle = 90 },
                    Physics = new BulletPhysics { Speed = 300, HitRadius = 10.0 }
                }
            ]
        };

        var engine = new DanmakuEngine(settings);
        // レイヤー1の自機 (0, 150)
        engine.Live.Targets = _ => [new TargetHitbox(new Vec2(0, 150), 20.0, 0)];

        engine.Advance(0.6); // 弾が (0, 150) に到達
        Assert.True(engine.HitCount > 0, "外部レイヤーの自機に対して被弾判定が実行されるべき");
    }

    [Fact]
    public void レイヤー1の自機無敵_CollisionEnabledが0の時は敵弾が命中しない()
    {
        var settings = new DanmakuSettings
        {
            Collision = new CollisionSettings { IsEnabled = true, TargetRadius = 20.0 },
            Emitters = [
                new EmitterSettings
                {
                    IsEnabled = true,
                    X = 0,
                    Y = 0,
                    Pattern = new PatternSettings { FireInterval = 10.0, Way = 1, BaseAngle = 90 },
                    Physics = new BulletPhysics { Speed = 300, HitRadius = 10.0 }
                }
            ]
        };

        var engine = new DanmakuEngine(settings);
        engine.Live.CollisionEnabled = _ => false; // 自機無敵 (0)

        engine.Advance(0.6);
        Assert.Equal(0, engine.HitCount); // 命中しない
        Assert.Empty(engine.TargetHitboxes); // 喰らい判定リストも空
    }

    [Fact]
    public void レイヤー1のボス無敵_EnemyHitEnabledが0の時は自機ショットが命中しない()
    {
        var settings = new DanmakuSettings
        {
            Collision = new CollisionSettings { IsEnabled = true, EnemyRadius = 30.0, EnemyHitEnabled = true },
            PlayerShot = new PlayerShotSettings
            {
                IsEnabled = true,
                Way = 1,
                Speed = 1000,
                FireInterval = 0.05
            },
            Emitters = [
                new EmitterSettings { IsEnabled = true, X = 0, Y = -100 }
            ]
        };

        var engine = new DanmakuEngine(settings);
        engine.Live.TargetPosition = _ => new Vec2(0, 100);
        engine.Live.EnemyHitEnabled = _ => false; // ボス無敵 (0)

        engine.Advance(0.3); // ショットがボス位置 (0, -100) を通過
        Assert.Equal(0, engine.EnemyHitCount); // ボス被弾カウントは 0
        Assert.Empty(engine.DamageHistory); // ダメージも記録されない
        Assert.Empty(engine.EnemyHitboxes); // ボス判定リストも空
    }

    [Fact]
    public void 片方無敵自機と片方被弾自機が同時に存在する場合に被弾自機のみが被弾する()
    {
        // 1P: 無敵 (CollisionEnabled = false) at (-100, 100)
        var p1Settings = new DanmakuSettings
        {
            Collision = new CollisionSettings { IsEnabled = false, TargetX = -100, TargetY = 100, TargetRadius = 20.0 }
        };
        var p1Engine = new DanmakuEngine(p1Settings);
        p1Engine.Advance(0.01);
        Assert.Empty(p1Engine.SelfTargetHitboxes); // 1Pは無敵なのでSelfTargetHitboxesは空

        // 2P: 被弾有効 (CollisionEnabled = true) at (100, 100)
        var p2Settings = new DanmakuSettings
        {
            Collision = new CollisionSettings { IsEnabled = true, TargetX = 100, TargetY = 100, TargetRadius = 20.0 }
        };
        var p2Engine = new DanmakuEngine(p2Settings);
        p2Engine.Advance(0.01);
        Assert.Single(p2Engine.SelfTargetHitboxes); // 2Pは有効なのでSelfTargetHitboxesに1件

        // Boss: 両方向に敵弾を発射
        var bossSettings = new DanmakuSettings
        {
            Collision = new CollisionSettings { IsEnabled = false },
            Emitters = [
                // 1P (-100, 100) 方向へ発射
                new EmitterSettings
                {
                    IsEnabled = true,
                    X = 0,
                    Y = 0,
                    Pattern = new PatternSettings { FireInterval = 10.0, Way = 1, BaseAngle = 135 },
                    Physics = new BulletPhysics { Speed = 200, HitRadius = 10.0 }
                },
                // 2P (100, 100) 方向へ発射
                new EmitterSettings
                {
                    IsEnabled = true,
                    X = 0,
                    Y = 0,
                    Pattern = new PatternSettings { FireInterval = 10.0, Way = 1, BaseAngle = 45 },
                    Physics = new BulletPhysics { Speed = 200, HitRadius = 10.0 }
                }
            ]
        };

        var bossEngine = new DanmakuEngine(bossSettings);
        // Boss は 1P(空) と 2P(1件) の SelfTargetHitbox スナップショットを受け取る
        var allTargets = p1Engine.SelfTargetHitboxes.Concat(p2Engine.SelfTargetHitboxes).ToList();
        bossEngine.Live.Targets = _ => allTargets;

        bossEngine.Advance(1.0); // 弾が両方の座標に到達

        // 2Pの弾のみがヒットし、1P方向の弾はすり抜ける (HitCount = 1)
        Assert.Equal(1, bossEngine.HitCount);
    }

    [Fact]
    public void 複数ボス存在時にショットが命中した側のボスのHPバーのみが減少する()
    {
        var boss1Key = new object();
        var boss2Key = new object();

        // ボス1: (-100, -100)
        var boss1Settings = new DanmakuSettings
        {
            Collision = new CollisionSettings { IsEnabled = true, EnemyRadius = 30.0, EnemyHitEnabled = true },
            Emitters = [new EmitterSettings { IsEnabled = true, X = -100, Y = -100 }],
            HpBar = new BossHpBarSettings { MaxHp = 1000.0, InitialHpPercentage = 100.0, DamagePerHit = 20.0 }
        };
        var boss1Engine = new DanmakuEngine(boss1Settings);
        boss1Engine.Live.LayerKey = boss1Key;
        boss1Engine.Advance(0.01);

        // ボス2: (100, -100)
        var boss2Settings = new DanmakuSettings
        {
            Collision = new CollisionSettings { IsEnabled = true, EnemyRadius = 30.0, EnemyHitEnabled = true },
            Emitters = [new EmitterSettings { IsEnabled = true, X = 100, Y = -100 }],
            HpBar = new BossHpBarSettings { MaxHp = 1000.0, InitialHpPercentage = 100.0, DamagePerHit = 20.0 }
        };
        var boss2Engine = new DanmakuEngine(boss2Settings);
        boss2Engine.Live.LayerKey = boss2Key;
        boss2Engine.Advance(0.01);

        // プレイヤー: (-100, 100) から上向きに正面集中ショット (ボス1 (-100, -100) に命中する軌道)
        var playerSettings = new DanmakuSettings
        {
            Collision = new CollisionSettings { IsEnabled = true, TargetX = -100, TargetY = 100 },
            PlayerShot = new PlayerShotSettings
            {
                IsEnabled = true,
                Way = 1,
                Speed = 1000,
                FireInterval = 0.05
            }
        };
        var playerEngine = new DanmakuEngine(playerSettings);
        // プレイヤーはボス1とボス2の自己判定を受け取る
        var allEnemies = boss1Engine.SelfEnemyHitboxes.Concat(boss2Engine.SelfEnemyHitboxes).ToList();
        playerEngine.Live.Enemies = _ => allEnemies;

        playerEngine.Advance(0.3); // ショットが (-100, -100) のボス1に命中

        Assert.NotEmpty(playerEngine.DamageHistory);
        Assert.All(playerEngine.DamageHistory, h => Assert.Same(boss1Key, h.TargetLayerKey));

        // ボス1は被弾したため外部ダメージを受け取る
        var b1Damage = playerEngine.DamageHistory
            .Where(h => ReferenceEquals(h.TargetLayerKey, boss1Key))
            .Sum(h => h.Damage);
        boss1Engine.Live.ExternalDamage = _ => b1Damage;
        boss1Engine.Advance(0.01);

        // ボス2は被弾していないため外部ダメージ0
        var b2Damage = playerEngine.DamageHistory
            .Where(h => ReferenceEquals(h.TargetLayerKey, boss2Key))
            .Sum(h => h.Damage);
        boss2Engine.Live.ExternalDamage = _ => b2Damage;
        boss2Engine.Advance(0.01);

        // ボス1のHPのみが減少し、ボス2のHPは1000のまま
        Assert.True(boss1Engine.CurrentBossHp < 1000.0, "ボス1のHPが減少していること");
        Assert.Equal(1000.0, boss2Engine.CurrentBossHp);
        Assert.True(boss1Engine.BossHpRatio < 1.0);
        Assert.Equal(1.0, boss2Engine.BossHpRatio);
    }
}







