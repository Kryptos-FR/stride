// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Stride.AvaloniaUI.Rendering.Fallback;

namespace Stride.AvaloniaUI.Tests;

/// <summary>
/// The text cases: views of 240×160 pixels that each draw one kind of text, Inter at 14 pixels on white unless said, to
/// compare the glyphs of a renderer with Skia's.
/// </summary>
internal static class TextCases
{
    /// <summary>
    /// The width of a case, in pixels.
    /// </summary>
    public const int Width = 240;

    /// <summary>
    /// The height of a case, in pixels.
    /// </summary>
    public const int Height = 160;

    /// <summary>
    /// The names of the cases.
    /// </summary>
    public static readonly string[] Names =
    [
        "FractionalPositions", "Scaled4By3", "Scaled5By4", "ScaleTransform", "Colors", "HintingModes", "Aliased", "SubpixelRequested",
        "UnalignedBaseline", "Simulations", "TwoColorsSameGlyphs", "Rotated", "LargeText", "ColorEmoji", "GradientText", "Whitespace",
        "CollectionFaces", "Size72",
    ];

    /// <summary>
    /// How many pixels of each case Skia on the GPU (Linux, Vulkan, Lavapipe) draws more than 12 away from Skia on the CPU,
    /// as <c>SkiaEdgeAllowanceTests</c> measures them (<see cref="ShapeCases.EdgeAllowance"/>).
    /// </summary>
    /// <remarks>
    /// Skia on the GPU draws subpixel text where the CPU's targets draw grayscale text (<c>SubpixelRequested</c>, and the
    /// third line of <c>Aliased</c>), and the edges of glyphs drawn as paths apart (<c>LargeText</c>). The native renderer
    /// draws as the CPU in each, so its comparisons with the CPU allow none of these pixels.
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, int> EdgeAllowance = new Dictionary<string, int>
    {
        ["FractionalPositions"] = 0,
        ["Scaled4By3"] = 0,
        ["Scaled5By4"] = 0,
        ["ScaleTransform"] = 0,
        ["Colors"] = 0,
        ["HintingModes"] = 0,
        ["Aliased"] = 567,
        ["SubpixelRequested"] = 567,
        ["UnalignedBaseline"] = 0,
        ["Simulations"] = 0,
        ["TwoColorsSameGlyphs"] = 0,
        ["Rotated"] = 0,
        ["LargeText"] = 1367,
        ["ColorEmoji"] = 0,
        ["GradientText"] = 0,
        ["Whitespace"] = 0,
        ["CollectionFaces"] = 0,
        ["Size72"] = 0,
    };

    /// <summary>
    /// How many pixels of each case the native renderer draws more than 12 away from Skia on the CPU where Skia's
    /// glyphs aren't FreeType's (<see cref="EdgeAllowances.ForText(Stride.Graphics.GraphicsAdapter, int, int)"/>), as the
    /// Windows game tests of CI measure them (run 44621323042, Vulkan; CollectionFaces and Size72 in run 44771862294, the
    /// same on the three graphics APIs).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> OtherGlyphsAllowance = new Dictionary<string, int>
    {
        ["FractionalPositions"] = 740,
        ["Scaled4By3"] = 478,
        ["Scaled5By4"] = 528,
        ["ScaleTransform"] = 155,
        ["Colors"] = 470,
        ["HintingModes"] = 784,
        ["Aliased"] = 404,
        ["SubpixelRequested"] = 200,
        ["UnalignedBaseline"] = 418,
        ["Simulations"] = 247,
        ["TwoColorsSameGlyphs"] = 68,
        ["Rotated"] = 0,
        ["LargeText"] = 0,
        ["ColorEmoji"] = 0,
        ["GradientText"] = 0,
        ["Whitespace"] = 6,
        ["CollectionFaces"] = 551,
        ["Size72"] = 255,
    };

    /// <summary>
    /// The reasons the native renderer hands each case's calls to the fallback for.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, FallbackReason[]> ExpectedReasons = new Dictionary<string, FallbackReason[]>
    {
        ["FractionalPositions"] = [],
        ["Scaled4By3"] = [],
        ["Scaled5By4"] = [],
        ["ScaleTransform"] = [],
        ["Colors"] = [],
        ["HintingModes"] = [],
        ["Aliased"] = [],
        ["SubpixelRequested"] = [],
        ["UnalignedBaseline"] = [],
        ["Simulations"] = [FallbackReason.TextSimulation],
        ["TwoColorsSameGlyphs"] = [],
        ["Rotated"] = [FallbackReason.TextTransform],
        ["LargeText"] = [FallbackReason.LargeText],
        ["ColorEmoji"] = [FallbackReason.ColorFont],
        ["GradientText"] = [FallbackReason.Brush],
        ["Whitespace"] = [],
        ["CollectionFaces"] = [],
        ["Size72"] = [],
    };

