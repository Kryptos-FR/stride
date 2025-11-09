// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Xunit;

namespace Stride.Core.Mathematics.Tests;

public class TestQuaternionOperations
{
    private const float Epsilon = 1e-6f;

    [Fact]
    public void TestQuaternionMultiplication()
    {
        // Test identity quaternion multiplication
        var identity = Quaternion.Identity;
        var rotation = Quaternion.RotationY(MathUtil.PiOverTwo);
        
        var result1 = identity * rotation;
        var result2 = rotation * identity;
        
        // Multiplication with identity should not change the quaternion
        Assert.Equal(rotation.X, result1.X, 3);
        Assert.Equal(rotation.Y, result1.Y, 3);
        Assert.Equal(rotation.Z, result1.Z, 3);
        Assert.Equal(rotation.W, result1.W, 3);
        
        Assert.Equal(rotation.X, result2.X, 3);
        Assert.Equal(rotation.Y, result2.Y, 3);
        Assert.Equal(rotation.Z, result2.Z, 3);
        Assert.Equal(rotation.W, result2.W, 3);
        
        // Test that quaternion multiplication properly combines rotations
        var v = Vector3.UnitX;
        var rotated = Vector3.Transform(v, rotation);
        var doubleRotated = Vector3.Transform(rotated, rotation);
        
        // Single 90-degree rotation around Y should take (1,0,0) to (0,0,-1)
        Assert.Equal(0.0f, rotated.X, 3);
        Assert.Equal(0.0f, rotated.Y, 3);
        Assert.Equal(-1.0f, rotated.Z, 3);
        
        // Two 90-degree rotations around Y should take (1,0,0) to (-1,0,0)
        Assert.Equal(-1.0f, doubleRotated.X, 3);
        Assert.Equal(0.0f, doubleRotated.Y, 3);
        Assert.Equal(0.0f, doubleRotated.Z, 3);
        
        // Test that multiplication of quaternions equals applying rotations in sequence
        var doubleRotation = rotation * rotation;
        var doubleRotatedDirect = Vector3.Transform(v, doubleRotation);
        
        Assert.Equal(doubleRotated.X, doubleRotatedDirect.X, 3);
        Assert.Equal(doubleRotated.Y, doubleRotatedDirect.Y, 3);
        Assert.Equal(doubleRotated.Z, doubleRotatedDirect.Z, 3);
    }

    [Fact]
    public void TestQuaternionVectorRotation()
    {
        // Create a quaternion that rotates 90 degrees around Y axis
        var rotation = Quaternion.RotationAxis(Vector3.UnitY, -MathUtil.PiOverTwo); // Negative for clockwise rotation
        
        // Rotate a vector pointing along Z axis
        var vector = Vector3.UnitZ;
        var rotated = Vector3.Transform(vector, rotation);

        // After 90 degree clockwise Y rotation, Z should point along negative X
        Assert.Equal(-1.0f, rotated.X, 3);
        Assert.Equal(0.0f, rotated.Y, 3);
        Assert.Equal(0.0f, rotated.Z, 3);
    }

    [Fact]
    public void TestQuaternionSlerp()
    {
        // Create two quaternions 90 degrees apart
        var start = Quaternion.Identity;
        var end = Quaternion.RotationAxis(Vector3.UnitY, -MathUtil.PiOverTwo); // Negative for clockwise rotation

        // Test interpolation at different points
        var halfway = Quaternion.Slerp(start, end, 0.5f);
        var quarterWay = Quaternion.Slerp(start, end, 0.25f);

        // Test interpolated rotations on a vector
        var vector = Vector3.UnitZ;
        var halfwayRotated = Vector3.Transform(vector, halfway);
        var quarterWayRotated = Vector3.Transform(vector, quarterWay);

        // At halfway point (45 degrees), rotated vector should be at (-0.707, 0, 0.707)
        Assert.Equal(-0.707f, halfwayRotated.X, 3);
        Assert.Equal(0.0f, halfwayRotated.Y, 3);
        Assert.Equal(0.707f, halfwayRotated.Z, 3);

        // At quarter way point (22.5 degrees), rotated vector should be at (-0.383, 0, 0.924)
        Assert.Equal(-0.383f, quarterWayRotated.X, 3);
        Assert.Equal(0.0f, quarterWayRotated.Y, 3);
        Assert.Equal(0.924f, quarterWayRotated.Z, 3);
    }

