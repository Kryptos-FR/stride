// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using Xunit;

namespace Stride.Core.Mathematics.Tests;

public class TestColor
{
    #region Color (RGBA byte) Tests

    [Fact]
    public void TestColorConstruction()
    {
        // Note: Color(128) matches Color(float) constructor, which clamps 128.0f to byte range
        // For byte constructor, need explicit cast or use component constructor
        var c1 = new Color(128, 128, 128, 128);
        Assert.Equal((byte)128, c1.R);
        Assert.Equal((byte)128, c1.G);
        Assert.Equal((byte)128, c1.B);
        Assert.Equal((byte)128, c1.A);

        var c2 = new Color(255, 128, 64, 32);
        Assert.Equal((byte)255, c2.R);
        Assert.Equal((byte)128, c2.G);
        Assert.Equal((byte)64, c2.B);
        Assert.Equal((byte)32, c2.A);

        var c3 = new Color(255, 128, 64);
        Assert.Equal((byte)255, c3.R);
        Assert.Equal((byte)128, c3.G);
        Assert.Equal((byte)64, c3.B);
        Assert.Equal((byte)255, c3.A);

        var c4 = new Color(1.0f, 0.5f, 0.25f, 0.125f);
        Assert.Equal((byte)255, c4.R);
        Assert.Equal((byte)127, c4.G);
        Assert.Equal((byte)63, c4.B);
        Assert.Equal((byte)31, c4.A);

        var c5 = new Color(new Vector4(1.0f, 0.5f, 0.25f, 0.125f));
        Assert.Equal((byte)255, c5.R);
        Assert.Equal((byte)127, c5.G);
        Assert.Equal((byte)63, c5.B);
        Assert.Equal((byte)31, c5.A);

        var c6 = new Color(new Vector3(1.0f, 0.5f, 0.25f), 0.125f);
        Assert.Equal((byte)255, c6.R);
        Assert.Equal((byte)127, c6.G);
        Assert.Equal((byte)63, c6.B);
        Assert.Equal((byte)31, c6.A);
    }

    [Fact]
    public void TestColorFromRgba()
    {
        // RGBA format: R in low byte, G next, B next, A in high byte
        var c1 = Color.FromRgba(0x204080FF);
        Assert.Equal((byte)0xFF, c1.R);
        Assert.Equal((byte)0x80, c1.G);
        Assert.Equal((byte)0x40, c1.B);
        Assert.Equal((byte)0x20, c1.A);
    }

    [Fact]
    public void TestColorFromBgra()
    {
        // BGRA format: B in bits 0-7, G in bits 8-15, R in bits 16-23, A in bits 24-31
        var c1 = Color.FromBgra(0x20804040);
        Assert.Equal((byte)0x80, c1.R);
        Assert.Equal((byte)0x40, c1.G);
        Assert.Equal((byte)0x40, c1.B);
        Assert.Equal((byte)0x20, c1.A);
    }

    [Fact]
    public void TestColorToRgba()
    {
        var c = new Color(255, 128, 64, 32);
        int rgba = c.ToRgba();
        // RGBA: R=255 (low), G=128, B=64, A=32 (high)
        // = 0x20408FF
        Assert.Equal(unchecked((int)0x204080FF), rgba);
    }

    [Fact]
    public void TestColorToVector()
    {
        var c = new Color(255, 128, 64, 32);
        var v3 = c.ToVector3();
        Assert.Equal(1.0f, v3.X, 3);
        Assert.Equal(128f / 255f, v3.Y, 3);
        Assert.Equal(64f / 255f, v3.Z, 3);

        var v4 = c.ToVector4();
        Assert.Equal(1.0f, v4.X, 3);
        Assert.Equal(128f / 255f, v4.Y, 3);
        Assert.Equal(64f / 255f, v4.Z, 3);
        Assert.Equal(32f / 255f, v4.W, 3);
    }

