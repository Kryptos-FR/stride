// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Xunit;

namespace Stride.Core.Mathematics.Tests;

public class TestMatrixOperations
{
    [Fact]
    public void TestMatrixMultiplication()
    {
        var m1 = new Matrix(
            1, 2, 3, 0,
            4, 5, 6, 0,
            7, 8, 9, 0,
            0, 0, 0, 1);

        var m2 = new Matrix(
            2, 0, 0, 0,
            0, 2, 0, 0,
            0, 0, 2, 0,
            0, 0, 0, 1);

        var result = m1 * m2;

        Assert.Equal(2f, result.M11);
        Assert.Equal(4f, result.M12);
        Assert.Equal(6f, result.M13);
        Assert.Equal(8f, result.M21);
        Assert.Equal(10f, result.M22);
        Assert.Equal(12f, result.M23);
        Assert.Equal(14f, result.M31);
        Assert.Equal(16f, result.M32);
        Assert.Equal(18f, result.M33);
        Assert.Equal(1f, result.M44);
    }

    [Fact]
    public void TestMatrixVectorMultiplication()
    {
        var matrix = new Matrix(
            2, 0, 0, 0,
            0, 3, 0, 0,
            0, 0, 4, 0,
            1, 2, 3, 1);

        var vector = new Vector3(1, 1, 1);
        var result = Vector3.Transform(vector, matrix);

        Assert.Equal(3f, result.X); // 2*1 + 0*1 + 0*1 + 1*1
        Assert.Equal(5f, result.Y); // 0*1 + 3*1 + 0*1 + 2*1
        Assert.Equal(7f, result.Z); // 0*1 + 0*1 + 4*1 + 3*1
    }

    [Fact]
    public void TestMatrixDeterminant()
    {
        var matrix = new Matrix(
            1, 0, 0, 0,
            0, 2, 0, 0,
            0, 0, 3, 0,
            0, 0, 0, 1);

        float det = matrix.Determinant();
        Assert.Equal(6f, det); // 1 * 2 * 3 * 1
    }

    [Fact]
    public void TestMatrixInverse()
    {
        var matrix = new Matrix(
            2, 0, 0, 0,
            0, 2, 0, 0,
            0, 0, 2, 0,
            1, 2, 3, 1);

        Matrix.Invert(ref matrix, out var inverse);
        var identity = matrix * inverse;

        // Check if the result is approximately identity matrix
        Assert.Equal(1f, identity.M11, 3);
        Assert.Equal(0f, identity.M12, 3);
        Assert.Equal(0f, identity.M13, 3);
        Assert.Equal(0f, identity.M14, 3);
        Assert.Equal(0f, identity.M21, 3);
        Assert.Equal(1f, identity.M22, 3);
        Assert.Equal(0f, identity.M23, 3);
        Assert.Equal(0f, identity.M24, 3);
        Assert.Equal(0f, identity.M31, 3);
        Assert.Equal(0f, identity.M32, 3);
        Assert.Equal(1f, identity.M33, 3);
        Assert.Equal(0f, identity.M34, 3);
        Assert.Equal(0f, identity.M41, 3);
        Assert.Equal(0f, identity.M42, 3);
        Assert.Equal(0f, identity.M43, 3);
        Assert.Equal(1f, identity.M44, 3);
    }

