// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Xunit;

namespace Stride.Core.Mathematics.Tests;

public class TestRectangleAndSize
{
    [Fact]
    public void TestRectangleConstruction()
    {
        var rect = new Rectangle(10, 20, 100, 200);
        Assert.Equal(10, rect.X);
        Assert.Equal(20, rect.Y);
        Assert.Equal(100, rect.Width);
        Assert.Equal(200, rect.Height);
    }

    [Fact]
    public void TestRectangleEmpty()
    {
        var empty = Rectangle.Empty;
        Assert.Equal(0, empty.X);
        Assert.Equal(0, empty.Y);
        Assert.Equal(0, empty.Width);
        Assert.Equal(0, empty.Height);
        Assert.True(empty.IsEmpty);
    }

    [Fact]
    public void TestRectangleProperties()
    {
        var rect = new Rectangle(10, 20, 100, 200);
        Assert.Equal(10, rect.Left);
        Assert.Equal(110, rect.Right);
        Assert.Equal(20, rect.Top);
        Assert.Equal(220, rect.Bottom);
        Assert.Equal(new Point(10, 20), rect.Location);
        Assert.Equal(new Point(60, 120), rect.Center);
    }

    [Theory]
    [InlineData(50, 100, true)]
    [InlineData(10, 20, true)]
    [InlineData(110, 220, false)]
    [InlineData(0, 0, false)]
    public void TestRectangleContainsPoint(int x, int y, bool expected)
    {
        var rect = new Rectangle(10, 20, 100, 200);
        Assert.Equal(expected, rect.Contains(x, y));
    }

    [Fact]
    public void TestRectangleFConstruction()
    {
        var rect = new RectangleF(10.5f, 20.5f, 100.5f, 200.5f);
        Assert.Equal(10.5f, rect.X);
        Assert.Equal(20.5f, rect.Y);
        Assert.Equal(100.5f, rect.Width);
        Assert.Equal(200.5f, rect.Height);
    }

    [Fact]
    public void TestRectangleFEmpty()
    {
        var empty = RectangleF.Empty;
        Assert.Equal(0f, empty.X);
        Assert.Equal(0f, empty.Y);
        Assert.Equal(0f, empty.Width);
        Assert.Equal(0f, empty.Height);
        Assert.True(empty.IsEmpty);
    }

    [Fact]
    public void TestRectangleFProperties()
    {
        var rect = new RectangleF(10f, 20f, 100f, 200f);
        Assert.Equal(10f, rect.Left);
        Assert.Equal(110f, rect.Right);
        Assert.Equal(20f, rect.Top);
        Assert.Equal(220f, rect.Bottom);
        Assert.Equal(new Vector2(10f, 20f), rect.TopLeft);
        Assert.Equal(new Vector2(60f, 120f), rect.Center);
    }

    [Theory]
    [InlineData(50f, 100f, true)]
    [InlineData(10f, 20f, true)]     // Top-left corner (inclusive)
    [InlineData(110f, 220f, true)]   // Bottom-right corner (inclusive: Right=110, Bottom=220)
    [InlineData(111f, 221f, false)]  // Outside bottom-right
    [InlineData(0f, 0f, false)]
    public void TestRectangleFContainsPoint(float x, float y, bool expected)
    {
        var rect = new RectangleF(10f, 20f, 100f, 200f);
        Assert.Equal(expected, rect.Contains(new Vector2(x, y)));
    }

    [Fact]
    public void TestSize2Construction()
    {
        var size = new Size2(800, 600);
        Assert.Equal(800, size.Width);
        Assert.Equal(600, size.Height);
    }

    [Fact]
    public void TestSize2Zero()
    {
        var zero = Size2.Zero;
        Assert.Equal(0, zero.Width);
        Assert.Equal(0, zero.Height);
    }

    [Fact]
    public void TestSize2Empty()
    {
        var empty = Size2.Empty;
        Assert.Equal(0, empty.Width);
        Assert.Equal(0, empty.Height);
    }

    [Fact]
    public void TestSize2FConstruction()
    {
        var size = new Size2F(800.5f, 600.5f);
        Assert.Equal(800.5f, size.Width);
        Assert.Equal(600.5f, size.Height);
    }

    [Fact]
    public void TestSize2FZero()
    {
        var zero = Size2F.Zero;
        Assert.Equal(0f, zero.Width);
        Assert.Equal(0f, zero.Height);
    }

    [Fact]
    public void TestSize3Construction()
    {
        var size = new Size3(800, 600, 32);
        Assert.Equal(800, size.Width);
        Assert.Equal(600, size.Height);
        Assert.Equal(32, size.Depth);
    }

    [Fact]
    public void TestSize3Zero()
    {
        var zero = Size3.Zero;
        Assert.Equal(0, zero.Width);
        Assert.Equal(0, zero.Height);
        Assert.Equal(0, zero.Depth);
    }

    [Fact]
    public void TestRectangleEquality()
    {
        var rect1 = new Rectangle(10, 20, 100, 200);
        var rect2 = new Rectangle(10, 20, 100, 200);
        var rect3 = new Rectangle(5, 10, 50, 100);

        Assert.Equal(rect1, rect2);
        Assert.NotEqual(rect1, rect3);
        Assert.True(rect1 == rect2);
        Assert.True(rect1 != rect3);
    }

    [Fact]
    public void TestRectangleFEquality()
    {
        var rect1 = new RectangleF(10f, 20f, 100f, 200f);
        var rect2 = new RectangleF(10f, 20f, 100f, 200f);
        var rect3 = new RectangleF(5f, 10f, 50f, 100f);

        Assert.Equal(rect1, rect2);
        Assert.NotEqual(rect1, rect3);
        Assert.True(rect1 == rect2);
        Assert.True(rect1 != rect3);
    }

    [Fact]
    public void TestSize2Equality()
    {
        var size1 = new Size2(800, 600);
        var size2 = new Size2(800, 600);
        var size3 = new Size2(1024, 768);

        Assert.Equal(size1, size2);
        Assert.NotEqual(size1, size3);
        Assert.True(size1 == size2);
        Assert.True(size1 != size3);
    }

    [Fact]
    public void TestSize2FEquality()
    {
        var size1 = new Size2F(800.5f, 600.5f);
        var size2 = new Size2F(800.5f, 600.5f);
        var size3 = new Size2F(1024.5f, 768.5f);

        Assert.Equal(size1, size2);
        Assert.NotEqual(size1, size3);
        Assert.True(size1 == size2);
        Assert.True(size1 != size3);
    }

    [Fact]
    public void TestSize3Equality()
    {
        var size1 = new Size3(800, 600, 32);
        var size2 = new Size3(800, 600, 32);
        var size3 = new Size3(1024, 768, 64);

        Assert.Equal(size1, size2);
        Assert.NotEqual(size1, size3);
        Assert.True(size1 == size2);
        Assert.True(size1 != size3);
    }
}
