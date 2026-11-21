// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using SkiaSharp;
using Xunit;

namespace Stride.AvaloniaUI.Tests;

/// <summary>
/// The test fonts, embedded in the test assemblies: subsets of Noto Sans Arabic, Noto Sans JP and Noto Color Emoji, in the
/// font collection <c>fonts:Test</c>, and a collection file of the first two, whose faces are in <c>fonts:TestCollection</c>.
/// </summary>
/// <remarks>
/// The families are <c>fonts:Test#Noto Sans Arabic</c>, <c>fonts:Test#Noto Sans JP</c>, <c>fonts:Test#Noto Color Emoji</c>,
/// <c>fonts:TestCollection#Noto Sans Arabic</c> (face 0 of the collection file) and <c>fonts:TestCollection#Noto Sans JP</c>
/// (face 1, not on macOS: see <see cref="HasEveryCollectionFace"/>).
/// </remarks>
internal sealed class TestFonts : FontCollectionBase
{
    /// <summary>
    /// The font files of the collection.
    /// </summary>
    public static readonly string[] Files = ["NotoSansArabic-Regular.ttf", "NotoSansJP-Regular.otf", "NotoColorEmoji-Subset.ttf"];

    /// <summary>
    /// The file of a font collection whose face 0 is the Arabic subset and face 1 the Japanese one.
    /// </summary>
    public const string CollectionFile = "TestCollection.ttc";

    private static bool added;

    /// <summary>
    /// Gets whether <c>fonts:TestCollection</c> has both faces of the collection file. Skia's font manager on macOS
    /// (CoreText) makes typefaces of only the first face of a collection's data, so there it has the Arabic face alone.
    /// </summary>
    public static bool HasEveryCollectionFace { get; } = !OperatingSystem.IsMacOS();

    private TestFonts(Uri key) => Key = key;

    public override Uri Key { get; }

    /// <summary>
    /// Adds the collections to Avalonia's font manager, once Avalonia is set up; later calls do nothing.
    /// </summary>
    public static void EnsureAdded()
    {
        if (added)
            return;

        var fonts = new TestFonts(new Uri("fonts:Test"));
        // The typefaces read their streams for their whole life
        foreach (var file in Files)
            fonts.TryAddGlyphTypeface(new MemoryStream(Read(file)), out _);
        FontManager.Current.AddFontCollection(fonts);

        var collection = new TestFonts(new Uri("fonts:TestCollection"));
        var data = Read(CollectionFile);
        for (var index = 0; index < (HasEveryCollectionFace ? 2 : 1); index++)
            collection.TryAddGlyphTypeface(CreateFace(data, index));
        FontManager.Current.AddFontCollection(collection);
        added = true;
    }

    /// <summary>
    /// Skips a test that needs the second face of the collection file where Skia can't make it (<see cref="HasEveryCollectionFace"/>).
    /// </summary>
    public static void SkipWithoutEveryCollectionFace()
        => Skip.IfNot(HasEveryCollectionFace, "Skia's font manager on macOS (CoreText) makes typefaces of only the first face of a font collection's data.");

    /// <summary>
    /// Reads the bytes of a font file of this folder.
    /// </summary>
    public static byte[] Read(string file)
    {
        using var stream = typeof(TestFonts).Assembly.GetManifestResourceStream("Fonts." + file)
            ?? throw new FileNotFoundException($"The test font {file} is not embedded.", file);
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    // A face of a collection file as the system fonts give it: Skia's typeface of the file and the face's index, whose
    // stream is the whole file. Avalonia loads only the first face of a collection from a stream, and has no public way to
    // make a Skia typeface.
    private static GlyphTypeface CreateFace(byte[] collection, int index)
    {
        var type = Type.GetType("Avalonia.Skia.SkiaTypeface, Avalonia.Skia", throwOnError: true)!;
        var typeface = (IPlatformTypeface)Activator.CreateInstance(type, SKTypeface.FromData(SKData.CreateCopy(collection), index), FontSimulations.None)!;
        return new GlyphTypeface(typeface, FontSimulations.None);
    }
}
