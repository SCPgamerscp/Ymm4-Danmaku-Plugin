using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Mathematics;
using Ymm4DanmakuPlugin.Core.Model;
using Ymm4DanmakuPlugin.Core.Rendering;

namespace Ymm4DanmakuPlugin.Core.Tests;

/// <summary>
/// 描画バッチ構築のテスト。
/// 開発計画書の「GPU バッチ描画 / インスタンシング」要件に対応する。
/// </summary>
public class RenderBatchBuilderTests
{
    private static readonly BulletAppearance DefaultAppearance = new();

    private static Func<int, BulletAppearance> Appearance(BulletAppearance? appearance = null)
    {
        var value = appearance ?? DefaultAppearance;
        return _ => value;
    }

    // Bullet.IsAlive の setter は internal のため、生存弾はプール経由で作る。
    private readonly BulletPool pool = new(16384);

    /// <summary>描画対象になる最低限の生存弾を作る。</summary>
    private Bullet Alive(
        int sprite = 0,
        bool additive = true,
        double x = 0,
        double y = 0,
        float alpha = 1f,
        double scale = 1.0)
    {
        var bullet = pool.Rent() ?? throw new InvalidOperationException("プールが枯渇しました。");
        bullet.SpriteIndex = sprite;
        bullet.Additive = additive;
        bullet.Position = new Vec2(x, y);
        bullet.Color = new BulletColor(1f, 1f, 1f, alpha);
        bullet.Scale = scale;
        bullet.Lifetime = double.PositiveInfinity;
        return bullet;
    }

    [Fact]
    public void 生存中の弾だけがインスタンス化される()
    {
        var first = Alive();
        var dead = Alive();
        var last = Alive();
        pool.Return(dead); // リストには残ったまま IsAlive だけが偷る

        var bullets = new[] { first, dead, last };
        var builder = new RenderBatchBuilder();

        builder.Build(bullets, Appearance());

        Assert.Equal(2, builder.Count);
    }

    [Fact]
    public void 弾が無ければバッチも作られない()
    {
        var builder = new RenderBatchBuilder();

        builder.Build([], Appearance());

        Assert.Equal(0, builder.Count);
        Assert.Empty(builder.Batches);
    }

    [Fact]
    public void 完全に透明な弾は除外される()
    {
        var bullets = new[] { Alive(alpha: 0f), Alive(alpha: 1f) };
        var builder = new RenderBatchBuilder();

        builder.Build(bullets, Appearance());

        Assert.Equal(1, builder.Count);
    }

    [Fact]
    public void globalOpacityが0なら何も描かれない()
    {
        var bullets = new[] { Alive(), Alive(), Alive() };
        var builder = new RenderBatchBuilder();

        builder.Build(bullets, Appearance(), globalOpacity: 0.0);

        Assert.Equal(0, builder.Count);
    }

    [Fact]
    public void globalOpacityがアルファへ乗算される()
    {
        var bullets = new[] { Alive(alpha: 0.8f) };
        var builder = new RenderBatchBuilder();

        builder.Build(bullets, Appearance(), globalOpacity: 0.5);

        Assert.Equal(0.4f, builder.Instances[0].A, 4);
    }

    [Fact]
    public void globalOpacityは0から1へ丸められる()
    {
        var bullets = new[] { Alive(alpha: 1f) };
        var builder = new RenderBatchBuilder();

        builder.Build(bullets, Appearance(), globalOpacity: 5.0);

        Assert.Equal(1.0f, builder.Instances[0].A, 4);
    }

    [Fact]
    public void 位置とスケールがインスタンスへ転写される()
    {
        var bullets = new[] { Alive(x: 123.5, y: -45.25, scale: 1.75) };
        var builder = new RenderBatchBuilder();

        builder.Build(bullets, Appearance());

        var instance = builder.Instances[0];
        Assert.Equal(123.5f, instance.X, 4);
        Assert.Equal(-45.25f, instance.Y, 4);
        Assert.Equal(1.75f, instance.Scale, 4);
    }

