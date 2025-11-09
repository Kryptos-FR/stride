// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using Xunit;

namespace Stride.Core.Mathematics.Tests;

public class TestQuaternion
{
    /* Note: As seen in the decomposition tests, we check both expectedQuat == decompedQuat and expectedQuat == -decompedQuat
     * This is because different combinations of yaw/pitch/roll can result in the same *orientation*, which is what we're actually testing.
     * This means that decomposing a rotation matrix or quaternion can actually have multiple answers, but we arbitrarily pick
     * one result, and this may not have actually been the original yaw/pitch/roll the user chose.
     */

    [Theory, ClassData(typeof(TestRotationsData.YRPTestData))]
    public void TestDecomposeYawPitchRollFromQuaternionYPR(float yawDegrees, float pitchDegrees, float rollDegrees)
    {
        var yawRadians = MathUtil.DegreesToRadians(yawDegrees);
        var pitchRadians = MathUtil.DegreesToRadians(pitchDegrees);
        var rollRadians = MathUtil.DegreesToRadians(rollDegrees);

        var rotQuat = Quaternion.RotationYawPitchRoll(yawRadians, pitchRadians, rollRadians);
        Quaternion.RotationYawPitchRoll(ref rotQuat, out float decomposedYaw, out float decomposedPitch, out float decomposedRoll);

        var expectedQuat = rotQuat;
        var decompedQuat = Quaternion.RotationYawPitchRoll(decomposedYaw, decomposedPitch, decomposedRoll);
        Assert.True(expectedQuat == decompedQuat || expectedQuat == -decompedQuat, $"Quat not equals: Expected: {expectedQuat} - Actual: {decompedQuat}");
    }

    [Theory, ClassData(typeof(TestRotationsData.YRPTestData))]
    public void TestDecomposeYawPitchRollFromQuaternionYXZ(float yawDegrees, float pitchDegrees, float rollDegrees)
    {
        var yawRadians = MathUtil.DegreesToRadians(yawDegrees);
        var pitchRadians = MathUtil.DegreesToRadians(pitchDegrees);
        var rollRadians = MathUtil.DegreesToRadians(rollDegrees);

        var rotX = Quaternion.RotationX(pitchRadians);
        var rotY = Quaternion.RotationY(yawRadians);
        var rotZ = Quaternion.RotationZ(rollRadians);
        // Yaw-Pitch-Roll is the intrinsic rotation order, so extrinsic is the reverse (ie. Z-X-Y)
        var rotQuat = rotZ * rotX * rotY;
        Quaternion.RotationYawPitchRoll(ref rotQuat, out float decomposedYaw, out float decomposedPitch, out float decomposedRoll);

        var expectedQuat = rotQuat;
        var decompRotX = Quaternion.RotationX(decomposedPitch);
        var decompRotY = Quaternion.RotationY(decomposedYaw);
        var decompRotZ = Quaternion.RotationZ(decomposedRoll);
        var decompedQuat = decompRotZ * decompRotX * decompRotY;
        Assert.True(expectedQuat == decompedQuat || expectedQuat == -decompedQuat, $"Quat not equals: Expected: {expectedQuat} - Actual: {decompedQuat}");
    }

    [Fact]
    public void TestQuaternionIdentity()
    {
        var identity = Quaternion.Identity;
        Assert.Equal(0.0f, identity.X);
        Assert.Equal(0.0f, identity.Y);
        Assert.Equal(0.0f, identity.Z);
        Assert.Equal(1.0f, identity.W);
    }

    [Fact]
    public void TestQuaternionZero()
    {
        var zero = Quaternion.Zero;
        Assert.Equal(0.0f, zero.X);
        Assert.Equal(0.0f, zero.Y);
        Assert.Equal(0.0f, zero.Z);
        Assert.Equal(0.0f, zero.W);
    }

