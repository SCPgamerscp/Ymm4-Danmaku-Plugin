namespace Ymm4DanmakuPlugin.Core.Model;

/// <summary>0〜1 の線形 RGBA カラー。</summary>
public readonly record struct BulletColor(float R, float G, float B, float A)
{
    public static readonly BulletColor White = new(1f, 1f, 1f, 1f);

    public BulletColor WithAlpha(float alpha) => this with { A = alpha };

    public BulletColor MultiplyAlpha(float factor) => this with { A = A * factor };

    public static BulletColor Lerp(BulletColor a, BulletColor b, float t)
    {
        t = t < 0 ? 0 : t > 1 ? 1 : t;
        return new BulletColor(
            a.R + (b.R - a.R) * t,
            a.G + (b.G - a.G) * t,
            a.B + (b.B - a.B) * t,
            a.A + (b.A - a.A) * t);
    }

    /// <summary>#AARRGGBB / #RRGGBB 形式の文字列から生成する。</summary>
    public static BulletColor FromHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return White;
        var s = hex.Trim().TrimStart('#');
        try
        {
            return s.Length switch
            {
                6 => new BulletColor(
                    Convert.ToInt32(s.Substring(0, 2), 16) / 255f,
                    Convert.ToInt32(s.Substring(2, 2), 16) / 255f,
                    Convert.ToInt32(s.Substring(4, 2), 16) / 255f,
                    1f),
                8 => new BulletColor(
                    Convert.ToInt32(s.Substring(2, 2), 16) / 255f,
                    Convert.ToInt32(s.Substring(4, 2), 16) / 255f,
                    Convert.ToInt32(s.Substring(6, 2), 16) / 255f,
                    Convert.ToInt32(s.Substring(0, 2), 16) / 255f),
                _ => White,
            };
        }
        catch (Exception e) when (e is FormatException or ArgumentOutOfRangeException or OverflowException)
        {
            return White;
        }
    }

    /// <summary>HSV から生成する。h: 0〜360、s/v: 0〜1。</summary>
    public static BulletColor FromHsv(double h, double s, double v, double a = 1.0)
    {
        h = ((h % 360) + 360) % 360;
        s = s < 0 ? 0 : s > 1 ? 1 : s;
        v = v < 0 ? 0 : v > 1 ? 1 : v;

        var c = v * s;
        var x = c * (1 - Math.Abs((h / 60.0 % 2) - 1));
        var m = v - c;

        var (r, g, b) = (int)(h / 60) switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };

        return new BulletColor((float)(r + m), (float)(g + m), (float)(b + m), (float)a);
    }

    /// <summary>東方風の代表的な弾色パレット (色相を段階的にずらしたもの)。</summary>
    public static BulletColor FromPaletteIndex(int index, double saturation = 0.85, double value = 1.0)
    {
        // 赤→橙→黄→緑→水→青→紫→桃 の 8 色相
        const int steps = 8;
        var i = ((index % steps) + steps) % steps;
        var hue = i * (360.0 / steps);
        return FromHsv(hue, saturation, value);
    }
}