    [Fact]
    public void TestColorAddition()
    {
        var c1 = new Color(100, 50, 25, 10);
        var c2 = new Color(50, 100, 150, 200);
        var result = Color.Add(c1, c2);
        Assert.Equal((byte)150, result.R);
        Assert.Equal((byte)150, result.G);
        Assert.Equal((byte)175, result.B);
        Assert.Equal((byte)210, result.A);
    }

    [Fact]
    public void TestColorSubtraction()
    {
        var c1 = new Color(200, 150, 100, 50);
        var c2 = new Color(50, 50, 50, 25);
        var result = Color.Subtract(c1, c2);
        Assert.Equal((byte)150, result.R);
        Assert.Equal((byte)100, result.G);
        Assert.Equal((byte)50, result.B);
        Assert.Equal((byte)25, result.A);
    }

    [Fact]
    public void TestColorModulate()
    {
        var c1 = new Color(255, 128, 64, 32);
        var c2 = new Color(128, 128, 128, 128);
        var result = Color.Modulate(c1, c2);
        // Modulate uses: (byte)(left * right / 255)
        Assert.Equal((byte)128, result.R); // 255 * 128 / 255 = 128
        Assert.Equal((byte)64, result.G);  // 128 * 128 / 255 = 64
        Assert.Equal((byte)32, result.B);  // 64 * 128 / 255 = 32
        Assert.Equal((byte)16, result.A);  // 32 * 128 / 255 = 16
    }

    [Fact]
    public void TestColorScale()
    {
        var c = new Color(100, 80, 60, 40);
        var result = Color.Scale(c, 2.0f);
        Assert.Equal((byte)200, result.R);
        Assert.Equal((byte)160, result.G);
        Assert.Equal((byte)120, result.B);
        Assert.Equal((byte)80, result.A);
    }

    [Fact]
    public void TestColorNegate()
    {
        var c = new Color(200, 150, 100, 50);
        var result = Color.Negate(c);
        Assert.Equal((byte)55, result.R);
        Assert.Equal((byte)105, result.G);
        Assert.Equal((byte)155, result.B);
        Assert.Equal((byte)205, result.A);
    }

    [Fact]
    public void TestColorClamp()
    {
        var c = new Color(50, 150, 200, 250);
        var min = new Color(100, 100, 100, 100);
        var max = new Color(180, 180, 180, 180);
        var result = Color.Clamp(c, min, max);
        Assert.Equal((byte)100, result.R);
        Assert.Equal((byte)150, result.G);
        Assert.Equal((byte)180, result.B);
        Assert.Equal((byte)180, result.A);
    }

    [Fact]
    public void TestColorEquality()
    {
        var c1 = new Color(255, 128, 64, 32);
        var c2 = new Color(255, 128, 64, 32);
        var c3 = new Color(255, 128, 64, 31);

        Assert.True(c1 == c2);
        Assert.False(c1 == c3);
        Assert.True(c1.Equals(c2));
        Assert.False(c1.Equals(c3));
    }

    #endregion

    #region Color3 Tests

    [Fact]
    public void TestColor3Construction()
    {
        var c1 = new Color3(0.5f);
        Assert.Equal(0.5f, c1.R);
        Assert.Equal(0.5f, c1.G);
        Assert.Equal(0.5f, c1.B);

        var c2 = new Color3(1.0f, 0.5f, 0.25f);
        Assert.Equal(1.0f, c2.R);
        Assert.Equal(0.5f, c2.G);
        Assert.Equal(0.25f, c2.B);

        var c3 = new Color3(new Vector3(1.0f, 0.5f, 0.25f));
        Assert.Equal(1.0f, c3.R);
        Assert.Equal(0.5f, c3.G);
        Assert.Equal(0.25f, c3.B);

        var c4 = new Color3(0x00FF8040);
        Assert.Equal(64f / 255f, c4.R, 3);
        Assert.Equal(128f / 255f, c4.G, 3);
        Assert.Equal(255f / 255f, c4.B, 3);
    }

    [Fact]
    public void TestColor3ToVector3()
    {
        var c = new Color3(1.0f, 0.5f, 0.25f);
        var v = c.ToVector3();
        Assert.Equal(1.0f, v.X);
        Assert.Equal(0.5f, v.Y);
        Assert.Equal(0.25f, v.Z);
    }