    [Theory]
    [InlineData(1, 0, 0, 0, 1)]
    [InlineData(0, 1, 0, 0, 1)]
    [InlineData(0, 0, 1, 0, 1)]
    [InlineData(0, 0, 0, 1, 1)]
    [InlineData(1, 2, 3, 4, 5.477226f)]
    public void TestQuaternionLength(float x, float y, float z, float w, float expectedLength)
    {
        var quat = new Quaternion(x, y, z, w);
        Assert.Equal(expectedLength, quat.Length(), 3);
    }

    [Fact]
    public void TestQuaternionDotProduct()
    {
        var q1 = new Quaternion(1, 2, 3, 4);
        var q2 = new Quaternion(5, 6, 7, 8);
        var dot = Quaternion.Dot(q1, q2);
        Assert.Equal(70.0f, dot); // 1*5 + 2*6 + 3*7 + 4*8 = 5 + 12 + 21 + 32
    }

    [Fact]
    public void TestQuaternionConstruction()
    {
        // Test component constructor
        var q1 = new Quaternion(1, 2, 3, 4);
        Assert.Equal(1f, q1.X);
        Assert.Equal(2f, q1.Y);
        Assert.Equal(3f, q1.Z);
        Assert.Equal(4f, q1.W);

        // Test Vector3 + W constructor
        var q2 = new Quaternion(new Vector3(5, 6, 7), 8);
        Assert.Equal(5f, q2.X);
        Assert.Equal(6f, q2.Y);
        Assert.Equal(7f, q2.Z);
        Assert.Equal(8f, q2.W);

        // Test Vector4 constructor
        var q3 = new Quaternion(new Vector4(9, 10, 11, 12));
        Assert.Equal(9f, q3.X);
        Assert.Equal(10f, q3.Y);
        Assert.Equal(11f, q3.Z);
        Assert.Equal(12f, q3.W);

        // Test single value constructor
        var q4 = new Quaternion(3.5f);
        Assert.Equal(3.5f, q4.X);
        Assert.Equal(3.5f, q4.Y);
        Assert.Equal(3.5f, q4.Z);
        Assert.Equal(3.5f, q4.W);
    }

