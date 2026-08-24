using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using Vortice.WIC;
using YukkuriMovieMaker.Commons;
using Ymm4DanmakuPlugin.Parameters;

namespace Ymm4DanmakuPlugin.Rendering;

/// <summary>
/// 1 スロットぶんのスプライト。組み込み形状 (ジオメトリ) か、ユーザー指定画像 (ビットマップ) のいずれか。
/// </summary>
/// <param name="Geometry">組み込み形状のジオメトリ。画像スロットでは null。</param>
/// <param name="Bitmap">ユーザー指定画像。組み込み形状では null。</param>
/// <param name="BaseRadius">スケール 1.0 のときの半径 (px)。</param>
public sealed record BulletSprite(ID2D1Geometry? Geometry, ID2D1Bitmap1? Bitmap, float BaseRadius)
{
    /// <summary>画像スプライトかどうか。</summary>
    public bool IsBitmap => Bitmap is not null;
}

/// <summary>
/// 弾のスプライト (組み込み形状のジオメトリ / ユーザー画像) を生成・キャッシュする。
/// <para>
/// ジオメトリは <b>原点中心・半径 1.0 の正規化座標</b>で作る。
/// 描画側は「<see cref="BulletSprite.BaseRadius"/> × 弾のスケール」を掛けた
/// 変換行列を設定するだけでよい。
/// </para>
/// <para>
/// 進行方向を向く弾 (米弾・矢弾など) は <b>+X 方向が前</b>となるように作る。
/// これはコアエンジンの角度定義 (0 度 = 右) と一致している。
/// </para>
/// </summary>
public sealed class BulletSpriteLibrary : IDisposable
{
    private readonly IGraphicsDevicesAndContext devices;
    private readonly DisposeCollector disposer = new();
    private readonly BulletSprite?[] sprites = new BulletSprite?[SpriteSlots.Capacity];

    /// <summary>画像スロットに現在読み込まれているファイルパス (再読み込みの判定に使用)。</summary>
    private readonly string?[] loadedImagePaths = new string?[SpriteSlots.Capacity];

    private ID2D1Factory? factory;
    private IWICImagingFactory? wic;

    public BulletSpriteLibrary(IGraphicsDevicesAndContext devices)
    {
        this.devices = devices;
    }

    /// <summary>画像の読み込みに失敗したファイルの一覧 (UI への警告表示用)。</summary>
    public IReadOnlyList<string> LoadErrors => loadErrors;
    private readonly List<string> loadErrors = [];

    /// <summary>スロット番号に対応するスプライトを取得する。組み込み形状は初回アクセス時に生成する。</summary>
    public BulletSprite? Get(int slot)
    {
        if (sprites[slot] is { } cached) return cached;

        if (slot == SpriteSlots.BuiltInMagicCircleSlot)
        {
            var created = CreateBuiltInMagicCircle();
            sprites[slot] = created;
            return created;
        }

        // 組み込み形状の範囲なら生成する。画像スロットは SetCustomImage 経由でのみ用意される。
        if (slot < SpriteSlots.BuiltInCount)
        {
            var created = CreateBuiltIn((BulletShape)slot);
            sprites[slot] = created;
            return created;
        }

        return null;
    }

    /// <summary>
    /// 画像スロットへユーザー指定画像を読み込む。
    /// 同じパスが既に読み込まれている場合は何もしない (毎フレーム呼ばれても安全)。
    /// </summary>
    /// <returns>スロットに有効な画像があるかどうか。</returns>
    public bool SetCustomImage(int slot, string? path)
    {
        if (slot < SpriteSlots.CustomBase || slot >= sprites.Length) return false;

        var normalized = string.IsNullOrWhiteSpace(path) ? null : path;

        if (string.Equals(loadedImagePaths[slot], normalized, StringComparison.Ordinal))
            return sprites[slot] is not null;

        // 旧画像を破棄する
        var oldBitmap = sprites[slot]?.Bitmap;
        if (oldBitmap is not null) disposer.RemoveAndDispose(ref oldBitmap);
        sprites[slot] = null;
        loadedImagePaths[slot] = normalized;

        if (normalized is null) return false;

        var bitmap = TryLoadBitmap(normalized);
        if (bitmap is null) return false;

        var size = bitmap.Size;
        // 画像は「長辺の半分」を半径とみなす。半径 1.0 の正規化座標に合わせるための基準値。
        var radius = MathF.Max(1f, MathF.Max(size.Width, size.Height) * 0.5f);
        sprites[slot] = new BulletSprite(null, bitmap, radius);
        return true;
    }