    [Fact]
    public void TestMatrixTranspose()
    {
        var matrix = new Matrix(
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16);

        Matrix.Transpose(ref matrix, out var transpose);

        // Check diagonal elements remain the same
        Assert.Equal(matrix.M11, transpose.M11);
        Assert.Equal(matrix.M22, transpose.M22);
        Assert.Equal(matrix.M33, transpose.M33);
        Assert.Equal(matrix.M44, transpose.M44);

        // Check off-diagonal elements are swapped
        Assert.Equal(matrix.M12, transpose.M21);
        Assert.Equal(matrix.M13, transpose.M31);
        Assert.Equal(matrix.M14, transpose.M41);
        Assert.Equal(matrix.M21, transpose.M12);
        Assert.Equal(matrix.M23, transpose.M32);
        Assert.Equal(matrix.M24, transpose.M42);
        Assert.Equal(matrix.M31, transpose.M13);
        Assert.Equal(matrix.M32, transpose.M23);
        Assert.Equal(matrix.M34, transpose.M43);
        Assert.Equal(matrix.M41, transpose.M14);
        Assert.Equal(matrix.M42, transpose.M24);
        Assert.Equal(matrix.M43, transpose.M34);

        // Verify double transpose returns original matrix
        Matrix.Transpose(ref transpose, out var doubleTranspose);
        Assert.Equal(matrix, doubleTranspose);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(90, 0, 0)]
    [InlineData(0, 90, 0)]
    [InlineData(0, 0, 90)]
    [InlineData(45, 45, 45)]
    public void TestRotationMatrixDecomposition(float x, float y, float z)
    {
        var rotationMatrix = Matrix.RotationX(MathUtil.DegreesToRadians(x)) *
                           Matrix.RotationY(MathUtil.DegreesToRadians(y)) *
                           Matrix.RotationZ(MathUtil.DegreesToRadians(z));

        rotationMatrix.Decompose(out Vector3 scale, out Quaternion rotation, out Vector3 translation);

        // Check scale is approximately unit
        Assert.Equal(1f, scale.X, 3);
        Assert.Equal(1f, scale.Y, 3);
        Assert.Equal(1f, scale.Z, 3);

        // Check translation is zero
        Assert.Equal(0f, translation.X, 3);
        Assert.Equal(0f, translation.Y, 3);
        Assert.Equal(0f, translation.Z, 3);

        // Reconstruct matrix from decomposed parts and compare
        var reconstructed = Matrix.Transformation(Vector3.Zero, Quaternion.Identity, scale, Vector3.Zero, rotation, translation);
        
        Assert.Equal(rotationMatrix.M11, reconstructed.M11, 3);
        Assert.Equal(rotationMatrix.M12, reconstructed.M12, 3);
        Assert.Equal(rotationMatrix.M13, reconstructed.M13, 3);
        Assert.Equal(rotationMatrix.M21, reconstructed.M21, 3);
        Assert.Equal(rotationMatrix.M22, reconstructed.M22, 3);
        Assert.Equal(rotationMatrix.M23, reconstructed.M23, 3);
        Assert.Equal(rotationMatrix.M31, reconstructed.M31, 3);
        Assert.Equal(rotationMatrix.M32, reconstructed.M32, 3);
        Assert.Equal(rotationMatrix.M33, reconstructed.M33, 3);
    }

    [Fact]
    public void TestMatrixTransformation()
    {
        var scale = new Vector3(2, 3, 4);
        var rotation = Quaternion.RotationYawPitchRoll(
            MathUtil.DegreesToRadians(30),
            MathUtil.DegreesToRadians(45),
            MathUtil.DegreesToRadians(60)
        );
        var translation = new Vector3(1, 2, 3);

        var transform = Matrix.Transformation(
            Vector3.Zero,    // scaling center
            Quaternion.Identity,  // scaling rotation
            scale,          // scale
            Vector3.Zero,    // rotation center
            rotation,       // rotation
            translation    // translation
        );

        // Test transformation of a point
        var point = new Vector3(1, 1, 1);
        var transformed = Vector3.Transform(point, transform);

        // The point should be:
        // 1. Scaled
        // 2. Rotated
        // 3. Translated

        // Verify the transformation by doing it step by step
        var scaled = new Vector3(
            point.X * scale.X,
            point.Y * scale.Y,
            point.Z * scale.Z
        );

        var rotated = Vector3.Transform(scaled, rotation);
        var final = rotated + translation;

        Assert.Equal(final.X, transformed.X, 3);
        Assert.Equal(final.Y, transformed.Y, 3);
        Assert.Equal(final.Z, transformed.Z, 3);
    }

    [Fact]
    public void TestMatrixScaling()
    {
        var scale = new Vector3(2, 3, 4);
        var scaleMatrix = Matrix.Scaling(scale);

        // Test scaling of a point
        var point = new Vector3(1, 1, 1);
        var scaled = Vector3.Transform(point, scaleMatrix);

        Assert.Equal(2f, scaled.X);
        Assert.Equal(3f, scaled.Y);
        Assert.Equal(4f, scaled.Z);
    }

