using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using Ymm4DanmakuPlugin.Core.Configuration;
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

/// <summary>エネミー (敵・ボス) および魔法陣・オーラの描画情報。</summary>
public readonly record struct EnemyRenderInfo(
    float X,
    float Y,
    bool EnemyEnabled,
    int EnemySlot,
    float EnemyScale,
    float EnemyRotation,
    float EnemyOpacity,
    bool EnemyBehindBullets,
    bool MagicCircleEnabled,
    int MagicCircleSlot,
    float MagicCircleScale,
    float MagicCircleAngle,
    Color4 MagicCircleColor,
    float MagicCircleOpacity,
    bool MagicCircleAdditive,
    bool IsBuiltInMagicCircle,
    bool AuraEnabled,
    float AuraIntensity,
    Color4 AuraColor
);

/// <summary>ボス体力バー (HP ゲージ) の描画情報。</summary>
public readonly record struct BossHpBarRenderInfo(
    bool Enabled,
    HpBarStyle Style,
    float HpRatio,
    float DamageLagRatio,
    float BossX,
    float BossY,
    float Radius,
    float Width,
    float Height,
    float X,
    float Y,
    float Thickness,
    Color4 BarColor,
    Color4 DangerColor,
    Color4 DamageLagColor,
    Color4 BackgroundColor,
    int PhaseCount,
    bool Glow,
    float Opacity
);

