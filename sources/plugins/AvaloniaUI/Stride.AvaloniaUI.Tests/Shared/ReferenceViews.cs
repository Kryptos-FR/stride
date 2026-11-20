// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Stride.AvaloniaUI.Rendering.Fallback;
using AvaloniaImage = Avalonia.Controls.Image;
using Image = Stride.Graphics.Image;
using ImageFileType = Stride.Graphics.ImageFileType;
using Path = Avalonia.Controls.Shapes.Path;
using PixelFormat = Stride.Graphics.PixelFormat;
using StrideColor = Stride.Core.Mathematics.Color;

namespace Stride.AvaloniaUI.Tests;

/// <summary>
/// The reference views: pages of 640×480 pixels that each show one family of what a renderer draws, with nothing that animates.
/// </summary>
internal static class ReferenceViews
{
    /// <summary>
    /// The width of a page, in pixels.
    /// </summary>
    public const int Width = 640;

    /// <summary>
    /// The height of a page, in pixels.
    /// </summary>
    public const int Height = 480;

    /// <summary>
    /// The names of the pages.
    /// </summary>
    public static readonly string[] Pages = ["Fluent", "Shapes", "Text", "Gradients", "Images", "Paths", "Clips", "Opacity", "Effects"];

    /// <summary>
    /// How many pixels of each page Skia on the GPU (Linux, Vulkan, Lavapipe) draws more than 12 away from Skia on the CPU,
    /// as <c>SkiaEdgeAllowanceTests</c> measures them: a renderer may differ from Skia on the CPU on as many there, and on
    /// the few more that <see cref="EdgeAllowances"/> gives other devices.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> EdgeAllowance = new Dictionary<string, int>
    {
        ["Fluent"] = 586,
        ["Shapes"] = 1142,
        ["Text"] = 0,
        ["Gradients"] = 812,
        ["Images"] = 992,
        ["Paths"] = 3846,
        ["Clips"] = 499,
        ["Opacity"] = 349,
        ["Effects"] = 790,
    };

    /// <summary>
    /// The reasons each page may still hand calls to the fallback for: those of steps of the native renderer to come.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, FallbackReason[]> AllowedFallbacks = new Dictionary<string, FallbackReason[]>
    {
        ["Fluent"] = [FallbackReason.Text, FallbackReason.Path, FallbackReason.Brush, FallbackReason.BoxShadow, FallbackReason.OpacityGroup],
        ["Shapes"] = [FallbackReason.Path, FallbackReason.BoxShadow],
        ["Text"] = [FallbackReason.Text],
        ["Gradients"] = [FallbackReason.Brush],
        ["Images"] = [FallbackReason.Bitmap, FallbackReason.Brush],
        ["Paths"] = [FallbackReason.Path, FallbackReason.Stroke],
        ["Clips"] = [FallbackReason.Brush, FallbackReason.GeometryClip],
        ["Opacity"] = [FallbackReason.OpacityGroup, FallbackReason.OpacityMask, FallbackReason.Bitmap],
        ["Effects"] = [FallbackReason.Effect, FallbackReason.Path],
    };