    [Fact]
    public void TestMatrixConstruction()
    {
        // Test value constructor
        var m1 = new Matrix(2.0f);
        Assert.Equal(2.0f, m1.M11);
        Assert.Equal(2.0f, m1.M22);
        Assert.Equal(2.0f, m1.M33);
        Assert.Equal(2.0f, m1.M44);

        // Test component constructor
        var m2 = new Matrix(
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16);
        
        Assert.Equal(1f, m2.M11);
        Assert.Equal(2f, m2.M12);
        Assert.Equal(16f, m2.M44);

        // Test array constructor
        float[] values = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        var m3 = new Matrix(values);
        Assert.Equal(1f, m3.M11);
        Assert.Equal(16f, m3.M44);
    }

    [Fact]
    public void TestMatrixStaticFields()
    {
        // Test Zero matrix
        Assert.Equal(0f, Matrix.Zero.M11);
        Assert.Equal(0f, Matrix.Zero.M22);
        Assert.Equal(0f, Matrix.Zero.M33);
        Assert.Equal(0f, Matrix.Zero.M44);

        // Test Identity matrix
        Assert.Equal(1f, Matrix.Identity.M11);
        Assert.Equal(1f, Matrix.Identity.M22);
        Assert.Equal(1f, Matrix.Identity.M33);
        Assert.Equal(1f, Matrix.Identity.M44);
        Assert.Equal(0f, Matrix.Identity.M12);
        Assert.Equal(0f, Matrix.Identity.M41);
        
        // Test IsIdentity property
        Assert.True(Matrix.Identity.IsIdentity);
        Assert.False(Matrix.Zero.IsIdentity);
    }

    [Fact]
    public void TestMatrixTranslation()
    {
        var translation = new Vector3(10, 20, 30);
        var matrix = Matrix.Translation(translation);

        Assert.Equal(10f, matrix.M41);
        Assert.Equal(20f, matrix.M42);
        Assert.Equal(30f, matrix.M43);
        Assert.Equal(1f, matrix.M44);

        // Test TranslationVector property
        Assert.Equal(translation, matrix.TranslationVector);

        // Test transforming a point
        var point = new Vector3(1, 2, 3);
        var transformed = Vector3.Transform(point, matrix);
        Assert.Equal(11f, transformed.X);
        Assert.Equal(22f, transformed.Y);
        Assert.Equal(33f, transformed.Z);
    }

    [Fact]
    public void TestMatrixScalingVariants()
    {
        // Test Scaling(x, y, z)
        var m1 = Matrix.Scaling(2, 3, 4);
        Assert.Equal(2f, m1.M11);
        Assert.Equal(3f, m1.M22);
        Assert.Equal(4f, m1.M33);

        // Test Scaling(uniform)
        var m2 = Matrix.Scaling(5f);
        Assert.Equal(5f, m2.M11);
        Assert.Equal(5f, m2.M22);
        Assert.Equal(5f, m2.M33);

        // Test Scaling(Vector3)
        var m3 = Matrix.Scaling(new Vector3(6, 7, 8));
        Assert.Equal(6f, m3.M11);
        Assert.Equal(7f, m3.M22);
        Assert.Equal(8f, m3.M33);
    }

    [Fact]
    public void TestMatrixRotationX()
    {
        var angle = MathUtil.Pi / 2; // 90 degrees
        var matrix = Matrix.RotationX(angle);

        // Rotate point (0, 1, 0) around X-axis by 90 degrees
        // Should result in approximately (0, 0, 1)
        var point = new Vector3(0, 1, 0);
        var rotated = Vector3.Transform(point, matrix);

        Assert.Equal(0f, rotated.X, 5);
        Assert.Equal(0f, rotated.Y, 5);
        Assert.Equal(1f, rotated.Z, 5);
    }

    [Fact]
    public void TestMatrixRotationY()
    {
        var angle = MathUtil.Pi / 2; // 90 degrees
        var matrix = Matrix.RotationY(angle);

        // Rotate point (1, 0, 0) around Y-axis by 90 degrees
        // Should result in approximately (0, 0, -1)
        var point = new Vector3(1, 0, 0);
        var rotated = Vector3.Transform(point, matrix);

        Assert.Equal(0f, rotated.X, 5);
        Assert.Equal(0f, rotated.Y, 5);
        Assert.Equal(-1f, rotated.Z, 5);
    }

