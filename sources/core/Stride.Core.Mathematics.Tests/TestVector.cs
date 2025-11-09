// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Xunit;

namespace Stride.Core.Mathematics.Tests;

public class TestVector
{
    #region Vector2 Tests

    [Fact]
    public void TestVector2Construction()
    {
        var v1 = new Vector2(5.5f, 10.3f);
        Assert.Equal(5.5f, v1.X);
        Assert.Equal(10.3f, v1.Y);

        var v2 = new Vector2(7.7f);
        Assert.Equal(7.7f, v2.X);
        Assert.Equal(7.7f, v2.Y);
    }

    [Fact]
    public void TestVector2StaticFields()
    {
        Assert.Equal(0.0f, Vector2.Zero.X);
        Assert.Equal(0.0f, Vector2.Zero.Y);

        Assert.Equal(1.0f, Vector2.One.X);
        Assert.Equal(1.0f, Vector2.One.Y);

        Assert.Equal(1.0f, Vector2.UnitX.X);
        Assert.Equal(0.0f, Vector2.UnitX.Y);

        Assert.Equal(0.0f, Vector2.UnitY.X);
        Assert.Equal(1.0f, Vector2.UnitY.Y);
    }

    [Fact]
    public void TestVector2Addition()
    {
        var v1 = new Vector2(1.0f, 2.0f);
        var v2 = new Vector2(3.0f, 4.0f);
        var result = v1 + v2;
        Assert.Equal(4.0f, result.X);
        Assert.Equal(6.0f, result.Y);

        Vector2.Add(ref v1, ref v2, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestVector2Subtraction()
    {
        var v1 = new Vector2(3.0f, 5.0f);
        var v2 = new Vector2(1.0f, 2.0f);
        var result = v1 - v2;
        Assert.Equal(2.0f, result.X);
        Assert.Equal(3.0f, result.Y);

        Vector2.Subtract(ref v1, ref v2, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestVector2Multiplication()
    {
        var v = new Vector2(3.0f, 4.0f);
        var result = v * 2.5f;
        Assert.Equal(7.5f, result.X);
        Assert.Equal(10.0f, result.Y);

        var result2 = 2.5f * v;
        Assert.Equal(result, result2);

        var v1 = new Vector2(2.0f, 3.0f);
        var v2 = new Vector2(4.0f, 5.0f);
        var result3 = v1 * v2;
        Assert.Equal(8.0f, result3.X);
        Assert.Equal(15.0f, result3.Y);
    }

    [Fact]
    public void TestVector2Division()
    {
        var v = new Vector2(10.0f, 20.0f);
        var result = v / 2.0f;
        Assert.Equal(5.0f, result.X);
        Assert.Equal(10.0f, result.Y);

        var v1 = new Vector2(12.0f, 20.0f);
        var v2 = new Vector2(3.0f, 4.0f);
        var result2 = v1 / v2;
        Assert.Equal(4.0f, result2.X);
        Assert.Equal(5.0f, result2.Y);
    }

    [Fact]
    public void TestVector2Negation()
    {
        var v = new Vector2(3.5f, -5.2f);
        var result = -v;
        Assert.Equal(-3.5f, result.X);
        Assert.Equal(5.2f, result.Y);

        Vector2.Negate(ref v, out var result2);
        Assert.Equal(result, result2);
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
        Assert.Equal(1.0f, normalized.Length(), 3);

        v.Normalize();
        Assert.Equal(0.6f, v.X, 3);
        Assert.Equal(0.8f, v.Y, 3);
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
        Assert.Equal(expectedLength * expectedLength, vector.LengthSquared());
    }

    [Fact]
    public void TestVector2Distance()
    {
        var v1 = new Vector2(1.0f, 2.0f);
        var v2 = new Vector2(4.0f, 6.0f);
        var distance = Vector2.Distance(v1, v2);
        Assert.Equal(5.0f, distance); // sqrt(3^2 + 4^2)

        var distanceSq = Vector2.DistanceSquared(v1, v2);
        Assert.Equal(25.0f, distanceSq);
    }

    [Fact]
    public void TestVector2MinMax()
    {
        var v1 = new Vector2(1.0f, 5.0f);
        var v2 = new Vector2(3.0f, 2.0f);
        
        var min = Vector2.Min(v1, v2);
        Assert.Equal(1.0f, min.X);
        Assert.Equal(2.0f, min.Y);

        var max = Vector2.Max(v1, v2);
        Assert.Equal(3.0f, max.X);
        Assert.Equal(5.0f, max.Y);
    }

    [Fact]
    public void TestVector2Clamp()
    {
        var value = new Vector2(5.0f, -2.0f);
        var min = new Vector2(0.0f, 0.0f);
        var max = new Vector2(10.0f, 10.0f);
        
        var clamped = Vector2.Clamp(value, min, max);
        Assert.Equal(5.0f, clamped.X);
        Assert.Equal(0.0f, clamped.Y);
    }

    [Fact]
    public void TestVector2Lerp()
    {
        var v1 = new Vector2(0.0f, 0.0f);
        var v2 = new Vector2(10.0f, 20.0f);
        
        var lerp = Vector2.Lerp(v1, v2, 0.5f);
        Assert.Equal(5.0f, lerp.X);
        Assert.Equal(10.0f, lerp.Y);
    }

    [Fact]
    public void TestVector2Reflect()
    {
        var vector = new Vector2(1.0f, 1.0f);
        var normal = new Vector2(0.0f, 1.0f);
        
        var reflected = Vector2.Reflect(vector, normal);
        Assert.Equal(1.0f, reflected.X);
        Assert.Equal(-1.0f, reflected.Y);
    }

    [Fact]
    public void TestVector2Equality()
    {
        var v1 = new Vector2(3.5f, 4.2f);
        var v2 = new Vector2(3.5f, 4.2f);
        var v3 = new Vector2(5.1f, 6.3f);

        Assert.True(v1 == v2);
        Assert.False(v1 == v3);
        Assert.False(v1 != v2);
        Assert.True(v1 != v3);

        Assert.True(v1.Equals(v2));
        Assert.False(v1.Equals(v3));
    }

    #endregion

    #region Vector3 Tests

    [Fact]
    public void TestVector3Construction()
    {
        var v1 = new Vector3(5.5f, 10.3f, 15.7f);
        Assert.Equal(5.5f, v1.X);
        Assert.Equal(10.3f, v1.Y);
        Assert.Equal(15.7f, v1.Z);

        var v2 = new Vector3(7.7f);
        Assert.Equal(7.7f, v2.X);
        Assert.Equal(7.7f, v2.Y);
        Assert.Equal(7.7f, v2.Z);

        var v3 = new Vector3(new Vector2(2.0f, 3.0f), 4.0f);
        Assert.Equal(2.0f, v3.X);
        Assert.Equal(3.0f, v3.Y);
        Assert.Equal(4.0f, v3.Z);
    }

    [Fact]
    public void TestVector3StaticFields()
    {
        Assert.Equal(0.0f, Vector3.Zero.X);
        Assert.Equal(1.0f, Vector3.One.X);
        Assert.Equal(1.0f, Vector3.UnitX.X);
        Assert.Equal(0.0f, Vector3.UnitX.Y);
        Assert.Equal(0.0f, Vector3.UnitY.X);
        Assert.Equal(1.0f, Vector3.UnitY.Y);
        Assert.Equal(0.0f, Vector3.UnitZ.X);
        Assert.Equal(1.0f, Vector3.UnitZ.Z);
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

        Vector3.Add(ref v1, ref v2, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestVector3Subtraction()
    {
        var v1 = new Vector3(5.0f, 8.0f, 10.0f);
        var v2 = new Vector3(2.0f, 3.0f, 4.0f);
        var result = v1 - v2;
        Assert.Equal(3.0f, result.X);
        Assert.Equal(5.0f, result.Y);
        Assert.Equal(6.0f, result.Z);

        Vector3.Subtract(ref v1, ref v2, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestVector3Multiplication()
    {
        var v = new Vector3(3.0f, 4.0f, 5.0f);
        var result = v * 2.5f;
        Assert.Equal(7.5f, result.X);
        Assert.Equal(10.0f, result.Y);
        Assert.Equal(12.5f, result.Z);

        var result2 = 2.5f * v;
        Assert.Equal(result, result2);

        var v1 = new Vector3(2.0f, 3.0f, 4.0f);
        var v2 = new Vector3(5.0f, 6.0f, 7.0f);
        var result3 = v1 * v2;
        Assert.Equal(10.0f, result3.X);
        Assert.Equal(18.0f, result3.Y);
        Assert.Equal(28.0f, result3.Z);
    }

    [Fact]
    public void TestVector3Division()
    {
        var v = new Vector3(10.0f, 20.0f, 30.0f);
        var result = v / 2.0f;
        Assert.Equal(5.0f, result.X);
        Assert.Equal(10.0f, result.Y);
        Assert.Equal(15.0f, result.Z);

        var v1 = new Vector3(12.0f, 20.0f, 35.0f);
        var v2 = new Vector3(3.0f, 4.0f, 7.0f);
        var result2 = v1 / v2;
        Assert.Equal(4.0f, result2.X);
        Assert.Equal(5.0f, result2.Y);
        Assert.Equal(5.0f, result2.Z);
    }

    [Fact]
    public void TestVector3Negation()
    {
        var v = new Vector3(3.5f, -5.2f, 7.8f);
        var result = -v;
        Assert.Equal(-3.5f, result.X);
        Assert.Equal(5.2f, result.Y);
        Assert.Equal(-7.8f, result.Z);

        Vector3.Negate(ref v, out var result2);
        Assert.Equal(result, result2);
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
        Assert.Equal(1.0f, normalized.Length(), 3);

        v.Normalize();
        Assert.Equal(1.0f, v.Length(), 3);
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
        Assert.Equal(expectedLength * expectedLength, vector.LengthSquared());
    }

    [Fact]
    public void TestVector3Distance()
    {
        var v1 = new Vector3(1.0f, 2.0f, 3.0f);
        var v2 = new Vector3(4.0f, 6.0f, 3.0f);
        var distance = Vector3.Distance(v1, v2);
        Assert.Equal(5.0f, distance); // sqrt(3^2 + 4^2)

        var distanceSq = Vector3.DistanceSquared(v1, v2);
        Assert.Equal(25.0f, distanceSq);
    }

    [Fact]
    public void TestVector3MinMax()
    {
        var v1 = new Vector3(1.0f, 5.0f, 3.0f);
        var v2 = new Vector3(3.0f, 2.0f, 4.0f);
        
        var min = Vector3.Min(v1, v2);
        Assert.Equal(1.0f, min.X);
        Assert.Equal(2.0f, min.Y);
        Assert.Equal(3.0f, min.Z);

        var max = Vector3.Max(v1, v2);
        Assert.Equal(3.0f, max.X);
        Assert.Equal(5.0f, max.Y);
        Assert.Equal(4.0f, max.Z);
    }

    [Fact]
    public void TestVector3Clamp()
    {
        var value = new Vector3(5.0f, -2.0f, 15.0f);
        var min = new Vector3(0.0f, 0.0f, 0.0f);
        var max = new Vector3(10.0f, 10.0f, 10.0f);
        
        var clamped = Vector3.Clamp(value, min, max);
        Assert.Equal(5.0f, clamped.X);
        Assert.Equal(0.0f, clamped.Y);
        Assert.Equal(10.0f, clamped.Z);
    }

    [Fact]
    public void TestVector3Lerp()
    {
        var v1 = new Vector3(0.0f, 0.0f, 0.0f);
        var v2 = new Vector3(10.0f, 20.0f, 30.0f);
        
        var lerp = Vector3.Lerp(v1, v2, 0.5f);
        Assert.Equal(5.0f, lerp.X);
        Assert.Equal(10.0f, lerp.Y);
        Assert.Equal(15.0f, lerp.Z);
    }

    [Fact]
    public void TestVector3Reflect()
    {
        var vector = new Vector3(1.0f, 1.0f, 0.0f);
        var normal = new Vector3(0.0f, 1.0f, 0.0f);
        
        var reflected = Vector3.Reflect(vector, normal);
        Assert.Equal(1.0f, reflected.X);
        Assert.Equal(-1.0f, reflected.Y);
        Assert.Equal(0.0f, reflected.Z);
    }

    [Fact]
    public void TestVector3Equality()
    {
        var v1 = new Vector3(3.5f, 4.2f, 5.1f);
        var v2 = new Vector3(3.5f, 4.2f, 5.1f);
        var v3 = new Vector3(5.1f, 6.3f, 7.2f);

        Assert.True(v1 == v2);
        Assert.False(v1 == v3);
        Assert.False(v1 != v2);
        Assert.True(v1 != v3);

        Assert.True(v1.Equals(v2));
        Assert.False(v1.Equals(v3));
    }

    #endregion

    #region Vector4 Tests

    [Fact]
    public void TestVector4Construction()
    {
        var v1 = new Vector4(5.5f, 10.3f, 15.7f, 20.1f);
        Assert.Equal(5.5f, v1.X);
        Assert.Equal(10.3f, v1.Y);
        Assert.Equal(15.7f, v1.Z);
        Assert.Equal(20.1f, v1.W);

        var v2 = new Vector4(7.7f);
        Assert.Equal(7.7f, v2.X);
        Assert.Equal(7.7f, v2.Y);
        Assert.Equal(7.7f, v2.Z);
        Assert.Equal(7.7f, v2.W);

        var v3 = new Vector4(new Vector2(2.0f, 3.0f), 4.0f, 5.0f);
        Assert.Equal(2.0f, v3.X);
        Assert.Equal(3.0f, v3.Y);
        Assert.Equal(4.0f, v3.Z);
        Assert.Equal(5.0f, v3.W);

        var v4 = new Vector4(new Vector3(1.0f, 2.0f, 3.0f), 4.0f);
        Assert.Equal(1.0f, v4.X);
        Assert.Equal(2.0f, v4.Y);
        Assert.Equal(3.0f, v4.Z);
        Assert.Equal(4.0f, v4.W);
    }

    [Fact]
    public void TestVector4StaticFields()
    {
        Assert.Equal(0.0f, Vector4.Zero.X);
        Assert.Equal(1.0f, Vector4.One.X);
        Assert.Equal(1.0f, Vector4.UnitX.X);
        Assert.Equal(0.0f, Vector4.UnitX.Y);
        Assert.Equal(1.0f, Vector4.UnitY.Y);
        Assert.Equal(1.0f, Vector4.UnitZ.Z);
        Assert.Equal(1.0f, Vector4.UnitW.W);
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

        // Test subtraction
        var diff = v2 - v1;
        Assert.Equal(4.0f, diff.X);
        Assert.Equal(4.0f, diff.Y);
        Assert.Equal(4.0f, diff.Z);
        Assert.Equal(4.0f, diff.W);

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
        Assert.Equal(1.0f, normalized.Length(), 3);
    }

    [Fact]
    public void TestVector4Multiplication()
    {
        var v = new Vector4(3.0f, 4.0f, 5.0f, 6.0f);
        var result = v * 2.5f;
        Assert.Equal(7.5f, result.X);
        Assert.Equal(10.0f, result.Y);
        Assert.Equal(12.5f, result.Z);
        Assert.Equal(15.0f, result.W);

        var result2 = 2.5f * v;
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestVector4Division()
    {
        var v = new Vector4(10.0f, 20.0f, 30.0f, 40.0f);
        var result = v / 2.0f;
        Assert.Equal(5.0f, result.X);
        Assert.Equal(10.0f, result.Y);
        Assert.Equal(15.0f, result.Z);
        Assert.Equal(20.0f, result.W);
    }

    [Fact]
    public void TestVector4Negation()
    {
        var v = new Vector4(3.5f, -5.2f, 7.8f, -9.1f);
        var result = -v;
        Assert.Equal(-3.5f, result.X);
        Assert.Equal(5.2f, result.Y);
        Assert.Equal(-7.8f, result.Z);
        Assert.Equal(9.1f, result.W);

        Vector4.Negate(ref v, out var result2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestVector4Distance()
    {
        var v1 = new Vector4(1.0f, 2.0f, 3.0f, 4.0f);
        var v2 = new Vector4(4.0f, 6.0f, 3.0f, 4.0f);
        var distance = Vector4.Distance(v1, v2);
        Assert.Equal(5.0f, distance); // sqrt(3^2 + 4^2)

        var distanceSq = Vector4.DistanceSquared(v1, v2);
        Assert.Equal(25.0f, distanceSq);
    }

    [Fact]
    public void TestVector4MinMax()
    {
        var v1 = new Vector4(1.0f, 5.0f, 3.0f, 7.0f);
        var v2 = new Vector4(3.0f, 2.0f, 4.0f, 6.0f);
        
        var min = Vector4.Min(v1, v2);
        Assert.Equal(1.0f, min.X);
        Assert.Equal(2.0f, min.Y);
        Assert.Equal(3.0f, min.Z);
        Assert.Equal(6.0f, min.W);

        var max = Vector4.Max(v1, v2);
        Assert.Equal(3.0f, max.X);
        Assert.Equal(5.0f, max.Y);
        Assert.Equal(4.0f, max.Z);
        Assert.Equal(7.0f, max.W);
    }

    [Fact]
    public void TestVector4Clamp()
    {
        var value = new Vector4(5.0f, -2.0f, 15.0f, 8.0f);
        var min = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
        var max = new Vector4(10.0f, 10.0f, 10.0f, 10.0f);
        
        var clamped = Vector4.Clamp(value, min, max);
        Assert.Equal(5.0f, clamped.X);
        Assert.Equal(0.0f, clamped.Y);
        Assert.Equal(10.0f, clamped.Z);
        Assert.Equal(8.0f, clamped.W);
    }

    [Fact]
    public void TestVector4Lerp()
    {
        var v1 = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
        var v2 = new Vector4(10.0f, 20.0f, 30.0f, 40.0f);
        
        var lerp = Vector4.Lerp(v1, v2, 0.5f);
        Assert.Equal(5.0f, lerp.X);
        Assert.Equal(10.0f, lerp.Y);
        Assert.Equal(15.0f, lerp.Z);
        Assert.Equal(20.0f, lerp.W);
    }

    [Fact]
    public void TestVector4Equality()
    {
        var v1 = new Vector4(3.5f, 4.2f, 5.1f, 6.3f);
        var v2 = new Vector4(3.5f, 4.2f, 5.1f, 6.3f);
        var v3 = new Vector4(5.1f, 6.3f, 7.2f, 8.4f);

        Assert.True(v1 == v2);
        Assert.False(v1 == v3);
        Assert.False(v1 != v2);
        Assert.True(v1 != v3);

        Assert.True(v1.Equals(v2));
        Assert.False(v1.Equals(v3));
    }

    #endregion
}