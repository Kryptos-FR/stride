// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Xunit;

namespace Stride.Core.Mathematics.Tests;

public class TestMathUtil
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(90, MathUtil.PiOverTwo)]
    [InlineData(180, MathUtil.Pi)]
    [InlineData(360, MathUtil.TwoPi)]
    [InlineData(45, MathUtil.PiOverFour)]
    public void TestDegreesToRadians(float degrees, float expectedRadians)
    {
        var radians = MathUtil.DegreesToRadians(degrees);
        Assert.Equal(expectedRadians, radians, 5);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(MathUtil.PiOverTwo, 90)]
    [InlineData(MathUtil.Pi, 180)]
    [InlineData(MathUtil.TwoPi, 360)]
    [InlineData(MathUtil.PiOverFour, 45)]
    public void TestRadiansToDegrees(float radians, float expectedDegrees)
    {
        var degrees = MathUtil.RadiansToDegrees(radians);
        Assert.Equal(expectedDegrees, degrees, 5);
    }

    [Theory]
    [InlineData(0, -1, 1, 0)]
    [InlineData(0.5f, 0, 1, 0.5f)]
    [InlineData(-0.5f, 0, 1, 0)]
    [InlineData(1.5f, 0, 1, 1)]
    [InlineData(0.5f, 0, 0, 0)]
    public void TestClamp(float value, float min, float max, float expected)
    {
        Assert.Equal(expected, MathUtil.Clamp(value, min, max));
    }

    [Theory]
    [InlineData(0, 1, 0, 0)]
    [InlineData(0, 1, 0.5f, 0.5f)]
    [InlineData(0, 1, 1, 1)]
    [InlineData(10, 20, 0.5f, 15)]
    public void TestLerp(float start, float end, float amount, float expected)
    {
        Assert.Equal(expected, MathUtil.Lerp(start, end, amount), 5);
    }

    [Theory]
    [InlineData(5, 0, 10, 0.5f)]
    [InlineData(0, 0, 10, 0)]
    [InlineData(10, 0, 10, 1)]
    [InlineData(15, 0, 10, 1.5f)]
    public void TestInverseLerp(float value, float start, float end, float expected)
    {
        Assert.Equal(expected, MathUtil.InverseLerp(start, end, value), 5);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(MathUtil.ZeroTolerance * 0.5f, true)]
    [InlineData(MathUtil.ZeroTolerance * 2, false)]
    [InlineData(-MathUtil.ZeroTolerance * 0.5f, true)]
    public void TestIsZero(float value, bool expected)
    {
        Assert.Equal(expected, MathUtil.IsZero(value));
    }

    [Theory]
    [InlineData(1.0f, true)]
    [InlineData(1.0f + MathUtil.ZeroTolerance * 0.5f, true)]
    [InlineData(1.0f - MathUtil.ZeroTolerance * 0.5f, true)]
    [InlineData(1.1f, false)]
    public void TestIsOne(float value, bool expected)
    {
        Assert.Equal(expected, MathUtil.IsOne(value));
    }

    [Theory]
    [InlineData(0, 0, 0, true)]
    [InlineData(1, 1, 0.0001f, true)]
    [InlineData(1, 1.1f, 0.0001f, false)]
    [InlineData(1, 1.00001f, 0.0001f, true)]
    public void TestWithinEpsilon(float a, float b, float epsilon, bool expected)
    {
        Assert.Equal(expected, MathUtil.WithinEpsilon(a, b, epsilon));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(1, 1)]
    [InlineData(0.25f, 0.15625f)]
    public void TestSmoothStep(float amount, float expected)
    {
        var result = MathUtil.SmoothStep(amount);
        Assert.Equal(expected, result, 5);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(1, 1)]
    public void TestSmootherStep(float amount, float expected)
    {
        var result = MathUtil.SmootherStep(amount);
        Assert.Equal(expected, result, 5);
    }

    [Theory]
    [InlineData(1.0f, 1.0f, true)]
    [InlineData(1.0f, 1.0000001f, true)]
    [InlineData(1.0f, 2.0f, false)]
    [InlineData(0.0f, 0.0f, true)]
    public void TestNearEqual(float a, float b, bool expected)
    {
        Assert.Equal(expected, MathUtil.NearEqual(a, b));
    }

    [Theory]
    [InlineData(45, 0.125f)]
    [InlineData(90, 0.25f)]
    [InlineData(180, 0.5f)]
    [InlineData(360, 1.0f)]
    public void TestDegreesToRevolutions(float degrees, float expected)
    {
        Assert.Equal(expected, MathUtil.DegreesToRevolutions(degrees), 5);
    }

    [Theory]
    [InlineData(0.125f, 45)]
    [InlineData(0.25f, 90)]
    [InlineData(0.5f, 180)]
    [InlineData(1.0f, 360)]
    public void TestRevolutionsToDegrees(float revolutions, float expected)
    {
        Assert.Equal(expected, MathUtil.RevolutionsToDegrees(revolutions), 5);
    }

    [Theory]
    [InlineData(MathUtil.PiOverFour, 50)]
    [InlineData(MathUtil.PiOverTwo, 100)]
    [InlineData(MathUtil.Pi, 200)]
    public void TestRadiansToGradians(float radians, float expectedGradians)
    {
        Assert.Equal(expectedGradians, MathUtil.RadiansToGradians(radians), 5);
    }

    [Theory]
    [InlineData(50, MathUtil.PiOverFour)]
    [InlineData(100, MathUtil.PiOverTwo)]
    [InlineData(200, MathUtil.Pi)]
    public void TestGradiansToRadians(float gradians, float expectedRadians)
    {
        Assert.Equal(expectedRadians, MathUtil.GradiansToRadians(gradians), 5);
    }

    [Fact]
    public void TestArrayCreation()
    {
        var array = MathUtil.Array(5, 10);
        Assert.Equal(10, array.Length);
        Assert.All(array, x => Assert.Equal(5, x));
    }

    [Theory]
    [InlineData(5, -10, 10, 5)]
    [InlineData(-15, -10, 10, -10)]
    [InlineData(15, -10, 10, 10)]
    public void TestClampInt(int value, int min, int max, int expected)
    {
        Assert.Equal(expected, MathUtil.Clamp(value, min, max));
    }

    [Theory]
    [InlineData(5.5, -10.0, 10.0, 5.5)]
    [InlineData(-15.5, -10.0, 10.0, -10.0)]
    [InlineData(15.5, -10.0, 10.0, 10.0)]
    public void TestClampDouble(double value, double min, double max, double expected)
    {
        Assert.Equal(expected, MathUtil.Clamp(value, min, max));
    }

    [Theory]
    [InlineData(0.0, 10.0, 0.5, 5.0)]
    [InlineData(0.0, 10.0, 0.0, 0.0)]
    [InlineData(0.0, 10.0, 1.0, 10.0)]
    public void TestLerpDouble(double start, double end, double amount, double expected)
    {
        Assert.Equal(expected, MathUtil.Lerp(start, end, amount), 5);
    }

    [Theory]
    [InlineData(5.0, 0.0, 10.0, 0.5)]
    [InlineData(0.0, 0.0, 10.0, 0.0)]
    [InlineData(10.0, 0.0, 10.0, 1.0)]
    public void TestInverseLerpDouble(double value, double start, double end, double expected)
    {
        Assert.Equal(expected, MathUtil.InverseLerp(start, end, value), 5);
    }

    [Fact]
    public void TestLerpByte()
    {
        byte result = MathUtil.Lerp((byte)0, (byte)255, 0.5f);
        Assert.InRange(result, (byte)126, (byte)128);
    }
}
