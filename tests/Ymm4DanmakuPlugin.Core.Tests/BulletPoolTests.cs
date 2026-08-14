using Ymm4DanmakuPlugin.Core.Mathematics;
using Ymm4DanmakuPlugin.Core.Model;

namespace Ymm4DanmakuPlugin.Core.Tests;

/// <summary>
/// オブジェクトプーリングの単体テスト。
/// 開発計画書の検証項目「オブジェクトプーリング動作」に対応する。
/// </summary>
public class BulletPoolTests
{
    [Fact]
    public void Rent_生存中の弾として登録される()
    {
        var pool = new BulletPool(8);

        var bullet = pool.Rent();

        Assert.NotNull(bullet);
        Assert.True(bullet.IsAlive);
        Assert.Equal(1, pool.ActiveCount);
        Assert.Contains(bullet, pool.ActiveBullets);
    }

    [Fact]
    public void Rent_一意なIDが割り振られる()
    {
        var pool = new BulletPool(8);

        var ids = Enumerable.Range(0, 5).Select(_ => pool.Rent()!.Id).ToArray();

        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void Rent_容量を超えるとnullを返し却下数が増える()
    {
        var pool = new BulletPool(3);

        for (var i = 0; i < 3; i++) Assert.NotNull(pool.Rent());

        Assert.Null(pool.Rent());
        Assert.Null(pool.Rent());
        Assert.Equal(2, pool.RejectedCount);
        Assert.Equal(3, pool.AllocatedCount);
    }

    [Fact]
    public void Return後にCompactすると生存リストから外れる()
    {
        var pool = new BulletPool(8);
        var a = pool.Rent()!;
        var b = pool.Rent()!;
        var c = pool.Rent()!;

        pool.Return(b);
        pool.Compact();

        Assert.Equal(2, pool.ActiveCount);
        Assert.Contains(a, pool.ActiveBullets);
        Assert.Contains(c, pool.ActiveBullets);
        Assert.DoesNotContain(b, pool.ActiveBullets);
    }

    [Fact]
    public void 返却されたインスタンスは再利用される()
    {
        var pool = new BulletPool(8);
        var first = pool.Rent()!;
        var allocatedBefore = pool.AllocatedCount;

        pool.Return(first);
        pool.Compact();
        var second = pool.Rent()!;

        // 新規確保ではなく、同じインスタンスが使い回されている
        Assert.Same(first, second);
        Assert.Equal(allocatedBefore, pool.AllocatedCount);
    }

    [Fact]
    public void 大量の生成と返却を繰り返しても確保数は容量を超えない()
    {
        var pool = new BulletPool(64);

        for (var cycle = 0; cycle < 100; cycle++)
        {
            for (var i = 0; i < 64; i++) pool.Rent();
            foreach (var bullet in pool.ActiveBullets.ToArray()) pool.Return(bullet);
            pool.Compact();
        }

        Assert.Equal(0, pool.ActiveCount);
        Assert.True(pool.AllocatedCount <= 64, $"確保数 {pool.AllocatedCount} が容量 64 を超えている");
        Assert.Equal(0, pool.RejectedCount);
    }

    [Fact]
    public void 再利用時はResetされ前回の状態が残らない()
    {
        var pool = new BulletPool(4);
        var bullet = pool.Rent()!;
        bullet.Position = new Vec2(123, 456);
        bullet.Speed = 999;
        bullet.Generation = 7;
        bullet.HasHit = true;
        bullet.TrailLength = 16;

        pool.Return(bullet);
        pool.Compact();
        var reused = pool.Rent()!;

        Assert.Same(bullet, reused);
        Assert.Equal(Vec2.Zero, reused.Position);
        Assert.Equal(0, reused.Speed);
        Assert.Equal(0, reused.Generation);
        Assert.False(reused.HasHit);
        Assert.Equal(0, reused.TrailLength);
    }

    [Fact]
    public void PoolIndexは確保順に固定される()
    {
        var pool = new BulletPool(4);

        var bullets = Enumerable.Range(0, 4).Select(_ => pool.Rent()!).ToArray();

        Assert.Equal([0, 1, 2, 3], bullets.Select(b => b.PoolIndex));
    }

    [Fact]
    public void Clearですべて解放され却下数もリセットされる()
    {
        var pool = new BulletPool(2);
        pool.Rent();
        pool.Rent();
        pool.Rent(); // 却下される

        Assert.Equal(1, pool.RejectedCount);

        pool.Clear();

        Assert.Equal(0, pool.ActiveCount);
        Assert.Equal(0, pool.RejectedCount);
        Assert.NotNull(pool.Rent());
    }

    [Fact]
    public void Compactは生存中の弾の順序を保つ()
    {
        var pool = new BulletPool(8);
        var bullets = Enumerable.Range(0, 6).Select(_ => pool.Rent()!).ToArray();

        // 偶数番目を返却する
        for (var i = 0; i < bullets.Length; i += 2) pool.Return(bullets[i]);
        pool.Compact();

        Assert.Equal([bullets[1], bullets[3], bullets[5]], pool.ActiveBullets);
    }

    [Fact]
    public void 二重Returnしても生存数がずれない()
    {
        var pool = new BulletPool(4);
        var bullet = pool.Rent()!;

        pool.Return(bullet);
        pool.Return(bullet); // 2 回目は無視されるべき
        pool.Compact();

        Assert.Equal(0, pool.ActiveCount);

        // free スタックが壊れていなければ、容量ぶんきっちり確保できる
        for (var i = 0; i < 4; i++) Assert.NotNull(pool.Rent());
        Assert.Null(pool.Rent());
    }

    // ---- 回帰テスト: Compact 前の Return→Rent による二重登録 ----

    [Fact]
    public void Compact前のReturn直後にRentしてもActiveBulletsが二重登録されない()
    {
        var pool = new BulletPool(8);
        var a = pool.Rent()!;
        _ = pool.Rent()!;

        // Compact を挟まずに返却 → 再確保 (分裂・被弾エフェクトで実際に起こる流れ)
        pool.Return(a);
        var recycled = pool.Rent()!;

        Assert.Same(a, recycled); // 同じインスタンスが再利用される
        Assert.Equal(1, pool.ActiveBullets.Count(b => ReferenceEquals(b, a)));
        Assert.Equal(2, pool.ActiveCount);

        pool.Compact();

        Assert.Equal(1, pool.ActiveBullets.Count(b => ReferenceEquals(b, a)));
        Assert.Equal(2, pool.ActiveCount);
        Assert.Equal(pool.ActiveCount, pool.ActiveBullets.Distinct().Count());
    }

    [Fact]
    public void Return_Rentを繰り返してもActiveBulletsに重複が出ない()
    {
        var pool = new BulletPool(32);
        var rented = new List<Bullet>();
        for (var i = 0; i < 16; i++) rented.Add(pool.Rent()!);

        var rng = new Random(4649);
        for (var round = 0; round < 200; round++)
        {
            // Compact を挟まずに返却と確保を混在させる
            var victim = rented[rng.Next(rented.Count)];
            pool.Return(victim);
            rented.Remove(victim);
            rented.Add(pool.Rent()!);

            if (round % 7 == 0) pool.Compact();

            Assert.Equal(pool.ActiveCount, pool.ActiveBullets.Distinct().Count());
            Assert.All(pool.ActiveBullets, b => Assert.True(b.IsAlive));
        }

        pool.Compact();
        Assert.Equal(16, pool.ActiveCount);
        Assert.Equal(16, pool.ActiveBullets.Distinct().Count());
    }

    [Fact]
    public void Clear後は容量ぶん再確保できて重複しない()
    {
        var pool = new BulletPool(6);
        var bullets = Enumerable.Range(0, 6).Select(_ => pool.Rent()!).ToArray();

        // Compact 前に一部返却した状態で Clear する
        pool.Return(bullets[0]);
        pool.Return(bullets[3]);
        pool.Clear();

        Assert.Equal(0, pool.ActiveCount);

        var again = new List<Bullet>();
        for (var i = 0; i < 6; i++)
        {
            var b = pool.Rent();
            Assert.NotNull(b);
            again.Add(b!);
        }

        Assert.Null(pool.Rent()); // 容量を超えたら却下
        Assert.Equal(6, again.Distinct().Count());
        Assert.Equal(6, pool.ActiveBullets.Distinct().Count());
        Assert.Equal(6, pool.AllocatedCount); // 新規確保は発生していない
    }
}

/// <summary>トレイル (残像) リングバッファのテスト。</summary>
public class BulletTrailTests
{
    [Fact]
    public void トレイル長が0なら履歴を記録しない()
    {
        var bullet = new Bullet();
        bullet.Reset();
        bullet.TrailLength = 0;

        for (var i = 0; i < 10; i++) bullet.UpdateTrail(1.0 / 60.0);

        Assert.Equal(0, bullet.TrailCount);
    }