/// <summary>
/// 弾幕・自機・エネミー・魔法陣・オーラ・体力バーを Direct2D の <see cref="ID2D1CommandList"/> へ描画する。
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
    /// 弾幕・自機・エネミー・体力バーを描画し、新しい <see cref="ID2D1CommandList"/> を返す。
    /// </summary>
    public ID2D1CommandList Render(
        RenderBatchBuilder builder,
        Func<int, double>? glowIntensityProvider = null,
        in TargetRenderInfo targetInfo = default,
        IReadOnlyList<EnemyRenderInfo>? enemies = null,
        in BossHpBarRenderInfo hpBarInfo = default)
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

            // --- レイヤー 1: 弾の奥に描画する要素 (オーラ、魔法陣、奥配置のエネミー) ---
            if (enemies is not null)
            {
                DrawEnemyBackgroundLayer(dc, enemies);
            }

            // --- レイヤー 2: 自機 (ターゲット) の描画 ---
            DrawTarget(dc, in targetInfo);

            // --- レイヤー 3: 弾幕のバッチ描画 ---
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

            // --- レイヤー 4: 弾の手前に描画するエネミー ---
            if (enemies is not null)
            {
                DrawEnemyForegroundLayer(dc, enemies);
            }

            // --- レイヤー 5: ボス体力バー / UI ゲージの描画 ---
            if (hpBarInfo.Enabled)
            {
                DrawBossHpBars(dc, in hpBarInfo);
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

    private void DrawEnemyBackgroundLayer(ID2D1DeviceContext6 dc, IReadOnlyList<EnemyRenderInfo> enemies)
    {
        for (var i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];

            // 1. オーラ発光の描画
            if (enemy.AuraEnabled && MathF.Abs(enemy.AuraIntensity) > 0.01f)
            {
                DrawAura(dc, in enemy);
            }

            // 2. 魔法陣の描画
            if (enemy.MagicCircleEnabled && MathF.Abs(enemy.MagicCircleOpacity) > 0.001f && MathF.Abs(enemy.MagicCircleScale) > 0.001f)
            {
                DrawMagicCircle(dc, in enemy);
            }

            // 3. 奥配置のエネミー画像
            if (enemy.EnemyEnabled && enemy.EnemyBehindBullets && MathF.Abs(enemy.EnemyOpacity) > 0.001f && MathF.Abs(enemy.EnemyScale) > 0.001f)
            {
                DrawEnemyImage(dc, in enemy);
            }
        }
    }

    private void DrawEnemyForegroundLayer(ID2D1DeviceContext6 dc, IReadOnlyList<EnemyRenderInfo> enemies)
    {
        for (var i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];
            if (enemy.EnemyEnabled && !enemy.EnemyBehindBullets && MathF.Abs(enemy.EnemyOpacity) > 0.001f && MathF.Abs(enemy.EnemyScale) > 0.001f)
            {
                DrawEnemyImage(dc, in enemy);
            }
        }
    }

    private void DrawAura(ID2D1DeviceContext6 dc, in EnemyRenderInfo enemy)
    {
        dc.PrimitiveBlend = PrimitiveBlend.Add;
        var radius = 70f * MathF.Abs(enemy.EnemyScale) * MathF.Abs(enemy.AuraIntensity);
        if (radius <= 0.1f) return;

        dc.Transform = Matrix3x2.CreateTranslation(enemy.X, enemy.Y);

        // 外側の淡いオーラ
        brush!.Color = new Color4(enemy.AuraColor.R, enemy.AuraColor.G, enemy.AuraColor.B, enemy.AuraColor.A * 0.18f);
        dc.FillEllipse(new Ellipse(Vector2.Zero, radius * 1.3f, radius * 1.3f), brush);

        // 中間のオーラ
        brush.Color = new Color4(enemy.AuraColor.R, enemy.AuraColor.G, enemy.AuraColor.B, enemy.AuraColor.A * 0.35f);
        dc.FillEllipse(new Ellipse(Vector2.Zero, radius, radius), brush);

        // 内側の濃いオーラ
        brush.Color = new Color4(1f, 1f, 1f, enemy.AuraColor.A * 0.45f);
        dc.FillEllipse(new Ellipse(Vector2.Zero, radius * 0.6f, radius * 0.6f), brush);

        LastDrawCallCount += 3;
    }

    private void DrawMagicCircle(ID2D1DeviceContext6 dc, in EnemyRenderInfo enemy)
    {
        dc.PrimitiveBlend = enemy.MagicCircleAdditive ? PrimitiveBlend.Add : PrimitiveBlend.SourceOver;

        if (enemy.IsBuiltInMagicCircle)
        {
            var outerSprite = sprites.Get(SpriteSlots.BuiltInMagicCircleOuterSlot);
            var innerSprite = sprites.Get(SpriteSlots.BuiltInMagicCircleInnerSlot);

            var baseRadius = outerSprite?.BaseRadius ?? 100f;
            var totalRadius = baseRadius * enemy.MagicCircleScale;
            var absTotalRadius = MathF.Abs(totalRadius);
            if (absTotalRadius <= 0.1f) return;

            // 1. 中心の柔らかな光彩
            if (enemy.MagicCircleAdditive)
            {
                dc.Transform = Matrix3x2.CreateTranslation(enemy.X, enemy.Y);
                brush!.Color = new Color4(
                    enemy.MagicCircleColor.R,
                    enemy.MagicCircleColor.G,
                    enemy.MagicCircleColor.B,
                    enemy.MagicCircleColor.A * MathF.Abs(enemy.MagicCircleOpacity) * 0.18f);
                dc.FillEllipse(new Ellipse(Vector2.Zero, absTotalRadius * 0.45f, absTotalRadius * 0.45f), brush);
                LastDrawCallCount++;
            }

            var strokeWidth = 2.0f / MathF.Max(0.1f, absTotalRadius);

            // 2. 外周幾何学魔法陣 (正回転)
            if (outerSprite?.Geometry is { } outerGeometry)
            {
                var transformOuter =
                    Matrix3x2.CreateScale(totalRadius) *
                    Matrix3x2.CreateRotation(enemy.MagicCircleAngle * MathF.PI / 180f) *
                    Matrix3x2.CreateTranslation(enemy.X, enemy.Y);
                dc.Transform = transformOuter;

                // 本体のシャープなライン
                brush!.Color = new Color4(
                    enemy.MagicCircleColor.R,
                    enemy.MagicCircleColor.G,
                    enemy.MagicCircleColor.B,
                    enemy.MagicCircleColor.A * MathF.Abs(enemy.MagicCircleOpacity));
                dc.DrawGeometry(outerGeometry, brush, strokeWidth);
                LastDrawCallCount++;

                // 加算合成時のブルーム発光パス
                if (enemy.MagicCircleAdditive)
                {
                    brush.Color = new Color4(
                        enemy.MagicCircleColor.R,
                        enemy.MagicCircleColor.G,
                        enemy.MagicCircleColor.B,
                        enemy.MagicCircleColor.A * MathF.Abs(enemy.MagicCircleOpacity) * 0.35f);
                    dc.DrawGeometry(outerGeometry, brush, strokeWidth * 2.8f);
                    LastDrawCallCount++;
                }
            }

            // 3. 内周幾何学魔法陣 (逆回転 1.25 倍速)
            if (innerSprite?.Geometry is { } innerGeometry)
            {
                var transformInner =
                    Matrix3x2.CreateScale(totalRadius) *
                    Matrix3x2.CreateRotation(-enemy.MagicCircleAngle * 1.25f * MathF.PI / 180f) *
                    Matrix3x2.CreateTranslation(enemy.X, enemy.Y);
                dc.Transform = transformInner;

                // 本体のシャープなライン
                brush!.Color = new Color4(
                    enemy.MagicCircleColor.R,
                    enemy.MagicCircleColor.G,
                    enemy.MagicCircleColor.B,
                    enemy.MagicCircleColor.A * MathF.Abs(enemy.MagicCircleOpacity));
                dc.DrawGeometry(innerGeometry, brush, strokeWidth);
                LastDrawCallCount++;

                // 加算合成時のブルーム発光パス
                if (enemy.MagicCircleAdditive)
                {
                    brush.Color = new Color4(
                        enemy.MagicCircleColor.R,
                        enemy.MagicCircleColor.G,
                        enemy.MagicCircleColor.B,
                        enemy.MagicCircleColor.A * MathF.Abs(enemy.MagicCircleOpacity) * 0.35f);
                    dc.DrawGeometry(innerGeometry, brush, strokeWidth * 2.8f);
                    LastDrawCallCount++;
                }
            }
        }
        else
        {
            var sprite = sprites.Get(enemy.MagicCircleSlot);
            if (sprite?.Bitmap is { } bitmap)
            {
                var transform =
                    Matrix3x2.CreateScale(sprite.BaseRadius * enemy.MagicCircleScale) *
                    Matrix3x2.CreateRotation(enemy.MagicCircleAngle * MathF.PI / 180f) *
                    Matrix3x2.CreateTranslation(enemy.X, enemy.Y);
                dc.Transform = transform;

                var size = bitmap.Size;
                var half = MathF.Max(size.Width, size.Height) * 0.5f;
                var w = size.Width / half * 0.5f;
                var h = size.Height / half * 0.5f;
                var dest = new Vortice.RawRectF(-w, -h, w, h);

                dc.DrawBitmap(
                    bitmap,
                    dest,
                    Math.Clamp(MathF.Abs(enemy.MagicCircleOpacity), 0f, 1f),
                    InterpolationMode.Linear,
                    null,
                    null);
                LastDrawCallCount++;
            }
        }
    }

    private void DrawEnemyImage(ID2D1DeviceContext6 dc, in EnemyRenderInfo enemy)
    {
        var sprite = sprites.Get(enemy.EnemySlot);
        if (sprite?.Bitmap is not { } bitmap) return;

        dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
        var transform =
            Matrix3x2.CreateScale(sprite.BaseRadius * enemy.EnemyScale) *
            Matrix3x2.CreateRotation(enemy.EnemyRotation * MathF.PI / 180f) *
            Matrix3x2.CreateTranslation(enemy.X, enemy.Y);
        dc.Transform = transform;

        var size = bitmap.Size;
        var half = MathF.Max(size.Width, size.Height) * 0.5f;
        var w = size.Width / half * 0.5f;
        var h = size.Height / half * 0.5f;
        var dest = new Vortice.RawRectF(-w, -h, w, h);

        dc.DrawBitmap(
            bitmap,
            dest,
            Math.Clamp(MathF.Abs(enemy.EnemyOpacity), 0f, 1f),
            InterpolationMode.Linear,
            null,
            null);

        LastDrawCallCount++;
        LastSpriteCount++;
    }

    private void DrawTarget(ID2D1DeviceContext6 dc, in TargetRenderInfo target)
    {
        if (!target.Enabled) return;

        // 1. 自機カスタム画像の描画
        if (target.HasCustomImage && MathF.Abs(target.Opacity) > 0.001f && MathF.Abs(target.Scale) > 0.001f)
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
                    Math.Clamp(MathF.Abs(target.Opacity), 0f, 1f),
                    InterpolationMode.Linear,
                    null,
                    null);

                LastDrawCallCount++;
                LastSpriteCount++;
            }
        }

        // 2. 当たり判定マーカー (喰らい判定サークル) の描画
        // 自機画像が未設定のときのみ描画する (キャラクター画像の上に赤い点が重なるのを防ぐ)
        var targetRadius = MathF.Abs(target.Radius);
        if (target.ShowMarker && !target.HasCustomImage && targetRadius > 0.5f)
        {
            dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
            dc.Transform = Matrix3x2.CreateTranslation(target.X, target.Y);

            // 外側の半透明赤塗り
            brush!.Color = new Color4(1f, 0.2f, 0.3f, 0.35f);
            dc.FillEllipse(new Ellipse(Vector2.Zero, targetRadius, targetRadius), brush);

            // 白い輪郭線
            brush.Color = new Color4(1f, 1f, 1f, 0.9f);
            dc.DrawEllipse(new Ellipse(Vector2.Zero, targetRadius, targetRadius), brush, 1.5f);

            // 中心の赤点
            var dotRadius = MathF.Min(3f, targetRadius * 0.35f);
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
        if (MathF.Abs(instance.Scale) <= 0.001f || instance.A <= 0.001f) return;

        for (var pass = 0; pass < glowPasses; pass++)
        {
            // 1 パス目は本体、2 パス目は「大きく薄い」滲み
            var isGlowPass = pass > 0;
            var scaleBoost = isGlowPass ? 1f + (float)Math.Clamp(glow - 1.0, 0.0, 2.0) * 0.6f : 1f;
            var alphaScale = isGlowPass ? (float)Math.Clamp(glow - 1.0, 0.0, 2.0) * 0.35f : 1f;
            if (alphaScale <= 0.001f) continue;

            var radius = sprite.BaseRadius * instance.Scale * scaleBoost;
            if (MathF.Abs(radius) <= 0.01f) continue;

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

    private void DrawBossHpBars(ID2D1DeviceContext6 dc, in BossHpBarRenderInfo info)
    {
        if (!info.Enabled || info.Opacity <= 0.001f) return;

        dc.Transform = Matrix3x2.Identity;

        var mainColor = info.HpRatio < 0.25f ? info.DangerColor : info.BarColor;
        var opacity = Math.Clamp(info.Opacity, 0f, 1f);

        var bgCol = new Color4(info.BackgroundColor.R, info.BackgroundColor.G, info.BackgroundColor.B, info.BackgroundColor.A * opacity);
        var lagCol = new Color4(info.DamageLagColor.R, info.DamageLagColor.G, info.DamageLagColor.B, info.DamageLagColor.A * opacity);
        var hpCol = new Color4(mainColor.R, mainColor.G, mainColor.B, mainColor.A * opacity);

        // 1. 円形ゲージ描画
        if (info.Style is HpBarStyle.CircularRing or HpBarStyle.Both)
        {
            var center = new Vector2(info.BossX, info.BossY);
            var r = MathF.Max(10f, info.Radius);
            var thick = MathF.Max(1f, info.Thickness);

            dc.PrimitiveBlend = PrimitiveBlend.SourceOver;

            // 背景リング (360度)
            DrawCircularHpArc(dc, center, r, 1.0f, bgCol, thick);

            // 被弾追従ラグバー (黄色/白)
            if (info.DamageLagRatio > info.HpRatio + 0.001f)
            {
                DrawCircularHpArc(dc, center, r, info.DamageLagRatio, lagCol, thick);
            }

            // メイン HP バー (緑/赤)
            DrawCircularHpArc(dc, center, r, info.HpRatio, hpCol, thick);

            // スペルカード (フェーズ) 区切り星/ドット
            if (info.PhaseCount > 1)
            {
                for (var p = 1; p < info.PhaseCount; p++)
                {
                    var phaseRatio = p / (float)info.PhaseCount;
                    var angle = -MathF.PI * 0.5f + MathF.PI * 2f * phaseRatio;
                    var dotPos = new Vector2(center.X + r * MathF.Cos(angle), center.Y + r * MathF.Sin(angle));
                    brush!.Color = new Color4(1f, 1f, 1f, 0.9f * opacity);
                    dc.FillEllipse(new Ellipse(dotPos, thick * 0.8f, thick * 0.8f), brush);
                    LastDrawCallCount++;
                }
            }

            // 加算発光グローパス
            if (info.Glow)
            {
                dc.PrimitiveBlend = PrimitiveBlend.Add;
                var glowCol = new Color4(hpCol.R, hpCol.G, hpCol.B, hpCol.A * 0.4f);
                DrawCircularHpArc(dc, center, r, info.HpRatio, glowCol, thick * 2.2f);
                dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
            }
        }

        // 2. 横長バー (TopBar または FloatingBar) 描画
        if (info.Style is HpBarStyle.TopBar or HpBarStyle.FloatingBar or HpBarStyle.Both)
        {
            var posX = info.Style == HpBarStyle.FloatingBar ? info.BossX : info.X;
            var posY = info.Style == HpBarStyle.FloatingBar ? (info.BossY - 60f) : info.Y;
            var width = MathF.Max(20f, info.Width);
            var height = MathF.Max(4f, info.Height);

            var halfW = width * 0.5f;
            var halfH = height * 0.5f;
            var left = posX - halfW;
            var top = posY - halfH;

            dc.PrimitiveBlend = PrimitiveBlend.SourceOver;

            // 背景枠
            brush!.Color = bgCol;
            var bgRect = new Vortice.RawRectF(left - 2f, top - 2f, left + width + 2f, top + height + 2f);
            dc.FillRoundedRectangle(new RoundedRectangle(bgRect, 4f, 4f), brush);
            brush.Color = new Color4(1f, 1f, 1f, 0.3f * opacity);
            dc.DrawRoundedRectangle(new RoundedRectangle(bgRect, 4f, 4f), brush, 1.5f);
            LastDrawCallCount += 2;

            // 被弾追従ラグバー
            if (info.DamageLagRatio > 0.001f)
            {
                var lagW = width * Math.Clamp(info.DamageLagRatio, 0f, 1f);
                brush.Color = lagCol;
                dc.FillRoundedRectangle(new RoundedRectangle(new Vortice.RawRectF(left, top, left + lagW, top + height), 3f, 3f), brush);
                LastDrawCallCount++;
            }

            // メイン HP バー
            if (info.HpRatio > 0.001f)
            {
                var hpW = width * Math.Clamp(info.HpRatio, 0f, 1f);
                brush.Color = hpCol;
                dc.FillRoundedRectangle(new RoundedRectangle(new Vortice.RawRectF(left, top, left + hpW, top + height), 3f, 3f), brush);
                LastDrawCallCount++;
            }

            // フェーズ区切り線
            if (info.PhaseCount > 1)
            {
                brush.Color = new Color4(1f, 1f, 1f, 0.7f * opacity);
                for (var p = 1; p < info.PhaseCount; p++)
                {
                    var notchX = left + width * (p / (float)info.PhaseCount);
                    dc.DrawLine(new Vector2(notchX, top - 2f), new Vector2(notchX, top + height + 2f), brush, 2f);
                    LastDrawCallCount++;
                }
            }

            // 加算発光グローパス
            if (info.Glow && info.HpRatio > 0.001f)
            {
                dc.PrimitiveBlend = PrimitiveBlend.Add;
                var glowCol = new Color4(hpCol.R, hpCol.G, hpCol.B, hpCol.A * 0.35f);
                brush.Color = glowCol;
                var hpW = width * Math.Clamp(info.HpRatio, 0f, 1f);
                dc.FillRoundedRectangle(new RoundedRectangle(new Vortice.RawRectF(left - 2f, top - 2f, left + hpW + 2f, top + height + 2f), 5f, 5f), brush);
                LastDrawCallCount++;
                dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
            }
        }
    }

    private void DrawCircularHpArc(ID2D1DeviceContext6 dc, Vector2 center, float radius, float ratio, Color4 color, float strokeWidth)
    {
        if (ratio <= 0.001f || radius <= 0f) return;
        var segments = Math.Clamp((int)(64 * ratio), 4, 64);
        var startAngle = -MathF.PI * 0.5f; // 上から時計回り
        var totalSweep = MathF.PI * 2f * Math.Clamp(ratio, 0f, 1f);

        using var path = dc.Factory.CreatePathGeometry();
        using (var sink = path.Open())
        {
            var p0 = new Vector2(center.X + radius * MathF.Cos(startAngle), center.Y + radius * MathF.Sin(startAngle));
            sink.BeginFigure(p0, FigureBegin.Hollow);
            for (var i = 1; i <= segments; i++)
            {
                var a = startAngle + totalSweep * (i / (float)segments);
                sink.AddLine(new Vector2(center.X + radius * MathF.Cos(a), center.Y + radius * MathF.Sin(a)));
            }
            sink.EndFigure(FigureEnd.Open);
            sink.Close();
        }

        brush!.Color = color;
        dc.DrawGeometry(path, brush, strokeWidth);
        LastDrawCallCount++;
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