    [Fact]
    public void TestMatrixRotationZ()
    {
        var angle = MathUtil.Pi / 2; // 90 degrees
        var matrix = Matrix.RotationZ(angle);

        // Rotate point (1, 0, 0) around Z-axis by 90 degrees
        // Should result in approximately (0, 1, 0)
        var point = new Vector3(1, 0, 0);
        var rotated = Vector3.Transform(point, matrix);

        Assert.Equal(0f, rotated.X, 5);
        Assert.Equal(1f, rotated.Y, 5);
        Assert.Equal(0f, rotated.Z, 5);
    }

    [Fact]
    public void TestMatrixRowProperties()
    {
        var matrix = new Matrix(
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16);

        Assert.Equal(new Vector4(1, 2, 3, 4), matrix.Row1);
        Assert.Equal(new Vector4(5, 6, 7, 8), matrix.Row2);
        Assert.Equal(new Vector4(9, 10, 11, 12), matrix.Row3);
        Assert.Equal(new Vector4(13, 14, 15, 16), matrix.Row4);

        // Test setting rows
        matrix.Row1 = new Vector4(10, 20, 30, 40);
        Assert.Equal(10f, matrix.M11);
        Assert.Equal(20f, matrix.M12);
        Assert.Equal(30f, matrix.M13);
        Assert.Equal(40f, matrix.M14);
    }

    [Fact]
    public void TestMatrixColumnProperties()
    {
        var matrix = new Matrix(
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16);

        Assert.Equal(new Vector4(1, 5, 9, 13), matrix.Column1);
        Assert.Equal(new Vector4(2, 6, 10, 14), matrix.Column2);
        Assert.Equal(new Vector4(3, 7, 11, 15), matrix.Column3);
        Assert.Equal(new Vector4(4, 8, 12, 16), matrix.Column4);

        // Test setting columns
        matrix.Column1 = new Vector4(100, 200, 300, 400);
        Assert.Equal(100f, matrix.M11);
        Assert.Equal(200f, matrix.M21);
        Assert.Equal(300f, matrix.M31);
        Assert.Equal(400f, matrix.M41);
    }

    [Fact]
    public void TestMatrixAddition()
    {
        var m1 = new Matrix(
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16);

        var m2 = new Matrix(
            16, 15, 14, 13,
            12, 11, 10, 9,
            8, 7, 6, 5,
            4, 3, 2, 1);

        var result = m1 + m2;

        Assert.Equal(17f, result.M11);
        Assert.Equal(17f, result.M12);
        Assert.Equal(17f, result.M22);
        Assert.Equal(17f, result.M44);
    }

    [Fact]
    public void TestMatrixSubtraction()
    {
        var m1 = new Matrix(
            10, 20, 30, 40,
            50, 60, 70, 80,
            90, 100, 110, 120,
            130, 140, 150, 160);

        var m2 = new Matrix(
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16);

        var result = m1 - m2;

        Assert.Equal(9f, result.M11);
        Assert.Equal(18f, result.M12);
        Assert.Equal(54f, result.M22);
        Assert.Equal(144f, result.M44);
    }

    [Fact]
    public void TestMatrixNegation()
    {
        var matrix = new Matrix(
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16);

        var negated = -matrix;

        Assert.Equal(-1f, negated.M11);
        Assert.Equal(-2f, negated.M12);
        Assert.Equal(-16f, negated.M44);
    }

    [Fact]
    public void TestMatrixScalarMultiplication()
    {
        var matrix = new Matrix(
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16);

        var result = matrix * 2f;

        Assert.Equal(2f, result.M11);
        Assert.Equal(4f, result.M12);
        Assert.Equal(32f, result.M44);

        // Test reverse order
        var result2 = 3f * matrix;
        Assert.Equal(3f, result2.M11);
        Assert.Equal(6f, result2.M12);
        Assert.Equal(48f, result2.M44);
    }

    [Fact]
    public void TestMatrixScalarDivision()
    {
        var matrix = new Matrix(
            2, 4, 6, 8,
            10, 12, 14, 16,
            18, 20, 22, 24,
            26, 28, 30, 32);

        var result = matrix / 2f;

        Assert.Equal(1f, result.M11);
        Assert.Equal(2f, result.M12);
        Assert.Equal(16f, result.M44);
    }

    [Fact]
    public void TestMatrixEquality()
    {
        var m1 = new Matrix(
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16);

        var m2 = new Matrix(
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16);

        var m3 = new Matrix(
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 17);

        Assert.True(m1 == m2);
        Assert.False(m1 == m3);
        Assert.True(m1 != m3);
        Assert.True(m1.Equals(m2));
        Assert.False(m1.Equals(m3));
    }