    [Fact]
    public void 色がインスタンスへ転写される()
    {
        var bullet = Alive();
        bullet.Color = new BulletColor(0.25f, 0.5f, 0.75f, 1f);
        var builder = new RenderBatchBuilder();

        builder.Build([bullet], Appearance());

        var instance = builder.Instances[0];
        Assert.Equal(0.25f, instance.R, 4);
        Assert.Equal(0.5f, instance.G, 4);
        Assert.Equal(0.75f, instance.B, 4);
    }

    [Fact]
    public void AlignToDirectionが真なら進行方向が回転へ加算される()
    {
        var aligned = Alive();
        aligned.AlignToDirection = true;
        aligned.Direction = 30;
        aligned.Rotation = 15;

        var free = Alive();
        free.AlignToDirection = false;
        free.Direction = 30;
        free.Rotation = 15;

        var builder = new RenderBatchBuilder();

        builder.Build([aligned], Appearance());
        Assert.Equal(45f, builder.Instances[0].Rotation, 4);

        builder.Build([free], Appearance());
        Assert.Equal(15f, builder.Instances[0].Rotation, 4);
    }

    [Fact]
    public void 同一スプライト_同一合成モードは1バッチにまとまる()
    {
        var bullets = Enumerable.Range(0, 50).Select(_ => Alive(sprite: 2, additive: true)).ToArray();
        var builder = new RenderBatchBuilder();

        builder.Build(bullets, Appearance());

        Assert.Single(builder.Batches);
        Assert.Equal(2, builder.Batches[0].SpriteIndex);
        Assert.True(builder.Batches[0].Additive);
        Assert.Equal(0, builder.Batches[0].Offset);
        Assert.Equal(50, builder.Batches[0].Count);
    }

    [Fact]
    public void スプライトが違えば別バッチになる()
    {
        var bullets = new[]
        {
            Alive(sprite: 0), Alive(sprite: 1), Alive(sprite: 0),
            Alive(sprite: 2), Alive(sprite: 1),
        };
        var builder = new RenderBatchBuilder();

        builder.Build(bullets, Appearance());

        // ソート後に (0,0,1,1,2) の 3 バッチへまとまる
        Assert.Equal(3, builder.Batches.Count);
        Assert.Equal([0, 1, 2], builder.Batches.Select(b => b.SpriteIndex).ToArray());
        Assert.Equal([2, 2, 1], builder.Batches.Select(b => b.Count).ToArray());
    }

    [Fact]
    public void 合成モードが違えば別バッチになる()
    {
        var bullets = new[]
        {
            Alive(sprite: 0, additive: true),
            Alive(sprite: 0, additive: false),
            Alive(sprite: 0, additive: true),
        };
        var builder = new RenderBatchBuilder();

        builder.Build(bullets, Appearance());

        Assert.Equal(2, builder.Batches.Count);
        // ソートキーの都合上、通常合成 (false) が先に来る
        Assert.False(builder.Batches[0].Additive);
        Assert.Equal(1, builder.Batches[0].Count);
        Assert.True(builder.Batches[1].Additive);
        Assert.Equal(2, builder.Batches[1].Count);
    }

    [Fact]
    public void バッチのOffsetとCountで全インスタンスを覆う()
    {
        var bullets = new[]
        {
            Alive(sprite: 3, additive: false), Alive(sprite: 1, additive: true),
            Alive(sprite: 3, additive: true),  Alive(sprite: 1, additive: false),
            Alive(sprite: 0, additive: true),
        };
        var builder = new RenderBatchBuilder();

        builder.Build(bullets, Appearance());

        // 隙間も重複もなく連続していること
        var expectedOffset = 0;
        foreach (var batch in builder.Batches)
        {
            Assert.Equal(expectedOffset, batch.Offset);
            Assert.True(batch.Count > 0);
            expectedOffset += batch.Count;
        }

        Assert.Equal(builder.Count, expectedOffset);
    }

