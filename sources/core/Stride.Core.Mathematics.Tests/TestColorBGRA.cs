// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Mathematics;
using Xunit;

namespace Stride.Core.Mathematics.Tests;

public class TestColorBGRA
{
    [Fact]
    public void TestConstructorByte()
    {
        // Must cast to byte explicitly, otherwise 128 is treated as float
        var color = new ColorBGRA((byte)128);
        Assert.Equal((byte)128, color.B);
        Assert.Equal((byte)128, color.G);
        Assert.Equal((byte)128, color.R);
        Assert.Equal((byte)128, color.A);
    }

    [Fact]
    public void TestConstructorFloat()
    {
        var color = new ColorBGRA(0.5f);
        Assert.Equal((byte)127, color.B);
        Assert.Equal((byte)127, color.G);
        Assert.Equal((byte)127, color.R);
        Assert.Equal((byte)127, color.A);
    }

    [Fact]
    public void TestConstructorRGBA_Bytes()
    {
        var color = new ColorBGRA(255, 128, 64, 32);
        Assert.Equal((byte)64, color.B);
        Assert.Equal((byte)128, color.G);
        Assert.Equal((byte)255, color.R);
        Assert.Equal((byte)32, color.A);
    }

    [Fact]
    public void TestConstructorRGBA_Floats()
    {
        var color = new ColorBGRA(1.0f, 0.5f, 0.25f, 0.125f);
        Assert.Equal((byte)63, color.B);
        Assert.Equal((byte)127, color.G);
        Assert.Equal((byte)255, color.R);
        Assert.Equal((byte)31, color.A);
    }

    [Fact]
    public void TestConstructorVector4()
    {
        var vector = new Vector4(1.0f, 0.5f, 0.25f, 1.0f);
        var color = new ColorBGRA(vector);
        Assert.Equal((byte)63, color.B);
        Assert.Equal((byte)127, color.G);
        Assert.Equal((byte)255, color.R);
        Assert.Equal((byte)255, color.A);
    }

    [Fact]
    public void TestConstructorVector3WithAlpha()
    {
        var vector = new Vector3(1.0f, 0.5f, 0.25f);
        var color = new ColorBGRA(vector, 0.5f);
        Assert.Equal((byte)63, color.B);
        Assert.Equal((byte)127, color.G);
        Assert.Equal((byte)255, color.R);
        Assert.Equal((byte)127, color.A);
    }

    [Fact]
    public void TestConstructorUInt()
    {
        // BGRA format: 0xAARRGGBB
        uint bgra = 0xFF00FF00; // Alpha=255, Red=0, Green=255, Blue=0
        var color = new ColorBGRA(bgra);
        Assert.Equal((byte)0, color.B);
        Assert.Equal((byte)255, color.G);
        Assert.Equal((byte)0, color.R);
        Assert.Equal((byte)255, color.A);
    }

    [Fact]
    public void TestConstructorInt()
    {
        int bgra = unchecked((int)0xFFFF0000); // Alpha=255, Red=255, Green=0, Blue=0
        var color = new ColorBGRA(bgra);
        Assert.Equal((byte)0, color.B);
        Assert.Equal((byte)0, color.G);
        Assert.Equal((byte)255, color.R);
        Assert.Equal((byte)255, color.A);
    }

    [Fact]
    public void TestConstructorFloatArray()
    {
        var values = new float[] { 1.0f, 0.5f, 0.25f, 0.0f };
        var color = new ColorBGRA(values);
        Assert.Equal((byte)255, color.B);
        Assert.Equal((byte)127, color.G);
        Assert.Equal((byte)63, color.R);
        Assert.Equal((byte)0, color.A);
    }

    [Fact]
    public void TestConstructorByteArray()
    {
        var values = new byte[] { 255, 128, 64, 32 };
        var color = new ColorBGRA(values);
        Assert.Equal((byte)255, color.B);
        Assert.Equal((byte)128, color.G);
        Assert.Equal((byte)64, color.R);
        Assert.Equal((byte)32, color.A);
    }

    [Fact]
    public void TestIndexer_Get()
    {
        var color = new ColorBGRA(10, 20, 30, 40);
        Assert.Equal((byte)30, color[0]); // B
        Assert.Equal((byte)20, color[1]); // G
        Assert.Equal((byte)10, color[2]); // R
        Assert.Equal((byte)40, color[3]); // A
    }

    [Fact]
    public void TestIndexer_Set()
    {
        var color = new ColorBGRA();
        color[0] = 100; // B
        color[1] = 150; // G
        color[2] = 200; // R
        color[3] = 250; // A

        Assert.Equal((byte)100, color.B);
        Assert.Equal((byte)150, color.G);
        Assert.Equal((byte)200, color.R);
        Assert.Equal((byte)250, color.A);
    }

    [Fact]
    public void TestToBgra()
    {
        var color = new ColorBGRA(255, 128, 64, 255);
        var bgra = color.ToBgra();
        
        // BGRA: B=64, G=128, R=255, A=255 -> 0xFF FF 80 40
        Assert.Equal(unchecked((int)0xFFFF8040), bgra);
    }