    // The icons of the Fluent theme's templates: the drop-down chevron, the close cross, the back arrow, the refresh arrow,
    // the folder, the expander caret, the check mark and the information sign (the last two are strokes)
    private static readonly string[] FilledIcons =
    [
        "M 18.935547 4.560547 L 19.814453 5.439453 L 10 15.253906 L 0.185547 5.439453 L 1.064453 4.560547 L 10 13.496094 Z",
        "M 11.416016,10 20,1.4160156 18.583984,0 10,8.5839846 1.4160156,0 0,1.4160156 8.5839844,10 0,18.583985 1.4160156,20 10,11.416015 18.583984,20 20,18.583985 Z",
        "M12.7347,4.20949 C13.0332,3.92233 13.508,3.93153 13.7952,4.23005 C14.0823,4.52857 14.0731,5.00335 13.7746,5.29051 L5.50039,13.25 L24.2532,13.25 C24.6674,13.25 25.0032,13.5858 25.0032,13.9999982 C25.0032,14.4142 24.6674,14.75 24.2532,14.75 L5.50137,14.75 L13.7746,22.7085 C14.0731,22.9957 14.0823,23.4705 13.7952,23.769 C13.508,24.0675 13.0332,24.0767 12.7347,23.7896 L3.30673,14.7202 C2.89776,14.3268 2.89776,13.6723 3.30673,13.2788 L12.7347,4.20949 Z",
        "M18.62 3.32c.39 0 .7.29.76.66v3c0 .39-.28.7-.66.76h-3a.77.77 0 0 1-.1-1.52h1.08a7.42 7.42 0 1 0 2.65 4.37.77.77 0 1 1 1.5-.3 8.94 8.94 0 1 1-3-5.12V4.09c0-.43.35-.77.77-.77Z",
        "M 0 3 C 0,1 0,1 2,1 L 5 1 C 5.5,1 6,1 6.5,1.5 L 8 3 L 14 3 C 16,3 16,3 16,5 L 16,12 C 16,14 16,14 14,14 L 2,14 C 0,14 0,14 0,12 Z",
        "M 2.29,3.88 L 7.00,8.59 C 7.27,8.86 7.71,8.86 7.98,8.59 L 12.69,3.88 C 13.14,3.43 12.82,2.67 12.19,2.67 L 2.79,2.67 C 2.16,2.67 1.84,3.43 2.29,3.88 Z",
    ];

    private static readonly string[] StrokedIcons =
    [
        "M0.5,8.5 5,13.5 15.5,3",
        "M14,7 A7,7 0 0,0 0,7 M0,7 A7,7 0 1,0 14,7 M7,3l0,5 M7,9l0,2",
    ];

    private const string Star = "M 50,0 L 79,90 L 2,35 L 98,35 L 21,90 Z";

    // Two squares drawn the same way round, one inside the other
    private const string Squares = "M 0,0 L 90,0 L 90,90 L 0,90 Z M 25,25 L 65,25 L 65,65 L 25,65 Z";