    [Fact]
    public void TestMatrixGetHashCode()
    {
        var m1 = new Matrix(
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16);

        var m2 = new Matrix(
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16);

        Assert.Equal(m1.GetHashCode(), m2.GetHashCode());
    }

    [Fact]
    public void TestMatrixToString()
    {
        var matrix = new Matrix(
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16);

        string str = matrix.ToString();
        Assert.NotNull(str);
        Assert.NotEmpty(str);
    }

    [Fact]
    public void TestMatrixLerp()
    {
        var m1 = Matrix.Identity;
        var m2 = Matrix.Scaling(2f);

        var result = Matrix.Lerp(m1, m2, 0.5f);

        // At 50%, diagonal should be 1.5 (midpoint between 1 and 2)
        Assert.Equal(1.5f, result.M11, 5);
        Assert.Equal(1.5f, result.M22, 5);
        Assert.Equal(1.5f, result.M33, 5);
        Assert.Equal(1f, result.M44, 5);
    }

    [Fact]
    public void TestMatrixPerspectiveFovLH()
    {
        float fov = MathUtil.PiOverFour; // 45 degrees
        float aspect = 16f / 9f;
        float znear = 0.1f;
        float zfar = 1000f;

        var matrix = Matrix.PerspectiveFovLH(fov, aspect, znear, zfar);

        // Basic sanity checks - perspective matrix should have specific structure
        Assert.NotEqual(0f, matrix.M11);
        Assert.NotEqual(0f, matrix.M22);
        Assert.NotEqual(0f, matrix.M33);
        Assert.Equal(0f, matrix.M44);
    }

    [Fact]
    public void TestMatrixPerspectiveFovRH()
    {
        float fov = MathUtil.PiOverFour;
        float aspect = 16f / 9f;
        float znear = 0.1f;
        float zfar = 1000f;

        var matrix = Matrix.PerspectiveFovRH(fov, aspect, znear, zfar);

        // RH should have different sign conventions than LH
        Assert.NotEqual(0f, matrix.M11);
        Assert.NotEqual(0f, matrix.M22);
        Assert.NotEqual(0f, matrix.M33);
        Assert.Equal(0f, matrix.M44);
    }

    [Fact]
    public void TestMatrixOrthoLH()
    {
        float width = 800f;
        float height = 600f;
        float znear = 0.1f;
        float zfar = 1000f;

        var matrix = Matrix.OrthoLH(width, height, znear, zfar);

        // Orthographic projection should have 1 in M44
        Assert.Equal(1f, matrix.M44);
        Assert.NotEqual(0f, matrix.M11);
        Assert.NotEqual(0f, matrix.M22);
        Assert.NotEqual(0f, matrix.M33);
    }

    [Fact]
    public void TestMatrixLookAtLH()
    {
        var eye = new Vector3(0, 0, -5);
        var target = new Vector3(0, 0, 0);
        var up = new Vector3(0, 1, 0);

        var matrix = Matrix.LookAtLH(eye, target, up);

        // LookAt matrix should transform eye position to origin in view space
        // The matrix should be invertible (non-zero determinant)
        Assert.NotEqual(0f, matrix.Determinant());
    }

    [Fact]
    public void TestMatrixDecomposeScaleRotationTranslation()
    {
        var originalScale = new Vector3(2, 3, 4);
        var originalRotation = Quaternion.RotationYawPitchRoll(0.1f, 0.2f, 0.3f);
        var originalTranslation = new Vector3(10, 20, 30);

        var matrix = Matrix.Transformation(
            Vector3.Zero, Quaternion.Identity, originalScale,
            Vector3.Zero, originalRotation, originalTranslation);

        bool success = matrix.Decompose(out Vector3 scale, out Quaternion rotation, out Vector3 translation);

        Assert.True(success);
        Assert.Equal(originalScale.X, scale.X, 3);
        Assert.Equal(originalScale.Y, scale.Y, 3);
        Assert.Equal(originalScale.Z, scale.Z, 3);
        Assert.Equal(originalTranslation.X, translation.X, 3);
        Assert.Equal(originalTranslation.Y, translation.Y, 3);
        Assert.Equal(originalTranslation.Z, translation.Z, 3);
        
        // Quaternions can be equivalent even with opposite signs
        Assert.True(
            Quaternion.Dot(originalRotation, rotation) > 0.999f ||
            Quaternion.Dot(originalRotation, -rotation) > 0.999f);
    }

