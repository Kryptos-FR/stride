// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Xunit;

namespace Stride.Core.Mathematics.Tests;

public class TestDouble2
{
    [Fact]
    public void TestDouble2Constants()
    {
        Assert.Equal(0.0, Double2.Zero.X);
        Assert.Equal(0.0, Double2.Zero.Y);
        
        Assert.Equal(1.0, Double2.One.X);
        Assert.Equal(1.0, Double2.One.Y);
        
        Assert.Equal(1.0, Double2.UnitX.X);
        Assert.Equal(0.0, Double2.UnitX.Y);
        
        Assert.Equal(0.0, Double2.UnitY.X);
        Assert.Equal(1.0, Double2.UnitY.Y);
    }

    [Fact]
    public void TestDouble2Constructors()
    {
        var v1 = new Double2(5.0);
        Assert.Equal(5.0, v1.X);
        Assert.Equal(5.0, v1.Y);

        var v2 = new Double2(3.0, 4.0);
        Assert.Equal(3.0, v2.X);
        Assert.Equal(4.0, v2.Y);

        var v3 = new Double2(new double[] { 1.0, 2.0 });
        Assert.Equal(1.0, v3.X);
        Assert.Equal(2.0, v3.Y);

        var v4 = new Double2(new Vector2(7.0f, 8.0f));
        Assert.Equal(7.0, v4.X);
        Assert.Equal(8.0, v4.Y);
    }

    [Fact]
    public void TestDouble2Length()
    {
        var v = new Double2(3.0, 4.0);
        Assert.Equal(5.0, v.Length());
    }

    [Fact]
    public void TestDouble2LengthSquared()
    {
        var v = new Double2(3.0, 4.0);
        Assert.Equal(25.0, v.LengthSquared());
    }

    [Fact]
    public void TestDouble2Distance()
    {
        var v1 = new Double2(1.0, 2.0);
        var v2 = new Double2(4.0, 6.0);
        Assert.Equal(5.0, Double2.Distance(v1, v2));
    }

    [Fact]
    public void TestDouble2DistanceSquared()
    {
        var v1 = new Double2(1.0, 2.0);
        var v2 = new Double2(4.0, 6.0);
        Assert.Equal(25.0, Double2.DistanceSquared(v1, v2));
    }

    [Fact]
    public void TestDouble2Dot()
    {
        var v1 = new Double2(1.0, 2.0);
        var v2 = new Double2(3.0, 4.0);
        Assert.Equal(11.0, Double2.Dot(v1, v2));
    }

    [Fact]
    public void TestDouble2Normalize()
    {
        var v = new Double2(3.0, 4.0);
        var normalized = Double2.Normalize(v);
        Assert.Equal(0.6, normalized.X, 5);
        Assert.Equal(0.8, normalized.Y, 5);
        Assert.Equal(1.0, normalized.Length(), 5);
    }

    [Fact]
    public void TestDouble2Add()
    {
        var v1 = new Double2(1.0, 2.0);
        var v2 = new Double2(3.0, 4.0);
        var result = v1 + v2;
        Assert.Equal(4.0, result.X);
        Assert.Equal(6.0, result.Y);
    }

    [Fact]
    public void TestDouble2Subtract()
    {
        var v1 = new Double2(5.0, 7.0);
        var v2 = new Double2(2.0, 3.0);
        var result = v1 - v2;
        Assert.Equal(3.0, result.X);
        Assert.Equal(4.0, result.Y);
    }

    [Fact]
    public void TestDouble2Multiply()
    {
        var v = new Double2(2.0, 3.0);
        var result = v * 2.0;
        Assert.Equal(4.0, result.X);
        Assert.Equal(6.0, result.Y);

        var result2 = 2.0 * v;
        Assert.Equal(4.0, result2.X);
        Assert.Equal(6.0, result2.Y);
    }

    [Fact]
    public void TestDouble2Divide()
    {
        var v = new Double2(4.0, 6.0);
        var result = v / 2.0;
        Assert.Equal(2.0, result.X);
        Assert.Equal(3.0, result.Y);
    }

    [Fact]
    public void TestDouble2Negate()
    {
        var v = new Double2(1.0, -2.0);
        var result = -v;
        Assert.Equal(-1.0, result.X);
        Assert.Equal(2.0, result.Y);
    }

    [Fact]
    public void TestDouble2Modulate()
    {
        var v1 = new Double2(2.0, 3.0);
        var v2 = new Double2(4.0, 5.0);
        var result = Double2.Modulate(v1, v2);
        Assert.Equal(8.0, result.X);
        Assert.Equal(15.0, result.Y);
    }

    [Fact]
    public void TestDouble2Demodulate()
    {
        var v1 = new Double2(8.0, 15.0);
        var v2 = new Double2(4.0, 5.0);
        var result = Double2.Demodulate(v1, v2);
        Assert.Equal(2.0, result.X);
        Assert.Equal(3.0, result.Y);
    }

    [Fact]
    public void TestDouble2Clamp()
    {
        var value = new Double2(5.0, -5.0);
        var min = new Double2(0.0, 0.0);
        var max = new Double2(10.0, 10.0);
        var result = Double2.Clamp(value, min, max);
        Assert.Equal(5.0, result.X);
        Assert.Equal(0.0, result.Y);
    }