    [Fact]
    public void TestColor3Addition()
    {
        var c1 = new Color3(0.5f, 0.3f, 0.1f);
        var c2 = new Color3(0.2f, 0.4f, 0.6f);
        var result = Color3.Add(c1, c2);
        Assert.Equal(0.7f, result.R, 5);
        Assert.Equal(0.7f, result.G, 5);
        Assert.Equal(0.7f, result.B, 5);
    }

    [Fact]
    public void TestColor3Subtraction()
    {
        var c1 = new Color3(0.8f, 0.6f, 0.4f);
        var c2 = new Color3(0.3f, 0.2f, 0.1f);
        var result = Color3.Subtract(c1, c2);
        Assert.Equal(0.5f, result.R, 5);
        Assert.Equal(0.4f, result.G, 5);
        Assert.Equal(0.3f, result.B, 5);
    }

    [Fact]
    public void TestColor3Modulate()
    {
        var c1 = new Color3(1.0f, 0.8f, 0.6f);
        var c2 = new Color3(0.5f, 0.5f, 0.5f);
        var result = Color3.Modulate(c1, c2);
        Assert.Equal(0.5f, result.R, 5);
        Assert.Equal(0.4f, result.G, 5);
        Assert.Equal(0.3f, result.B, 5);
    }

    [Fact]
    public void TestColor3Scale()
    {
        var c = new Color3(0.5f, 0.3f, 0.1f);
        var result = Color3.Scale(c, 2.0f);
        Assert.Equal(1.0f, result.R, 5);
        Assert.Equal(0.6f, result.G, 5);
        Assert.Equal(0.2f, result.B, 5);
    }

    [Fact]
    public void TestColor3Negate()
    {
        var c = new Color3(0.8f, 0.6f, 0.4f);
        var result = Color3.Negate(c);
        // Negate uses: 1.0f - value (complement, not arithmetic negation)
        Assert.Equal(0.2f, result.R, 5);
        Assert.Equal(0.4f, result.G, 5);
        Assert.Equal(0.6f, result.B, 5);
    }

    [Fact]
    public void TestColor3Lerp()
    {
        var start = new Color3(0.0f, 0.0f, 0.0f);
        var end = new Color3(1.0f, 1.0f, 1.0f);
        var result = Color3.Lerp(start, end, 0.5f);
        Assert.Equal(0.5f, result.R, 5);
        Assert.Equal(0.5f, result.G, 5);
        Assert.Equal(0.5f, result.B, 5);
    }

    [Fact]
    public void TestColor3Clamp()
    {
        var c = new Color3(0.2f, 0.5f, 0.8f);
        var min = new Color3(0.3f, 0.3f, 0.3f);
        var max = new Color3(0.7f, 0.7f, 0.7f);
        var result = Color3.Clamp(c, min, max);
        Assert.Equal(0.3f, result.R, 5);
        Assert.Equal(0.5f, result.G, 5);
        Assert.Equal(0.7f, result.B, 5);
    }

    [Fact]
    public void TestColor3Equality()
    {
        var c1 = new Color3(1.0f, 0.5f, 0.25f);
        var c2 = new Color3(1.0f, 0.5f, 0.25f);
        var c3 = new Color3(1.0f, 0.5f, 0.24f);

        Assert.True(c1 == c2);
        Assert.False(c1 == c3);
        Assert.True(c1.Equals(c2));
        Assert.False(c1.Equals(c3));
    }

    #endregion

    #region Color4 Tests

