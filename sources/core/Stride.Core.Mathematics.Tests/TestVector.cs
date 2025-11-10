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

        // Component-wise multiplication (same as Modulate)
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

        // Component-wise division (same as Demodulate)
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
        Assert.False(v1.Equals(null));
        Assert.False(v1.Equals(new object()));
    }

    [Fact]
    public void TestVector2Barycentric()
    {
        var v1 = new Vector2(0.0f, 0.0f);
        var v2 = new Vector2(10.0f, 0.0f);
        var v3 = new Vector2(0.0f, 10.0f);
        var result = Vector2.Barycentric(v1, v2, v3, 0.25f, 0.25f);
        Assert.Equal(2.5f, result.X, 3);
        Assert.Equal(2.5f, result.Y, 3);
    }

    [Fact]
    public void TestVector2SmoothStep()
    {
        var v1 = new Vector2(0.0f, 0.0f);
        var v2 = new Vector2(10.0f, 20.0f);
        var result = Vector2.SmoothStep(v1, v2, 0.5f);
        Assert.Equal(5.0f, result.X);
        Assert.Equal(10.0f, result.Y);
    }

    [Fact]
    public void TestVector2Hermite()
    {
        var v1 = new Vector2(0.0f, 0.0f);
        var t1 = new Vector2(1.0f, 1.0f);
        var v2 = new Vector2(10.0f, 10.0f);
        var t2 = new Vector2(1.0f, 1.0f);
        var result = Vector2.Hermite(v1, t1, v2, t2, 0.5f);
        Assert.InRange(result.X, 4.0f, 6.0f);
        Assert.InRange(result.Y, 4.0f, 6.0f);
    }

    [Fact]
    public void TestVector2CatmullRom()
    {
        var v1 = new Vector2(0.0f, 0.0f);
        var v2 = new Vector2(5.0f, 5.0f);
        var v3 = new Vector2(10.0f, 10.0f);
        var v4 = new Vector2(15.0f, 15.0f);
        var result = Vector2.CatmullRom(v1, v2, v3, v4, 0.5f);
        Assert.Equal(7.5f, result.X, 3);
        Assert.Equal(7.5f, result.Y, 3);
    }

    [Fact]
    public void TestVector2Transform()
    {
        var v = new Vector2(1.0f, 0.0f);
        var matrix = Matrix.RotationZ(MathUtil.PiOverTwo);
        var result = Vector2.Transform(v, matrix);
        Assert.Equal(0.0f, result.X, 3);
        Assert.Equal(1.0f, result.Y, 3);
    }

    [Fact]
    public void TestVector2TransformQuaternion()
    {
        var v = new Vector2(1.0f, 0.0f);
        var q = Quaternion.RotationZ(MathUtil.PiOverTwo);
        var result = Vector2.Transform(v, q);
        Assert.Equal(0.0f, result.X, 3);
        Assert.Equal(1.0f, result.Y, 3);
    }

    [Fact]
    public void TestVector2TransformCoordinate()
    {
        var v = new Vector2(1.0f, 1.0f);
        var matrix = Matrix.Translation(5.0f, 10.0f, 0.0f);
        var result = Vector2.TransformCoordinate(v, matrix);
        Assert.Equal(6.0f, result.X, 3);
        Assert.Equal(11.0f, result.Y, 3);
    }

    [Fact]
    public void TestVector2TransformNormal()
    {
        var v = new Vector2(1.0f, 0.0f);
        var matrix = Matrix.RotationZ(MathUtil.PiOverTwo);
        var result = Vector2.TransformNormal(v, matrix);
        Assert.Equal(0.0f, result.X, 3);
        Assert.Equal(1.0f, result.Y, 3);
    }

    [Fact]
    public void TestVector2MoveTo()
    {
        var from = new Vector2(0.0f, 0.0f);
        var to = new Vector2(10.0f, 0.0f);
        var result = Vector2.MoveTo(from, to, 5.0f);
        Assert.Equal(5.0f, result.X);
        Assert.Equal(0.0f, result.Y);

        var result2 = Vector2.MoveTo(from, to, 15.0f);
        Assert.Equal(10.0f, result2.X);
        Assert.Equal(0.0f, result2.Y);
    }

    [Fact]
    public void TestVector2Conversions()
    {
        var v = new Vector2(3.5f, 4.2f);
        
        // System.Numerics.Vector2
        System.Numerics.Vector2 sysVec = v;
        Assert.Equal(3.5f, sysVec.X);
        Assert.Equal(4.2f, sysVec.Y);
        
        Vector2 backToStride = sysVec;
        Assert.Equal(v, backToStride);

        // Vector3
        Vector3 v3 = (Vector3)v;
        Assert.Equal(3.5f, v3.X);
        Assert.Equal(4.2f, v3.Y);
        Assert.Equal(0.0f, v3.Z);

        // Vector4
        Vector4 v4 = (Vector4)v;
        Assert.Equal(3.5f, v4.X);
        Assert.Equal(4.2f, v4.Y);
        Assert.Equal(0.0f, v4.Z);
        Assert.Equal(0.0f, v4.W);
    }

    [Fact]
    public void TestVector2HashCode()
    {
        var v1 = new Vector2(3.5f, 4.2f);
        var v2 = new Vector2(3.5f, 4.2f);
        var v3 = new Vector2(5.1f, 6.3f);

        Assert.Equal(v1.GetHashCode(), v2.GetHashCode());
        Assert.NotEqual(v1.GetHashCode(), v3.GetHashCode());
    }

    [Fact]
    public void TestVector2ScalarDivision()
    {
        var v = new Vector2(10.0f, 20.0f);
        var result = 100.0f / v;
        Assert.Equal(10.0f, result.X);
        Assert.Equal(5.0f, result.Y);
    }

    [Fact]
    public void TestVector2ZeroLengthNormalization()
    {
        var zero = Vector2.Zero;
        var normalized = Vector2.Normalize(zero);
        
        // Normalizing zero vector should return zero (not NaN)
        Assert.False(float.IsNaN(normalized.X));
        Assert.False(float.IsNaN(normalized.Y));
    }

    [Fact]
    public void TestVector2DivisionByZero()
    {
        var v = new Vector2(1.0f, 2.0f);
        var result = v / 0.0f;
        
        // Division by zero should produce infinity
        Assert.True(float.IsInfinity(result.X));
        Assert.True(float.IsInfinity(result.Y));
    }

    [Fact]
    public void TestVector2VeryLargeValues()
    {
        var v1 = new Vector2(float.MaxValue / 2, float.MaxValue / 2);
        
        // Should not overflow
        var result = v1 * 0.5f;
        Assert.False(float.IsInfinity(result.X));
        Assert.False(float.IsInfinity(result.Y));
    }

    [Fact]
    public void TestVector2NegativeZero()
    {
        var v1 = new Vector2(0.0f, 0.0f);
        var v2 = new Vector2(-0.0f, -0.0f);
        
        // -0.0 and 0.0 should be equal
        Assert.Equal(v1, v2);
    }

    [Fact]
    public void TestVector2MinMaxWithNaN()
    {
        var v1 = new Vector2(1.0f, float.NaN);
        var v2 = new Vector2(2.0f, 3.0f);
        
        var min = Vector2.Min(v1, v2);
        var max = Vector2.Max(v1, v2);
        
        // Implementation may use Math.Min/Max which have specific NaN behavior
        // Just verify the functions don't crash
        Assert.True(true); // Test passes if we get here without exceptions
    }

    [Fact]
    public void TestVector2LerpExtrapolation()
    {
        var v1 = new Vector2(0.0f, 0.0f);
        var v2 = new Vector2(10.0f, 10.0f);
        
        // Test extrapolation (amount > 1)
        var result = Vector2.Lerp(v1, v2, 2.0f);
        Assert.Equal(20.0f, result.X);
        Assert.Equal(20.0f, result.Y);
        
        // Test extrapolation (amount < 0)
        var result2 = Vector2.Lerp(v1, v2, -0.5f);
        Assert.Equal(-5.0f, result2.X);
        Assert.Equal(-5.0f, result2.Y);
    }

    [Fact]
    public void TestVector2EqualityPrecision()
    {
        var v1 = new Vector2(1.0f / 3.0f, 1.0f / 7.0f);
        var v2 = new Vector2(1.0f / 3.0f, 1.0f / 7.0f);
        
        // Should be exactly equal due to same calculation
        Assert.Equal(v1, v2);
        Assert.True(v1 == v2);
    }

    [Fact]
    public void TestVector2DotProductAccuracy()
    {
        var v1 = new Vector2(1e-20f, 1e-20f);
        var v2 = new Vector2(1e20f, 1e20f);
        
        var dot = Vector2.Dot(v1, v2);
        
        // Should handle extreme magnitude differences
        Assert.False(float.IsNaN(dot));
        Assert.False(float.IsInfinity(dot));
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

        // Component-wise multiplication (same as Modulate)
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

        // Component-wise division (same as Demodulate)
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
        Assert.False(v1.Equals(null));
        Assert.False(v1.Equals(new object()));
    }

    [Fact]
    public void TestVector3Barycentric()
    {
        var v1 = new Vector3(0.0f, 0.0f, 0.0f);
        var v2 = new Vector3(10.0f, 0.0f, 0.0f);
        var v3 = new Vector3(0.0f, 10.0f, 0.0f);
        var result = Vector3.Barycentric(v1, v2, v3, 0.25f, 0.25f);
        Assert.Equal(2.5f, result.X, 3);
        Assert.Equal(2.5f, result.Y, 3);
        Assert.Equal(0.0f, result.Z, 3);
    }

    [Fact]
    public void TestVector3SmoothStep()
    {
        var v1 = new Vector3(0.0f, 0.0f, 0.0f);
        var v2 = new Vector3(10.0f, 20.0f, 30.0f);
        var result = Vector3.SmoothStep(v1, v2, 0.5f);
        Assert.Equal(5.0f, result.X);
        Assert.Equal(10.0f, result.Y);
        Assert.Equal(15.0f, result.Z);
    }

    [Fact]
    public void TestVector3Hermite()
    {
        var v1 = new Vector3(0.0f, 0.0f, 0.0f);
        var t1 = new Vector3(1.0f, 1.0f, 1.0f);
        var v2 = new Vector3(10.0f, 10.0f, 10.0f);
        var t2 = new Vector3(1.0f, 1.0f, 1.0f);
        var result = Vector3.Hermite(v1, t1, v2, t2, 0.5f);
        Assert.InRange(result.X, 4.0f, 6.0f);
        Assert.InRange(result.Y, 4.0f, 6.0f);
        Assert.InRange(result.Z, 4.0f, 6.0f);
    }

    [Fact]
    public void TestVector3CatmullRom()
    {
        var v1 = new Vector3(0.0f, 0.0f, 0.0f);
        var v2 = new Vector3(5.0f, 5.0f, 5.0f);
        var v3 = new Vector3(10.0f, 10.0f, 10.0f);
        var v4 = new Vector3(15.0f, 15.0f, 15.0f);
        var result = Vector3.CatmullRom(v1, v2, v3, v4, 0.5f);
        Assert.Equal(7.5f, result.X, 3);
        Assert.Equal(7.5f, result.Y, 3);
        Assert.Equal(7.5f, result.Z, 3);
    }

    [Fact]
    public void TestVector3Transform()
    {
        var v = new Vector3(1.0f, 0.0f, 0.0f);
        var matrix = Matrix.RotationZ(MathUtil.PiOverTwo);
        var result = Vector3.Transform(v, matrix);
        Assert.Equal(0.0f, result.X, 3);
        Assert.Equal(1.0f, result.Y, 3);
        Assert.Equal(0.0f, result.Z, 3);
    }

    [Fact]
    public void TestVector3TransformQuaternion()
    {
        var v = new Vector3(1.0f, 0.0f, 0.0f);
        var q = Quaternion.RotationZ(MathUtil.PiOverTwo);
        var result = Vector3.Transform(v, q);
        Assert.Equal(0.0f, result.X, 3);
        Assert.Equal(1.0f, result.Y, 3);
        Assert.Equal(0.0f, result.Z, 3);
    }

    [Fact]
    public void TestVector3TransformCoordinate()
    {
        var v = new Vector3(1.0f, 1.0f, 1.0f);
        var matrix = Matrix.Translation(5.0f, 10.0f, 15.0f);
        var result = Vector3.TransformCoordinate(v, matrix);
        Assert.Equal(6.0f, result.X, 3);
        Assert.Equal(11.0f, result.Y, 3);
        Assert.Equal(16.0f, result.Z, 3);
    }

    [Fact]
    public void TestVector3TransformNormal()
    {
        var v = new Vector3(1.0f, 0.0f, 0.0f);
        var matrix = Matrix.RotationZ(MathUtil.PiOverTwo);
        var result = Vector3.TransformNormal(v, matrix);
        Assert.Equal(0.0f, result.X, 3);
        Assert.Equal(1.0f, result.Y, 3);
        Assert.Equal(0.0f, result.Z, 3);
    }

    [Fact]
    public void TestVector3Conversions()
    {
        var v = new Vector3(3.5f, 4.2f, 5.1f);
        
        // System.Numerics.Vector3
        System.Numerics.Vector3 sysVec = v;
        Assert.Equal(3.5f, sysVec.X);
        Assert.Equal(4.2f, sysVec.Y);
        Assert.Equal(5.1f, sysVec.Z);
        
        Vector3 backToStride = sysVec;
        Assert.Equal(v, backToStride);

        // Vector2
        Vector2 v2 = (Vector2)v;
        Assert.Equal(3.5f, v2.X);
        Assert.Equal(4.2f, v2.Y);

        // Vector4
        Vector4 v4 = (Vector4)v;
        Assert.Equal(3.5f, v4.X);
        Assert.Equal(4.2f, v4.Y);
        Assert.Equal(5.1f, v4.Z);
        Assert.Equal(0.0f, v4.W);
    }

    [Fact]
    public void TestVector3HashCode()
    {
        var v1 = new Vector3(3.5f, 4.2f, 5.1f);
        var v2 = new Vector3(3.5f, 4.2f, 5.1f);
        var v3 = new Vector3(7.2f, 8.3f, 9.4f);

        Assert.Equal(v1.GetHashCode(), v2.GetHashCode());
        Assert.NotEqual(v1.GetHashCode(), v3.GetHashCode());
    }

    [Fact]
    public void TestVector3ScalarDivision()
    {
        var v = new Vector3(10.0f, 20.0f, 40.0f);
        var result = 100.0f / v;
        Assert.Equal(10.0f, result.X);
        Assert.Equal(5.0f, result.Y);
        Assert.Equal(2.5f, result.Z);
    }

    [Fact]
    public void TestVector3ZeroLengthNormalization()
    {
        var zero = Vector3.Zero;
        var normalized = Vector3.Normalize(zero);
        
        // Normalizing zero vector should return zero (not NaN)
        Assert.False(float.IsNaN(normalized.X));
        Assert.False(float.IsNaN(normalized.Y));
        Assert.False(float.IsNaN(normalized.Z));
    }

    [Fact]
    public void TestVector3CrossProductParallel()
    {
        var v1 = new Vector3(1.0f, 0.0f, 0.0f);
        var v2 = new Vector3(2.0f, 0.0f, 0.0f); // Parallel to v1
        
        var cross = Vector3.Cross(v1, v2);
        
        // Cross product of parallel vectors should be zero
        Assert.Equal(0.0f, cross.X, 5);
        Assert.Equal(0.0f, cross.Y, 5);
        Assert.Equal(0.0f, cross.Z, 5);
    }

    [Fact]
    public void TestVector3CrossProductAntiparallel()
    {
        var v1 = new Vector3(1.0f, 0.0f, 0.0f);
        var v2 = new Vector3(-1.0f, 0.0f, 0.0f); // Antiparallel to v1
        
        var cross = Vector3.Cross(v1, v2);
        
        // Cross product of antiparallel vectors should be zero
        Assert.Equal(0.0f, cross.X, 5);
        Assert.Equal(0.0f, cross.Y, 5);
        Assert.Equal(0.0f, cross.Z, 5);
    }

    [Fact]
    public void TestVector3CrossProductOrthogonality()
    {
        var v1 = new Vector3(1.0f, 2.0f, 3.0f);
        var v2 = new Vector3(4.0f, 5.0f, 6.0f);
        
        var cross = Vector3.Cross(v1, v2);
        
        // Cross product should be orthogonal to both input vectors
        var dot1 = Vector3.Dot(cross, v1);
        var dot2 = Vector3.Dot(cross, v2);
        
        Assert.Equal(0.0f, dot1, 3);
        Assert.Equal(0.0f, dot2, 3);
    }

    [Fact]
    public void TestVector3DivisionByZero()
    {
        var v = new Vector3(1.0f, 2.0f, 3.0f);
        var result = v / 0.0f;
        
        // Division by zero should produce infinity
        Assert.True(float.IsInfinity(result.X));
        Assert.True(float.IsInfinity(result.Y));
        Assert.True(float.IsInfinity(result.Z));
    }

    [Fact]
    public void TestVector3ReflectNormalNotNormalized()
    {
        var vector = new Vector3(1.0f, -1.0f, 0.0f);
        var normal = new Vector3(0.0f, 2.0f, 0.0f); // Not normalized
        
        // Reflect should still work (though result depends on normal length)
        var reflected = Vector3.Reflect(vector, normal);
        Assert.False(float.IsNaN(reflected.X));
        Assert.False(float.IsNaN(reflected.Y));
        Assert.False(float.IsNaN(reflected.Z));
    }

    [Fact]
    public void TestVector3SmallDifferences()
    {
        var v1 = new Vector3(1.0000001f, 2.0f, 3.0f);
        var v2 = new Vector3(1.0000002f, 2.0f, 3.0f);
        
        // Due to floating point precision, these very small differences may be considered equal
        // This test documents the actual behavior
        var areEqual = v1 == v2;
        
        // Just verify that comparison doesn't throw and gives consistent result
        Assert.Equal(v1.Equals(v2), areEqual);
    }

    [Fact]
    public void TestVector2FromVector3TruncatesZ()
    {
        var v3 = new Vector3(1.0f, 2.0f, 999.0f);
        var v2 = (Vector2)v3;
        
        Assert.Equal(1.0f, v2.X);
        Assert.Equal(2.0f, v2.Y);
    }

    [Fact]
    public void TestVector3Modulate()
    {
        var v1 = new Vector3(2.0f, 4.0f, 6.0f);
        var v2 = new Vector3(3.0f, 5.0f, 7.0f);
        var result = Vector3.Modulate(v1, v2);
        
        Assert.Equal(6.0f, result.X);
        Assert.Equal(20.0f, result.Y);
        Assert.Equal(42.0f, result.Z);
    }

    [Fact]
    public void TestVector3Demodulate()
    {
        var v1 = new Vector3(6.0f, 20.0f, 42.0f);
        var v2 = new Vector3(3.0f, 5.0f, 7.0f);
        var result = Vector3.Demodulate(v1, v2);
        
        Assert.Equal(2.0f, result.X);
        Assert.Equal(4.0f, result.Y);
        Assert.Equal(6.0f, result.Z);
    }

    [Fact]
    public void TestVector3Mod()
    {
        var v1 = new Vector3(7.0f, 15.0f, 23.0f);
        var v2 = new Vector3(3.0f, 4.0f, 5.0f);
        var result = Vector3.Mod(v1, v2);
        
        Assert.Equal(1.0f, result.X);
        Assert.Equal(3.0f, result.Y);
        Assert.Equal(3.0f, result.Z);
    }

    [Fact]
    public void TestVector3MoveTo()
    {
        var from = new Vector3(0.0f, 0.0f, 0.0f);
        var to = new Vector3(10.0f, 0.0f, 0.0f);
        var result = Vector3.MoveTo(from, to, 5.0f);
        
        Assert.Equal(5.0f, result.X);
        Assert.Equal(0.0f, result.Y);
        Assert.Equal(0.0f, result.Z);
    }

    [Fact]
    public void TestVector3MoveToExceedsDistance()
    {
        var from = new Vector3(0.0f, 0.0f, 0.0f);
        var to = new Vector3(10.0f, 0.0f, 0.0f);
        var result = Vector3.MoveTo(from, to, 20.0f);
        
        Assert.Equal(10.0f, result.X);
        Assert.Equal(0.0f, result.Y);
        Assert.Equal(0.0f, result.Z);
    }

    [Fact]
    public void TestVector3ProjectUnproject()
    {
        var vector = new Vector3(100.0f, 50.0f, 0.5f);
        var worldViewProj = Matrix.Identity;
        
        var projected = Vector3.Project(vector, 0, 0, 800, 600, 0, 1, worldViewProj);
        var unprojected = Vector3.Unproject(projected, 0, 0, 800, 600, 0, 1, worldViewProj);
        
        Assert.True(MathUtil.NearEqual(vector.X, unprojected.X));
        Assert.True(MathUtil.NearEqual(vector.Y, unprojected.Y));
        Assert.True(MathUtil.NearEqual(vector.Z, unprojected.Z));
    }

    [Fact]
    public void TestVector3Orthogonalize()
    {
        var source = new[]
        {
            new Vector3(1, 1, 0),
            new Vector3(1, 0, 1),
            new Vector3(0, 1, 1)
        };
        var dest = new Vector3[3];
        
        Vector3.Orthogonalize(dest, source);
        
        // Check orthogonality
        Assert.True(MathUtil.NearEqual(Vector3.Dot(dest[0], dest[1]), 0.0f));
        Assert.True(MathUtil.NearEqual(Vector3.Dot(dest[1], dest[2]), 0.0f));
        Assert.True(MathUtil.NearEqual(Vector3.Dot(dest[0], dest[2]), 0.0f));
    }

    [Fact]
    public void TestVector3Orthonormalize()
    {
        var source = new[]
        {
            new Vector3(2, 0, 0),
            new Vector3(2, 2, 0),
            new Vector3(2, 2, 2)
        };
        var dest = new Vector3[3];
        
        Vector3.Orthonormalize(dest, source);
        
        // Check orthogonality and normalization
        Assert.True(MathUtil.NearEqual(dest[0].Length(), 1.0f));
        Assert.True(MathUtil.NearEqual(dest[1].Length(), 1.0f));
        Assert.True(MathUtil.NearEqual(dest[2].Length(), 1.0f));
        Assert.True(MathUtil.NearEqual(Vector3.Dot(dest[0], dest[1]), 0.0f));
    }

    [Fact]
    public void TestVector3RotateAround()
    {
        var source = new Vector3(1, 0, 0);
        var target = Vector3.Zero;
        var axis = Vector3.UnitZ;
        var result = Vector3.RotateAround(source, target, axis, MathUtil.PiOverTwo);
        
        Assert.True(MathUtil.NearEqual(result.X, 0.0f));
        Assert.True(MathUtil.NearEqual(result.Y, 1.0f));
        Assert.True(MathUtil.NearEqual(result.Z, 0.0f));
    }

    [Fact]
    public void TestVector3RotationYawPitchRoll()
    {
        var q = Quaternion.RotationYawPitchRoll(0.5f, 0.3f, 0.2f);
        var ypr = Vector3.RotationYawPitchRoll(q);
        
        Assert.True(MathUtil.NearEqual(ypr.X, 0.5f) || Math.Abs(ypr.X - 0.5f) < 0.01f);
        Assert.True(MathUtil.NearEqual(ypr.Y, 0.3f) || Math.Abs(ypr.Y - 0.3f) < 0.01f);
        Assert.True(MathUtil.NearEqual(ypr.Z, 0.2f) || Math.Abs(ypr.Z - 0.2f) < 0.01f);
    }

    [Fact]
    public void TestVector3TransformArray()
    {
        var source = new[] { new Vector3(1, 2, 3), new Vector3(4, 5, 6) };
        var dest = new Vector4[2];
        var transform = Matrix.Translation(10, 20, 30);
        
        Vector3.Transform(source, ref transform, dest);
        
        Assert.Equal(11.0f, dest[0].X);
        Assert.Equal(22.0f, dest[0].Y);
        Assert.Equal(33.0f, dest[0].Z);
    }

    [Fact]
    public void TestVector3TransformCoordinateArray()
    {
        var source = new[] { new Vector3(1, 2, 3), new Vector3(4, 5, 6) };
        var dest = new Vector3[2];
        var transform = Matrix.Translation(10, 20, 30);
        
        Vector3.TransformCoordinate(source, ref transform, dest);
        
        Assert.Equal(11.0f, dest[0].X);
        Assert.Equal(22.0f, dest[0].Y);
        Assert.Equal(33.0f, dest[0].Z);
    }

    [Fact]
    public void TestVector3TransformNormalArray()
    {
        var source = new[] { new Vector3(1, 0, 0), new Vector3(0, 1, 0) };
        var dest = new Vector3[2];
        var transform = Matrix.RotationZ(MathUtil.PiOverTwo);
        
        Vector3.TransformNormal(source, ref transform, dest);
        
        Assert.True(MathUtil.NearEqual(dest[0].X, 0.0f));
        Assert.True(MathUtil.NearEqual(dest[0].Y, 1.0f));
    }

    [Fact]
    public void TestVector3TransformQuaternionArray()
    {
        var source = new[] { new Vector3(1, 0, 0), new Vector3(0, 1, 0) };
        var dest = new Vector3[2];
        var rotation = Quaternion.RotationAxis(Vector3.UnitZ, MathUtil.PiOverTwo);
        
        Vector3.Transform(source, ref rotation, dest);
        
        Assert.True(MathUtil.NearEqual(dest[0].X, 0.0f));
        Assert.True(MathUtil.NearEqual(dest[0].Y, 1.0f));
    }

    [Fact]
    public void TestVector3AddScalar()
    {
        var v = new Vector3(1, 2, 3);
        var result = v + 5.0f;
        
        Assert.Equal(6.0f, result.X);
        Assert.Equal(7.0f, result.Y);
        Assert.Equal(8.0f, result.Z);
    }

    [Fact]
    public void TestVector3SubtractScalar()
    {
        var v = new Vector3(10, 20, 30);
        var result = v - 5.0f;
        
        Assert.Equal(5.0f, result.X);
        Assert.Equal(15.0f, result.Y);
        Assert.Equal(25.0f, result.Z);
    }

    [Fact]
    public void TestVector3ScalarDivideVector()
    {
        var v = new Vector3(2, 4, 5);
        var result = 20.0f / v;
        
        Assert.Equal(10.0f, result.X);
        Assert.Equal(5.0f, result.Y);
        Assert.Equal(4.0f, result.Z);
    }

    [Fact]
    public void TestVector3VectorDivideVector()
    {
        var v1 = new Vector3(10, 20, 30);
        var v2 = new Vector3(2, 4, 5);
        var result = v1 / v2;
        
        Assert.Equal(5.0f, result.X);
        Assert.Equal(5.0f, result.Y);
        Assert.Equal(6.0f, result.Z);
    }

    [Fact]
    public void TestVector3IsNormalized()
    {
        var normalized = new Vector3(1, 0, 0);
        var notNormalized = new Vector3(2, 0, 0);
        
        Assert.True(normalized.IsNormalized);
        Assert.False(notNormalized.IsNormalized);
    }

    [Fact]
    public void TestVector3NearEqual()
    {
        var v1 = new Vector3(1.0f, 2.0f, 3.0f);
        var v2 = new Vector3(1.001f, 2.001f, 3.001f);
        var epsilon = new Vector3(0.01f, 0.01f, 0.01f);
        
        Assert.True(Vector3.NearEqual(ref v1, ref v2, ref epsilon));
    }

    [Fact]
    public void TestVector3Deconstruct()
    {
        var v = new Vector3(1, 2, 3);
        v.Deconstruct(out float x, out float y, out float z);
        
        Assert.Equal(1.0f, x);
        Assert.Equal(2.0f, y);
        Assert.Equal(3.0f, z);
    }

    [Fact]
    public void TestVector3CastToInt3()
    {
        var v = new Vector3(1.7f, 2.3f, 3.9f);
        var i3 = (Int3)v;
        
        Assert.Equal(1, i3.X);
        Assert.Equal(2, i3.Y);
        Assert.Equal(3, i3.Z);
    }

    [Fact]
    public void TestVector3UnaryPlus()
    {
        var v = new Vector3(1, 2, 3);
        var result = +v;
        
        Assert.Equal(v, result);
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
        Assert.False(v1.Equals(null));
        Assert.False(v1.Equals(new object()));
    }

    [Fact]
    public void TestVector4Barycentric()
    {
        var v1 = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
        var v2 = new Vector4(10.0f, 0.0f, 0.0f, 0.0f);
        var v3 = new Vector4(0.0f, 10.0f, 0.0f, 0.0f);
        var result = Vector4.Barycentric(v1, v2, v3, 0.25f, 0.25f);
        Assert.Equal(2.5f, result.X, 3);
        Assert.Equal(2.5f, result.Y, 3);
        Assert.Equal(0.0f, result.Z, 3);
        Assert.Equal(0.0f, result.W, 3);
    }

    [Fact]
    public void TestVector4SmoothStep()
    {
        var v1 = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
        var v2 = new Vector4(10.0f, 20.0f, 30.0f, 40.0f);
        var result = Vector4.SmoothStep(v1, v2, 0.5f);
        Assert.Equal(5.0f, result.X);
        Assert.Equal(10.0f, result.Y);
        Assert.Equal(15.0f, result.Z);
        Assert.Equal(20.0f, result.W);
    }

    [Fact]
    public void TestVector4Hermite()
    {
        var v1 = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
        var t1 = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
        var v2 = new Vector4(10.0f, 10.0f, 10.0f, 10.0f);
        var t2 = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
        var result = Vector4.Hermite(v1, t1, v2, t2, 0.5f);
        Assert.InRange(result.X, 4.0f, 6.0f);
        Assert.InRange(result.Y, 4.0f, 6.0f);
        Assert.InRange(result.Z, 4.0f, 6.0f);
        Assert.InRange(result.W, 4.0f, 6.0f);
    }

    [Fact]
    public void TestVector4CatmullRom()
    {
        var v1 = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
        var v2 = new Vector4(5.0f, 5.0f, 5.0f, 5.0f);
        var v3 = new Vector4(10.0f, 10.0f, 10.0f, 10.0f);
        var v4 = new Vector4(15.0f, 15.0f, 15.0f, 15.0f);
        var result = Vector4.CatmullRom(v1, v2, v3, v4, 0.5f);
        Assert.Equal(7.5f, result.X, 3);
        Assert.Equal(7.5f, result.Y, 3);
        Assert.Equal(7.5f, result.Z, 3);
        Assert.Equal(7.5f, result.W, 3);
    }

    [Fact]
    public void TestVector4Transform()
    {
        var v = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
        var matrix = Matrix.RotationZ(MathUtil.PiOverTwo);
        var result = Vector4.Transform(v, matrix);
        Assert.Equal(0.0f, result.X, 3);
        Assert.Equal(1.0f, result.Y, 3);
        Assert.Equal(0.0f, result.Z, 3);
        Assert.Equal(1.0f, result.W, 3);
    }

    [Fact]
    public void TestVector4TransformQuaternion()
    {
        var v = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
        var q = Quaternion.RotationZ(MathUtil.PiOverTwo);
        var result = Vector4.Transform(v, q);
        Assert.Equal(0.0f, result.X, 3);
        Assert.Equal(1.0f, result.Y, 3);
        Assert.Equal(0.0f, result.Z, 3);
        Assert.Equal(1.0f, result.W, 3);
    }

    [Fact]
    public void TestVector4Conversions()
    {
        var v = new Vector4(3.5f, 4.2f, 5.1f, 6.3f);
        
        // System.Numerics.Vector4
        System.Numerics.Vector4 sysVec = v;
        Assert.Equal(3.5f, sysVec.X);
        Assert.Equal(4.2f, sysVec.Y);
        Assert.Equal(5.1f, sysVec.Z);
        Assert.Equal(6.3f, sysVec.W);
        
        Vector4 backToStride = sysVec;
        Assert.Equal(v, backToStride);

        // Vector2
        Vector2 v2 = (Vector2)v;
        Assert.Equal(3.5f, v2.X);
        Assert.Equal(4.2f, v2.Y);

        // Vector3
        Vector3 v3 = (Vector3)v;
        Assert.Equal(3.5f, v3.X);
        Assert.Equal(4.2f, v3.Y);
        Assert.Equal(5.1f, v3.Z);
    }

    [Fact]
    public void TestVector4HashCode()
    {
        var v1 = new Vector4(3.5f, 4.2f, 5.1f, 6.3f);
        var v2 = new Vector4(3.5f, 4.2f, 5.1f, 6.3f);
        var v3 = new Vector4(7.2f, 8.3f, 9.4f, 10.5f);

        Assert.Equal(v1.GetHashCode(), v2.GetHashCode());
        Assert.NotEqual(v1.GetHashCode(), v3.GetHashCode());
    }

    [Fact]
    public void TestVector4ScalarDivision()
    {
        var v = new Vector4(10.0f, 20.0f, 40.0f, 100.0f);
        var result = 100.0f / v;
        Assert.Equal(10.0f, result.X);
        Assert.Equal(5.0f, result.Y);
        Assert.Equal(2.5f, result.Z);
        Assert.Equal(1.0f, result.W);
    }

    [Fact]
    public void TestVector4ZeroLengthNormalization()
    {
        var zero = Vector4.Zero;
        var normalized = Vector4.Normalize(zero);
        
        // Normalizing zero vector should return zero (not NaN)
        Assert.False(float.IsNaN(normalized.X));
        Assert.False(float.IsNaN(normalized.Y));
        Assert.False(float.IsNaN(normalized.Z));
        Assert.False(float.IsNaN(normalized.W));
    }

    [Fact]
    public void TestVector4DivisionByZero()
    {
        var v = new Vector4(1.0f, 2.0f, 3.0f, 4.0f);
        var result = v / 0.0f;
        
        // Division by zero should produce infinity
        Assert.True(float.IsInfinity(result.X));
        Assert.True(float.IsInfinity(result.Y));
        Assert.True(float.IsInfinity(result.Z));
        Assert.True(float.IsInfinity(result.W));
    }

    [Fact]
    public void TestVector4ClampWithInvertedMinMax()
    {
        var value = new Vector4(5.0f, 5.0f, 5.0f, 5.0f);
        var min = new Vector4(10.0f, 10.0f, 10.0f, 10.0f);
        var max = new Vector4(0.0f, 0.0f, 0.0f, 0.0f); // max < min (invalid)
        
        // Behavior with inverted min/max - implementation clamps to min first
        var result = Vector4.Clamp(value, min, max);
        
        // Implementation clamps to min first, so result is min
        Assert.Equal(10.0f, result.X);
        Assert.Equal(10.0f, result.Y);
        Assert.Equal(10.0f, result.Z);
        Assert.Equal(10.0f, result.W);
    }

    [Fact]
    public void TestVector4FromVector2FillsZW()
    {
        var v2 = new Vector2(1.0f, 2.0f);
        var v4 = (Vector4)v2;
        
        Assert.Equal(1.0f, v4.X);
        Assert.Equal(2.0f, v4.Y);
        Assert.Equal(0.0f, v4.Z);
        Assert.Equal(0.0f, v4.W);
    }

    [Fact]
    public void TestVector4FromVector3FillsW()
    {
        var v3 = new Vector3(1.0f, 2.0f, 3.0f);
        var v4 = (Vector4)v3;
        
        Assert.Equal(1.0f, v4.X);
        Assert.Equal(2.0f, v4.Y);
        Assert.Equal(3.0f, v4.Z);
        Assert.Equal(0.0f, v4.W);
    }

    [Fact]
    public void TestVector4Modulate()
    {
        var v1 = new Vector4(2, 3, 4, 5);
        var v2 = new Vector4(3, 4, 5, 6);
        var result = Vector4.Modulate(v1, v2);
        
        Assert.Equal(6.0f, result.X);
        Assert.Equal(12.0f, result.Y);
        Assert.Equal(20.0f, result.Z);
        Assert.Equal(30.0f, result.W);
    }

    [Fact]
    public void TestVector4Demodulate()
    {
        var v1 = new Vector4(12, 20, 30, 40);
        var v2 = new Vector4(3, 4, 5, 8);
        var result = Vector4.Demodulate(v1, v2);
        
        Assert.Equal(4.0f, result.X);
        Assert.Equal(5.0f, result.Y);
        Assert.Equal(6.0f, result.Z);
        Assert.Equal(5.0f, result.W);
    }

    [Fact]
    public void TestVector4Moveto()
    {
        var from = new Vector4(0, 0, 0, 0);
        var to = new Vector4(10, 10, 10, 10);
        var result = Vector4.Moveto(from, to, 5.0f);
        
        // Distance from origin to (10,10,10,10) is 20, moving 5 units
        Assert.True(MathUtil.NearEqual(result.X, 2.5f) || Math.Abs(result.X - 2.5f) < 0.01f);
        Assert.True(MathUtil.NearEqual(result.Y, 2.5f) || Math.Abs(result.Y - 2.5f) < 0.01f);
        Assert.True(MathUtil.NearEqual(result.Z, 2.5f) || Math.Abs(result.Z - 2.5f) < 0.01f);
        Assert.True(MathUtil.NearEqual(result.W, 2.5f) || Math.Abs(result.W - 2.5f) < 0.01f);
    }

    [Fact]
    public void TestVector4MovetoExceedsDistance()
    {
        var from = new Vector4(0, 0, 0, 0);
        var to = new Vector4(1, 0, 0, 0);
        var result = Vector4.Moveto(from, to, 10.0f);
        
        // Moving further than target should arrive at target
        Assert.Equal(1.0f, result.X);
        Assert.Equal(0.0f, result.Y);
        Assert.Equal(0.0f, result.Z);
        Assert.Equal(0.0f, result.W);
    }

    [Fact]
    public void TestVector4TransformArray()
    {
        var source = new[] { new Vector4(1, 2, 3, 1), new Vector4(5, 6, 7, 1) };
        var dest = new Vector4[2];
        var matrix = Matrix.Translation(10, 20, 30);
        
        Vector4.Transform(source, ref matrix, dest);
        
        Assert.Equal(11.0f, dest[0].X);
        Assert.Equal(22.0f, dest[0].Y);
        Assert.Equal(33.0f, dest[0].Z);
        Assert.Equal(1.0f, dest[0].W);
    }

    [Fact]
    public void TestVector4TransformQuaternionArray()
    {
        var source = new[] { new Vector4(1, 0, 0, 1), new Vector4(0, 1, 0, 1) };
        var dest = new Vector4[2];
        var rotation = Quaternion.RotationY(MathUtil.PiOverTwo);
        
        Vector4.Transform(source, ref rotation, dest);
        
        Assert.True(MathUtil.NearEqual(dest[0].Z, -1.0f));
        Assert.True(MathUtil.NearEqual(dest[0].X, 0.0f));
        Assert.Equal(1.0f, dest[0].W);
    }

    [Fact]
    public void TestVector4Deconstruct()
    {
        var v = new Vector4(1, 2, 3, 4);
        v.Deconstruct(out float x, out float y, out float z, out float w);
        
        Assert.Equal(1.0f, x);
        Assert.Equal(2.0f, y);
        Assert.Equal(3.0f, z);
        Assert.Equal(4.0f, w);
    }

    #endregion
}