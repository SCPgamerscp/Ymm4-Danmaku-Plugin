using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using Ymm4DanmakuPlugin.Core.Rendering;
using Ymm4DanmakuPlugin.Interop;

namespace Ymm4DanmakuPlugin.Rendering;

/// <summary>
/// 弾幕を Direct2D の <see cref="ID2D1CommandList"/> へ描画する。
/// <para>
/// <b>速度のための工夫</b>
/// </para>
/// <list type="bullet">
/// <item>
///   コアの <see cref="RenderBatchBuilder"/> が「同一スプライト・同一合成モード」で
///   ソート済みの配列を渡してくるため、ブラシ / 合成モードの切り替え回数が最小になる。
/// </item>
/// <item>
///   ブラシは 1 本だけ作って色だけ差し替える (弾ごとにブラシを作らない)。
/// </item>
/// <item>
///   形状ジオメトリは <see cref="BulletSpriteLibrary"/> でキャッシュし、毎フレーム作らない。
/// </item>
/// <item>
///   <see cref="ID2D1CommandList"/> はベクター命令の記録なので、
///   結果をそのまま YMM4 側のエフェクトチェーンへ渡せる (中間ビットマップを作らない)。
/// </item>
/// </list>
/// <para>
/// <b>CommandList の作法 (重要)</b>: Target 設定 → BeginDraw → Clear → 描画 → EndDraw →
/// <b>Target を null に戻す</b> → <b>Close()</b> の順を守る。
/// Target を戻す前に Close するとデバイスロストの原因になる。
/// </para>
/// </summary>
public sealed class DanmakuRenderer : IDisposable
{
    private readonly IGraphicsDevicesAndContext devices;
    private readonly BulletSpriteLibrary sprites;
    private readonly DisposeCollector disposer = new();

    private ID2D1SolidColorBrush? brush;
    private ID2D1CommandList? commandList;

    public DanmakuRenderer(IGraphicsDevicesAndContext devices)
    {
        this.devices = devices;
        sprites = new BulletSpriteLibrary(devices);
        disposer.Collect(sprites);
    }

    /// <summary>スプライト管理。ユーザー画像の登録に使用する。</summary>
    public BulletSpriteLibrary Sprites => sprites;

    /// <summary>直近の描画結果。<see cref="Render"/> 前は null。</summary>
    public ID2D1CommandList? Output => commandList;

    /// <summary>直近の描画で発行したドローコール数 (性能計測・デバッグ用)。</summary>
    public int LastDrawCallCount { get; private set; }

    /// <summary>直近の描画で描いたスプライト数。</summary>
    public int LastSpriteCount { get; private set; }

    /// <summary>
    /// 弾幕を描画し、新しい <see cref="ID2D1CommandList"/> を返す。
    /// <para>
    /// 呼び出しごとに CommandList を作り直す (Close 済みの CommandList は再記録できない)。
    /// 前回のものはここで破棄されるため、呼び出し側は保持し続けてはいけない。
    /// </para>
    /// </summary>
    /// <param name="builder">ソート済みの描画データ。</param>
    /// <param name="glowIntensityProvider">
    /// スプライト番号からグロー (発光) の強さを引く関数。
    /// 1.0 より大きい場合、加算合成の弾を少し大きめに重ね描きして光の滲みを作る。
    /// </param>
    public ID2D1CommandList Render(RenderBatchBuilder builder, Func<int, double>? glowIntensityProvider = null)
    {
        var dc = devices.DeviceContext;

        // 前回の CommandList を破棄してから作り直す
        if (commandList is not null) disposer.RemoveAndDispose(ref commandList);

        commandList = dc.CreateCommandList();
        disposer.Collect(commandList);

        brush ??= Collect(dc.CreateSolidColorBrush(new Color4(1f, 1f, 1f, 1f)));

        var previousTarget = dc.Target;
        var previousBlend = dc.PrimitiveBlend;
        var previousTransform = dc.Transform;

        LastDrawCallCount = 0;
        LastSpriteCount = 0;

        dc.Target = commandList;
        dc.BeginDraw();
        try
        {
            dc.Clear(null);
            dc.AntialiasMode = AntialiasMode.PerPrimitive;

            var instances = builder.Instances;

            foreach (var batch in builder.Batches)
            {
                var sprite = sprites.Get(batch.SpriteIndex);
                if (sprite is null) continue;

                dc.PrimitiveBlend = batch.Additive ? PrimitiveBlend.Add : PrimitiveBlend.SourceOver;

                var glow = glowIntensityProvider?.Invoke(batch.SpriteIndex) ?? 1.0;
                // 加算合成のときだけグローの重ね描きを行う (通常合成では単に濃くなるだけなので無意味)
                var glowPasses = batch.Additive && glow > 1.01 ? 2 : 1;

                var end = batch.Offset + batch.Count;
                for (var i = batch.Offset; i < end; i++)
                {
                    ref var instance = ref instances[i];
                    DrawInstance(dc, sprite, in instance, glow, glowPasses);
                    LastSpriteCount++;
                }
            }
        }
        finally
        {
            dc.EndDraw();

            // Target を戻してから Close する (順序が逆だと不正な状態になる)
            dc.Target = previousTarget;
            dc.PrimitiveBlend = previousBlend;
            dc.Transform = previousTransform;

            commandList.Close();
        }

        return commandList;
    }