    [Fact]
    public void トレイルは指定長で頭打ちになる()
    {
        var bullet = new Bullet();
        bullet.Reset();
        bullet.TrailLength = 5;
        bullet.TrailInterval = 0.01;

        for (var i = 0; i < 100; i++)
        {
            bullet.Position = new Vec2(i, 0);
            bullet.UpdateTrail(0.01);
        }

        Assert.Equal(5, bullet.TrailCount);
    }

    [Fact]
    public void トレイルは上限MaxTrailLengthを超えない()
    {
        var bullet = new Bullet();
        bullet.Reset();
        bullet.TrailLength = 1000; // 上限より大きい値
        bullet.TrailInterval = 0.001;

        for (var i = 0; i < 500; i++)
        {
            bullet.Position = new Vec2(i, 0);
            bullet.UpdateTrail(0.001);
        }

        Assert.True(bullet.TrailCount <= Bullet.MaxTrailLength);
    }

    [Fact]
    public void GetTrailPositionは新しい順に返る()
    {
        var bullet = new Bullet();
        bullet.Reset();
        bullet.TrailLength = 4;
        bullet.TrailInterval = 1.0;

        for (var i = 1; i <= 4; i++)
        {
            bullet.Position = new Vec2(i * 10, 0);
            bullet.UpdateTrail(1.0);
        }

        // index 0 が直近 (x=40)、以降さかのぼる
        Assert.Equal(40, bullet.GetTrailPosition(0).X, 6);
        Assert.Equal(30, bullet.GetTrailPosition(1).X, 6);
        Assert.Equal(20, bullet.GetTrailPosition(2).X, 6);
        Assert.Equal(10, bullet.GetTrailPosition(3).X, 6);
    }

