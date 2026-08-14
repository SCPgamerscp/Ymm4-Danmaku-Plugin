using Vortice.Mathematics;
using Ymm4DanmakuPlugin.Core.Model;

// Vortice と WPF の両方に Color 型があるため、曖昧参照を避けるためエイリアスを張る。
using Color = System.Windows.Media.Color;

namespace Ymm4DanmakuPlugin.Interop;

/// <summary>
/// WPF / Direct2D / コアエンジンの色型を相互変換する拡張メソッド。
/// <para>
/// 3 つの色型が登場するため、変換はすべてここに集約している。
/// ・<see cref="System.Windows.Media.Color"/> … YMM4 の <c>ColorPicker</c> が扱う型 (sRGB 8bit)<br/>
/// ・<see cref="BulletColor"/> … コアエンジン内部の型 (0〜1 の float RGBA)<br/>
/// ・<see cref="Color4"/> … Direct2D (Vortice) のブラシが扱う型 (0〜1 の float RGBA)
/// </para>
/// <para>
/// <b>ガンマについて:</b> YMM4 の描画パイプラインは D2D の既定どおり
/// 「sRGB の値をそのまま線形として扱う (=ガンマ変換しない)」動作のため、
/// ここでも 255 で割るだけの単純な変換を行う。
/// 独自にガンマ補正を掛けると YMM4 の他の図形と色が食い違うので行わない。
/// </para>
/// </summary>
public static class ColorExtensions
{
    /// <summary>WPF の色をコアエンジンの色へ変換する。</summary>
    public static BulletColor ToBulletColor(this Color color) => new(
        color.R / 255f,
        color.G / 255f,
        color.B / 255f,
        color.A / 255f);

    /// <summary>コアエンジンの色を WPF の色へ変換する。</summary>
    public static Color ToMediaColor(this BulletColor color) => Color.FromArgb(
        ToByte(color.A),
        ToByte(color.R),
        ToByte(color.G),
        ToByte(color.B));

    /// <summary>コアエンジンの色を Direct2D の色へ変換する。</summary>
    public static Color4 ToColor4(this BulletColor color) => new(color.R, color.G, color.B, color.A);

    /// <summary>WPF の色を Direct2D の色へ変換する。</summary>
    public static Color4 ToColor4(this Color color) => color.ToBulletColor().ToColor4();

    /// <summary>
    /// 描画用インスタンスの色を Direct2D の色へ変換する。
    /// アルファは <paramref name="alphaScale"/> 倍したうえで 0〜1 に収める。
    /// </summary>
    public static Color4 ToColor4(float r, float g, float b, float a, float alphaScale = 1f) => new(
        Math.Clamp(r, 0f, 1f),
        Math.Clamp(g, 0f, 1f),
        Math.Clamp(b, 0f, 1f),
        Math.Clamp(a * alphaScale, 0f, 1f));

    private static byte ToByte(float value) =>
        (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
}