    private void DrawInstance(
        ID2D1DeviceContext6 dc,
        BulletSprite sprite,
        in BulletInstance instance,
        double glow,
        int glowPasses)
    {
        if (instance.Scale <= 0f || instance.A <= 0.001f) return;

        for (var pass = 0; pass < glowPasses; pass++)
        {
            // 1 パス目は本体、2 パス目は「大きく薄い」滲み
            var isGlowPass = pass > 0;
            var scaleBoost = isGlowPass ? 1f + (float)Math.Clamp(glow - 1.0, 0.0, 2.0) * 0.6f : 1f;
            var alphaScale = isGlowPass ? (float)Math.Clamp(glow - 1.0, 0.0, 2.0) * 0.35f : 1f;
            if (alphaScale <= 0.001f) continue;

            var radius = sprite.BaseRadius * instance.Scale * scaleBoost;
            if (radius <= 0.01f) continue;

            var transform =
                Matrix3x2.CreateScale(radius) *
                Matrix3x2.CreateRotation(instance.Rotation * MathF.PI / 180f) *
                Matrix3x2.CreateTranslation(instance.X, instance.Y);

            dc.Transform = transform;

            if (sprite.Bitmap is { } bitmap)
            {
                // 画像は原点中心・半径 1.0 の矩形に収まるよう描く
                var size = bitmap.Size;
                var half = MathF.Max(size.Width, size.Height) * 0.5f;
                var w = size.Width / half * 0.5f;
                var h = size.Height / half * 0.5f;
                var dest = new Vortice.RawRectF(-w, -h, w, h);

                var isTinted = Math.Abs(instance.R - 1f) > 0.01f ||
                               Math.Abs(instance.G - 1f) > 0.01f ||
                               Math.Abs(instance.B - 1f) > 0.01f;

                if (isTinted)
                {
                    brush!.Color = ColorExtensions.ToColor4(instance.R, instance.G, instance.B, instance.A, alphaScale);
                    dc.FillOpacityMask(
                        bitmap,
                        brush,
                        OpacityMaskContent.Graphics,
                        dest,
                        null);
                }
                else
                {
                    dc.DrawBitmap(
                        bitmap,
                        dest,
                        Math.Clamp(instance.A * alphaScale, 0f, 1f),
                        InterpolationMode.Linear,
                        null,
                        null);
                }
            }
            else if (sprite.Geometry is { } geometry)
            {
                brush!.Color = ColorExtensions.ToColor4(instance.R, instance.G, instance.B, instance.A, alphaScale);
                dc.FillGeometry(geometry, brush);
            }
            else
            {
                continue;
            }

            LastDrawCallCount++;
        }
    }

    private T Collect<T>(T disposable) where T : IDisposable
    {
        disposer.Collect(disposable);
        return disposable;
    }

    public void Dispose()
    {
        brush = null;
        commandList = null;
        disposer.DisposeAndClear();
    }
}
