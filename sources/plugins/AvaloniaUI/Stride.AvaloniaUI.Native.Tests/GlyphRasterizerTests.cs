// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Avalonia.Media.TextFormatting;
using SkiaSharp;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.AvaloniaUI.Rendering.Text;
using Stride.AvaloniaUI.Tests;
using Xunit;

namespace Stride.AvaloniaUI.Native.Tests;

/// <summary>
/// Compares the glyph masks of the native renderer's FreeType rasterizer with those Skia draws on the CPU.
/// </summary>
/// <remarks>
/// Skia's mask of a glyph is the alpha of the glyph drawn alone by SkiaSharp into a transparent surface, at
/// (<see cref="OriginX"/> + SubpixelX / 4, <see cref="OriginY"/>), with an SKFont made as Avalonia's Skia glyph runs make
/// theirs, from the same font bytes.
/// </remarks>
public sealed class GlyphRasterizerTests : IDisposable
{
    private const int SurfaceSize = 160;
    private const int OriginX = 20;
    private const int OriginY = 100;

    private static readonly string[] Texts = ["Quartz Жёлудь", "مرحبا", "日本語"];

    private readonly FreeTypeLibrary library = new();

    /// <summary>
    /// Gets a case per font and size: 13⅓ is 4/3 of 10 px and 18⅔ is 4/3 of 14 px, sizes whose 26.6 value isn't whole.
    /// </summary>
    public static IEnumerable<object[]> MaskCases
        => from face in new[] { "Inter Regular", "Inter Bold", "Noto Sans Arabic", "Noto Sans JP" }
           from size in new[] { 9, 12, 14, 18, 24, 36, 72, 40 / 3.0, 56 / 3.0 }
           select new object[] { face, size };

    public void Dispose() => library.Dispose();

    [SkippableTheory]
    [MemberData(nameof(MaskCases))]
    public void MaskMatchesSkia(string face, double size) => TestAvalonia.Run(() =>
    {
        Skip.IfNot(OperatingSystem.IsLinux(), "Skia rasterizes glyphs with FreeType only on Linux.");
        AssertMasksMatch(face, (float)size, SKFontEdging.Antialias);
    });

    [SkippableFact]
    public void ObliqueMaskMatchesSkia() => TestAvalonia.Run(() =>
    {
        Skip.IfNot(OperatingSystem.IsLinux(), "Skia rasterizes glyphs with FreeType only on Linux.");
        Assert.Equal(FontSimulations.Oblique, GetTypeface("Inter Italic").FontSimulations);
        foreach (var size in new[] { 12f, 24f, 36f })
            AssertMasksMatch("Inter Italic", size, SKFontEdging.Antialias);
    });

    [Fact]
    public void SimulatedBoldFallsBack() => TestAvalonia.Run(() =>
    {
        var typeface = GetTypeface("Noto Sans Arabic Bold");
        Assert.Equal(FontSimulations.Bold, typeface.FontSimulations);
        Assert.False(NativeFont.TryOpen(library.Handle, typeface, out var font, out var reason));
        Assert.Null(font);
        Assert.Equal(FallbackReason.TextSimulation, reason);
    });

    [SkippableFact]
    public void AliasedMaskMatchesSkia() => TestAvalonia.Run(() =>
    {
        Skip.IfNot(OperatingSystem.IsLinux(), "Skia rasterizes glyphs with FreeType only on Linux.");
        foreach (var size in new[] { 12f, 14f, 24f })
            AssertMasksMatch("Inter Regular", size, SKFontEdging.Alias);
    });

    [Fact]
    public void GammaTablesAreSkias()
    {
        Assert.Equal(0, TextGamma.GetLuminance(Colors.Black));
        Assert.Equal(7, TextGamma.GetLuminance(Colors.White));
        Assert.Equal(4, TextGamma.GetLuminance(Color.FromRgb(128, 128, 128)));
        // Measured from Skia: coverage 32, 126 and 224 of black text become 21, 89 and 181; of gray, 40, 142 and 230; of
        // white, 99, 187 and 241
        Assert.Equal(new byte[] { 21, 89, 181 }, new[] { 32, 126, 224 }.Select(c => TextGamma.GetTable(0)[c]));
        Assert.Equal(new byte[] { 40, 142, 230 }, new[] { 32, 126, 224 }.Select(c => TextGamma.GetTable(4)[c]));
        Assert.Equal(new byte[] { 99, 187, 241 }, new[] { 32, 126, 224 }.Select(c => TextGamma.GetTable(7)[c]));
        Assert.Equal(0, TextGamma.GetTable(3)[0]);
        Assert.Equal(255, TextGamma.GetTable(3)[255]);
    }