    [Fact]
    public void 範囲外のインデックスは現在位置を返す()
    {
        var bullet = new Bullet();
        bullet.Reset();
        bullet.Position = new Vec2(7, 8);

        Assert.Equal(new Vec2(7, 8), bullet.GetTrailPosition(0));
        Assert.Equal(new Vec2(7, 8), bullet.GetTrailPosition(-1));
        Assert.Equal(new Vec2(7, 8), bullet.GetTrailPosition(999));
    }

    [Theory]
    [InlineData(0.0, 0.0, 1.0)]     // フェード無し
    [InlineData(0.5, 0.0, 1.0)]     // フェードイン途中でも FadeInDuration=0 なら 1
    public void OpacityFactor_フェード無効時は常に1(double age, double fadeIn, double expected)
    {
        var bullet = new Bullet();
        bullet.Reset();
        bullet.Age = age;
        bullet.FadeInDuration = fadeIn;
        bullet.FadeOutDuration = 0;
        bullet.Lifetime = 10;

        Assert.Equal(expected, bullet.OpacityFactor, 5);
    }

    [Fact]
    public void OpacityFactor_フェードインは線形に増える()
    {
        var bullet = new Bullet();
        bullet.Reset();
        bullet.FadeInDuration = 1.0;
        bullet.FadeOutDuration = 0;
        bullet.Lifetime = 100;

        bullet.Age = 0.0;
        Assert.Equal(0f, bullet.OpacityFactor, 5);

        bullet.Age = 0.5;
        Assert.Equal(0.5f, bullet.OpacityFactor, 5);

        bullet.Age = 1.0;
        Assert.Equal(1f, bullet.OpacityFactor, 5);
    }

    [Fact]
    public void OpacityFactor_フェードアウトは寿命間際で減る()
    {
        var bullet = new Bullet();
        bullet.Reset();
        bullet.FadeInDuration = 0;
        bullet.FadeOutDuration = 1.0;
        bullet.Lifetime = 5.0;

        bullet.Age = 3.0; // 残り 2 秒 → まだ 1.0
        Assert.Equal(1f, bullet.OpacityFactor, 5);

        bullet.Age = 4.5; // 残り 0.5 秒 → 0.5
        Assert.Equal(0.5f, bullet.OpacityFactor, 5);

        bullet.Age = 5.0; // 残り 0 → 0
        Assert.Equal(0f, bullet.OpacityFactor, 5);
    }

    [Fact]
    public void AdvanceAnimationはコマ数で循環する()
    {
        var bullet = new Bullet();
        bullet.Reset();
        bullet.AnimationFps = 10;

        for (var i = 0; i < 4; i++) bullet.AdvanceAnimation(0.1, frameCount: 4);

        // 4 コマぶん進めたので一周して 0 に戻る
        Assert.Equal(0, bullet.AnimationFrame);

        bullet.AdvanceAnimation(0.1, frameCount: 4);
        Assert.Equal(1, bullet.AnimationFrame);
    }
}