    [Fact]
    public void TestColor4Construction()
    {
        var c1 = new Color4(0.5f);
        Assert.Equal(0.5f, c1.R);
        Assert.Equal(0.5f, c1.G);
        Assert.Equal(0.5f, c1.B);
        Assert.Equal(0.5f, c1.A);

        var c2 = new Color4(1.0f, 0.75f, 0.5f, 0.25f);
        Assert.Equal(1.0f, c2.R);
        Assert.Equal(0.75f, c2.G);
        Assert.Equal(0.5f, c2.B);
        Assert.Equal(0.25f, c2.A);

        var c3 = new Color4(1.0f, 0.75f, 0.5f);
        Assert.Equal(1.0f, c3.R);
        Assert.Equal(0.75f, c3.G);
        Assert.Equal(0.5f, c3.B);
        Assert.Equal(1.0f, c3.A);

        var c4 = new Color4(new Vector4(1.0f, 0.75f, 0.5f, 0.25f));
        Assert.Equal(1.0f, c4.R);
        Assert.Equal(0.75f, c4.G);
        Assert.Equal(0.5f, c4.B);
        Assert.Equal(0.25f, c4.A);

        var c5 = new Color4(new Vector3(1.0f, 0.75f, 0.5f), 0.25f);
        Assert.Equal(1.0f, c5.R);
        Assert.Equal(0.75f, c5.G);
        Assert.Equal(0.5f, c5.B);
        Assert.Equal(0.25f, c5.A);
    }

    [Fact]
    public void TestColor4StaticFields()
    {
        Assert.Equal(0.0f, Color4.Black.R);
        Assert.Equal(0.0f, Color4.Black.G);
        Assert.Equal(0.0f, Color4.Black.B);
        Assert.Equal(1.0f, Color4.Black.A);

        Assert.Equal(1.0f, Color4.White.R);
        Assert.Equal(1.0f, Color4.White.G);
        Assert.Equal(1.0f, Color4.White.B);
        Assert.Equal(1.0f, Color4.White.A);
    }

    [Fact]
    public void TestColor4ToVector()
    {
        var c = new Color4(1.0f, 0.75f, 0.5f, 0.25f);
        var v3 = c.ToVector3();
        Assert.Equal(1.0f, v3.X);
        Assert.Equal(0.75f, v3.Y);
        Assert.Equal(0.5f, v3.Z);

        var v4 = c.ToVector4();
        Assert.Equal(1.0f, v4.X);
        Assert.Equal(0.75f, v4.Y);
        Assert.Equal(0.5f, v4.Z);
        Assert.Equal(0.25f, v4.W);
    }

    [Fact]
    public void TestColor4Addition()
    {
        var c1 = new Color4(0.5f, 0.3f, 0.1f, 0.2f);
        var c2 = new Color4(0.2f, 0.4f, 0.6f, 0.3f);
        var result = Color4.Add(c1, c2);
        Assert.Equal(0.7f, result.R, 5);
        Assert.Equal(0.7f, result.G, 5);
        Assert.Equal(0.7f, result.B, 5);
        Assert.Equal(0.5f, result.A, 5);
    }

    [Fact]
    public void TestColor4Subtraction()
    {
        var c1 = new Color4(0.8f, 0.6f, 0.4f, 0.9f);
        var c2 = new Color4(0.3f, 0.2f, 0.1f, 0.4f);
        var result = Color4.Subtract(c1, c2);
        Assert.Equal(0.5f, result.R, 5);
        Assert.Equal(0.4f, result.G, 5);
        Assert.Equal(0.3f, result.B, 5);
        Assert.Equal(0.5f, result.A, 5);
    }

    [Fact]
    public void TestColor4Modulate()
    {
        var c1 = new Color4(1.0f, 0.8f, 0.6f, 0.4f);
        var c2 = new Color4(0.5f, 0.5f, 0.5f, 0.5f);
        var result = Color4.Modulate(c1, c2);
        Assert.Equal(0.5f, result.R, 5);
        Assert.Equal(0.4f, result.G, 5);
        Assert.Equal(0.3f, result.B, 5);
        Assert.Equal(0.2f, result.A, 5);
    }

    [Fact]
    public void TestColor4Scale()
    {
        var c = new Color4(0.5f, 0.3f, 0.1f, 0.2f);
        var result = Color4.Scale(c, 2.0f);
        Assert.Equal(1.0f, result.R, 5);
        Assert.Equal(0.6f, result.G, 5);
        Assert.Equal(0.2f, result.B, 5);
        Assert.Equal(0.4f, result.A, 5);
    }