    [Fact]
    public void TestMatrixRotationAxis()
    {
        var axis = Vector3.UnitY;
        var angle = MathUtil.Pi / 2; // 90 degrees

        var matrix = Matrix.RotationAxis(axis, angle);

        // Rotate (1, 0, 0) around Y-axis by 90 degrees
        // Should result in approximately (0, 0, -1)
        var point = new Vector3(1, 0, 0);
        var rotated = Vector3.Transform(point, matrix);

        Assert.Equal(0f, rotated.X, 5);
        Assert.Equal(0f, rotated.Y, 5);
        Assert.Equal(-1f, rotated.Z, 5);
    }

    [Fact]
    public void TestMatrixRotationQuaternion()
    {
        var quaternion = Quaternion.RotationYawPitchRoll(0, MathUtil.PiOverFour, 0);
        var matrix = Matrix.RotationQuaternion(quaternion);

        // Should create a rotation matrix
        Assert.NotEqual(0f, matrix.M11);
        Assert.NotEqual(0f, matrix.M22);
        Assert.NotEqual(0f, matrix.M33);
        Assert.Equal(1f, matrix.M44);
    }

    [Fact]
    public void TestMatrixBillboard()
    {
        var objectPosition = new Vector3(5, 0, 0);
        var cameraPosition = Vector3.Zero;
        var cameraUpVector = Vector3.UnitY;
        var cameraForwardVector = Vector3.UnitZ;

        var matrix = Matrix.Billboard(objectPosition, cameraPosition, cameraUpVector, cameraForwardVector);

        // Billboard should create a matrix that orients the object to face the camera
        Assert.NotEqual(0f, matrix.Determinant());
    }

    [Fact]
    public void TestMatrixReflection()
    {
        var plane = new Plane(Vector3.UnitY, 0); // XZ plane at Y=0

        var matrix = Matrix.Reflection(plane);

        // Reflecting a point above the plane should put it below
        var point = new Vector3(1, 2, 3);
        var reflected = Vector3.Transform(point, matrix);

        Assert.Equal(1f, reflected.X, 5);
        Assert.Equal(-2f, reflected.Y, 5); // Y should be mirrored
        Assert.Equal(3f, reflected.Z, 5);
    }

    [Fact]
    public void TestMatrixShadow()
    {
        var light = new Vector4(0, 10, 0, 1); // Light above
        var plane = new Plane(Vector3.UnitY, 0); // Ground plane

        var matrix = Matrix.Shadow(light, plane);

        // Shadow matrix should project points onto the plane
        var point = new Vector3(1, 5, 1);
        var shadow = Vector3.Transform(point, matrix);

        // Shadow should be on the ground plane (Y ≈ 0)
        Assert.Equal(0f, shadow.Y, 2);
    }

    [Fact]
    public void TestMatrixRotationYawPitchRoll()
    {
        float yaw = MathUtil.PiOverFour;
        float pitch = MathUtil.PiOverFour / 2;
        float roll = MathUtil.PiOverFour / 3;

        var matrix = Matrix.RotationYawPitchRoll(yaw, pitch, roll);

        // Should create a valid rotation matrix
        Assert.NotEqual(0f, matrix.Determinant());
        
        // Rotation matrices should have determinant close to ±1
        Assert.Equal(1f, Math.Abs(matrix.Determinant()), 1);
    }

    [Fact]
    public void TestMatrixTransformation2D()
    {
        var scalingCenter = new Vector2(5, 5);
        float scalingRotation = 0f;
        var scaling = new Vector2(2, 2);
        var rotationCenter = new Vector2(5, 5);
        float rotation = MathUtil.PiOverFour;
        var translation = new Vector2(10, 10);

        var matrix = Matrix.Transformation2D(scalingCenter, scalingRotation, scaling, rotationCenter, rotation, translation);

        // Should create a valid 2D transformation matrix
        Assert.NotEqual(0f, matrix.Determinant());
    }

    [Fact]
    public void TestMatrixArrayAccess()
    {
        var matrix = new Matrix(
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16);

        Assert.Equal(1f, matrix[0]);
        Assert.Equal(2f, matrix[1]);
        Assert.Equal(16f, matrix[15]);

        // Test setter
        matrix[0] = 100f;
        Assert.Equal(100f, matrix.M11);
    }