    [Fact]
    public void FaceOfACollectionIsFoundByItsTables()
    {
        // The tables of each face as Skia's typefaces of the collection give them, and those of a font not in it
        var collection = TestFonts.Read(TestFonts.CollectionFile);
        using var arabic = SKTypeface.FromData(SKData.CreateCopy(collection), 0);
        using var japanese = SKTypeface.FromData(SKData.CreateCopy(collection), 1);
        using var inter = SKTypeface.FromData(SKData.CreateCopy(ReadFont(GetTypeface("Inter Regular"))));

        Assert.Equal(1, NativeFont.FindFaceIndex(library.Handle, collection, TablesOf(japanese)));
        Assert.Equal(0, NativeFont.FindFaceIndex(library.Handle, collection, TablesOf(arabic)));
        Assert.Equal(-1, NativeFont.FindFaceIndex(library.Handle, collection, TablesOf(inter)));
    }

    [Fact]
    public void FileIsReadOncePerTypeface() => TestAvalonia.Run(() =>
    {
        // The typeface opened in two libraries, as by the atlases of a device detached and attached again
        var counting = new CountingTypeface(GetTypeface("Inter Regular").PlatformTypeface);
        var typeface = new GlyphTypeface(counting, FontSimulations.None);
        using var otherLibrary = new FreeTypeLibrary();
        Assert.True(NativeFont.TryOpen(library.Handle, typeface, out var first, out var reason), $"Inter can't be opened: {reason}.");
        first.Dispose();
        Assert.True(NativeFont.TryOpen(otherLibrary.Handle, typeface, out var second, out reason), $"Inter can't be opened again: {reason}.");
        using (second)
        {
            Assert.Equal(1, counting.Streams);
            Assert.Same(first.File, second.File);
        }
    });

    [Fact]
    public void FacesOfACollectionShareItsFile() => TestAvalonia.Run(() =>
    {
        Assert.True(NativeFont.TryOpen(library.Handle, GetTypeface("Collection Arabic"), out var arabic, out var reason), $"Face 0 of the collection can't be opened: {reason}.");
        using var arabicFont = arabic;
        Assert.True(NativeFont.TryOpen(library.Handle, GetTypeface("Collection Japanese"), out var japanese, out reason), $"Face 1 of the collection can't be opened: {reason}.");
        using var japaneseFont = japanese;
        Assert.Equal("Noto Sans JP", japanese.FamilyName);
        Assert.Same(arabic.File, japanese.File);
    });

    [Theory]
    [InlineData("COLR")]
    [InlineData("CBDT")]
    [InlineData("sbix")]
    [InlineData("SVG ")]
    public void ColourFontsFallBack(string table)
        => Assert.Equal(FallbackReason.ColorFont, NativeFont.Classify(tag => tag == Tag(table) || tag == Tag("fvar")));

    [Fact]
    public void VariableFontsFallBack() => Assert.Equal(FallbackReason.FontData, NativeFont.Classify(tag => tag == Tag("fvar")));

    [Fact]
    public void EmojiTypefaceIsAColourFont() => TestAvalonia.Run(() =>
    {
        Assert.False(NativeFont.TryOpen(library.Handle, new Typeface("fonts:Test#Noto Color Emoji").GlyphTypeface, out _, out var reason));
        Assert.Equal(FallbackReason.ColorFont, reason);
    });

    private static uint Tag(string tag) => OpenTypeTag.Parse(tag);

    private static Func<uint, ReadOnlyMemory<byte>?> TablesOf(SKTypeface typeface)
        => tag => typeface.TryGetTableData(tag, out var data) ? data : null;

    private static GlyphTypeface GetTypeface(string face) => face switch
    {
        "Inter Regular" => new Typeface("fonts:Inter#Inter").GlyphTypeface,
        "Inter Bold" => new Typeface("fonts:Inter#Inter", FontStyle.Normal, FontWeight.Bold).GlyphTypeface,
        "Inter Italic" => new Typeface("fonts:Inter#Inter", FontStyle.Italic).GlyphTypeface,
        "Noto Sans Arabic" => new Typeface("fonts:Test#Noto Sans Arabic").GlyphTypeface,
        "Noto Sans Arabic Bold" => new Typeface("fonts:Test#Noto Sans Arabic", FontStyle.Normal, FontWeight.Bold).GlyphTypeface,
        "Noto Sans JP" => new Typeface("fonts:Test#Noto Sans JP").GlyphTypeface,
        "Collection Arabic" => new Typeface("fonts:TestCollection#Noto Sans Arabic").GlyphTypeface,
        "Collection Japanese" => new Typeface("fonts:TestCollection#Noto Sans JP").GlyphTypeface,
        _ => throw new ArgumentOutOfRangeException(nameof(face), face, "No test face has this name."),
    };