    private const string Line = "Quartz Жёлудь";

    private static readonly Color Background = Colors.White;

    /// <summary>
    /// Gets the scaling of the view of a case: its pixels per logical unit.
    /// </summary>
    public static double GetScaling(string name) => name switch
    {
        "Scaled4By3" => 4 / 3.0,
        "Scaled5By4" => 5 / 4.0,
        _ => 1,
    };

    /// <summary>
    /// Skips a test of a case whose fonts Skia can't make on this system: <c>CollectionFaces</c> needs both faces of the test
    /// collection (<see cref="TestFonts.HasEveryCollectionFace"/>), and <c>ColorEmoji</c> the colour emoji font
    /// (<see cref="TestFonts.HasColorEmoji"/>). Other names, of cases or not, don't skip.
    /// </summary>
    public static void SkipWithoutItsFonts(string name)
    {
        if (name == "CollectionFaces")
            TestFonts.SkipWithoutEveryCollectionFace();
        else if (name == "ColorEmoji")
            TestFonts.SkipWithoutColorEmoji();
    }

    /// <summary>
    /// Creates the case of a name.
    /// </summary>
    public static Control Create(string name) => name switch
    {
        "FractionalPositions" => FractionalPositions(),
        "Scaled4By3" or "Scaled5By4" => Page((Text(Line), 8, 8), (Text(Line), 8, 30)),
        "ScaleTransform" => Page((new TextBlock { Text = "Quartz", RenderTransform = new ScaleTransform(1.5, 1.5) }, 40, 40)),
        "Colors" => ColorLines(),
        "HintingModes" => Page(
            (WithHinting(Text(Line), TextHintingMode.None), 8, 8),
            (WithHinting(Text(Line), TextHintingMode.Light), 8, 30),
            (WithHinting(Text(Line), TextHintingMode.Strong), 8, 52)),
        "Aliased" => Aliased(),
        "SubpixelRequested" => WithTopLevelModeCleared(Page((WithRenderingMode(Text(Line), TextRenderingMode.SubpixelAntialias), 8, 8))),
        "UnalignedBaseline" => Page((Unrounded(WithBaseline(Text(Line), BaselinePixelAlignment.Unaligned)), 8, 20.3), (WithBaseline(Text(Line), BaselinePixelAlignment.Unaligned), 8, 50)),
        "Simulations" => Page(
            (new TextBlock { Text = Line, FontStyle = FontStyle.Italic }, 8, 8),
            (new TextBlock { Text = "مرحبا بالعالم", FontFamily = new FontFamily("fonts:Test#Noto Sans Arabic"), FontWeight = FontWeight.Bold }, 8, 30),
            (new TextBlock { Text = "مرحبا بالعالم", FontFamily = new FontFamily("fonts:Test#Noto Sans Arabic"), FontWeight = FontWeight.Bold, FontStyle = FontStyle.Italic }, 8, 60)),
        "TwoColorsSameGlyphs" => TwoColors(),
        "Rotated" => Page((new TextBlock { Text = "Quartz", RenderTransform = new RotateTransform(30) }, 60, 60)),
        "LargeText" => Page((new TextBlock { Text = "Ag", FontSize = 300 }, -20, -120)),
        "ColorEmoji" => Page((new TextBlock { Text = "😀 ❤", FontFamily = new FontFamily("fonts:Test#Noto Color Emoji"), FontSize = 24 }, 8, 8)),
        "GradientText" => Page((new TextBlock
        {
            Text = Line,
            Foreground = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Colors.Navy, 0), new GradientStop(Colors.Crimson, 1) },
            },
        }, 8, 8)),
        "Whitespace" => Page(
            (Text("     "), 8, 8),
            (new TextBlock(), 8, 30),
            (new TextBlock { Text = "a b", Foreground = new SolidColorBrush(Colors.Black, 0.5) }, 8, 52)),
        "CollectionFaces" => Page(
            (new TextBlock { Text = "مرحبا بالعالم", FontFamily = new FontFamily("fonts:TestCollection#Noto Sans Arabic"), FontSize = 20 }, 8, 8),
            (new TextBlock { Text = "日本語", FontFamily = new FontFamily("fonts:TestCollection#Noto Sans JP"), FontSize = 24 }, 8, 60)),
        "Size72" => Page((new TextBlock { Text = "Ag Ж", FontSize = 72 }, 8, 8)),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "No text case has this name."),
    };

    // "Quartz" at x = 10 + k/8 and y = 10 + 18k + k/8: each eighth of a pixel, across the subpixel positions and the
    // rounding of the baseline
    private static Canvas FractionalPositions()
    {
        var page = Page();
        page.UseLayoutRounding = false;
        for (var k = 0; k < 8; k++)
            Add(page, Unrounded(Text("Quartz")), 10 + k / 8.0, 10 + 18 * k + k / 8.0);
        return page;
    }

    // Black, white on a dark band, half-transparent red on a mid-gray band, and the gray of each luminance table
    private static Canvas ColorLines()
    {
        var page = Page();
        Add(page, new Border { Width = Width, Height = 20, Background = new SolidColorBrush(Color.FromRgb(32, 32, 32)) }, 0, 22);
        Add(page, new Border { Width = Width, Height = 20, Background = new SolidColorBrush(Color.FromRgb(128, 128, 128)) }, 0, 42);
        Add(page, Text("Quartz", Colors.Black), 8, 4);
        Add(page, Text("Quartz", Colors.White), 8, 24);
        Add(page, Text("Quartz", Color.FromArgb(128, 255, 0, 0)), 8, 44);
        for (var table = 0; table < 8; table++)
        {
            var gray = (byte)(table * 32 + 16);
            Add(page, Text("Quartz", Color.FromRgb(gray, gray, gray)), 8 + table / 4 * 110, 66 + table % 4 * 22);
        }
        return page;
    }

    // A line in TextRenderingMode.Alias, one under aliased edges, one with neither, and one in the render options' obsolete
    // Alias, which a context without subpixel text replaces with Antialias before reading it
    private static Canvas Aliased() => WithTopLevelModeCleared(Page(
        (new Border { Width = Width, Height = 24, Background = new SolidColorBrush(Color.FromRgb(128, 128, 128)) }, 0, 26),
        (WithRenderingMode(Text(Line), TextRenderingMode.Alias), 8, 8),
        (WithAliasedEdges(Text(Line)), 8, 30),
        (Text(Line), 8, 52),
        (WithObsoleteRenderingMode(Text(Line), TextRenderingMode.Alias), 8, 74)));

    // The view's top level asks for antialiased text, and a parent's text options win over its children's: a case whose
    // lines ask for a mode clears the top level's once it is attached
    private static Canvas WithTopLevelModeCleared(Canvas page)
    {
        page.AttachedToVisualTree += (_, _) => TextOptions.SetTextRenderingMode(TopLevel.GetTopLevel(page)!, TextRenderingMode.Unspecified);
        return page;
    }

    // One line in black and one in white, with the same glyphs, side by side on a gray band
    private static Canvas TwoColors()
    {
        var page = Page();
        Add(page, new Border { Width = Width, Height = 40, Background = new SolidColorBrush(Color.FromRgb(128, 128, 128)) }, 0, 20);
        Add(page, Text("Quartz", Colors.Black), 8, 30);
        Add(page, Text("Quartz", Colors.White), 120, 30);
        return page;
    }

    private static Canvas Page(params (Control Control, double Left, double Top)[] content)
    {
        var page = new Canvas
        {
            Background = new SolidColorBrush(Background),
            [TextElement.FontFamilyProperty] = new FontFamily("fonts:Inter#Inter"),
            [TextElement.ForegroundProperty] = Brushes.Black,
        };
        foreach (var (control, left, top) in content)
            Add(page, control, left, top);
        return page;
    }

    private static void Add(Canvas page, Control control, double left, double top)
    {
        Canvas.SetLeft(control, left);
        Canvas.SetTop(control, top);
        page.Children.Add(control);
    }

    private static TextBlock Text(string text) => new() { Text = text };

    private static TextBlock Text(string text, Color color) => new() { Text = text, Foreground = new SolidColorBrush(color) };

    private static T Unrounded<T>(T control) where T : Control
    {
        control.UseLayoutRounding = false;
        return control;
    }

    private static T WithHinting<T>(T control, TextHintingMode mode) where T : Visual
    {
        TextOptions.SetTextHintingMode(control, mode);
        return control;
    }

    private static T WithRenderingMode<T>(T control, TextRenderingMode mode) where T : Visual
    {
        TextOptions.SetTextRenderingMode(control, mode);
        return control;
    }

    private static T WithBaseline<T>(T control, BaselinePixelAlignment alignment) where T : Visual
    {
        TextOptions.SetBaselinePixelAlignment(control, alignment);
        return control;
    }

    private static T WithObsoleteRenderingMode<T>(T control, TextRenderingMode mode) where T : Visual
    {
#pragma warning disable CS0618 // The render options' text mode is obsolete, and Skia's context still reads it
        RenderOptions.SetTextRenderingMode(control, mode);
#pragma warning restore CS0618
        return control;
    }

    private static T WithAliasedEdges<T>(T control) where T : Visual
    {
        RenderOptions.SetEdgeMode(control, EdgeMode.Aliased);
        return control;
    }
}