    [Fact]
    public void TestColor4Negate()
    {
        var c = new Color4(0.8f, 0.6f, 0.4f, 0.2f);
        var result = Color4.Negate(c);
        // Negate uses: 1.0f - value (complement, not arithmetic negation)
        Assert.Equal(0.2f, result.R, 5);
        Assert.Equal(0.4f, result.G, 5);
        Assert.Equal(0.6f, result.B, 5);
        Assert.Equal(0.8f, result.A, 5);
    }

    [Fact]
    public void TestColor4Lerp()
    {
        var start = new Color4(0.0f, 0.0f, 0.0f, 0.0f);
        var end = new Color4(1.0f, 1.0f, 1.0f, 1.0f);
        var result = Color4.Lerp(start, end, 0.5f);
        Assert.Equal(0.5f, result.R, 5);
        Assert.Equal(0.5f, result.G, 5);
        Assert.Equal(0.5f, result.B, 5);
        Assert.Equal(0.5f, result.A, 5);
    }

    [Fact]
    public void TestColor4Clamp()
    {
        var c = new Color4(0.2f, 0.5f, 0.8f, 0.9f);
        var min = new Color4(0.3f, 0.3f, 0.3f, 0.3f);
        var max = new Color4(0.7f, 0.7f, 0.7f, 0.7f);
        var result = Color4.Clamp(c, min, max);
        Assert.Equal(0.3f, result.R, 5);
        Assert.Equal(0.5f, result.G, 5);
        Assert.Equal(0.7f, result.B, 5);
        Assert.Equal(0.7f, result.A, 5);
    }

    [Fact]
    public void TestColor4Equality()
    {
        var c1 = new Color4(1.0f, 0.5f, 0.25f, 0.125f);
        var c2 = new Color4(1.0f, 0.5f, 0.25f, 0.125f);
        var c3 = new Color4(1.0f, 0.5f, 0.25f, 0.124f);

        Assert.True(c1 == c2);
        Assert.False(c1 == c3);
        Assert.True(c1.Equals(c2));
        Assert.False(c1.Equals(c3));
    }

    #endregion

    #region ColorBGRA Tests

    [Fact]
    public void TestColorBGRAConstruction()
    {
        // Note: ColorBGRA(128) matches ColorBGRA(float) constructor
        // For byte constructor, need explicit byte arguments
        var c1 = new ColorBGRA(128, 128, 128, 128);
        Assert.Equal((byte)128, c1.R);
        Assert.Equal((byte)128, c1.G);
        Assert.Equal((byte)128, c1.B);
        Assert.Equal((byte)128, c1.A);

        var c2 = new ColorBGRA(255, 128, 64, 32);
        Assert.Equal((byte)255, c2.R);
        Assert.Equal((byte)128, c2.G);
        Assert.Equal((byte)64, c2.B);
        Assert.Equal((byte)32, c2.A);

        var c3 = new ColorBGRA(1.0f, 0.5f, 0.25f, 0.125f);
        Assert.Equal((byte)255, c3.R);
        Assert.Equal((byte)127, c3.G);
        Assert.Equal((byte)63, c3.B);
        Assert.Equal((byte)31, c3.A);

        var c4 = new ColorBGRA(new Vector4(1.0f, 0.5f, 0.25f, 0.125f));
        Assert.Equal((byte)255, c4.R);
        Assert.Equal((byte)127, c4.G);
        Assert.Equal((byte)63, c4.B);
        Assert.Equal((byte)31, c4.A);

        var c5 = new ColorBGRA(new Vector3(1.0f, 0.5f, 0.25f), 0.125f);
        Assert.Equal((byte)255, c5.R);
        Assert.Equal((byte)127, c5.G);
        Assert.Equal((byte)63, c5.B);
        Assert.Equal((byte)31, c5.A);

        // BGRA packed format: A in high byte, R next, G next, B in low byte
        var c6 = new ColorBGRA(0xFF804020);
        Assert.Equal((byte)0x80, c6.R);
        Assert.Equal((byte)0x40, c6.G);
        Assert.Equal((byte)0x20, c6.B);
        Assert.Equal((byte)0xFF, c6.A);
    }