    [Fact]
    public void TestQuaternionAdd()
    {
        var q1 = new Quaternion(1, 2, 3, 4);
        var q2 = new Quaternion(5, 6, 7, 8);

        var result = q1 + q2;
        Assert.Equal(6f, result.X);
        Assert.Equal(8f, result.Y);
        Assert.Equal(10f, result.Z);
        Assert.Equal(12f, result.W);

        // Test static method
        var result2 = Quaternion.Add(q1, q2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestQuaternionSubtract()
    {
        var q1 = new Quaternion(10, 20, 30, 40);
        var q2 = new Quaternion(1, 2, 3, 4);

        var result = q1 - q2;
        Assert.Equal(9f, result.X);
        Assert.Equal(18f, result.Y);
        Assert.Equal(27f, result.Z);
        Assert.Equal(36f, result.W);

        // Test static method
        var result2 = Quaternion.Subtract(q1, q2);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestQuaternionMultiplyScalar()
    {
        var q = new Quaternion(1, 2, 3, 4);
        var result = q * 2.5f;

        Assert.Equal(2.5f, result.X);
        Assert.Equal(5f, result.Y);
        Assert.Equal(7.5f, result.Z);
        Assert.Equal(10f, result.W);

        // Test reverse order
        var result2 = 2.5f * q;
        Assert.Equal(result, result2);
    }

    [Fact]
    public void TestQuaternionMultiplyQuaternion()
    {
        var q1 = Quaternion.RotationY(MathUtil.PiOverTwo);
        var q2 = Quaternion.RotationZ(MathUtil.PiOverTwo);

        var result = q1 * q2;

        // Multiplying quaternions should compose rotations
        Assert.NotEqual(Quaternion.Identity, result);
        Assert.NotEqual(Quaternion.Zero, result);
    }

    [Fact]
    public void TestQuaternionNegate()
    {
        var q = new Quaternion(1, 2, 3, 4);
        var negated = -q;

        Assert.Equal(-1f, negated.X);
        Assert.Equal(-2f, negated.Y);
        Assert.Equal(-3f, negated.Z);
        Assert.Equal(-4f, negated.W);

        // Test static method
        var negated2 = Quaternion.Negate(q);
        Assert.Equal(negated, negated2);
    }

    [Fact]
    public void TestQuaternionConjugate()
    {
        var q = new Quaternion(1, 2, 3, 4);
        var conjugate = Quaternion.Conjugate(q);

        // Conjugate negates X, Y, Z but keeps W
        Assert.Equal(-1f, conjugate.X);
        Assert.Equal(-2f, conjugate.Y);
        Assert.Equal(-3f, conjugate.Z);
        Assert.Equal(4f, conjugate.W);
    }

    [Fact]
    public void TestQuaternionInvert()
    {
        var q = Quaternion.RotationY(MathUtil.PiOverFour);
        var inverted = Quaternion.Invert(q);

        // q * q^-1 should equal identity
        var product = q * inverted;

        Assert.Equal(Quaternion.Identity.X, product.X, 5);
        Assert.Equal(Quaternion.Identity.Y, product.Y, 5);
        Assert.Equal(Quaternion.Identity.Z, product.Z, 5);
        Assert.Equal(Quaternion.Identity.W, product.W, 5);
    }

    [Fact]
    public void TestQuaternionNormalize()
    {
        var q = new Quaternion(1, 2, 3, 4);
        var normalized = Quaternion.Normalize(q);

        // Normalized quaternion should have length 1
        var length = normalized.Length();
        Assert.Equal(1f, length, 5);
    }

    [Fact]
    public void TestQuaternionLengthSquared()
    {
        var q = new Quaternion(1, 2, 3, 4);
        var lengthSquared = q.LengthSquared();

        // 1² + 2² + 3² + 4² = 1 + 4 + 9 + 16 = 30
        Assert.Equal(30f, lengthSquared);
    }

    [Fact]
    public void TestQuaternionRotationX()
    {
        var angle = MathUtil.PiOverTwo;
        var q = Quaternion.RotationX(angle);

        // Rotation quaternions should have unit length
        Assert.Equal(1f, q.Length(), 5);

        // Apply rotation to a vector
        var v = new Vector3(0, 1, 0);
        var rotated = Vector3.Transform(v, q);

        // Rotating (0,1,0) by 90° around X should give approximately (0,0,1)
        Assert.Equal(0f, rotated.X, 5);
        Assert.Equal(0f, rotated.Y, 5);
        Assert.Equal(1f, rotated.Z, 5);
    }

    [Fact]
    public void TestQuaternionRotationY()
    {
        var angle = MathUtil.PiOverTwo;
        var q = Quaternion.RotationY(angle);

        Assert.Equal(1f, q.Length(), 5);

        var v = new Vector3(1, 0, 0);
        var rotated = Vector3.Transform(v, q);

        // Rotating (1,0,0) by 90° around Y should give approximately (0,0,-1)
        Assert.Equal(0f, rotated.X, 5);
        Assert.Equal(0f, rotated.Y, 5);
        Assert.Equal(-1f, rotated.Z, 5);
    }

    [Fact]
    public void TestQuaternionRotationZ()
    {
        var angle = MathUtil.PiOverTwo;
        var q = Quaternion.RotationZ(angle);

        Assert.Equal(1f, q.Length(), 5);

        var v = new Vector3(1, 0, 0);
        var rotated = Vector3.Transform(v, q);

        // Rotating (1,0,0) by 90° around Z should give approximately (0,1,0)
        Assert.Equal(0f, rotated.X, 5);
        Assert.Equal(1f, rotated.Y, 5);
        Assert.Equal(0f, rotated.Z, 5);
    }

    [Fact]
    public void TestQuaternionRotationAxis()
    {
        var axis = Vector3.UnitY;
        var angle = MathUtil.Pi;

        var q = Quaternion.RotationAxis(axis, angle);

        // Should be a unit quaternion
        Assert.Equal(1f, q.Length(), 5);

        // 180° rotation around Y should be equivalent to RotationY
        var qY = Quaternion.RotationY(angle);
        
        // Quaternions q and -q represent the same rotation
        Assert.True(
            (Math.Abs(q.X - qY.X) < 0.001f && Math.Abs(q.W - qY.W) < 0.001f) ||
            (Math.Abs(q.X + qY.X) < 0.001f && Math.Abs(q.W + qY.W) < 0.001f));
    }

    [Fact]
    public void TestQuaternionRotationMatrix()
    {
        var matrix = Matrix.RotationY(MathUtil.PiOverFour);
        var q = Quaternion.RotationMatrix(matrix);

        // Should be a unit quaternion
        Assert.Equal(1f, q.Length(), 5);

        // Convert back to matrix and compare
        var matrix2 = Matrix.RotationQuaternion(q);
        
        // Matrices should be approximately equal
        Assert.Equal(matrix.M11, matrix2.M11, 4);
        Assert.Equal(matrix.M22, matrix2.M22, 4);
        Assert.Equal(matrix.M33, matrix2.M33, 4);
    }

    [Fact]
    public void TestQuaternionLerp()
    {
        var q1 = Quaternion.Identity;
        var q2 = Quaternion.RotationY(MathUtil.Pi);

        // Lerp at 0 should return q1
        var lerp0 = Quaternion.Lerp(q1, q2, 0f);
        Assert.Equal(q1.X, lerp0.X, 5);
        Assert.Equal(q1.W, lerp0.W, 5);

        // Lerp at 1 should return q2 (or -q2, same rotation)
        var lerp1 = Quaternion.Lerp(q1, q2, 1f);
        Assert.True(
            (Math.Abs(lerp1.X - q2.X) < 0.001f && Math.Abs(lerp1.W - q2.W) < 0.001f) ||
            (Math.Abs(lerp1.X + q2.X) < 0.001f && Math.Abs(lerp1.W + q2.W) < 0.001f));

        // Lerp at 0.5 should be between them
        var lerp05 = Quaternion.Lerp(q1, q2, 0.5f);
        Assert.NotEqual(q1, lerp05);
        Assert.NotEqual(q2, lerp05);
    }

    [Fact]
    public void TestQuaternionSlerp()
    {
        var q1 = Quaternion.Identity;
        var q2 = Quaternion.RotationY(MathUtil.PiOverTwo);

        // Slerp at 0 should return q1
        var slerp0 = Quaternion.Slerp(q1, q2, 0f);
        Assert.Equal(q1.X, slerp0.X, 5);
        Assert.Equal(q1.W, slerp0.W, 5);

        // Slerp at 1 should return q2
        var slerp1 = Quaternion.Slerp(q1, q2, 1f);
        Assert.Equal(q2.X, slerp1.X, 5);
        Assert.Equal(q2.W, slerp1.W, 5);

        // Slerp provides smooth spherical interpolation
        var slerp05 = Quaternion.Slerp(q1, q2, 0.5f);
        Assert.Equal(1f, slerp05.Length(), 5); // Should maintain unit length
    }

    [Fact]
    public void TestQuaternionSquad()
    {
        var q1 = Quaternion.Identity;
        var q2 = Quaternion.RotationY(MathUtil.PiOverFour);
        var q3 = Quaternion.RotationY(MathUtil.PiOverTwo);
        var q4 = Quaternion.RotationY(MathUtil.Pi);

        var result = Quaternion.Squad(q1, q2, q3, q4, 0.5f);

        // Squad should produce smooth interpolation
        Assert.Equal(1f, result.Length(), 4); // Should be unit quaternion
    }

    [Fact]
    public void TestQuaternionBaryCentric()
    {
        var q1 = Quaternion.Identity;
        var q2 = Quaternion.RotationX(MathUtil.PiOverFour);
        var q3 = Quaternion.RotationY(MathUtil.PiOverFour);

        var result = Quaternion.Barycentric(q1, q2, q3, 0.33f, 0.33f);

        // Should produce a valid quaternion
        Assert.NotEqual(Quaternion.Zero, result);
    }

    [Fact]
    public void TestQuaternionEquality()
    {
        var q1 = new Quaternion(1, 2, 3, 4);
        var q2 = new Quaternion(1, 2, 3, 4);
        var q3 = new Quaternion(1, 2, 3, 5);

        Assert.True(q1 == q2);
        Assert.False(q1 == q3);
        Assert.True(q1 != q3);
        Assert.True(q1.Equals(q2));
        Assert.False(q1.Equals(q3));
    }

    [Fact]
    public void TestQuaternionGetHashCode()
    {
        var q1 = new Quaternion(1, 2, 3, 4);
        var q2 = new Quaternion(1, 2, 3, 4);

        Assert.Equal(q1.GetHashCode(), q2.GetHashCode());
    }

    [Fact]
    public void TestQuaternionToString()
    {
        var q = new Quaternion(1, 2, 3, 4);
        var str = q.ToString();

        Assert.NotNull(str);
        Assert.NotEmpty(str);
        Assert.Contains("1", str);
        Assert.Contains("2", str);
        Assert.Contains("3", str);
        Assert.Contains("4", str);
    }

    [Fact]
    public void TestQuaternionBetweenDirections()
    {
        var from = Vector3.UnitZ;
        var to = Vector3.UnitX;

        var q = Quaternion.BetweenDirections(from, to);

        // Apply rotation
        var rotated = Vector3.Transform(from, q);

        // Should rotate from to to
        Assert.Equal(to.X, rotated.X, 4);
        Assert.Equal(to.Y, rotated.Y, 4);
        Assert.Equal(to.Z, rotated.Z, 4);
    }

    [Fact]
    public void TestQuaternionIsIdentity()
    {
        Assert.True(Quaternion.Identity.IsIdentity);
        Assert.False(Quaternion.Zero.IsIdentity);
        Assert.False(new Quaternion(1, 2, 3, 4).IsIdentity);
        
        // Normalized identity should still be identity
        var q = new Quaternion(0, 0, 0, 2);
        var normalized = Quaternion.Normalize(q);
        Assert.True(normalized.IsIdentity);
    }

    [Fact]
    public void TestQuaternionAngleBetween()
    {
        var q1 = Quaternion.Identity;
        var q2 = Quaternion.RotationY(MathUtil.PiOverTwo);

        var angle = Quaternion.AngleBetween(q1, q2);

        // Angle should be approximately π/2
        Assert.Equal(MathUtil.PiOverTwo, angle, 4);
    }

    [Fact]
    public void TestQuaternionAngleProperty()
    {
        var axis = Vector3.UnitY;
        var angleIn = MathUtil.PiOverFour;
        
        var q = Quaternion.RotationAxis(axis, angleIn);
        var angleOut = q.Angle;

        // Angle property should return the rotation angle
        Assert.Equal(angleIn, angleOut, 4);
    }

    [Fact]
    public void TestQuaternionExponential()
    {
        var q = new Quaternion(0.1f, 0.2f, 0.3f, 0f);
        var exp = Quaternion.Exponential(q);

        // Exponential should produce a valid quaternion
        Assert.NotEqual(Quaternion.Zero, exp);
    }

    [Fact]
    public void TestQuaternionLogarithm()
    {
        var q = Quaternion.RotationY(MathUtil.PiOverFour);
        var log = Quaternion.Logarithm(q);

        // Log of a rotation should have W near 0
        Assert.Equal(0f, log.W, 4);
    }

    [Fact]
    public void TestQuaternionFromToNumeric()
    {
        var q = new Quaternion(1, 2, 3, 4);
        
        // Convert to System.Numerics.Quaternion
        var numeric = (System.Numerics.Quaternion)q;
        Assert.Equal(1f, numeric.X);
        Assert.Equal(2f, numeric.Y);
        Assert.Equal(3f, numeric.Z);
        Assert.Equal(4f, numeric.W);

        // Convert back
        var q2 = (Quaternion)numeric;
        Assert.Equal(q, q2);
    }
}