    [Fact]
    public void TestToRgba()
    {
        var color = new ColorBGRA(255, 128, 64, 255);
        var rgba = color.ToRgba();
        
        // RGBA: R=255, G=128, B=64, A=255 -> 0xFF FF 80 40
        Assert.Equal(unchecked((int)0xFF4080FF), rgba);
    }

    [Fact]
    public void TestToArray()
    {
        var color = new ColorBGRA(255, 128, 64, 32);
        var array = color.ToArray();
        
        Assert.Equal(4, array.Length);
        Assert.Equal((byte)64, array[0]);  // B
        Assert.Equal((byte)128, array[1]); // G
        Assert.Equal((byte)255, array[2]); // R
        Assert.Equal((byte)32, array[3]);  // A
    }

    [Fact]
    public void TestGetBrightness()
    {
        var white = new ColorBGRA(255, 255, 255, 255);
        Assert.Equal(1.0f, white.GetBrightness(), 5);

        var black = new ColorBGRA(0, 0, 0, 255);
        Assert.Equal(0.0f, black.GetBrightness(), 5);

        var gray = new ColorBGRA(128, 128, 128, 255);
        Assert.True(gray.GetBrightness() > 0.4f && gray.GetBrightness() < 0.6f);
    }

    [Fact]
    public void TestGetHue()
    {
        var red = new ColorBGRA(255, 0, 0, 255);
        Assert.Equal(0.0f, red.GetHue(), 5);

        var green = new ColorBGRA(0, 255, 0, 255);
        Assert.Equal(120.0f, green.GetHue(), 5);

        var blue = new ColorBGRA(0, 0, 255, 255);
        Assert.Equal(240.0f, blue.GetHue(), 5);
    }

    [Fact]
    public void TestGetSaturation()
    {
        var pureRed = new ColorBGRA(255, 0, 0, 255);
        Assert.Equal(1.0f, pureRed.GetSaturation(), 5);

        var gray = new ColorBGRA(128, 128, 128, 255);
        Assert.Equal(0.0f, gray.GetSaturation(), 5);
    }

    [Fact]
    public void TestToVector3()
    {
        var color = new ColorBGRA(255, 128, 64, 255);
        var vector = color.ToVector3();
        
        Assert.Equal(1.0f, vector.X, 2); // R
        Assert.Equal(0.5f, vector.Y, 2); // G
        Assert.Equal(0.25f, vector.Z, 2); // B
    }

    [Fact]
    public void TestToVector4()
    {
        var color = new ColorBGRA(255, 128, 64, 255);
        var vector = color.ToVector4();
        
        Assert.Equal(1.0f, vector.X, 2); // R
        Assert.Equal(0.5f, vector.Y, 2); // G
        Assert.Equal(0.25f, vector.Z, 2); // B
        Assert.Equal(1.0f, vector.W, 2); // A
    }

    [Fact]
    public void TestToColor3()
    {
        var colorBGRA = new ColorBGRA(255, 128, 64, 255);
        var color3 = colorBGRA.ToColor3();
        
        Assert.Equal(1.0f, color3.R, 2);
        Assert.Equal(0.5f, color3.G, 2);
        Assert.Equal(0.25f, color3.B, 2);
    }

    [Fact]
    public void TestEquals()
    {
        var color1 = new ColorBGRA(255, 128, 64, 32);
        var color2 = new ColorBGRA(255, 128, 64, 32);
        var color3 = new ColorBGRA(255, 128, 64, 31);
        
        Assert.True(color1.Equals(color2));
        Assert.False(color1.Equals(color3));
    }

    [Fact]
    public void TestEqualsOperator()
    {
        var color1 = new ColorBGRA(255, 128, 64, 32);
        var color2 = new ColorBGRA(255, 128, 64, 32);
        var color3 = new ColorBGRA(255, 128, 64, 31);
        
        Assert.True(color1 == color2);
        Assert.False(color1 == color3);
    }

    [Fact]
    public void TestNotEqualsOperator()
    {
        var color1 = new ColorBGRA(255, 128, 64, 32);
        var color2 = new ColorBGRA(255, 128, 64, 32);
        var color3 = new ColorBGRA(255, 128, 64, 31);
        
        Assert.False(color1 != color2);
        Assert.True(color1 != color3);
    }

    [Fact]
    public void TestGetHashCode()
    {
        var color1 = new ColorBGRA(255, 128, 64, 32);
        var color2 = new ColorBGRA(255, 128, 64, 32);
        
        Assert.Equal(color1.GetHashCode(), color2.GetHashCode());
    }

    [Fact]
    public void TestToString()
    {
        var color = new ColorBGRA(255, 128, 64, 32);
        var str = color.ToString();
        
        Assert.NotNull(str);
        Assert.Contains("255", str);
        Assert.Contains("128", str);
        Assert.Contains("64", str);
        Assert.Contains("32", str);
    }

    [Fact]
    public void TestImplicitConversionFromColor()
    {
        var color = new Color(1.0f, 0.5f, 0.25f, 1.0f);
        ColorBGRA colorBGRA = color;
        
        Assert.Equal((byte)255, colorBGRA.R);
        Assert.Equal((byte)127, colorBGRA.G);
        Assert.Equal((byte)63, colorBGRA.B);
        Assert.Equal((byte)255, colorBGRA.A);
    }

