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
}