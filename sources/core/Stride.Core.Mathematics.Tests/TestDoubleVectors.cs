// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Xunit;

namespace Stride.Core.Mathematics.Tests;

public class TestDoubleVectors
{
    #region Double2 Tests

    [Fact]
    public void TestDouble2Construction()
    {
        var v1 = new Double2(5.5, 10.3);
        Assert.Equal(5.5, v1.X);
        Assert.Equal(10.3, v1.Y);

        var v2 = new Double2(7.7);
        Assert.Equal(7.7, v2.X);
        Assert.Equal(7.7, v2.Y);
    }

    [Fact]
    public void TestDouble2StaticFields()
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
    public void TestDouble2Addition()
    {
        var v1 = new Double2(3.5, 4.2);
        var v2 = new Double2(1.1, 2.3);
        var result = v1 + v2;
        Assert.Equal(4.6, result.X, 10);
        Assert.Equal(6.5, result.Y, 10);

        Double2.Add(ref v1, ref v2, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestDouble2Subtraction()
    {
        var v1 = new Double2(5.5, 8.8);
        var v2 = new Double2(2.2, 3.3);
        var result = v1 - v2;
        Assert.Equal(3.3, result.X, 10);
        Assert.Equal(5.5, result.Y, 10);

        Double2.Subtract(ref v1, ref v2, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestDouble2Multiplication()
    {
        var v = new Double2(3.0, 4.0);
        var result = v * 2.5;
        Assert.Equal(7.5, result.X);
        Assert.Equal(10.0, result.Y);

        var result2 = 2.5 * v;
        Assert.Equal(result, result2);

        Double2.Multiply(ref v, 2.5, out var result3);
        Assert.Equal(result, result3);
    }

    [Fact]
    public void TestDouble2Division()
    {
        var v = new Double2(10.0, 20.0);
        var result = v / 2.0;
        Assert.Equal(5.0, result.X);
        Assert.Equal(10.0, result.Y);

        Double2.Divide(ref v, 2.0, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestDouble2Negation()
    {
        var v = new Double2(3.5, -5.2);
        var result = -v;
        Assert.Equal(-3.5, result.X);
        Assert.Equal(5.2, result.Y);

        Double2.Negate(ref v, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestDouble2Length()
    {
        var v = new Double2(3.0, 4.0);
        Assert.Equal(5.0, v.Length(), 10);
        Assert.Equal(25.0, v.LengthSquared());
    }

    [Fact]
    public void TestDouble2Normalize()
    {
        var v = new Double2(3.0, 4.0);
        v.Normalize();
        Assert.Equal(0.6, v.X, 5);
        Assert.Equal(0.8, v.Y, 5);
        Assert.Equal(1.0, v.Length(), 5);

        var v2 = new Double2(5.0, 12.0);
        var normalized = Double2.Normalize(v2);
        Assert.Equal(1.0, normalized.Length(), 5);
    }

    [Fact]
    public void TestDouble2DotProduct()
    {
        var v1 = new Double2(2.0, 3.0);
        var v2 = new Double2(4.0, 5.0);
        var dot = Double2.Dot(v1, v2);
        Assert.Equal(23.0, dot); // 2*4 + 3*5 = 23
    }

    [Fact]
    public void TestDouble2Equality()
    {
        var v1 = new Double2(3.5, 4.2);
        var v2 = new Double2(3.5, 4.2);
        var v3 = new Double2(5.1, 6.3);

        Assert.True(v1 == v2);
        Assert.False(v1 == v3);
        Assert.False(v1 != v2);
        Assert.True(v1 != v3);

        Assert.True(v1.Equals(v2));
        Assert.False(v1.Equals(v3));
    }

    #endregion

    #region Double3 Tests

    [Fact]
    public void TestDouble3Construction()
    {
        var v1 = new Double3(5.5, 10.3, 15.7);
        Assert.Equal(5.5, v1.X);
        Assert.Equal(10.3, v1.Y);
        Assert.Equal(15.7, v1.Z);

        var v2 = new Double3(7.7);
        Assert.Equal(7.7, v2.X);
        Assert.Equal(7.7, v2.Y);
        Assert.Equal(7.7, v2.Z);
    }

    [Fact]
    public void TestDouble3StaticFields()
    {
        Assert.Equal(0.0, Double3.Zero.X);
        Assert.Equal(0.0, Double3.Zero.Y);
        Assert.Equal(0.0, Double3.Zero.Z);

        Assert.Equal(1.0, Double3.One.X);
        Assert.Equal(1.0, Double3.One.Y);
        Assert.Equal(1.0, Double3.One.Z);

        Assert.Equal(1.0, Double3.UnitX.X);
        Assert.Equal(0.0, Double3.UnitY.X);
        Assert.Equal(0.0, Double3.UnitZ.X);
    }

    [Fact]
    public void TestDouble3Addition()
    {
        var v1 = new Double3(3.5, 4.2, 5.1);
        var v2 = new Double3(1.1, 2.3, 3.4);
        var result = v1 + v2;
        Assert.Equal(4.6, result.X, 10);
        Assert.Equal(6.5, result.Y, 10);
        Assert.Equal(8.5, result.Z, 10);

        Double3.Add(ref v1, ref v2, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestDouble3Subtraction()
    {
        var v1 = new Double3(5.5, 8.8, 10.1);
        var v2 = new Double3(2.2, 3.3, 4.4);
        var result = v1 - v2;
        Assert.Equal(3.3, result.X, 10);
        Assert.Equal(5.5, result.Y, 10);
        Assert.Equal(5.7, result.Z, 10);

        Double3.Subtract(ref v1, ref v2, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestDouble3Multiplication()
    {
        var v = new Double3(3.0, 4.0, 5.0);
        var result = v * 2.5;
        Assert.Equal(7.5, result.X);
        Assert.Equal(10.0, result.Y);
        Assert.Equal(12.5, result.Z);

        var result2 = 2.5 * v;
        Assert.Equal(result, result2);

        Double3.Multiply(ref v, 2.5, out var result3);
        Assert.Equal(result, result3);
    }

    [Fact]
    public void TestDouble3Division()
    {
        var v = new Double3(10.0, 20.0, 30.0);
        var result = v / 2.0;
        Assert.Equal(5.0, result.X);
        Assert.Equal(10.0, result.Y);
        Assert.Equal(15.0, result.Z);

        Double3.Divide(ref v, 2.0, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestDouble3Negation()
    {
        var v = new Double3(3.5, -5.2, 7.8);
        var result = -v;
        Assert.Equal(-3.5, result.X);
        Assert.Equal(5.2, result.Y);
        Assert.Equal(-7.8, result.Z);

        Double3.Negate(ref v, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestDouble3Length()
    {
        var v = new Double3(1.0, 2.0, 2.0);
        Assert.Equal(3.0, v.Length());
        Assert.Equal(9.0, v.LengthSquared());
    }

    [Fact]
    public void TestDouble3Normalize()
    {
        var v = new Double3(3.0, 4.0, 0.0);
        v.Normalize();
        Assert.Equal(1.0, v.Length(), 5);

        var v2 = new Double3(1.0, 2.0, 2.0);
        var normalized = Double3.Normalize(v2);
        Assert.Equal(1.0, normalized.Length(), 5);
    }

    [Fact]
    public void TestDouble3DotProduct()
    {
        var v1 = new Double3(2.0, 3.0, 4.0);
        var v2 = new Double3(5.0, 6.0, 7.0);
        var dot = Double3.Dot(v1, v2);
        Assert.Equal(56.0, dot); // 2*5 + 3*6 + 4*7 = 56
    }

    [Fact]
    public void TestDouble3CrossProduct()
    {
        var v1 = new Double3(1.0, 0.0, 0.0);
        var v2 = new Double3(0.0, 1.0, 0.0);
        var cross = Double3.Cross(v1, v2);
        Assert.Equal(0.0, cross.X);
        Assert.Equal(0.0, cross.Y);
        Assert.Equal(1.0, cross.Z);
    }

    [Fact]
    public void TestDouble3Equality()
    {
        var v1 = new Double3(3.5, 4.2, 5.1);
        var v2 = new Double3(3.5, 4.2, 5.1);
        var v3 = new Double3(5.1, 6.3, 7.2);

        Assert.True(v1 == v2);
        Assert.False(v1 == v3);
        Assert.False(v1 != v2);
        Assert.True(v1 != v3);

        Assert.True(v1.Equals(v2));
        Assert.False(v1.Equals(v3));
    }

    #endregion

    #region Double4 Tests

    [Fact]
    public void TestDouble4Construction()
    {
        var v1 = new Double4(5.5, 10.3, 15.7, 20.1);
        Assert.Equal(5.5, v1.X);
        Assert.Equal(10.3, v1.Y);
        Assert.Equal(15.7, v1.Z);
        Assert.Equal(20.1, v1.W);

        var v2 = new Double4(7.7);
        Assert.Equal(7.7, v2.X);
        Assert.Equal(7.7, v2.Y);
        Assert.Equal(7.7, v2.Z);
        Assert.Equal(7.7, v2.W);
    }

    [Fact]
    public void TestDouble4StaticFields()
    {
        Assert.Equal(0.0, Double4.Zero.X);
        Assert.Equal(1.0, Double4.One.X);
        Assert.Equal(1.0, Double4.UnitX.X);
        Assert.Equal(0.0, Double4.UnitX.Y);
    }

    [Fact]
    public void TestDouble4Addition()
    {
        var v1 = new Double4(3.5, 4.2, 5.1, 6.3);
        var v2 = new Double4(1.1, 2.3, 3.4, 4.5);
        var result = v1 + v2;
        Assert.Equal(4.6, result.X, 10);
        Assert.Equal(6.5, result.Y, 10);
        Assert.Equal(8.5, result.Z, 10);
        Assert.Equal(10.8, result.W, 10);

        Double4.Add(ref v1, ref v2, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestDouble4Subtraction()
    {
        var v1 = new Double4(5.5, 8.8, 10.1, 12.4);
        var v2 = new Double4(2.2, 3.3, 4.4, 5.5);
        var result = v1 - v2;
        Assert.Equal(3.3, result.X, 10);
        Assert.Equal(5.5, result.Y, 10);
        Assert.Equal(5.7, result.Z, 10);
        Assert.Equal(6.9, result.W, 10);

        Double4.Subtract(ref v1, ref v2, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestDouble4Multiplication()
    {
        var v = new Double4(3.0, 4.0, 5.0, 6.0);
        var result = v * 2.5;
        Assert.Equal(7.5, result.X);
        Assert.Equal(10.0, result.Y);
        Assert.Equal(12.5, result.Z);
        Assert.Equal(15.0, result.W);

        var result2 = 2.5 * v;
        Assert.Equal(result, result2);

        Double4.Multiply(ref v, 2.5, out var result3);
        Assert.Equal(result, result3);
    }

    [Fact]
    public void TestDouble4Division()
    {
        var v = new Double4(10.0, 20.0, 30.0, 40.0);
        var result = v / 2.0;
        Assert.Equal(5.0, result.X);
        Assert.Equal(10.0, result.Y);
        Assert.Equal(15.0, result.Z);
        Assert.Equal(20.0, result.W);

        Double4.Divide(ref v, 2.0, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestDouble4Negation()
    {
        var v = new Double4(3.5, -5.2, 7.8, -9.1);
        var result = -v;
        Assert.Equal(-3.5, result.X);
        Assert.Equal(5.2, result.Y);
        Assert.Equal(-7.8, result.Z);
        Assert.Equal(9.1, result.W);

        Double4.Negate(ref v, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestDouble4Length()
    {
        var v = new Double4(2.0, 3.0, 6.0, 0.0);
        Assert.Equal(7.0, v.Length());
        Assert.Equal(49.0, v.LengthSquared());
    }

    [Fact]
    public void TestDouble4Normalize()
    {
        var v = new Double4(3.0, 4.0, 0.0, 0.0);
        v.Normalize();
        Assert.Equal(1.0, v.Length(), 5);

        var v2 = new Double4(2.0, 3.0, 6.0, 0.0);
        var normalized = Double4.Normalize(v2);
        Assert.Equal(1.0, normalized.Length(), 5);
    }

    [Fact]
    public void TestDouble4DotProduct()
    {
        var v1 = new Double4(2.0, 3.0, 4.0, 5.0);
        var v2 = new Double4(6.0, 7.0, 8.0, 9.0);
        var dot = Double4.Dot(v1, v2);
        Assert.Equal(110.0, dot); // 2*6 + 3*7 + 4*8 + 5*9 = 110
    }

    [Fact]
    public void TestDouble4Equality()
    {
        var v1 = new Double4(3.5, 4.2, 5.1, 6.3);
        var v2 = new Double4(3.5, 4.2, 5.1, 6.3);
        var v3 = new Double4(5.1, 6.3, 7.2, 8.4);

        Assert.True(v1 == v2);
        Assert.False(v1 == v3);
        Assert.False(v1 != v2);
        Assert.True(v1 != v3);

        Assert.True(v1.Equals(v2));
        Assert.False(v1.Equals(v3));
    }

    #endregion
}
