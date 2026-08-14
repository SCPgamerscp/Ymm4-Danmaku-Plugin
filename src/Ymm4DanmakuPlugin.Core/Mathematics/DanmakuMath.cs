using System.Runtime.CompilerServices;

namespace Ymm4DanmakuPlugin.Core.Mathematics;

/// <summary>弾幕計算で多用する数学ユーティリティ。</summary>
public static class DanmakuMath
{
    public const double Deg2Rad = Math.PI / 180.0;
    public const double Rad2Deg = 180.0 / Math.PI;
    public const double Tau = Math.PI * 2.0;

    /// <summary>黄金角。花弁状 (ロゼッタ) 弾幕で自然な分布を得るために使う。</summary>
    public const double GoldenAngleDegrees = 137.50776405003785;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Clamp(double value, double min, double max) =>
        value < min ? min : value > max ? max : value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Clamp(int value, int min, int max) =>
        value < min ? min : value > max ? max : value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Lerp(double a, double b, double t) => a + (b - a) * t;

    /// <summary>0〜1 に正規化する。max==min の場合は 0。</summary>
    public static double Normalize01(double value, double min, double max)
    {
        var range = max - min;
        return Math.Abs(range) < 1e-12 ? 0.0 : Clamp((value - min) / range, 0.0, 1.0);
    }

    /// <summary>角度を -180〜180 の範囲へ正規化する。</summary>
    public static double NormalizeAngle(double degrees)
    {
        degrees %= 360.0;
        if (degrees > 180.0) degrees -= 360.0;
        else if (degrees < -180.0) degrees += 360.0;
        return degrees;
    }

    /// <summary>角度を 0〜360 の範囲へ正規化する。</summary>
    public static double NormalizeAngle360(double degrees)
    {
        degrees %= 360.0;
        if (degrees < 0) degrees += 360.0;
        return degrees;
    }

    /// <summary>from から to への最短角度差 (-180〜180)。</summary>
    public static double DeltaAngle(double from, double to) => NormalizeAngle(to - from);

    /// <summary>maxDelta を上限として from を to に近づける (度)。</summary>
    public static double MoveTowardsAngle(double from, double to, double maxDelta)
    {
        var delta = DeltaAngle(from, to);
        if (Math.Abs(delta) <= maxDelta) return NormalizeAngle(to);
        return NormalizeAngle(from + Math.Sign(delta) * maxDelta);
    }

    /// <summary>イージング: ease-in-out (3次)。</summary>
    public static double SmoothStep(double t)
    {
        t = Clamp(t, 0, 1);
        return t * t * (3.0 - 2.0 * t);
    }

    /// <summary>半音単位のピッチ比率を返す (12平均律)。</summary>
    public static double SemitoneToRatio(double semitones) => Math.Pow(2.0, semitones / 12.0);
}