    [Fact]
    public void バッチ内のインスタンスはスプライトと合成モードが揃っている()
    {
        var bullets = Enumerable.Range(0, 40)
            .Select(i => Alive(sprite: i % 4, additive: i % 3 == 0))
            .ToArray();
        var builder = new RenderBatchBuilder();

        builder.Build(bullets, Appearance());

        foreach (var batch in builder.Batches)
        {
            for (var i = batch.Offset; i < batch.Offset + batch.Count; i++)
            {
                Assert.Equal(batch.SpriteIndex, builder.Instances[i].SpriteIndex);
                Assert.Equal(batch.Additive, builder.Instances[i].Additive);
            }
        }
    }

    [Fact]
    public void トレイルは弾本体より先に描かれる()
    {
        var bullet = Alive(sprite: 1);
        bullet.TrailLength = 4;
        bullet.TrailInterval = 0.01;
        for (var i = 0; i < 4; i++)
        {
            bullet.Position = new Vec2(i * 10, 0);
            bullet.UpdateTrail(0.02);
        }

        var builder = new RenderBatchBuilder();
        builder.Build([bullet], Appearance());

        // 1 本体 + トレイル点数
        Assert.Equal(1 + bullet.TrailCount, builder.Count);

        // 同一バッチ内では IsTrail=true が先、本体が最後
        var trailIndices = Enumerable.Range(0, builder.Count)
            .Where(i => builder.Instances[i].IsTrail)
            .ToArray();
        var bodyIndices = Enumerable.Range(0, builder.Count)
            .Where(i => !builder.Instances[i].IsTrail)
            .ToArray();

        Assert.NotEmpty(trailIndices);
        Assert.Single(bodyIndices);
        Assert.True(trailIndices.Max() < bodyIndices[0], "トレイルが本体より後ろに描かれている");
    }

    [Fact]
    public void トレイル無効なら本体だけが描かれる()
    {
        var bullet = Alive();
        bullet.TrailLength = 0;
        bullet.UpdateTrail(0.5);

        var builder = new RenderBatchBuilder();
        builder.Build([bullet], Appearance());

        Assert.Equal(1, builder.Count);
        Assert.False(builder.Instances[0].IsTrail);
    }

    [Fact]
    public void トレイルのアルファは本体より薄い()
    {
        var bullet = Alive();
        bullet.TrailLength = 6;
        bullet.TrailInterval = 0.01;
        for (var i = 0; i < 6; i++)
        {
            bullet.Position = new Vec2(i * 5, 0);
            bullet.UpdateTrail(0.02);
        }

        var builder = new RenderBatchBuilder();
        builder.Build([bullet], Appearance(new BulletAppearance { TrailFade = 0.0, TrailScale = 0.5 }));

        var body = Enumerable.Range(0, builder.Count).First(i => !builder.Instances[i].IsTrail);
        var trails = Enumerable.Range(0, builder.Count).Where(i => builder.Instances[i].IsTrail).ToArray();

        Assert.All(trails, i => Assert.True(
            builder.Instances[i].A < builder.Instances[body].A,
            "トレイルのアルファが本体以上になっている"));
    }

    [Fact]
    public void トレイルのスケールはTrailScaleと本体の間になる()
    {
        var bullet = Alive(scale: 2.0);
        bullet.TrailLength = 8;
        bullet.TrailInterval = 0.01;
        for (var i = 0; i < 8; i++)
        {
            bullet.Position = new Vec2(i * 5, 0);
            bullet.UpdateTrail(0.02);
        }

        var builder = new RenderBatchBuilder();
        builder.Build([bullet], Appearance(new BulletAppearance { TrailScale = 0.5 }));

        var trails = Enumerable.Range(0, builder.Count)
            .Where(i => builder.Instances[i].IsTrail)
            .Select(i => builder.Instances[i].Scale)
            .ToArray();

        // 本体スケール 2.0 に対して TrailScale 0.5 〜 1.0 倍の範囲
        Assert.All(trails, s => Assert.InRange(s, 2.0f * 0.5f - 0.001f, 2.0f + 0.001f));
    }