    private ID2D1Bitmap1? TryLoadBitmap(string path)
    {
        if (!File.Exists(path))
        {
            AddLoadError($"画像が見つかりません: {path}");
            return null;
        }

        try
        {
            wic ??= new IWICImagingFactory();

            using var decoder = wic.CreateDecoderFromFileName(path, FileAccess.Read, DecodeOptions.CacheOnLoad);
            using var frame = decoder.GetFrame(0);
            using var converter = wic.CreateFormatConverter();
            converter.Initialize(frame, PixelFormat.Format32bppPBGRA);

            var bitmap = devices.DeviceContext.CreateBitmapFromWicBitmap(converter, null);
            disposer.Collect(bitmap);
            return bitmap;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SharpGen.Runtime.SharpGenException)
        {
            AddLoadError($"画像を読み込めません: {Path.GetFileName(path)} ({e.Message})");
            return null;
        }
    }

    private void AddLoadError(string message)
    {
        if (loadErrors.Contains(message)) return;
        if (loadErrors.Count > 16) return;
        loadErrors.Add(message);
    }

    // =====================================================================
    // 組み込み形状
    // =====================================================================

    /// <summary>スケール 1.0 における各形状の半径 (px)。東方風の弾サイズ感に寄せている。</summary>
    private static float BaseRadiusOf(BulletShape shape) => shape switch
    {
        BulletShape.Circle => 8f,
        BulletShape.Medium => 13f,
        BulletShape.Large => 22f,
        BulletShape.Rice => 9f,
        BulletShape.Card => 12f,
        BulletShape.Scale => 10f,
        BulletShape.Star => 12f,
        BulletShape.Ring => 14f,
        BulletShape.Glow => 16f,
        BulletShape.Arrow => 11f,
        BulletShape.Butterfly => 14f,
        BulletShape.Particle => 4f,
        _ => 8f,
    };

    private BulletSprite CreateBuiltIn(BulletShape shape)
    {
        var f = GetFactory();
        var radius = BaseRadiusOf(shape);

        ID2D1Geometry geometry = shape switch
        {
            BulletShape.Rice => Collect(f.CreateEllipseGeometry(new Ellipse(Vector2.Zero, 1f, 0.42f))),
            BulletShape.Card => CreateCard(f),
            BulletShape.Scale => CreateScale(f),
            BulletShape.Star => CreateStar(f, points: 5, innerRatio: 0.45f),
            BulletShape.Ring => CreateRing(f, innerRatio: 0.58f),
            BulletShape.Arrow => CreateArrow(f),
            BulletShape.Butterfly => CreateButterfly(f),

            // 丸系 (丸弾 / 中弾 / 大玉 / 光弾 / 粒) は同じ真円で、半径だけを変える
            _ => Collect(f.CreateEllipseGeometry(new Ellipse(Vector2.Zero, 1f, 1f))),
        };

        return new BulletSprite(geometry, null, radius);
    }

    /// <summary>札弾: 角の丸い縦長の板。進行方向 (+X) に長い。</summary>
    private ID2D1Geometry CreateCard(ID2D1Factory f)
    {
        var rect = new RoundedRectangle(new Vortice.Mathematics.Rect(-1f, -0.55f, 2f, 1.1f), 0.35f, 0.35f);
        return Collect(f.CreateRoundedRectangleGeometry(rect));
    }

    /// <summary>鱗弾: 先端が尖り後方が丸い水滴形。</summary>
    private ID2D1Geometry CreateScale(ID2D1Factory f)
    {
        var geometry = f.CreatePathGeometry();
        using (var sink = geometry.Open())
        {
            sink.SetFillMode(FillMode.Winding);
            sink.BeginFigure(new Vector2(1f, 0f), FigureBegin.Filled);
            sink.AddBezier(new BezierSegment(
                new Vector2(0.2f, 0.75f),
                new Vector2(-0.85f, 0.7f),
                new Vector2(-1f, 0f)));
            sink.AddBezier(new BezierSegment(
                new Vector2(-0.85f, -0.7f),
                new Vector2(0.2f, -0.75f),
                new Vector2(1f, 0f)));
            sink.EndFigure(FigureEnd.Closed);
            sink.Close();
        }

        return Collect(geometry);
    }