    [Fact]
    public void TestMatrixFromNumeric()
    {
        var numeric = new System.Numerics.Matrix4x4(
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16);

        var matrix = (Matrix)numeric;

        Assert.Equal(1f, matrix.M11);
        Assert.Equal(2f, matrix.M12);
        Assert.Equal(16f, matrix.M44);
    }

    [Fact]
    public void TestMatrixToNumeric()
    {
        var matrix = new Matrix(
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16);

        var numeric = (System.Numerics.Matrix4x4)matrix;

        Assert.Equal(1f, numeric.M11);
        Assert.Equal(2f, numeric.M12);
        Assert.Equal(16f, numeric.M44);
    }

    [Fact]
    public void TestMatrixOrthoRH()
    {
        float width = 800f;
        float height = 600f;
        float znear = 0.1f;
        float zfar = 1000f;

        var matrix = Matrix.OrthoRH(width, height, znear, zfar);

        // RH should have different characteristics than LH
        Assert.Equal(1f, matrix.M44);
        Assert.NotEqual(0f, matrix.M11);
        Assert.NotEqual(0f, matrix.M22);
        Assert.NotEqual(0f, matrix.M33);
    }

    [Fact]
    public void TestMatrixOrthoOffCenterLH()
    {
        float left = -400f;
        float right = 400f;
        float bottom = -300f;
        float top = 300f;
        float znear = 0.1f;
        float zfar = 1000f;

        var matrix = Matrix.OrthoOffCenterLH(left, right, bottom, top, znear, zfar);

        Assert.Equal(1f, matrix.M44);
        Assert.NotEqual(0f, matrix.M11);
        Assert.NotEqual(0f, matrix.M22);
    }

    [Fact]
    public void TestMatrixPerspectiveLH()
    {
        float width = 800f;
        float height = 600f;
        float znear = 0.1f;
        float zfar = 1000f;

        var matrix = Matrix.PerspectiveLH(width, height, znear, zfar);

        // Perspective matrices have M44 = 0
        Assert.Equal(0f, matrix.M44);
        Assert.NotEqual(0f, matrix.M11);
        Assert.NotEqual(0f, matrix.M22);
    }

    [Fact]
    public void TestMatrixLookAtRH()
    {
        var eye = new Vector3(0, 0, 5);
        var target = new Vector3(0, 0, 0);
        var up = new Vector3(0, 1, 0);

        var matrix = Matrix.LookAtRH(eye, target, up);

        // LookAt matrix should be invertible
        Assert.NotEqual(0f, matrix.Determinant());
    }

    [Fact]
    public void TestMatrixSmoothStep()
    {
        var start = Matrix.Identity;
        var end = Matrix.Scaling(2f);
        float amount = 0.5f;

        var result = Matrix.SmoothStep(start, end, amount);

        // Result should be between start and end
        Assert.True(result.M11 > 1f && result.M11 < 2f);
    }

    [Fact]
    public void TestMatrixOrthogonalize()
    {
        // Create a matrix that's slightly non-orthogonal
        var matrix = Matrix.RotationY(0.1f);
        matrix.M12 += 0.01f; // Perturb it slightly

        var ortho = Matrix.Orthogonalize(matrix);

        // After orthogonalization, rows should be more perpendicular
        var row1 = new Vector3(ortho.M11, ortho.M12, ortho.M13);
        var row2 = new Vector3(ortho.M21, ortho.M22, ortho.M23);
        
        float dot = Vector3.Dot(row1, row2);
        Assert.Equal(0f, dot, 2); // Rows should be nearly perpendicular
    }

    [Fact]
    public void TestMatrixOrthonormalize()
    {
        // Create a non-orthonormal matrix (scaled rotation)
        var matrix = new Matrix(
            2, 0, 0, 0,
            0, 2, 0, 0,
            0, 0, 2, 0,
            0, 0, 0, 1);

        var orthonormal = Matrix.Orthonormalize(matrix);

        // After orthonormalization, row vectors should have unit length
        var row1 = new Vector3(orthonormal.M11, orthonormal.M12, orthonormal.M13);
        Assert.Equal(1f, row1.Length(), 3);
    }
}
