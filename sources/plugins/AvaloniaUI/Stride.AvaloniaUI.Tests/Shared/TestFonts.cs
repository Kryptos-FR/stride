// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Fonts;

namespace Stride.AvaloniaUI.Tests;

/// <summary>
/// The test fonts, embedded in the test assemblies: subsets of Noto Sans Arabic, Noto Sans JP and Noto Color Emoji, in the
/// font collection <c>fonts:Test</c>, and a collection file of the first two.
/// </summary>
/// <remarks>
/// The families are <c>fonts:Test#Noto Sans Arabic</c>, <c>fonts:Test#Noto Sans JP</c> and <c>fonts:Test#Noto Color Emoji</c>.
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

    private static readonly Uri CollectionKey = new("fonts:Test");
    private static bool added;

    private TestFonts()
    {
        // The typefaces read their streams for their whole life
        foreach (var file in Files)
            TryAddGlyphTypeface(new MemoryStream(Read(file)), out _);
    }

    public override Uri Key => CollectionKey;

    /// <summary>
    /// Adds the collection to Avalonia's font manager, once Avalonia is set up; later calls do nothing.
    /// </summary>
    public static void EnsureAdded()
    {
        if (added)
            return;
        FontManager.Current.AddFontCollection(new TestFonts());
        added = true;
    }

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
}