    [Fact]
    public void TestColorBGRAToVector()
    {
        var c = new ColorBGRA(255, 128, 64, 32);
        var v3 = c.ToVector3();
        Assert.Equal(1.0f, v3.X, 3);
        Assert.Equal(128f / 255f, v3.Y, 3);
        Assert.Equal(64f / 255f, v3.Z, 3);

        var v4 = c.ToVector4();
        Assert.Equal(1.0f, v4.X, 3);
        Assert.Equal(128f / 255f, v4.Y, 3);
        Assert.Equal(64f / 255f, v4.Z, 3);
        Assert.Equal(32f / 255f, v4.W, 3);
    }

    [Fact]
    public void TestColorBGRAEquality()
    {
        var c1 = new ColorBGRA(255, 128, 64, 32);
        var c2 = new ColorBGRA(255, 128, 64, 32);
        var c3 = new ColorBGRA(255, 128, 64, 31);

        Assert.True(c1 == c2);
        Assert.False(c1 == c3);
        Assert.True(c1.Equals(c2));
        Assert.False(c1.Equals(c3));
    }

    #endregion

    #region HSV Conversion Tests

    [Fact]
    public void TestRGB2HSVConversion()
    {
        Assert.Equal(new ColorHSV(312, 1, 1, 1), ColorHSV.FromColor(new Color4(1, 0, 0.8f, 1)));
        Assert.Equal(new ColorHSV(0, 0, 0, 1), ColorHSV.FromColor(Color.Black));
        Assert.Equal(new ColorHSV(0, 0, 1, 1), ColorHSV.FromColor(Color.White));
        Assert.Equal(new ColorHSV(0, 1, 1, 1), ColorHSV.FromColor(Color.Red));
        Assert.Equal(new ColorHSV(120, 1, 1, 1), ColorHSV.FromColor(Color.Lime));
        Assert.Equal(new ColorHSV(240, 1, 1, 1), ColorHSV.FromColor(Color.Blue));
        Assert.Equal(new ColorHSV(60, 1, 1, 1), ColorHSV.FromColor(Color.Yellow));
        Assert.Equal(new ColorHSV(180, 1, 1, 1), ColorHSV.FromColor(Color.Cyan));
        Assert.Equal(new ColorHSV(300, 1, 1, 1), ColorHSV.FromColor(Color.Magenta));
        Assert.Equal(new ColorHSV(0, 0, 0.7529412f, 1), ColorHSV.FromColor(Color.Silver));
        Assert.Equal(new ColorHSV(0, 0, 0.5019608f, 1), ColorHSV.FromColor(Color.Gray));
        Assert.Equal(new ColorHSV(0, 1, 0.5019608f, 1), ColorHSV.FromColor(Color.Maroon));
    }

    [Fact]
    public void TestHSV2RGBConversion()
    {
        Assert.Equal(Color.Black.ToColor4(), ColorHSV.FromColor(Color.Black).ToColor());
        Assert.Equal(Color.White.ToColor4(), ColorHSV.FromColor(Color.White).ToColor());
        Assert.Equal(Color.Red.ToColor4(), ColorHSV.FromColor(Color.Red).ToColor());
        Assert.Equal(Color.Lime.ToColor4(), ColorHSV.FromColor(Color.Lime).ToColor());
        Assert.Equal(Color.Blue.ToColor4(), ColorHSV.FromColor(Color.Blue).ToColor());
        Assert.Equal(Color.Silver.ToColor4(), ColorHSV.FromColor(Color.Silver).ToColor());
        Assert.Equal(Color.Maroon.ToColor4(), ColorHSV.FromColor(Color.Maroon).ToColor());
        Assert.Equal(new Color(184, 209, 219, 255).ToRgba(), ColorHSV.FromColor(new Color(184, 209, 219, 255)).ToColor().ToRgba());
    }

    #endregion
}