    private static byte[] ReadFont(GlyphTypeface typeface)
    {
        Assert.True(typeface.PlatformTypeface.TryGetStream(out var stream));
        using (stream)
        {
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
    }

    // The glyphs of the texts the face has, as shaping gives them (Arabic's joined forms included), each once
    private static ushort[] GetGlyphs(GlyphTypeface typeface, float size)
    {
        var glyphs = new SortedSet<ushort>();
        foreach (var text in Texts)
        {
            var rightToLeft = text.Any(c => c is >= '؀' and <= 'ۿ');
            using var shaped = TextShaper.Current.ShapeText(text, new TextShaperOptions(typeface, size, bidiLevel: (sbyte)(rightToLeft ? 1 : 0)));
            foreach (var glyph in shaped)
            {
                if (glyph.GlyphIndex != 0)
                    glyphs.Add(glyph.GlyphIndex);
            }
        }
        return glyphs.ToArray();
    }

    // The glyph of the space, when it is one of the glyphs
    private static SortedSet<ushort> GetSpaces(GlyphTypeface typeface, ushort[] glyphs)
    {
        using var shaped = TextShaper.Current.ShapeText(" ", new TextShaperOptions(typeface, 12));
        return glyphs.Contains(shaped[0].GlyphIndex) ? [shaped[0].GlyphIndex] : [];
    }

    // Black, white, a semi-transparent red, and a gray in each of the 8 luminance tables
    private static IEnumerable<Color> GetColors()
    {
        yield return Colors.Black;
        yield return Colors.White;
        yield return Color.FromArgb(128, 255, 0, 0);
        for (var table = 0; table < 8; table++)
        {
            var gray = (byte)(table * 32 + 16);
            yield return Color.FromRgb(gray, gray, gray);
        }
    }

    private void AssertMasksMatch(string face, float size, SKFontEdging edging)
    {
        var typeface = GetTypeface(face);
        var simulations = typeface.FontSimulations;
        Assert.True(NativeFont.TryOpen(library.Handle, typeface, out var font, out var reason), $"{face} can't be opened: {reason}.");
        using var nativeFont = font;
        using var skTypeface = SKTypeface.FromData(SKData.CreateCopy(ReadFont(typeface)));
        // As Avalonia's Skia typefaces and glyph runs make their fonts
        using var skFont = new SKFont(skTypeface, size, 1, (simulations & FontSimulations.Oblique) != 0 ? -0.3f : 0)
        {
            LinearMetrics = true,
            Embolden = (simulations & FontSimulations.Bold) != 0,
            Hinting = SKFontHinting.Full,
            Edging = edging,
            Subpixel = edging != SKFontEdging.Alias,
            BaselineSnap = true,
        };
        using var surface = SKSurface.Create(new SKImageInfo(SurfaceSize, SurfaceSize, SKColorType.Rgba8888, SKAlphaType.Premul), new SKSurfaceProperties(SKPixelGeometry.RgbHorizontal));
        var aliased = edging == SKFontEdging.Alias;
        var rendering = aliased ? GlyphRendering.Alias : GlyphRendering.Antialias;
        var size26Dot6 = GlyphRasterizer.ToSize26Dot6(size);
        var coverage = new ArrayBufferWriter<byte>();
        var failures = new List<string>();
        var maxDifference = 0;
        var skiaInkOutsideTheImage = 0;
        var glyphs = GetGlyphs(typeface, size);
        Assert.NotEmpty(glyphs);
        var glyphsSkiaDrewNothingOf = new SortedSet<ushort>();

        foreach (var glyph in glyphs)
        {
            for (var subpixel = 0; subpixel < (aliased ? 1 : 4); subpixel++)
            {
                foreach (var color in aliased ? [Colors.Black] : GetColors())
                {
                    var luminance = aliased ? (byte)0 : TextGamma.GetLuminance(color);
                    var key = new GlyphKey(nativeFont, glyph, size26Dot6, rendering, TextHintingMode.Strong, (byte)subpixel, luminance);
                    coverage.Clear();
                    Assert.True(GlyphRasterizer.TryRasterize(library.Handle, key, coverage, out var image), $"{face} {size}: glyph {glyph} can't be rasterized.");
                    Assert.Equal(image.Width * image.Height, coverage.WrittenCount);

                    // The mask, drawn with the color made opaque: the alpha doesn't change the gamma table, which the
                    // semi-transparent drawing below checks
                    var x = aliased ? OriginX + 0.4f : OriginX + subpixel / 4f;
                    var mask = DrawGlyph(surface, skFont, glyph, x, color.A == 255 ? color : Color.FromRgb(color.R, color.G, color.B));
                    var translucent = color.A == 255 ? null : DrawGlyph(surface, skFont, glyph, x, color);
                    if (!mask.AsSpan().ContainsAnyExcept((byte)0))
                        glyphsSkiaDrewNothingOf.Add(glyph);
                    var ours = coverage.WrittenSpan;
                    for (var py = 0; py < SurfaceSize; py++)
                    {
                        for (var px = 0; px < SurfaceSize; px++)
                        {
                            var skia = mask[py * SurfaceSize + px];
                            var ix = px - OriginX - image.Left;
                            var iy = py - OriginY - image.Top;
                            var inside = ix >= 0 && iy >= 0 && ix < image.Width && iy < image.Height;
                            if (!inside)
                            {
                                if (skia != 0)
                                {
                                    skiaInkOutsideTheImage++;
                                    Fail($"Skia has ink {skia} at ({px}, {py}), outside the image {image}");
                                }
                                continue;
                            }

                            var value = ours[iy * image.Width + ix];
                            var difference = Math.Abs(value - skia);
                            if (difference > 0)
                                Fail($"at ({px}, {py}) coverage {value}, Skia {skia}");
                            maxDifference = Math.Max(maxDifference, difference);
                            if (translucent != null)
                            {
                                var expected = value * color.A / 255.0;
                                if (Math.Abs(translucent[py * SurfaceSize + px] - expected) > 1)
                                    Fail($"at ({px}, {py}) the translucent alpha is {translucent[py * SurfaceSize + px]}, not about {expected:F1}");
                            }
                        }
                    }

                    void Fail(string what)
                    {
                        if (failures.Count < 12)
                            failures.Add($"glyph {glyph}, subpixel {subpixel}, color {color}: {what}");
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, $"{face} at {size} px, {edging}:\n{string.Join("\n", failures)}");
        Assert.Equal(0, maxDifference);
        Assert.Equal(0, skiaInkOutsideTheImage);
        // Skia drew each glyph but the space: masks of nothing match too, and prove nothing
        Assert.Equal(GetSpaces(typeface, glyphs), glyphsSkiaDrewNothingOf);
    }

    // A typeface that counts the streams of its file it gives
    private sealed class CountingTypeface(IPlatformTypeface typeface) : IPlatformTypeface
    {
        public int Streams { get; private set; }

        public string FamilyName => typeface.FamilyName;

        public FontWeight Weight => typeface.Weight;

        public FontStyle Style => typeface.Style;

        public FontStretch Stretch => typeface.Stretch;

        public FontSimulations FontSimulations => typeface.FontSimulations;

        public bool TryGetStream([NotNullWhen(true)] out Stream? stream)
        {
            Streams++;
            return typeface.TryGetStream(out stream);
        }

        public bool TryGetTable(OpenTypeTag tag, out ReadOnlyMemory<byte> table) => typeface.TryGetTable(tag, out table);

        public void Dispose()
        {
        }
    }

    // The alpha of a glyph drawn alone into the cleared surface at (x, OriginY)
    private static byte[] DrawGlyph(SKSurface surface, SKFont font, ushort glyph, float x, Color color)
    {
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        using var builder = new SKTextBlobBuilder();
        var run = builder.AllocatePositionedRun(font, 1);
        run.Glyphs[0] = glyph;
        run.Positions[0] = new SKPoint(0, 0);
        using var blob = builder.Build()!;
        using var paint = new SKPaint { Color = new SKColor(color.R, color.G, color.B, color.A), IsAntialias = true };
        canvas.DrawText(blob, x, OriginY, paint);
        canvas.Flush();

        using var pixmap = surface.PeekPixels();
        var pixels = pixmap.GetPixelSpan();
        var alpha = new byte[SurfaceSize * SurfaceSize];
        for (var i = 0; i < alpha.Length; i++)
            alpha[i] = pixels[i * 4 + 3];
        return alpha;
    }
}
