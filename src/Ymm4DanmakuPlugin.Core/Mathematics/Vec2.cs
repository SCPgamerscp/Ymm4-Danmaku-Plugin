using System.Globalization;
using System.Runtime.CompilerServices;

namespace Ymm4DanmakuPlugin.Core.Mathematics;

/// <summary>
/// 弾幕計算用の 2 次元ベクトル。
/// System.Numerics.Vector2 (float) ではなく double を用いることで、
/// 長時間シミュレーション時の座標ドリフトを抑える。
/// </summary>
public readonly struct Vec2 : IEquatable<Vec2>
{
    public readonly double X;
    public readonly double Y;

    public static readonly Vec2 Zero = new(0, 0);
    public static readonly Vec2 UnitX = new(1, 0);
    public static readonly Vec2 UnitY = new(0, 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec2(double x, double y)
    {
        X = x;
        Y = y;
    }

    /// <summary>角度(度)と大きさから生成する。0度=右方向、時計回りが正(画面座標系: Y下向き)。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 FromDegrees(double degrees, double length = 1.0)
    {
        var rad = degrees * DanmakuMath.Deg2Rad;
        return new Vec2(Math.Cos(rad) * length, Math.Sin(rad) * length);
    }

    public double Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Math.Sqrt(X * X + Y * Y);
    }

    public double LengthSquared
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => X * X + Y * Y;
    }

    /// <summary>ベクトルの向き(度)。</summary>
    public double Degrees
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Math.Atan2(Y, X) * DanmakuMath.Rad2Deg;
    }

    public Vec2 Normalized
    {
        get
        {
            var len = Length;
            return len <= double.Epsilon ? Zero : new Vec2(X / len, Y / len);
        }
    }

    /// <summary>指定した長さに丸めたベクトルを返す。</summary>
    public Vec2 WithLength(double length)
    {
        var cur = Length;
        return cur <= double.Epsilon ? new Vec2(length, 0) : new Vec2(X / cur * length, Y / cur * length);
    }

    /// <summary>反時計/時計回りに回転させる(度)。</summary>
    public Vec2 Rotate(double degrees)
    {
        var rad = degrees * DanmakuMath.Deg2Rad;
        var c = Math.Cos(rad);
        var s = Math.Sin(rad);
        return new Vec2(X * c - Y * s, X * s + Y * c);
    }

    public double Dot(Vec2 other) => X * other.X + Y * other.Y;

    public double Cross(Vec2 other) => X * other.Y - Y * other.X;

    public double DistanceTo(Vec2 other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public double DistanceSquaredTo(Vec2 other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return dx * dx + dy * dy;
    }

    public static Vec2 Lerp(Vec2 a, Vec2 b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public static Vec2 operator *(double s, Vec2 a) => new(a.X * s, a.Y * s);
    public static Vec2 operator /(Vec2 a, double s) => new(a.X / s, a.Y / s);

    public static bool operator ==(Vec2 a, Vec2 b) => a.Equals(b);
    public static bool operator !=(Vec2 a, Vec2 b) => !a.Equals(b);

    public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
    public override bool Equals(object? obj) => obj is Vec2 v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(X, Y);

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"({X:F3}, {Y:F3})");
}
