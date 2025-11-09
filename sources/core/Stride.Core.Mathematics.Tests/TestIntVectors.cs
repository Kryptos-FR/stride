// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Xunit;

namespace Stride.Core.Mathematics.Tests;

public class TestIntVectors
{
    #region Int2 Tests

    [Fact]
    public void TestInt2Construction()
    {
        var v1 = new Int2(5, 10);
        Assert.Equal(5, v1.X);
        Assert.Equal(10, v1.Y);

        var v2 = new Int2(7);
        Assert.Equal(7, v2.X);
        Assert.Equal(7, v2.Y);
    }

    [Fact]
    public void TestInt2StaticFields()
    {
        Assert.Equal(0, Int2.Zero.X);
        Assert.Equal(0, Int2.Zero.Y);

        Assert.Equal(1, Int2.One.X);
        Assert.Equal(1, Int2.One.Y);

        Assert.Equal(1, Int2.UnitX.X);
        Assert.Equal(0, Int2.UnitX.Y);

        Assert.Equal(0, Int2.UnitY.X);
        Assert.Equal(1, Int2.UnitY.Y);
    }

    [Fact]
    public void TestInt2Addition()
    {
        var v1 = new Int2(3, 4);
        var v2 = new Int2(1, 2);
        var result = v1 + v2;
        Assert.Equal(4, result.X);
        Assert.Equal(6, result.Y);

        Int2.Add(ref v1, ref v2, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestInt2Subtraction()
    {
        var v1 = new Int2(5, 8);
        var v2 = new Int2(2, 3);
        var result = v1 - v2;
        Assert.Equal(3, result.X);
        Assert.Equal(5, result.Y);

        Int2.Subtract(ref v1, ref v2, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestInt2Multiplication()
    {
        var v = new Int2(3, 4);
        var result = v * 2;
        Assert.Equal(6, result.X);
        Assert.Equal(8, result.Y);

        var result2 = 2 * v;
        Assert.Equal(result, result2);

        Int2.Multiply(ref v, 2, out var result3);
        Assert.Equal(result, result3);
    }

    [Fact]
    public void TestInt2Division()
    {
        var v = new Int2(10, 20);
        var result = v / 2;
        Assert.Equal(5, result.X);
        Assert.Equal(10, result.Y);

        Int2.Divide(ref v, 2, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestInt2Negation()
    {
        var v = new Int2(3, -5);
        var result = -v;
        Assert.Equal(-3, result.X);
        Assert.Equal(5, result.Y);

        Int2.Negate(ref v, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestInt2Equality()
    {
        var v1 = new Int2(3, 4);
        var v2 = new Int2(3, 4);
        var v3 = new Int2(5, 6);

        Assert.True(v1 == v2);
        Assert.False(v1 == v3);
        Assert.False(v1 != v2);
        Assert.True(v1 != v3);

        Assert.True(v1.Equals(v2));
        Assert.False(v1.Equals(v3));
        Assert.True(v1.Equals((object)v2));
        Assert.False(v1.Equals((object)v3));
        Assert.False(v1.Equals(null));
    }

    [Fact]
    public void TestInt2GetHashCode()
    {
        var v1 = new Int2(3, 4);
        var v2 = new Int2(3, 4);
        var v3 = new Int2(5, 6);

        Assert.Equal(v1.GetHashCode(), v2.GetHashCode());
        Assert.NotEqual(v1.GetHashCode(), v3.GetHashCode());
    }

    [Fact]
    public void TestInt2ToString()
    {
        var v = new Int2(3, 4);
        var str = v.ToString();
        Assert.Contains("3", str);
        Assert.Contains("4", str);
    }

    #endregion

    #region Int3 Tests

    [Fact]
    public void TestInt3Construction()
    {
        var v1 = new Int3(5, 10, 15);
        Assert.Equal(5, v1.X);
        Assert.Equal(10, v1.Y);
        Assert.Equal(15, v1.Z);

        var v2 = new Int3(7);
        Assert.Equal(7, v2.X);
        Assert.Equal(7, v2.Y);
        Assert.Equal(7, v2.Z);
    }

    [Fact]
    public void TestInt3StaticFields()
    {
        Assert.Equal(0, Int3.Zero.X);
        Assert.Equal(0, Int3.Zero.Y);
        Assert.Equal(0, Int3.Zero.Z);

        Assert.Equal(1, Int3.One.X);
        Assert.Equal(1, Int3.One.Y);
        Assert.Equal(1, Int3.One.Z);

        Assert.Equal(1, Int3.UnitX.X);
        Assert.Equal(0, Int3.UnitX.Y);
        Assert.Equal(0, Int3.UnitX.Z);

        Assert.Equal(0, Int3.UnitY.X);
        Assert.Equal(1, Int3.UnitY.Y);
        Assert.Equal(0, Int3.UnitY.Z);

        Assert.Equal(0, Int3.UnitZ.X);
        Assert.Equal(0, Int3.UnitZ.Y);
        Assert.Equal(1, Int3.UnitZ.Z);
    }

    [Fact]
    public void TestInt3Addition()
    {
        var v1 = new Int3(3, 4, 5);
        var v2 = new Int3(1, 2, 3);
        var result = v1 + v2;
        Assert.Equal(4, result.X);
        Assert.Equal(6, result.Y);
        Assert.Equal(8, result.Z);

        Int3.Add(ref v1, ref v2, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestInt3Subtraction()
    {
        var v1 = new Int3(5, 8, 10);
        var v2 = new Int3(2, 3, 4);
        var result = v1 - v2;
        Assert.Equal(3, result.X);
        Assert.Equal(5, result.Y);
        Assert.Equal(6, result.Z);

        Int3.Subtract(ref v1, ref v2, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestInt3Multiplication()
    {
        var v = new Int3(3, 4, 5);
        var result = v * 2;
        Assert.Equal(6, result.X);
        Assert.Equal(8, result.Y);
        Assert.Equal(10, result.Z);

        var result2 = 2 * v;
        Assert.Equal(result, result2);

        Int3.Multiply(ref v, 2, out var result3);
        Assert.Equal(result, result3);
    }

    [Fact]
    public void TestInt3Division()
    {
        var v = new Int3(10, 20, 30);
        var result = v / 2;
        Assert.Equal(5, result.X);
        Assert.Equal(10, result.Y);
        Assert.Equal(15, result.Z);

        Int3.Divide(ref v, 2, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestInt3Negation()
    {
        var v = new Int3(3, -5, 7);
        var result = -v;
        Assert.Equal(-3, result.X);
        Assert.Equal(5, result.Y);
        Assert.Equal(-7, result.Z);

        Int3.Negate(ref v, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestInt3Equality()
    {
        var v1 = new Int3(3, 4, 5);
        var v2 = new Int3(3, 4, 5);
        var v3 = new Int3(5, 6, 7);

        Assert.True(v1 == v2);
        Assert.False(v1 == v3);
        Assert.False(v1 != v2);
        Assert.True(v1 != v3);

        Assert.True(v1.Equals(v2));
        Assert.False(v1.Equals(v3));
        Assert.True(v1.Equals((object)v2));
        Assert.False(v1.Equals((object)v3));
    }

    [Fact]
    public void TestInt3GetHashCode()
    {
        var v1 = new Int3(3, 4, 5);
        var v2 = new Int3(3, 4, 5);
        var v3 = new Int3(5, 6, 7);

        Assert.Equal(v1.GetHashCode(), v2.GetHashCode());
        Assert.NotEqual(v1.GetHashCode(), v3.GetHashCode());
    }

    [Fact]
    public void TestInt3ToString()
    {
        var v = new Int3(3, 4, 5);
        var str = v.ToString();
        Assert.Contains("3", str);
        Assert.Contains("4", str);
        Assert.Contains("5", str);
    }

    #endregion

    #region Int4 Tests

    [Fact]
    public void TestInt4Construction()
    {
        var v1 = new Int4(5, 10, 15, 20);
        Assert.Equal(5, v1.X);
        Assert.Equal(10, v1.Y);
        Assert.Equal(15, v1.Z);
        Assert.Equal(20, v1.W);

        var v2 = new Int4(7);
        Assert.Equal(7, v2.X);
        Assert.Equal(7, v2.Y);
        Assert.Equal(7, v2.Z);
        Assert.Equal(7, v2.W);
    }

    [Fact]
    public void TestInt4StaticFields()
    {
        Assert.Equal(0, Int4.Zero.X);
        Assert.Equal(0, Int4.Zero.Y);
        Assert.Equal(0, Int4.Zero.Z);
        Assert.Equal(0, Int4.Zero.W);

        Assert.Equal(1, Int4.One.X);
        Assert.Equal(1, Int4.One.Y);
        Assert.Equal(1, Int4.One.Z);
        Assert.Equal(1, Int4.One.W);

        Assert.Equal(1, Int4.UnitX.X);
        Assert.Equal(0, Int4.UnitX.Y);
        Assert.Equal(0, Int4.UnitX.Z);
        Assert.Equal(0, Int4.UnitX.W);

        Assert.Equal(0, Int4.UnitY.X);
        Assert.Equal(1, Int4.UnitY.Y);
        Assert.Equal(0, Int4.UnitY.Z);
        Assert.Equal(0, Int4.UnitY.W);

        Assert.Equal(0, Int4.UnitZ.X);
        Assert.Equal(0, Int4.UnitZ.Y);
        Assert.Equal(1, Int4.UnitZ.Z);
        Assert.Equal(0, Int4.UnitZ.W);

        Assert.Equal(0, Int4.UnitW.X);
        Assert.Equal(0, Int4.UnitW.Y);
        Assert.Equal(0, Int4.UnitW.Z);
        Assert.Equal(1, Int4.UnitW.W);
    }

    [Fact]
    public void TestInt4Addition()
    {
        var v1 = new Int4(3, 4, 5, 6);
        var v2 = new Int4(1, 2, 3, 4);
        var result = v1 + v2;
        Assert.Equal(4, result.X);
        Assert.Equal(6, result.Y);
        Assert.Equal(8, result.Z);
        Assert.Equal(10, result.W);

        Int4.Add(ref v1, ref v2, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestInt4Subtraction()
    {
        var v1 = new Int4(5, 8, 10, 12);
        var v2 = new Int4(2, 3, 4, 5);
        var result = v1 - v2;
        Assert.Equal(3, result.X);
        Assert.Equal(5, result.Y);
        Assert.Equal(6, result.Z);
        Assert.Equal(7, result.W);

        Int4.Subtract(ref v1, ref v2, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestInt4Multiplication()
    {
        var v = new Int4(3, 4, 5, 6);
        var result = v * 2;
        Assert.Equal(6, result.X);
        Assert.Equal(8, result.Y);
        Assert.Equal(10, result.Z);
        Assert.Equal(12, result.W);

        var result2 = 2 * v;
        Assert.Equal(result, result2);

        Int4.Multiply(ref v, 2, out var result3);
        Assert.Equal(result, result3);
    }

    [Fact]
    public void TestInt4Division()
    {
        var v = new Int4(10, 20, 30, 40);
        var result = v / 2;
        Assert.Equal(5, result.X);
        Assert.Equal(10, result.Y);
        Assert.Equal(15, result.Z);
        Assert.Equal(20, result.W);

        Int4.Divide(ref v, 2, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestInt4Negation()
    {
        var v = new Int4(3, -5, 7, -9);
        var result = -v;
        Assert.Equal(-3, result.X);
        Assert.Equal(5, result.Y);
        Assert.Equal(-7, result.Z);
        Assert.Equal(9, result.W);

        Int4.Negate(ref v, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestInt4Equality()
    {
        var v1 = new Int4(3, 4, 5, 6);
        var v2 = new Int4(3, 4, 5, 6);
        var v3 = new Int4(5, 6, 7, 8);

        Assert.True(v1 == v2);
        Assert.False(v1 == v3);
        Assert.False(v1 != v2);
        Assert.True(v1 != v3);

        Assert.True(v1.Equals(v2));
        Assert.False(v1.Equals(v3));
        Assert.True(v1.Equals((object)v2));
        Assert.False(v1.Equals((object)v3));
    }

    [Fact]
    public void TestInt4GetHashCode()
    {
        var v1 = new Int4(3, 4, 5, 6);
        var v2 = new Int4(3, 4, 5, 6);
        var v3 = new Int4(5, 6, 7, 8);

        Assert.Equal(v1.GetHashCode(), v2.GetHashCode());
        Assert.NotEqual(v1.GetHashCode(), v3.GetHashCode());
    }

    [Fact]
    public void TestInt4ToString()
    {
        var v = new Int4(3, 4, 5, 6);
        var str = v.ToString();
        Assert.Contains("3", str);
        Assert.Contains("4", str);
        Assert.Contains("5", str);
        Assert.Contains("6", str);
    }

    #endregion

    #region UInt4 Tests

    [Fact]
    public void TestUInt4Construction()
    {
        var v1 = new UInt4(5u, 10u, 15u, 20u);
        Assert.Equal(5u, v1.X);
        Assert.Equal(10u, v1.Y);
        Assert.Equal(15u, v1.Z);
        Assert.Equal(20u, v1.W);

        var v2 = new UInt4(7u);
        Assert.Equal(7u, v2.X);
        Assert.Equal(7u, v2.Y);
        Assert.Equal(7u, v2.Z);
        Assert.Equal(7u, v2.W);
    }

    [Fact]
    public void TestUInt4StaticFields()
    {
        Assert.Equal(0u, UInt4.Zero.X);
        Assert.Equal(0u, UInt4.Zero.Y);
        Assert.Equal(0u, UInt4.Zero.Z);
        Assert.Equal(0u, UInt4.Zero.W);

        Assert.Equal(1u, UInt4.One.X);
        Assert.Equal(1u, UInt4.One.Y);
        Assert.Equal(1u, UInt4.One.Z);
        Assert.Equal(1u, UInt4.One.W);

        Assert.Equal(1u, UInt4.UnitX.X);
        Assert.Equal(0u, UInt4.UnitX.Y);
        Assert.Equal(0u, UInt4.UnitX.Z);
        Assert.Equal(0u, UInt4.UnitX.W);

        Assert.Equal(0u, UInt4.UnitY.X);
        Assert.Equal(1u, UInt4.UnitY.Y);
        Assert.Equal(0u, UInt4.UnitY.Z);
        Assert.Equal(0u, UInt4.UnitY.W);

        Assert.Equal(0u, UInt4.UnitZ.X);
        Assert.Equal(0u, UInt4.UnitZ.Y);
        Assert.Equal(1u, UInt4.UnitZ.Z);
        Assert.Equal(0u, UInt4.UnitZ.W);

        Assert.Equal(0u, UInt4.UnitW.X);
        Assert.Equal(0u, UInt4.UnitW.Y);
        Assert.Equal(0u, UInt4.UnitW.Z);
        Assert.Equal(1u, UInt4.UnitW.W);
    }

    [Fact]
    public void TestUInt4Addition()
    {
        var v1 = new UInt4(3u, 4u, 5u, 6u);
        var v2 = new UInt4(1u, 2u, 3u, 4u);
        var result = v1 + v2;
        Assert.Equal(4u, result.X);
        Assert.Equal(6u, result.Y);
        Assert.Equal(8u, result.Z);
        Assert.Equal(10u, result.W);

        UInt4.Add(ref v1, ref v2, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestUInt4Subtraction()
    {
        var v1 = new UInt4(5u, 8u, 10u, 12u);
        var v2 = new UInt4(2u, 3u, 4u, 5u);
        var result = v1 - v2;
        Assert.Equal(3u, result.X);
        Assert.Equal(5u, result.Y);
        Assert.Equal(6u, result.Z);
        Assert.Equal(7u, result.W);

        UInt4.Subtract(ref v1, ref v2, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestUInt4Multiplication()
    {
        var v = new UInt4(3u, 4u, 5u, 6u);
        var result = v * 2u;
        Assert.Equal(6u, result.X);
        Assert.Equal(8u, result.Y);
        Assert.Equal(10u, result.Z);
        Assert.Equal(12u, result.W);

        var result2 = 2u * v;
        Assert.Equal(result, result2);

        UInt4.Multiply(ref v, 2u, out var result3);
        Assert.Equal(result, result3);
    }

    [Fact]
    public void TestUInt4Division()
    {
        var v = new UInt4(10u, 20u, 30u, 40u);
        var result = v / 2u;
        Assert.Equal(5u, result.X);
        Assert.Equal(10u, result.Y);
        Assert.Equal(15u, result.Z);
        Assert.Equal(20u, result.W);

        UInt4.Divide(ref v, 2u, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestUInt4Equality()
    {
        var v1 = new UInt4(3u, 4u, 5u, 6u);
        var v2 = new UInt4(3u, 4u, 5u, 6u);
        var v3 = new UInt4(5u, 6u, 7u, 8u);

        Assert.True(v1 == v2);
        Assert.False(v1 == v3);
        Assert.False(v1 != v2);
        Assert.True(v1 != v3);

        Assert.True(v1.Equals(v2));
        Assert.False(v1.Equals(v3));
        Assert.True(v1.Equals((object)v2));
        Assert.False(v1.Equals((object)v3));
    }

    [Fact]
    public void TestUInt4GetHashCode()
    {
        var v1 = new UInt4(3u, 4u, 5u, 6u);
        var v2 = new UInt4(3u, 4u, 5u, 6u);
        var v3 = new UInt4(5u, 6u, 7u, 8u);

        Assert.Equal(v1.GetHashCode(), v2.GetHashCode());
        Assert.NotEqual(v1.GetHashCode(), v3.GetHashCode());
    }

    [Fact]
    public void TestUInt4ToString()
    {
        var v = new UInt4(3u, 4u, 5u, 6u);
        var str = v.ToString();
        Assert.Contains("3", str);
        Assert.Contains("4", str);
        Assert.Contains("5", str);
        Assert.Contains("6", str);
    }

    #endregion
}