    /// <summary>星弾。</summary>
    private ID2D1Geometry CreateStar(ID2D1Factory f, int points, float innerRatio)
    {
        var vertices = new Vector2[points * 2];
        // 先端を +X に向ける (進行方向を向く設定と整合させる)
        for (var i = 0; i < vertices.Length; i++)
        {
            var angle = MathF.PI * i / points;
            var r = (i % 2 == 0) ? 1f : innerRatio;
            vertices[i] = new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r);
        }

        return CreatePolygon(f, vertices);
    }

    /// <summary>矢弾: 進行方向 (+X) に尖った鏃形。</summary>
    private ID2D1Geometry CreateArrow(ID2D1Factory f) => CreatePolygon(f,
    [
        new Vector2(1f, 0f),
        new Vector2(-0.45f, 0.8f),
        new Vector2(-0.15f, 0f),
        new Vector2(-0.45f, -0.8f),
    ]);

    /// <summary>
    /// 輪弾: 外円と内円のジオメトリグループ。
    /// <see cref="FillMode.Alternate"/> により内側が抜けてドーナツ状になる。
    /// </summary>
    private ID2D1Geometry CreateRing(ID2D1Factory f, float innerRatio)
    {
        var outer = Collect(f.CreateEllipseGeometry(new Ellipse(Vector2.Zero, 1f, 1f)));
        var inner = Collect(f.CreateEllipseGeometry(new Ellipse(Vector2.Zero, innerRatio, innerRatio)));
        return Collect(f.CreateGeometryGroup(FillMode.Alternate, [outer, inner]));
    }

    /// <summary>蝶弾: 4 枚の羽を持つ蝶のシルエット。</summary>
    private ID2D1Geometry CreateButterfly(ID2D1Factory f)
    {
        var geometry = f.CreatePathGeometry();
        using (var sink = geometry.Open())
        {
            sink.SetFillMode(FillMode.Winding);

            // 上の羽 (前羽 → 後羽) を一続きに描き、下側はそれを反転して描く
            sink.BeginFigure(new Vector2(0f, 0f), FigureBegin.Filled);
            sink.AddBezier(new BezierSegment(
                new Vector2(0.55f, -0.95f),
                new Vector2(1.0f, -0.55f),
                new Vector2(0.62f, -0.12f)));
            sink.AddBezier(new BezierSegment(
                new Vector2(0.95f, 0.05f),
                new Vector2(0.72f, 0.85f),
                new Vector2(0.1f, 0.32f)));
            sink.AddBezier(new BezierSegment(
                new Vector2(-0.2f, 0.9f),
                new Vector2(-0.95f, 0.55f),
                new Vector2(-0.6f, 0.1f)));
            sink.AddBezier(new BezierSegment(
                new Vector2(-1.0f, -0.1f),
                new Vector2(-0.6f, -0.9f),
                new Vector2(0f, 0f)));
            sink.EndFigure(FigureEnd.Closed);

            // 胴体
            sink.BeginFigure(new Vector2(0.18f, 0f), FigureBegin.Filled);
            sink.AddBezier(new BezierSegment(
                new Vector2(0.1f, 0.16f),
                new Vector2(-0.1f, 0.16f),
                new Vector2(-0.2f, 0f)));
            sink.AddBezier(new BezierSegment(
                new Vector2(-0.1f, -0.16f),
                new Vector2(0.1f, -0.16f),
                new Vector2(0.18f, 0f)));
            sink.EndFigure(FigureEnd.Closed);

            sink.Close();
        }

        return Collect(geometry);
    }

    private ID2D1Geometry CreatePolygon(ID2D1Factory f, Vector2[] vertices)
    {
        var geometry = f.CreatePathGeometry();
        using (var sink = geometry.Open())
        {
            sink.SetFillMode(FillMode.Winding);
            sink.BeginFigure(vertices[0], FigureBegin.Filled);
            sink.AddLines(vertices[1..]);
            sink.EndFigure(FigureEnd.Closed);
            sink.Close();
        }

        return Collect(geometry);
    }

    /// <summary>東方風の幾何学魔法陣スプライト (二重同心円・八芒星・ルーン目盛り・ダイヤ) を生成する。</summary>
    private BulletSprite CreateBuiltInMagicCircle()
    {
        var f = GetFactory();
        var geometries = new List<ID2D1Geometry>();

        // 1. 同心円 (外周リング)
        geometries.Add(f.CreateEllipseGeometry(new Ellipse(Vector2.Zero, 1f, 1f)));
        geometries.Add(f.CreateEllipseGeometry(new Ellipse(Vector2.Zero, 0.88f, 0.88f)));

        // 2. 中間リング & 内周リング
        geometries.Add(f.CreateEllipseGeometry(new Ellipse(Vector2.Zero, 0.55f, 0.55f)));
        geometries.Add(f.CreateEllipseGeometry(new Ellipse(Vector2.Zero, 0.25f, 0.25f)));

        // 3. 八芒星 (2つの正方形)
        var starVertices1 = new Vector2[5];
        var starVertices2 = new Vector2[5];
        for (var i = 0; i < 4; i++)
        {
            var angle1 = MathF.PI * 0.5f * i;
            var angle2 = MathF.PI * 0.5f * i + MathF.PI * 0.25f;
            starVertices1[i] = new Vector2(MathF.Cos(angle1) * 0.88f, MathF.Sin(angle1) * 0.88f);
            starVertices2[i] = new Vector2(MathF.Cos(angle2) * 0.88f, MathF.Sin(angle2) * 0.88f);
        }
        starVertices1[4] = starVertices1[0];
        starVertices2[4] = starVertices2[0];
        geometries.Add(CreatePolyline(f, starVertices1));
        geometries.Add(CreatePolyline(f, starVertices2));

        // 4. 外周の放射状ルーン目盛り (16 本)
        var tickPath = f.CreatePathGeometry();
        using (var sink = tickPath.Open())
        {
            for (var i = 0; i < 16; i++)
            {
                var angle = MathF.PI * 2f * i / 16f;
                var cos = MathF.Cos(angle);
                var sin = MathF.Sin(angle);
                sink.BeginFigure(new Vector2(cos * 0.88f, sin * 0.88f), FigureBegin.Hollow);
                sink.AddLine(new Vector2(cos * 1.0f, sin * 1.0f));
                sink.EndFigure(FigureEnd.Open);
            }
            sink.Close();
        }
        geometries.Add(tickPath);

        // 5. 中心の四芒星 / ダイヤ
        var diamondVertices = new Vector2[5];
        for (var i = 0; i < 4; i++)
        {
            var angle = MathF.PI * 0.5f * i;
            var r = (i % 2 == 0) ? 0.45f : 0.15f;
            diamondVertices[i] = new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r);
        }
        diamondVertices[4] = diamondVertices[0];
        geometries.Add(CreatePolyline(f, diamondVertices));

        foreach (var g in geometries) disposer.Collect(g);

        var group = Collect(f.CreateGeometryGroup(FillMode.Winding, geometries.ToArray()));
        return new BulletSprite(group, null, 100f);
    }

    private ID2D1Geometry CreatePolyline(ID2D1Factory f, Vector2[] vertices)
    {
        var geometry = f.CreatePathGeometry();
        using (var sink = geometry.Open())
        {
            sink.SetFillMode(FillMode.Winding);
            sink.BeginFigure(vertices[0], FigureBegin.Hollow);
            sink.AddLines(vertices[1..]);
            sink.EndFigure(FigureEnd.Closed);
            sink.Close();
        }
        return Collect(geometry);
    }

    private ID2D1Factory GetFactory() => factory ??= Collect(devices.DeviceContext.Factory);

    private T Collect<T>(T disposable) where T : IDisposable
    {
        disposer.Collect(disposable);
        return disposable;
    }

    public void Dispose()
    {
        Array.Clear(sprites);
        Array.Clear(loadedImagePaths);
        loadErrors.Clear();
        wic?.Dispose();
        wic = null;
        factory = null;
        disposer.DisposeAndClear();
    }
}
