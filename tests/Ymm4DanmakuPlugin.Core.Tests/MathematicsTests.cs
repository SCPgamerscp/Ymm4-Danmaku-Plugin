using Ymm4DanmakuPlugin.Core.Mathematics;

namespace Ymm4DanmakuPlugin.Core.Tests;

public class Vec2Tests
{
    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(90, 0, 1)]
    [InlineData(180, -1, 0)]
    [InlineData(-90, 0, -1)]
    public void FromDegrees_単位ベクトルを生成する(double degrees, double expectedX, double expectedY)
    {
        var v = Vec2.FromDegrees(degrees);
        Assert.Equal(expectedX, v.X, 6);
        Assert.Equal(expectedY, v.Y, 6);
    }

    [Fact]
    public void Degrees_は元の角度を復元する()
    {
        for (var angle = -180.0; angle < 180.0; angle += 7.5)
        {
            var v = Vec2.FromDegrees(angle, 3.5);
            Assert.Equal(angle, v.Degrees, 6);
            Assert.Equal(3.5, v.Length, 6);
        }
    }

    [Fact]
    public void Rotate_は長さを保つ()
    {
        var v = new Vec2(3, 4);
        var rotated = v.Rotate(37);
        Assert.Equal(v.Length, rotated.Length, 9);
    }

    [Fact]
    public void WithLength_は指定した長さになる()
    {
        var v = new Vec2(3, 4).WithLength(10);
        Assert.Equal(10, v.Length, 9);
    }

    [Fact]
    public void ゼロベクトルの正規化は例外を投げない()
    {
        Assert.Equal(Vec2.Zero, Vec2.Zero.Normalized);
    }
}

public class DanmakuMathTests
{
    [Theory]
    [InlineData(370, 10)]
    [InlineData(-370, -10)]
    [InlineData(180, 180)]
    [InlineData(-180, -180)]
    public void NormalizeAngle_は範囲内に収める(double input, double expected)
    {
        Assert.Equal(expected, DanmakuMath.NormalizeAngle(input), 6);
    }

    [Fact]
    public void DeltaAngle_は最短方向を返す()
    {
        Assert.Equal(20, DanmakuMath.DeltaAngle(350, 10), 6);
        Assert.Equal(-20, DanmakuMath.DeltaAngle(10, 350), 6);
    }

    [Fact]
    public void MoveTowardsAngle_は上限を超えない()
    {
        var result = DanmakuMath.MoveTowardsAngle(0, 90, 10);
        Assert.Equal(10, result, 6);
    }

    [Fact]
    public void MoveTowardsAngle_は目標を追い越さない()
    {
        var result = DanmakuMath.MoveTowardsAngle(85, 90, 10);
        Assert.Equal(90, result, 6);
    }

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(12, 2.0)]
    [InlineData(-12, 0.5)]
    public void SemitoneToRatio_は12平均律に従う(double semitones, double expected)
    {
        Assert.Equal(expected, DanmakuMath.SemitoneToRatio(semitones), 9);
    }
}

public class DeterministicRandomTests
{
    [Fact]
    public void 同一シードなら同一系列になる()
    {
        var a = new DeterministicRandom(1234);
        var b = new DeterministicRandom(1234);

        for (var i = 0; i < 1000; i++)
            Assert.Equal(a.NextDouble(), b.NextDouble());
    }

    [Fact]
    public void 異なるシードなら異なる系列になる()
    {
        var a = new DeterministicRandom(1);
        var b = new DeterministicRandom(2);

        var differences = 0;
        for (var i = 0; i < 100; i++)
        {
            if (Math.Abs(a.NextDouble() - b.NextDouble()) > 1e-12) differences++;
        }

        Assert.True(differences > 90, $"系列がほぼ同一です (差分 {differences}/100)");
    }

    [Fact]
    public void Reset_で系列が巻き戻る()
    {
        var random = new DeterministicRandom(999);
        var first = Enumerable.Range(0, 50).Select(_ => random.NextDouble()).ToArray();

        random.Reset();
        var second = Enumerable.Range(0, 50).Select(_ => random.NextDouble()).ToArray();

        Assert.Equal(first, second);
    }

    [Fact]
    public void CaptureState_と_RestoreState_で状態を復元できる()
    {
        var random = new DeterministicRandom(42);
        random.NextDouble();

        var state = random.CaptureState();
        var expected = Enumerable.Range(0, 20).Select(_ => random.NextDouble()).ToArray();

        random.RestoreState(state);
        var actual = Enumerable.Range(0, 20).Select(_ => random.NextDouble()).ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NextDouble_は0以上1未満()
    {
        var random = new DeterministicRandom(7);
        for (var i = 0; i < 10000; i++)
        {
            var value = random.NextDouble();
            Assert.InRange(value, 0.0, 0.9999999999999999);
        }
    }

    [Fact]
    public void NextInt_は範囲内に収まる()
    {
        var random = new DeterministicRandom(7);
        for (var i = 0; i < 10000; i++)
            Assert.InRange(random.NextInt(5, 10), 5, 9);
    }
}