    [Fact]
    public void 配列は再利用され容量は自動拡張される()
    {
        var builder = new RenderBatchBuilder();

        // 初期容量 (1024) を超える数を投げる
        var many = Enumerable.Range(0, 3000).Select(i => Alive(sprite: i % 8)).ToArray();
        builder.Build(many, Appearance());
        Assert.Equal(3000, builder.Count);
        Assert.True(builder.Instances.Length >= 3000);

        var capacityAfterGrow = builder.Instances.Length;

        // 少ない数で再構築しても配列は縮まない (= 使い回される)
        builder.Build([Alive()], Appearance());
        Assert.Equal(1, builder.Count);
        Assert.Equal(capacityAfterGrow, builder.Instances.Length);
    }

    [Fact]
    public void 再構築のたびにバッチはクリアされる()
    {
        var builder = new RenderBatchBuilder();

        builder.Build([Alive(sprite: 0), Alive(sprite: 1), Alive(sprite: 2)], Appearance());
        Assert.Equal(3, builder.Batches.Count);

        builder.Build([Alive(sprite: 5)], Appearance());
        Assert.Single(builder.Batches);
        Assert.Equal(5, builder.Batches[0].SpriteIndex);
    }

    [Fact]
    public void MaxSpriteSlotsを超えるスプライト番号でも破綻しない()
    {
        var builder = new RenderBatchBuilder { MaxSpriteSlots = 8 };
        var bullets = new[] { Alive(sprite: 0), Alive(sprite: 100), Alive(sprite: -5) };

        builder.Build(bullets, Appearance());

        Assert.Equal(3, builder.Count);
        Assert.NotEmpty(builder.Batches);
        Assert.Equal(3, builder.Batches.Sum(b => b.Count));
    }

    [Fact]
    public void 大量の弾でもドローコール数はスプライト_合成モードの組数に収まる()
    {
        // インスタンシングの効果を検証: 5000 発でも最大 8 通り (4 スプライト × 2 合成) までのバッチ数
        var bullets = Enumerable.Range(0, 5000)
            .Select(i => Alive(sprite: i % 4, additive: i % 2 == 0))
            .ToArray();
        var builder = new RenderBatchBuilder();

        builder.Build(bullets, Appearance());

        Assert.Equal(5000, builder.Count);
        Assert.True(builder.Batches.Count <= 8, $"バッチ数が多すぎる: {builder.Batches.Count}");
        Assert.Equal(5000, builder.Batches.Sum(b => b.Count));
    }

    [Fact]
    public void AnimationFrameがインスタンスへ転写される()
    {
        var bullet = Alive();
        bullet.AnimationFrame = 3;

        var builder = new RenderBatchBuilder();
        builder.Build([bullet], Appearance());

        Assert.Equal(3, builder.Instances[0].AnimationFrame);
    }

    [Fact]
    public void OpacityFactorがアルファへ反映される()
    {
        var bullet = Alive(alpha: 1f);
        bullet.FadeInDuration = 1.0;
        bullet.Age = 0.25; // フェードイン 25%

        var builder = new RenderBatchBuilder();
        builder.Build([bullet], Appearance());

        Assert.Equal(0.25f, builder.Instances[0].A, 3);
    }
}

/// <summary>BulletInstance / DanmakuRenderBatch 構造体のテスト。</summary>
public class RenderStructureTests
{
    [Fact]
    public void BulletInstanceは値型()
    {
        Assert.True(typeof(BulletInstance).IsValueType);
    }

    [Fact]
    public void DanmakuRenderBatchは等値比較できる()
    {
        var a = new DanmakuRenderBatch(1, true, 0, 10);
        var b = new DanmakuRenderBatch(1, true, 0, 10);
        var c = new DanmakuRenderBatch(1, false, 0, 10);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void DanmakuRenderBatchの各要素を読み出せる()
    {
        var batch = new DanmakuRenderBatch(SpriteIndex: 7, Additive: true, Offset: 128, Count: 64);

        Assert.Equal(7, batch.SpriteIndex);
        Assert.True(batch.Additive);
        Assert.Equal(128, batch.Offset);
        Assert.Equal(64, batch.Count);
    }
}
