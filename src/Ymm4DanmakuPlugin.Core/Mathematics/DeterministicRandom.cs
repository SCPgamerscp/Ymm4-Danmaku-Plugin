using System.Runtime.CompilerServices;

namespace Ymm4DanmakuPlugin.Core.Mathematics;

/// <summary>
/// 決定論的疑似乱数生成器 (xoshiro256**)。
/// <para>
/// 動画編集ソフトではタイムライン上の任意フレームへシークされるため、
/// 同一シードから常に同一の弾幕が再現される必要がある。
/// <see cref="System.Random"/> は .NET のバージョンによって実装が変わりうるため、
/// 自前の固定アルゴリズムを用いる。
/// </para>
/// </summary>
public sealed class DeterministicRandom
{
    private ulong s0, s1, s2, s3;

    public int Seed { get; }

    public DeterministicRandom(int seed)
    {
        Seed = seed;
        Reset();
    }

    /// <summary>初期シード状態へ巻き戻す。</summary>
    public void Reset()
    {
        // SplitMix64 で 64bit シードを 256bit 状態へ拡張する
        var x = (ulong)(uint)Seed * 0x9E3779B97F4A7C15UL + 0xBF58476D1CE4E5B9UL;
        s0 = SplitMix64(ref x);
        s1 = SplitMix64(ref x);
        s2 = SplitMix64(ref x);
        s3 = SplitMix64(ref x);
        if ((s0 | s1 | s2 | s3) == 0) s0 = 0x9E3779B97F4A7C15UL;
    }

    private static ulong SplitMix64(ref ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        var z = x;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

    /// <summary>次の 64bit 乱数。</summary>
    public ulong NextUInt64()
    {
        var result = Rotl(s1 * 5, 7) * 9;
        var t = s1 << 17;

        s2 ^= s0;
        s3 ^= s1;
        s1 ^= s2;
        s0 ^= s3;
        s2 ^= t;
        s3 = Rotl(s3, 45);

        return result;
    }

    /// <summary>0.0 以上 1.0 未満の倍精度乱数。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>min 以上 max 未満の倍精度乱数。</summary>
    public double NextDouble(double min, double max) => min + NextDouble() * (max - min);

    /// <summary>-range 以上 +range 未満の倍精度乱数。</summary>
    public double NextSymmetric(double range) => NextDouble(-range, range);

    /// <summary>0 以上 maxExclusive 未満の整数。</summary>
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0) return 0;
        return (int)(NextUInt64() % (ulong)maxExclusive);
    }

    /// <summary>min 以上 maxExclusive 未満の整数。</summary>
    public int NextInt(int min, int maxExclusive) =>
        maxExclusive <= min ? min : min + NextInt(maxExclusive - min);

    /// <summary>確率 probability (0〜1) で true。</summary>
    public bool NextBool(double probability) => NextDouble() < probability;

    /// <summary>正規分布に近い乱数 (Box-Muller)。</summary>
    public double NextGaussian(double mean = 0.0, double stdDev = 1.0)
    {
        var u1 = 1.0 - NextDouble();
        var u2 = NextDouble();
        var normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(DanmakuMath.Tau * u2);
        return mean + stdDev * normal;
    }

    /// <summary>現在の内部状態を保存する (シーク時のスナップショット用)。</summary>
    public RandomState CaptureState() => new(s0, s1, s2, s3);

    /// <summary>保存した内部状態を復元する。</summary>
    public void RestoreState(in RandomState state)
    {
        s0 = state.S0;
        s1 = state.S1;
        s2 = state.S2;
        s3 = state.S3;
    }

    public readonly record struct RandomState(ulong S0, ulong S1, ulong S2, ulong S3);
}
