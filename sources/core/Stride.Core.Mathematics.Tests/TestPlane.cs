// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Xunit;

namespace Stride.Core.Mathematics.Tests;

public class TestPlane
{
    [Fact]
    public void TestPlaneConstruction()
    {
        // Normal and D
        var plane1 = new Plane(0, 1, 0, 5);
        Assert.Equal(0, plane1.Normal.X);
        Assert.Equal(1, plane1.Normal.Y);
        Assert.Equal(0, plane1.Normal.Z);
        Assert.Equal(5, plane1.D);

        // Vector3 normal and D
        var plane2 = new Plane(new Vector3(0, 1, 0), 5);
        Assert.Equal(plane1, plane2);

        // Three points (cross product of (1,0,0) and (0,0,1) is (0,-1,0))
        var p1 = new Vector3(0, 0, 0);
        var p2 = new Vector3(1, 0, 0);
        var p3 = new Vector3(0, 0, 1);
        var plane3 = new Plane(p1, p2, p3);
        Assert.Equal(0, plane3.Normal.X, 3);
        Assert.Equal(-1, plane3.Normal.Y, 3);
        Assert.Equal(0, plane3.Normal.Z, 3);
    }

    [Fact]
    public void TestPlaneNormalize()
    {
        var plane = new Plane(2, 0, 0, 4);
        var normalized = Plane.Normalize(plane);
        Assert.Equal(1, normalized.Normal.X, 3);
        Assert.Equal(0, normalized.Normal.Y, 3);
        Assert.Equal(0, normalized.Normal.Z, 3);
        Assert.Equal(2, normalized.D, 3);

        // Test instance method
        plane.Normalize();
        Assert.Equal(1, plane.Normal.X, 3);
        Assert.Equal(2, plane.D, 3);
    }

    [Fact]
    public void TestPlaneDot()
    {
        var plane = new Plane(0, 1, 0, -5);
        var point = new Vector4(1, 2, 3, 1);
        var result = Plane.Dot(plane, point);
        Assert.Equal(-3, result); // (0*1 + 1*2 + 0*3 + (-5)*1)
    }

    [Fact]
    public void TestPlaneDotCoordinate()
    {
        var plane = new Plane(0, 1, 0, -5);
        var point = new Vector3(1, 2, 3);
        var result = Plane.DotCoordinate(plane, point);
        Assert.Equal(-3, result); // (0*1 + 1*2 + 0*3 - 5)
    }

    [Fact]
    public void TestPlaneDotNormal()
    {
        var plane = new Plane(0, 1, 0, -5);
        var vector = new Vector3(1, 2, 3);
        var result = Plane.DotNormal(plane, vector);
        Assert.Equal(2, result); // (0*1 + 1*2 + 0*3)
    }

    [Fact]
    public void TestPlaneProject()
    {
        var plane = new Plane(0, 1, 0, 0); // XZ plane
        var point = new Vector3(1, 5, 2);
        var projected = Plane.Project(plane, point);
        Assert.Equal(1, projected.X, 3);
        Assert.Equal(0, projected.Y, 3);
        Assert.Equal(2, projected.Z, 3);
    }

    [Fact]
    public void TestPlaneMultiply()
    {
        var plane = new Plane(1, 0, 0, 2);
        var result = plane * 3.0f;
        Assert.Equal(3, result.Normal.X);
        Assert.Equal(0, result.Normal.Y);
        Assert.Equal(0, result.Normal.Z);
        Assert.Equal(6, result.D);

        var result2 = 3.0f * plane;
        Assert.Equal(result, result2);

        var result3 = Plane.Multiply(plane, 3.0f);
        Assert.Equal(result, result3);
    }

    [Fact]
    public void TestPlaneNegate()
    {
        var plane = new Plane(2, 0, 0, 4);
        
        // Test static method
        var negated = Plane.Negate(plane);
        Assert.Equal(-2, negated.Normal.X);
        Assert.Equal(0, negated.Normal.Y);
        Assert.Equal(0, negated.Normal.Z);
        Assert.Equal(-4, negated.D);

        // Test unary operator
        var negated2 = -plane;
        Assert.Equal(negated, negated2);
        
        // Test instance method
        plane.Negate();
        Assert.Equal(-2, plane.Normal.X);
        Assert.Equal(-4, plane.D);
    }

    [Fact]
    public void TestPlaneTransformMatrix()
    {
        var plane = new Plane(0, 1, 0, 0); // XZ plane
        var matrix = Matrix.RotationZ(MathUtil.PiOverTwo);
        var transformed = Plane.Transform(plane, matrix);
        
        // After 90° rotation around Z, Y-up becomes X-left
        Assert.InRange(transformed.Normal.X, -1.1f, -0.9f);
        Assert.InRange(transformed.Normal.Y, -0.1f, 0.1f);
    }

    [Fact]
    public void TestPlaneTransformQuaternion()
    {
        var plane = new Plane(0, 1, 0, 0); // XZ plane
        var rotation = Quaternion.RotationZ(MathUtil.PiOverTwo);
        var transformed = Plane.Transform(plane, rotation);
        
        // After 90° rotation around Z, Y-up becomes X-left
        Assert.InRange(transformed.Normal.X, -1.1f, -0.9f);
        Assert.InRange(transformed.Normal.Y, -0.1f, 0.1f);
    }

    [Fact]
    public void TestPlaneEquality()
    {
        var plane1 = new Plane(1, 0, 0, 2);
        var plane2 = new Plane(1, 0, 0, 2);
        var plane3 = new Plane(0, 1, 0, 2);

        Assert.True(plane1 == plane2);
        Assert.False(plane1 == plane3);
        Assert.False(plane1 != plane2);
        Assert.True(plane1 != plane3);

        Assert.True(plane1.Equals(plane2));
        Assert.False(plane1.Equals(plane3));
    }

    [Fact]
    public void TestPlaneHashCode()
    {
        var plane1 = new Plane(1, 0, 0, 2);
        var plane2 = new Plane(1, 0, 0, 2);
        var plane3 = new Plane(0, 1, 0, 2);

        Assert.Equal(plane1.GetHashCode(), plane2.GetHashCode());
        Assert.NotEqual(plane1.GetHashCode(), plane3.GetHashCode());
    }

    [Fact]
    public void TestPlaneToString()
    {
        var plane = new Plane(1, 0, 0, 2);
        var str = plane.ToString();
        Assert.NotEmpty(str);
    }
}