    [Theory]
    [InlineData(0, 0, 0)]      // Identity
    [InlineData(90, 0, 0)]     // Pure X rotation
    [InlineData(0, 90, 0)]     // Pure Y rotation
    [InlineData(0, 0, 90)]     // Pure Z rotation
    [InlineData(45, 45, 45)]   // Combined rotation
    public void TestQuaternionEulerConversion(float pitchDegrees, float yawDegrees, float rollDegrees)
    {
        var pitch = MathUtil.DegreesToRadians(pitchDegrees);
        var yaw = MathUtil.DegreesToRadians(yawDegrees);
        var roll = MathUtil.DegreesToRadians(rollDegrees);

        // Create quaternion from euler angles
        var quat = Quaternion.RotationYawPitchRoll(yaw, pitch, roll);
        
        // Create matrix from both quaternion and euler angles directly
        var matFromQuat = Matrix.RotationQuaternion(quat);
        var matFromEuler = Matrix.RotationYawPitchRoll(yaw, pitch, roll);

        // Test vectors to verify rotations
        var vectors = new[]
        {
            Vector3.UnitX,
            Vector3.UnitY,
            Vector3.UnitZ,
            new Vector3(1, 1, 1)
        };

        foreach (var vector in vectors)
        {
            var rotatedByQuat = Vector3.Transform(vector, matFromQuat);
            var rotatedByEuler = Vector3.Transform(vector, matFromEuler);

            // Both rotations should produce the same result
            Assert.Equal(rotatedByQuat.X, rotatedByEuler.X, 3);
            Assert.Equal(rotatedByQuat.Y, rotatedByEuler.Y, 3);
            Assert.Equal(rotatedByQuat.Z, rotatedByEuler.Z, 3);
        }
    }

    [Fact]
    public void TestQuaternionNormalization()
    {
        // Create a non-normalized quaternion
        var q = new Quaternion(2.0f, 3.0f, 4.0f, 5.0f);
        var normalized = Quaternion.Normalize(q);

        // Length should be 1
        var length = (float)Math.Sqrt(
            normalized.X * normalized.X +
            normalized.Y * normalized.Y +
            normalized.Z * normalized.Z +
            normalized.W * normalized.W
        );
        Assert.Equal(1.0f, length, 3);

        // Original ratios should be preserved
        var ratio = 2.0f / 3.0f;
        Assert.Equal(ratio, normalized.X / normalized.Y, 3);
    }

    [Fact]
    public void TestQuaternionInverse()
    {
        var q = Quaternion.RotationAxis(Vector3.UnitY, MathUtil.PiOverFour);
        Quaternion.Invert(ref q, out var inverse);

        // q * q^-1 should equal identity
        var product = q * inverse;
        Assert.Equal(0.0f, product.X, Epsilon);
        Assert.Equal(0.0f, product.Y, Epsilon);
        Assert.Equal(0.0f, product.Z, Epsilon);
        Assert.Equal(1.0f, product.W, Epsilon);

        // Test inverse rotation on a vector
        var vector = new Vector3(1, 0, 1);
        var rotated = Vector3.Transform(vector, q);
        var unrotated = Vector3.Transform(rotated, inverse);

        // Should get back original vector
        Assert.Equal(vector.X, unrotated.X, 3);
        Assert.Equal(vector.Y, unrotated.Y, 3);
        Assert.Equal(vector.Z, unrotated.Z, 3);
    }

    [Fact]
    public void TestQuaternionToRotationMatrix()
    {
        var q = Quaternion.RotationYawPitchRoll(
            MathUtil.DegreesToRadians(30),
            MathUtil.DegreesToRadians(45),
            MathUtil.DegreesToRadians(60)
        );

        var matrix = Matrix.RotationQuaternion(q);
        
        // Test that both quaternion and matrix rotate a vector the same way
        var vector = new Vector3(1, 2, 3);
        var rotatedByQuaternion = Vector3.Transform(vector, q);
        var rotatedByMatrix = Vector3.Transform(vector, matrix);

        Assert.Equal(rotatedByQuaternion.X, rotatedByMatrix.X, 3);
        Assert.Equal(rotatedByQuaternion.Y, rotatedByMatrix.Y, 3);
        Assert.Equal(rotatedByQuaternion.Z, rotatedByMatrix.Z, 3);
    }
}