    [Fact]
    public void TestDouble2Min()
    {
        var v1 = new Double2(1.0, 5.0);
        var v2 = new Double2(3.0, 2.0);
        var result = Double2.Min(v1, v2);
        Assert.Equal(1.0, result.X);
        Assert.Equal(2.0, result.Y);
    }

    [Fact]
    public void TestDouble2Max()
    {
        var v1 = new Double2(1.0, 5.0);
        var v2 = new Double2(3.0, 2.0);
        var result = Double2.Max(v1, v2);
        Assert.Equal(3.0, result.X);
        Assert.Equal(5.0, result.Y);
    }

    [Fact]
    public void TestDouble2Lerp()
    {
        var start = new Double2(0.0, 0.0);
        var end = new Double2(10.0, 10.0);
        var result = Double2.Lerp(start, end, 0.5);
        Assert.Equal(5.0, result.X);
        Assert.Equal(5.0, result.Y);
    }

    [Fact]
    public void TestDouble2SmoothStep()
    {
        var start = new Double2(0.0, 0.0);
        var end = new Double2(10.0, 10.0);
        var result = Double2.SmoothStep(start, end, 0.5);
        Assert.Equal(5.0, result.X);
        Assert.Equal(5.0, result.Y);
    }

    [Fact]
    public void TestDouble2Barycentric()
    {
        var v1 = new Double2(0.0, 0.0);
        var v2 = new Double2(10.0, 0.0);
        var v3 = new Double2(0.0, 10.0);
        var result = Double2.Barycentric(v1, v2, v3, 0.5, 0.5);
        Assert.Equal(5.0, result.X);
        Assert.Equal(5.0, result.Y);
    }

    [Fact]
    public void TestDouble2CatmullRom()
    {
        var v1 = new Double2(0.0, 0.0);
        var v2 = new Double2(1.0, 1.0);
        var v3 = new Double2(2.0, 2.0);
        var v4 = new Double2(3.0, 3.0);
        var result = Double2.CatmullRom(v1, v2, v3, v4, 0.5);
        Assert.Equal(1.5, result.X, 5);
        Assert.Equal(1.5, result.Y, 5);
    }

    [Fact]
    public void TestDouble2Hermite()
    {
        var v1 = new Double2(0.0, 0.0);
        var t1 = new Double2(1.0, 1.0);
        var v2 = new Double2(2.0, 2.0);
        var t2 = new Double2(1.0, 1.0);
        var result = Double2.Hermite(v1, t1, v2, t2, 0.5);
        Assert.Equal(1.0, result.X, 5);
        Assert.Equal(1.0, result.Y, 5);
    }

    [Fact]
    public void TestDouble2Reflect()
    {
        var vector = new Double2(1.0, -1.0);
        var normal = new Double2(0.0, 1.0);
        var result = Double2.Reflect(vector, normal);
        Assert.Equal(1.0, result.X, 5);
        Assert.Equal(1.0, result.Y, 5);
    }

    [Fact]
    public void TestDouble2TransformByQuaternion()
    {
        var vector = new Double2(1.0, 0.0);
        var rotation = Quaternion.RotationZ(MathUtil.PiOverTwo);
        var result = Double2.Transform(vector, rotation);
        Assert.Equal(0.0, result.X, 5);
        Assert.Equal(1.0, result.Y, 5);
    }

    [Fact]
    public void TestDouble2TransformCoordinate()
    {
        var vector = new Double2(1.0, 0.0);
        var matrix = Matrix.Translation(1.0f, 2.0f, 0.0f);
        var result = Double2.TransformCoordinate(vector, matrix);
        Assert.Equal(2.0, result.X, 5);
        Assert.Equal(2.0, result.Y, 5);
    }

    [Fact]
    public void TestDouble2TransformNormal()
    {
        var normal = new Double2(1.0, 0.0);
        var matrix = Matrix.RotationZ(MathUtil.PiOverTwo);
        var result = Double2.TransformNormal(normal, matrix);
        Assert.Equal(0.0, result.X, 5);
        Assert.Equal(1.0, result.Y, 5);
    }

    [Fact]
    public void TestDouble2Equality()
    {
        var v1 = new Double2(1.0, 2.0);
        var v2 = new Double2(1.0, 2.0);
        var v3 = new Double2(3.0, 4.0);

        Assert.True(v1 == v2);
        Assert.False(v1 == v3);
        Assert.False(v1 != v2);
        Assert.True(v1 != v3);
        Assert.True(v1.Equals(v2));
        Assert.False(v1.Equals(v3));
    }

    [Fact]
    public void TestDouble2HashCode()
    {
        var v1 = new Double2(1.0, 2.0);
        var v2 = new Double2(1.0, 2.0);
        Assert.Equal(v1.GetHashCode(), v2.GetHashCode());
    }

    [Fact]
    public void TestDouble2ToString()
    {
        var v = new Double2(1.5, 2.5);
        var str = v.ToString();
        Assert.Contains("1", str);
        Assert.Contains("2", str);
        Assert.Contains("5", str);
    }

    [Fact]
    public void TestDouble2ConversionFromVector2()
    {
        var v = new Vector2(1.0f, 2.0f);
        Double2 d = v;
        Assert.Equal(1.0, d.X);
        Assert.Equal(2.0, d.Y);
    }

    [Fact]
    public void TestDouble2ConversionToVector2()
    {
        var d = new Double2(1.0, 2.0);
        Vector2 v = (Vector2)d;
        Assert.Equal(1.0f, v.X);
        Assert.Equal(2.0f, v.Y);
    }
}