    /// <summary>
    /// Creates the page of a name.
    /// </summary>
    public static Control Create(string page) => page switch
    {
        "Fluent" => CreateFluent(),
        "Shapes" => CreateShapes(),
        "Text" => CreateText(),
        "Gradients" => CreateGradients(),
        "Images" => CreateImages(),
        "Paths" => CreatePaths(),
        "Clips" => CreateClips(),
        "Opacity" => CreateOpacity(),
        "Effects" => CreateEffects(),
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, "No reference view has this name."),
    };

    // Buttons, text, a text box with a selection, check boxes, radio buttons, toggle switches, a slider, a progress bar,
    // a combo box, a list box with a selection, and a scroll viewer whose scroll bars stay shown
    private static Control CreateFluent()
    {
        var left = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new Button { Content = "Button" },
                        new Button { Content = "Accent", Classes = { "accent" } },
                        new Button { Content = "Disabled", IsEnabled = false },
                    },
                },
                new TextBlock { Text = "A text block" },
                new TextBlock { Text = "A text block whose words wrap on a second line", TextWrapping = TextWrapping.Wrap, Width = 220, HorizontalAlignment = HorizontalAlignment.Left },
                new TextBox { Text = "Selected text", Width = 220, HorizontalAlignment = HorizontalAlignment.Left, CaretBrush = Brushes.Transparent, SelectionStart = 0, SelectionEnd = 8 },
                new CheckBox { Content = "On", IsChecked = true },
                new CheckBox { Content = "Off", IsChecked = false },
                new CheckBox { Content = "Indeterminate", IsThreeState = true, IsChecked = null },
                new RadioButton { Content = "Chosen", GroupName = "ReferenceViews", IsChecked = true },
                new RadioButton { Content = "Not chosen", GroupName = "ReferenceViews" },
            },
        };
        var right = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 16,
                    Children =
                    {
                        new ToggleSwitch { IsChecked = true },
                        new ToggleSwitch { IsChecked = false },
                    },
                },
                new Slider { Minimum = 0, Maximum = 100, Value = 40, Width = 260 },
                new ProgressBar { Minimum = 0, Maximum = 100, Value = 60, Width = 260 },
                new ComboBox { ItemsSource = new[] { "First", "Second", "Third" }, SelectedIndex = 1, Width = 260 },
                new ListBox { ItemsSource = new[] { "Apple", "Banana", "Cherry" }, SelectedIndex = 1, Width = 260 },
                new ScrollViewer
                {
                    Width = 260,
                    Height = 96,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                    AllowAutoHide = false,
                    Content = new Border { Width = 480, Height = 240, Background = Diagonal(Colors.DarkSlateBlue, Colors.Teal, Colors.Goldenrod) },
                },
            },
        };
        Grid.SetColumn(right, 1);

        return new Border
        {
            Width = Width,
            Height = Height,
            Background = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
            Padding = new Thickness(24),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*, Auto"),
                Children = { left, right },
            },
        };
    }

    // Rectangles, rounded rectangles, borders of uneven thickness, ellipses, lines and box shadows
    private static Control CreateShapes() => CreatePage(Color.FromRgb(240, 240, 240),
        (new Rectangle { Width = 120, Height = 72, Fill = Brushes.SteelBlue }, 24, 24),
        (new Rectangle { Width = 120, Height = 72, Stroke = Brushes.Crimson, StrokeThickness = 3 }, 168, 24),
        (new Rectangle { Width = 120, Height = 72, Fill = Brushes.Gold, Stroke = Brushes.DarkSlateGray, StrokeThickness = 1 }, 312, 24),
        (new Rectangle { Width = 120, Height = 72, RadiusX = 16, RadiusY = 8, Fill = Brushes.MediumSeaGreen }, 456, 24),
        (new Border { Width = 120, Height = 72, CornerRadius = new CornerRadius(4, 16, 32, 0), Background = Brushes.SteelBlue }, 24, 120),
        (new Border { Width = 120, Height = 72, CornerRadius = new CornerRadius(36), BorderThickness = new Thickness(4), BorderBrush = Brushes.DarkOrange, Background = Brushes.Bisque }, 168, 120),
        (new Border { Width = 120, Height = 72, BorderThickness = new Thickness(1, 4, 8, 2), BorderBrush = Brushes.Crimson, Background = Brushes.LightPink }, 312, 120),
        (new Border { Width = 120, Height = 72, CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(2, 6, 10, 4), BorderBrush = Brushes.Indigo, Background = Brushes.Lavender }, 456, 120),
        (new Ellipse { Width = 120, Height = 72, Fill = Brushes.Orchid }, 24, 216),
        (new Ellipse { Width = 72, Height = 72, Stroke = Brushes.Teal, StrokeThickness = 5 }, 192, 216),
        (new Ellipse { Width = 120, Height = 48, Fill = Brushes.LightSkyBlue, Stroke = Brushes.Navy, StrokeThickness = 2 }, 288, 228),
        (new Line { StartPoint = new Point(0, 0), EndPoint = new Point(184, 0), Stroke = Brushes.Black, StrokeThickness = 1 }, 432, 220),
        (new Line { StartPoint = new Point(0, 0), EndPoint = new Point(184, 32), Stroke = Brushes.DarkGreen, StrokeThickness = 2 }, 432, 236),
        (new Line { StartPoint = new Point(0, 0), EndPoint = new Point(0, 64), Stroke = Brushes.Maroon, StrokeThickness = 4 }, 440, 224),
        (new Line { StartPoint = new Point(0, 64), EndPoint = new Point(120, 8), Stroke = Brushes.RoyalBlue, StrokeThickness = 6 }, 488, 224),
        (new Border { Width = 160, Height = 96, Background = Brushes.White, BoxShadow = BoxShadows.Parse("6 6 12 0 #80000000") }, 32, 336),
        (new Border { Width = 160, Height = 96, CornerRadius = new CornerRadius(20), Background = Brushes.White, BoxShadow = BoxShadows.Parse("0 0 16 4 #C01E90FF") }, 240, 336),
        (new Border { Width = 160, Height = 96, CornerRadius = new CornerRadius(8), Background = Brushes.White, BoxShadow = BoxShadows.Parse("inset 4 4 10 2 #A0800000") }, 448, 336));

    // Inter at several sizes, regular and bold, with Latin and Cyrillic letters, and an underlined line
    private static Control CreateText()
    {
        var lines = new StackPanel { Spacing = 6, Margin = new Thickness(24, 16) };
        foreach (var size in new[] { 9, 12, 14, 18, 24, 36 })
            lines.Children.Add(CreateTextLine("Quartz Жёлудь", size));
        lines.Children.Add(CreateTextLine("Ag Жд", 72));
        lines.Children.Add(new TextBlock { Text = "Underlined line, подчёркнутая строка", FontSize = 18, TextDecorations = TextDecorations.Underline });

        return new Border
        {
            Width = Width,
            Height = Height,
            Background = Brushes.White,
            Child = lines,
            [TextElement.FontFamilyProperty] = new FontFamily("fonts:Inter#Inter"),
            [TextElement.ForegroundProperty] = Brushes.Black,
        };
    }

    private static StackPanel CreateTextLine(string text, double size) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 16,
        Children =
        {
            new TextBlock { Text = text, FontSize = size },
            new TextBlock { Text = text, FontSize = size, FontWeight = FontWeight.Bold },
        },
    };

    // Linear, radial and conic gradients, with each spread method
    private static Control CreateGradients()
    {
        var page = CreatePage(Color.FromRgb(240, 240, 240));
        var spreads = new[] { GradientSpreadMethod.Pad, GradientSpreadMethod.Reflect, GradientSpreadMethod.Repeat };
        for (var column = 0; column < spreads.Length; column++)
        {
            var spread = spreads[column];
            var left = 24 + column * 204;
            Place(page, new Border
            {
                Width = 184,
                Height = 132,
                Background = WithStops(new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0.35, 0.3, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0.65, 0.7, RelativeUnit.Relative),
                    SpreadMethod = spread,
                }),
            }, left, 24);
            Place(page, new Border
            {
                Width = 184,
                Height = 132,
                Background = WithStops(new RadialGradientBrush
                {
                    Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                    GradientOrigin = new RelativePoint(0.4, 0.4, RelativeUnit.Relative),
                    RadiusX = new RelativeScalar(0.25, RelativeUnit.Relative),
                    RadiusY = new RelativeScalar(0.2, RelativeUnit.Relative),
                    SpreadMethod = spread,
                }),
            }, left, 172);
            Place(page, new Ellipse
            {
                Width = 184,
                Height = 132,
                Fill = WithStops(new ConicGradientBrush
                {
                    Center = new RelativePoint(0.4, 0.6, RelativeUnit.Relative),
                    Angle = 30 + column * 60,
                    SpreadMethod = spread,
                }),
            }, left, 320);
        }
        return page;
    }

    // Bitmaps decoded from PNGs made here, drawn at 1:1, scaled, and as tiled image brushes
    private static Control CreateImages()
    {
        var pattern = CreatePng(32, 32, (x, y) => new StrideColor((byte)(x * 8), (byte)(y * 8), (byte)((x / 4 + y / 4) % 2 * 255), 255));
        var glow = CreatePng(32, 32, (x, y) =>
        {
            var distance = Math.Sqrt((x - 15.5) * (x - 15.5) + (y - 15.5) * (y - 15.5));
            return new StrideColor((byte)255, (byte)(x * 8), (byte)0, (byte)Math.Clamp(255 - distance * 15, 0, 255));
        });

        var smooth = new AvaloniaImage { Source = pattern, Width = 128, Height = 128, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapInterpolationMode(smooth, BitmapInterpolationMode.HighQuality);
        var nearest = new AvaloniaImage { Source = pattern, Width = 128, Height = 128, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapInterpolationMode(nearest, BitmapInterpolationMode.None);

        return CreatePage(Color.FromRgb(200, 200, 200),
            (new AvaloniaImage { Source = pattern, Stretch = Stretch.None }, 24, 24),
            (new AvaloniaImage { Source = glow, Stretch = Stretch.None }, 72, 24),
            (new AvaloniaImage { Source = pattern, Width = 16, Height = 16, Stretch = Stretch.Fill }, 120, 32),
            (smooth, 24, 80),
            (nearest, 176, 80),
            (new AvaloniaImage { Source = glow, Width = 128, Height = 128, Stretch = Stretch.Fill }, 328, 80),
            (new AvaloniaImage { Source = pattern, Width = 96, Height = 48, Stretch = Stretch.Uniform }, 480, 80),
            (new Border { Width = 184, Height = 176, Background = new ImageBrush(pattern) { TileMode = TileMode.Tile, DestinationRect = new RelativeRect(0, 0, 24, 24, RelativeUnit.Absolute), Stretch = Stretch.Fill } }, 24, 280),
            (new Border { Width = 184, Height = 176, Background = new ImageBrush(pattern) { TileMode = TileMode.FlipXY, DestinationRect = new RelativeRect(0, 0, 40, 40, RelativeUnit.Absolute), Stretch = Stretch.Fill } }, 228, 280),
            (new Border { Width = 184, Height = 176, Background = Brushes.SteelBlue, Child = new Border { Background = new ImageBrush(glow) { TileMode = TileMode.Tile, DestinationRect = new RelativeRect(0, 0, 0.25, 0.25, RelativeUnit.Relative), Stretch = Stretch.Fill } } }, 432, 280));
    }

    // The Fluent theme's icon geometries, strokes with each cap and join, dashes, and both fill rules
    private static Control CreatePaths()
    {
        var page = CreatePage(Color.FromRgb(240, 240, 240));
        for (var i = 0; i < FilledIcons.Length; i++)
            Place(page, new Path { Data = Geometry.Parse(FilledIcons[i]), Fill = Brushes.Black, Width = 48, Height = 48, Stretch = Stretch.Uniform }, 24 + i * 76, 24);
        for (var i = 0; i < StrokedIcons.Length; i++)
            Place(page, new Path { Data = Geometry.Parse(StrokedIcons[i]), Stroke = Brushes.Black, StrokeThickness = 2, Width = 48, Height = 48, Stretch = Stretch.Uniform }, 24 + (FilledIcons.Length + i) * 76, 24);

        var caps = new[] { PenLineCap.Flat, PenLineCap.Round, PenLineCap.Square };
        var joins = new[] { PenLineJoin.Miter, PenLineJoin.Round, PenLineJoin.Bevel };
        for (var i = 0; i < 3; i++)
        {
            var left = 48 + i * 200;
            Place(page, new Path { Data = Geometry.Parse("M 0,0 L 120,0"), Stroke = Brushes.SteelBlue, StrokeThickness = 16, StrokeLineCap = caps[i] }, left, 120);
            Place(page, new Path { Data = Geometry.Parse("M 0,0 L 120,0"), Stroke = Brushes.Crimson, StrokeThickness = 1 }, left, 120);
            Place(page, new Path { Data = Geometry.Parse("M 0,64 L 56,0 L 112,64"), Stroke = Brushes.DarkGreen, StrokeThickness = 14, StrokeJoin = joins[i] }, left, 160);
        }

        Place(page, new Path { Data = Geometry.Parse("M 0,0 L 280,0"), Stroke = Brushes.Black, StrokeThickness = 3, StrokeDashArray = new AvaloniaList<double> { 4, 2 } }, 24, 264);
        Place(page, new Path { Data = Geometry.Parse("M 0,0 L 280,0"), Stroke = Brushes.Black, StrokeThickness = 3, StrokeDashArray = new AvaloniaList<double> { 1, 1, 3, 1 }, StrokeDashOffset = 1 }, 24, 288);
        Place(page, new Path { Data = Geometry.Parse("M 0,0 L 280,0"), Stroke = Brushes.Black, StrokeThickness = 6, StrokeDashArray = new AvaloniaList<double> { 0, 2 }, StrokeLineCap = PenLineCap.Round }, 24, 316);
        Place(page, new Path { Data = Geometry.Parse("M 0,40 C 70,-20 140,100 280,0"), Stroke = Brushes.Purple, StrokeThickness = 4, StrokeDashArray = new AvaloniaList<double> { 3, 2 }, StrokeLineCap = PenLineCap.Round }, 24, 336);
        Place(page, new Ellipse { Width = 200, Height = 100, Stroke = Brushes.DarkOrange, StrokeThickness = 5, StrokeDashArray = new AvaloniaList<double> { 2, 1 } }, 384, 264);

        Place(page, new Path { Data = Geometry.Parse("F0 " + Star), Fill = Brushes.Goldenrod, Stroke = Brushes.Black, StrokeThickness = 1 }, 48, 384);
        Place(page, new Path { Data = Geometry.Parse("F1 " + Star), Fill = Brushes.Goldenrod, Stroke = Brushes.Black, StrokeThickness = 1 }, 192, 384);
        Place(page, new Path { Data = Geometry.Parse("F0 " + Squares), Fill = Brushes.SteelBlue }, 368, 384);
        Place(page, new Path { Data = Geometry.Parse("F1 " + Squares), Fill = Brushes.SteelBlue }, 496, 384);
        return page;
    }

    // Rectangle, rounded and geometry clips, and a rotated clip
    private static Control CreateClips()
    {
        var rectangleClip = new Canvas { Width = 184, Height = 132, ClipToBounds = true };
        Place(rectangleClip, new Ellipse { Width = 200, Height = 160, Fill = Diagonal(Colors.Crimson, Colors.Gold, Colors.SteelBlue) }, 60, 40);

        var roundedClip = new Border { Width = 184, Height = 132, CornerRadius = new CornerRadius(32), ClipToBounds = true, Child = CreateStripes(184, 132) };

        var triangleClip = CreateStripes(184, 132);
        triangleClip.Clip = Geometry.Parse("M 92,0 L 184,132 L 0,132 Z");

        var ellipseClip = CreateStripes(184, 132);
        ellipseClip.Clip = new EllipseGeometry(new Rect(16, 8, 152, 116));

        var rotatedClip = new Canvas { Width = 150, Height = 90, ClipToBounds = true, RenderTransform = new RotateTransform(25) };
        Place(rotatedClip, CreateStripes(260, 200), -50, -50);

        var outerClip = new Canvas { Width = 184, Height = 132, ClipToBounds = true, Background = Brushes.LightGray };
        var innerClip = new Canvas { Width = 150, Height = 90, ClipToBounds = true, RenderTransform = new RotateTransform(-30) };
        Place(innerClip, CreateStripes(260, 200), -50, -50);
        Place(outerClip, innerClip, 70, 60);

        return CreatePage(Color.FromRgb(240, 240, 240),
            (rectangleClip, 24, 24),
            (roundedClip, 228, 24),
            (triangleClip, 432, 24),
            (rotatedClip, 40, 230),
            (ellipseClip, 228, 210),
            (outerClip, 432, 210));
    }

    // Nested opacity, an opacity mask, and controls drawn through an offscreen layer (a bitmap cache)
    private static Control CreateOpacity()
    {
        var page = CreatePage(Colors.White);
        for (var column = 0; column < Width / 40; column += 2)
            Place(page, new Rectangle { Width = 40, Height = Height, Fill = Brushes.DarkSlateGray }, column * 40, 0);

        var nested = new Border
        {
            Width = 184,
            Height = 184,
            Background = Brushes.Crimson,
            Opacity = 0.8,
            Child = new Border
            {
                Margin = new Thickness(24),
                Background = Brushes.RoyalBlue,
                Opacity = 0.6,
                Child = new Border { Margin = new Thickness(24), Background = Brushes.LimeGreen, Opacity = 0.5 },
            },
        };
        Place(page, nested, 24, 24);

        Place(page, new Border
        {
            Width = 184,
            Height = 184,
            Background = Brushes.DarkOrange,
            OpacityMask = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Colors.Black, 0), new GradientStop(Colors.Transparent, 1) },
            },
        }, 228, 24);
        Place(page, new Ellipse
        {
            Width = 184,
            Height = 184,
            Fill = Diagonal(Colors.Crimson, Colors.Gold, Colors.SteelBlue),
            OpacityMask = new RadialGradientBrush { GradientStops = { new GradientStop(Colors.Black, 0.4), new GradientStop(Colors.Transparent, 1) } },
        }, 432, 24);

        Place(page, CreateCachedContent(1), 24, 256);
        Place(page, CreateCachedContent(0.5), 228, 256);
        return page;
    }

    private static Control CreateCachedContent(double opacity)
    {
        var content = new Canvas { Width = 184, Height = 184, Opacity = opacity, CacheMode = new BitmapCache() };
        Place(content, new Ellipse { Width = 184, Height = 184, Fill = Brushes.MediumSeaGreen }, 0, 0);
        Place(content, new Rectangle { Width = 96, Height = 96, Fill = Brushes.Indigo, RenderTransform = new RotateTransform(30) }, 44, 44);
        return content;
    }

    // Blur and drop shadow effects
    private static Control CreateEffects()
    {
        var sharpBlur = CreateStripes(184, 132);
        sharpBlur.Effect = new BlurEffect { Radius = 4 };

        return CreatePage(Color.FromRgb(240, 240, 240),
            (sharpBlur, 24, 24),
            (new Ellipse { Width = 132, Height = 132, Fill = Brushes.Crimson, Effect = new BlurEffect { Radius = 16 } }, 252, 24),
            (new Border { Width = 132, Height = 132, Background = Brushes.Gold, Child = new Ellipse { Margin = new Thickness(24), Fill = Brushes.Navy }, Effect = new BlurEffect { Radius = 8 } }, 456, 24),
            (new Border { Width = 184, Height = 132, CornerRadius = new CornerRadius(16), Background = Brushes.SteelBlue, Effect = new DropShadowEffect { OffsetX = 6, OffsetY = 6, BlurRadius = 12, Color = Colors.Black, Opacity = 0.7 } }, 24, 240),
            (new Path { Data = Geometry.Parse(Star), Fill = Brushes.Goldenrod, Width = 132, Height = 132, Stretch = Stretch.Uniform, Effect = new DropShadowEffect { OffsetX = 8, OffsetY = 4, BlurRadius = 0, Color = Colors.DarkRed, Opacity = 1 } }, 252, 240),
            (new Ellipse { Width = 132, Height = 132, Fill = Brushes.MediumSeaGreen, Effect = new DropShadowDirectionEffect { Direction = 315, ShadowDepth = 10, BlurRadius = 6, Color = Colors.Black, Opacity = 0.5 } }, 456, 240));
    }

    private static Canvas CreatePage(Color background, params (Control Control, double Left, double Top)[] content)
    {
        var page = new Canvas { Width = Width, Height = Height, Background = new SolidColorBrush(background) };
        foreach (var (control, left, top) in content)
            Place(page, control, left, top);
        return page;
    }

    private static void Place(Canvas canvas, Control control, double left, double top)
    {
        Canvas.SetLeft(control, left);
        Canvas.SetTop(control, top);
        canvas.Children.Add(control);
    }

    // Vertical bands of three colors under a horizontal one, to show what a clip or an effect does to edges
    private static Canvas CreateStripes(double width, double height)
    {
        var colors = new[] { Brushes.Crimson, Brushes.Gold, Brushes.SteelBlue };
        var stripes = new Canvas { Width = width, Height = height, Background = Brushes.White };
        for (var i = 0; i * 20 < width; i++)
            Place(stripes, new Rectangle { Width = 12, Height = height, Fill = colors[i % colors.Length] }, i * 20, 0);
        Place(stripes, new Rectangle { Width = width, Height = 16, Fill = Brushes.Black }, 0, height / 2 - 8);
        return stripes;
    }

    private static LinearGradientBrush Diagonal(Color first, Color second, Color third) => new()
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(first, 0), new GradientStop(second, 0.5), new GradientStop(third, 1) },
    };

    private static T WithStops<T>(T brush) where T : GradientBrush
    {
        brush.GradientStops.Add(new GradientStop(Colors.Crimson, 0));
        brush.GradientStops.Add(new GradientStop(Colors.Gold, 0.5));
        brush.GradientStops.Add(new GradientStop(Colors.RoyalBlue, 1));
        return brush;
    }

    // A PNG made by Stride, decoded by the renderer's bitmap loader
    private static Bitmap CreatePng(int width, int height, Func<int, int, StrideColor> colorAt)
    {
        using var image = Image.New2D(width, height, 1, PixelFormat.R8G8B8A8_UNorm);
        var pixels = image.PixelBuffer[0];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                pixels.SetPixel(x, y, colorAt(x, y));
        }

        using var stream = new MemoryStream();
        image.Save(stream, ImageFileType.Png);
        stream.Position = 0;
        return new Bitmap(stream);
    }
}