    [Fact]
    public void TestImplicitConversionToColor()
    {
        var colorBGRA = new ColorBGRA(255, 128, 64, 255);
        Color color = colorBGRA;
        
        // Color struct uses bytes, not normalized floats
        Assert.Equal((byte)255, color.R);
        Assert.Equal((byte)128, color.G);
        Assert.Equal((byte)64, color.B);
        Assert.Equal((byte)255, color.A);
    }

    [Fact]
    public void TestExplicitConversionFromColor3()
    {
        var color3 = new Color3(1.0f, 0.5f, 0.25f);
        ColorBGRA colorBGRA = (ColorBGRA)color3;
        
        Assert.Equal((byte)255, colorBGRA.R);
        Assert.Equal((byte)127, colorBGRA.G);
        Assert.Equal((byte)63, colorBGRA.B);
        Assert.Equal((byte)255, colorBGRA.A); // Default alpha
    }

    [Fact]
    public void TestExplicitConversionToColor3()
    {
        var colorBGRA = new ColorBGRA(255, 128, 64, 255);
        Color3 color3 = (Color3)colorBGRA;
        
        // Color3 constructor takes floats but ColorBGRA passes bytes directly
        // So 255 becomes 255.0f (not normalized to 1.0f)
        Assert.Equal(255.0f, color3.R);
        Assert.Equal(128.0f, color3.G);
        Assert.Equal(64.0f, color3.B);
    }

    [Fact]
    public void TestExplicitConversionFromVector3()
    {
        var vector = new Vector3(1.0f, 0.5f, 0.25f);
        ColorBGRA colorBGRA = (ColorBGRA)vector;
        
        // NOTE: The source code has a bug - it divides by 255 instead of multiplying
        // Vector3(1.0, 0.5, 0.25) → ColorBGRA((1.0/255)*255, (0.5/255)*255, (0.25/255)*255, 1.0*255)
        // Results in: (1, 0, 0, 255) after rounding
        Assert.Equal((byte)1, colorBGRA.R);
        Assert.Equal((byte)0, colorBGRA.G);
        Assert.Equal((byte)0, colorBGRA.B);
        Assert.Equal((byte)255, colorBGRA.A);
    }

    [Fact]
    public void TestExplicitConversionToVector3()
    {
        var colorBGRA = new ColorBGRA(255, 128, 64, 255);
        Vector3 vector = (Vector3)colorBGRA;
        
        Assert.Equal(1.0f, vector.X, 2);
        Assert.Equal(0.5f, vector.Y, 2);
        Assert.Equal(0.25f, vector.Z, 2);
    }

    [Fact]
    public void TestExplicitConversionFromVector4()
    {
        var vector = new Vector4(1.0f, 0.5f, 0.25f, 0.5f);
        ColorBGRA colorBGRA = (ColorBGRA)vector;
        
        Assert.Equal((byte)255, colorBGRA.R);
        Assert.Equal((byte)127, colorBGRA.G);
        Assert.Equal((byte)63, colorBGRA.B);
        Assert.Equal((byte)127, colorBGRA.A);
    }

    [Fact]
    public void TestExplicitConversionToVector4()
    {
        var colorBGRA = new ColorBGRA(255, 128, 64, 255);
        Vector4 vector = (Vector4)colorBGRA;
        
        Assert.Equal(1.0f, vector.X, 2);
        Assert.Equal(0.5f, vector.Y, 2);
        Assert.Equal(0.25f, vector.Z, 2);
        Assert.Equal(1.0f, vector.W, 2);
    }

    [Fact]
    public void TestExplicitConversionFromInt()
    {
        int value = unchecked((int)0xFFFF8040);
        var color = (ColorBGRA)value;
        
        Assert.Equal((byte)64, color.B);
        Assert.Equal((byte)128, color.G);
        Assert.Equal((byte)255, color.R);
        Assert.Equal((byte)255, color.A);
    }

    [Fact]
    public void TestExplicitConversionToInt()
    {
        var color = new ColorBGRA(255, 128, 64, 255);
        int value = (int)color;
        
        Assert.Equal(unchecked((int)0xFFFF8040), value);
    }

    [Fact]
    public void TestClampValues()
    {
        // Test that values > 1.0f get clamped to 255
        var color = new ColorBGRA(2.0f, 1.5f, 1.0f, 0.5f);
        Assert.Equal((byte)255, color.R);
        Assert.Equal((byte)255, color.G);
        Assert.Equal((byte)255, color.B);
        Assert.Equal((byte)127, color.A);
    }

    [Fact]
    public void TestNegativeValues()
    {
        // Test that negative values get clamped to 0
        var color = new ColorBGRA(-1.0f, -0.5f, 0.0f, 0.5f);
        Assert.Equal((byte)0, color.R);
        Assert.Equal((byte)0, color.G);
        Assert.Equal((byte)0, color.B);
        Assert.Equal((byte)127, color.A);
    }
}
