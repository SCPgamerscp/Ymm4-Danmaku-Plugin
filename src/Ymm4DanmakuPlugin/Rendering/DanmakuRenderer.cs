using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using Ymm4DanmakuPlugin.Core.Rendering;
using Ymm4DanmakuPlugin.Interop;
using Ymm4DanmakuPlugin.Parameters;

namespace Ymm4DanmakuPlugin.Rendering;

/// <summary>自機 (ターゲット) の描画情報。</summary>
public readonly record struct TargetRenderInfo(
    bool Enabled,
    float X,
    float Y,
    float Scale,
    float Rotation,
    float Opacity,
    float Radius,
    bool ShowMarker,
    bool HasCustomImage
);

/// <summary>
/// 弾幕および自機を Direct2D の <see cref="ID2D1CommandList"/> へ描画する。
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
    /// 弾幕および自機を描画し、新しい <see cref="ID2D1CommandList"/> を返す。
    /// </summary>
    public ID2D1CommandList Render(
        RenderBatchBuilder builder,
        Func<int, double>? glowIntensityProvider = null,
        in TargetRenderInfo targetInfo = default)
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

            // --- 自機 (ターゲット) の描画 (弾幕の下に描画) ---
            DrawTarget(dc, in targetInfo);

            // --- 弾幕のバッチ描画 ---
            var instances = builder.Instances;

            foreach (var batch in builder.Batches)
            {
                var sprite = sprites.Get(batch.SpriteIndex);
                if (sprite is null) continue;

                dc.PrimitiveBlend = batch.Additive ? PrimitiveBlend.Add : PrimitiveBlend.SourceOver;

                var glow = glowIntensityProvider?.Invoke(batch.SpriteIndex) ?? 1.0;
                // 加算合成のときだけグローの重ね描きを行う
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

    private void DrawTarget(ID2D1DeviceContext6 dc, in TargetRenderInfo target)
    {
        if (!target.Enabled) return;

        // 1. 自機カスタム画像の描画
        if (target.HasCustomImage && target.Opacity > 0.001f && target.Scale > 0.001f)
        {
            var targetSprite = sprites.Get(SpriteSlots.TargetCustomSlot);
            if (targetSprite?.Bitmap is { } targetBitmap)
            {
                dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
                var transform =
                    Matrix3x2.CreateScale(targetSprite.BaseRadius * target.Scale) *
                    Matrix3x2.CreateRotation(target.Rotation * MathF.PI / 180f) *
                    Matrix3x2.CreateTranslation(target.X, target.Y);
                dc.Transform = transform;

                var size = targetBitmap.Size;
                var half = MathF.Max(size.Width, size.Height) * 0.5f;
                var w = size.Width / half * 0.5f;
                var h = size.Height / half * 0.5f;
                var dest = new Vortice.RawRectF(-w, -h, w, h);

                dc.DrawBitmap(
                    targetBitmap,
                    dest,
                    Math.Clamp(target.Opacity, 0f, 1f),
                    InterpolationMode.Linear,
                    null,
                    null);

                LastDrawCallCount++;
                LastSpriteCount++;
            }
        }

        // 2. 当たり判定マーカー (喰らい判定サークル) の描画
        if (target.ShowMarker && target.Radius > 0.5f)
        {
            dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
            dc.Transform = Matrix3x2.CreateTranslation(target.X, target.Y);

            // 外側の半透明赤塗り
            brush!.Color = new Color4(1f, 0.2f, 0.3f, 0.35f);
            dc.FillEllipse(new Ellipse(Vector2.Zero, target.Radius, target.Radius), brush);

            // 白い輪郭線
            brush.Color = new Color4(1f, 1f, 1f, 0.9f);
            dc.DrawEllipse(new Ellipse(Vector2.Zero, target.Radius, target.Radius), brush, 1.5f);

            // 中心の赤点
            var dotRadius = MathF.Min(3f, target.Radius * 0.35f);
            brush.Color = new Color4(1f, 0.1f, 0.2f, 1f);
            dc.FillEllipse(new Ellipse(Vector2.Zero, dotRadius, dotRadius), brush);

            LastDrawCallCount += 3;
        }
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

                dc.DrawBitmap(
                    bitmap,
                    dest,
                    Math.Clamp(instance.A * alphaScale, 0f, 1f),
                    InterpolationMode.Linear,
                    null,
                    null);
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
