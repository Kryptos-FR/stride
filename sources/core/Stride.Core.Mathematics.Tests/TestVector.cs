// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Xunit;

namespace Stride.Core.Mathematics.Tests;

public class TestVector
{
    [Fact]
    public void TestVector2Addition()
    {
        var v1 = new Vector2(1.0f, 2.0f);
        var v2 = new Vector2(3.0f, 4.0f);
        var result = v1 + v2;
        Assert.Equal(4.0f, result.X);
        Assert.Equal(6.0f, result.Y);
    }

    [Fact]
    public void TestVector2Subtraction()
    {
        var v1 = new Vector2(3.0f, 5.0f);
        var v2 = new Vector2(1.0f, 2.0f);
        var result = v1 - v2;
        Assert.Equal(2.0f, result.X);
        Assert.Equal(3.0f, result.Y);
    }

    [Fact]
    public void TestVector2DotProduct()
    {
        var v1 = new Vector2(1.0f, 2.0f);
        var v2 = new Vector2(3.0f, 4.0f);
        var result = Vector2.Dot(v1, v2);
        Assert.Equal(11.0f, result); // (1 * 3) + (2 * 4)
    }

    [Fact]
    public void TestVector2Normalization()
    {
        var v = new Vector2(3.0f, 4.0f);
        var normalized = Vector2.Normalize(v);
        Assert.Equal(0.6f, normalized.X, 3); // 3/5
        Assert.Equal(0.8f, normalized.Y, 3); // 4/5
    }

    [Fact]
    public void TestVector3CrossProduct()
    {
        var v1 = new Vector3(1.0f, 0.0f, 0.0f); // i vector
        var v2 = new Vector3(0.0f, 1.0f, 0.0f); // j vector
        var result = Vector3.Cross(v1, v2);
        Assert.Equal(0.0f, result.X);
        Assert.Equal(0.0f, result.Y);
        Assert.Equal(1.0f, result.Z); // i × j = k
    }

    [Fact]
    public void TestVector3Addition()
    {
        var v1 = new Vector3(1.0f, 2.0f, 3.0f);
        var v2 = new Vector3(4.0f, 5.0f, 6.0f);
        var result = v1 + v2;
        Assert.Equal(5.0f, result.X);
        Assert.Equal(7.0f, result.Y);
        Assert.Equal(9.0f, result.Z);
    }

    [Fact]
    public void TestVector3DotProduct()
    {
        var v1 = new Vector3(1.0f, 2.0f, 3.0f);
        var v2 = new Vector3(4.0f, 5.0f, 6.0f);
        var result = Vector3.Dot(v1, v2);
        Assert.Equal(32.0f, result); // (1 * 4) + (2 * 5) + (3 * 6)
    }

    [Fact]
    public void TestVector3Normalization()
    {
        var v = new Vector3(2.0f, 2.0f, 1.0f);
        var normalized = Vector3.Normalize(v);
        var length = (float)Math.Sqrt(9.0f); // sqrt(4 + 4 + 1)
        Assert.Equal(2.0f/length, normalized.X, 3);
        Assert.Equal(2.0f/length, normalized.Y, 3);
        Assert.Equal(1.0f/length, normalized.Z, 3);
    }

    [Fact]
    public void TestVector4Operations()
    {
        var v1 = new Vector4(1.0f, 2.0f, 3.0f, 4.0f);
        var v2 = new Vector4(5.0f, 6.0f, 7.0f, 8.0f);
        
        // Test addition
        var sum = v1 + v2;
        Assert.Equal(6.0f, sum.X);
        Assert.Equal(8.0f, sum.Y);
        Assert.Equal(10.0f, sum.Z);
        Assert.Equal(12.0f, sum.W);

        // Test dot product
        var dot = Vector4.Dot(v1, v2);
        Assert.Equal(70.0f, dot); // (1*5) + (2*6) + (3*7) + (4*8)

        // Test normalization
        var v = new Vector4(2.0f, 2.0f, 2.0f, 1.0f);
        var normalized = Vector4.Normalize(v);
        var length = (float)Math.Sqrt(13.0f); // sqrt(4 + 4 + 4 + 1)
        Assert.Equal(2.0f/length, normalized.X, 3);
        Assert.Equal(2.0f/length, normalized.Y, 3);
        Assert.Equal(2.0f/length, normalized.Z, 3);
        Assert.Equal(1.0f/length, normalized.W, 3);
    }

    [Theory]
    [InlineData(0.0f, 0.0f)]
    [InlineData(1.0f, 1.0f)]
    [InlineData(-1.0f, 1.0f)]
    [InlineData(3.0f, 3.0f)]
    public void TestVector2Length(float value, float expectedLength)
    {
        var vector = new Vector2(value, 0.0f);
        Assert.Equal(expectedLength, vector.Length());
    }

    [Theory]
    [InlineData(0.0f, 0.0f, 0.0f)]
    [InlineData(1.0f, 0.0f, 1.0f)]
    [InlineData(-1.0f, 0.0f, 1.0f)]
    [InlineData(3.0f, 4.0f, 5.0f)]
    public void TestVector3Length(float x, float y, float expectedLength)
    {
        var vector = new Vector3(x, y, 0.0f);
        Assert.Equal(expectedLength, vector.Length());
    }
}