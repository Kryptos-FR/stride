// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace Stride.AvaloniaUI.Tests;

/// <summary>
/// The shape cases: views of 240×160 pixels that each draw one kind of shape on a solid background, to compare the edges
/// of a renderer with Skia's.
/// </summary>
internal static class ShapeCases
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
        "FilledRectangle", "FractionalRectangle", "ThinStroke", "RectangleStroke1", "RectangleStroke3", "RoundedPerCorner",
        "RoundedStroke", "RadiiTooLarge", "EllipseFill", "EllipseStroke", "CircleStroke", "DrawEllipseCall", "LineFlat",
        "LineSquare", "LineRound", "DiagonalLine", "RotatedRectangle", "ScaledView", "HalfOpaqueBrush", "AliasedEllipse",
        "Degenerate", "HugeRectangle", "GradientRectangle", "ShadowedRectangle", "ScissorClip", "FractionalClip", "RotatedClip",
        "RoundedClip", "RotatedInScissor", "TextInRoundedClip", "TwoShaderClips", "GeometryClipCase", "ThinFills",
    ];

    /// <summary>
    /// How many pixels of each case Skia on the GPU (Linux, Vulkan, Lavapipe) draws more than 12 away from Skia on the CPU,
    /// as <c>SkiaEdgeAllowanceTests</c> measures them: a renderer may differ from Skia on the CPU on as many there, and on
    /// the few more that <see cref="EdgeAllowances"/> gives other devices.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> EdgeAllowance = new Dictionary<string, int>
    {
        ["FilledRectangle"] = 0,
        ["FractionalRectangle"] = 4,
        ["ThinStroke"] = 760,
        ["RectangleStroke1"] = 4,
        ["RectangleStroke3"] = 0,
        ["RoundedPerCorner"] = 59,
        ["RoundedStroke"] = 233,
        ["RadiiTooLarge"] = 52,
        ["EllipseFill"] = 167,
        ["EllipseStroke"] = 258,
        ["CircleStroke"] = 249,
        ["DrawEllipseCall"] = 372,
        ["LineFlat"] = 0,
        ["LineSquare"] = 0,
        ["LineRound"] = 17,
        ["DiagonalLine"] = 386,
        ["RotatedRectangle"] = 250,
        ["ScaledView"] = 431,
        ["HalfOpaqueBrush"] = 0,
        ["AliasedEllipse"] = 67,
        ["Degenerate"] = 0,
        ["HugeRectangle"] = 0,
        ["GradientRectangle"] = 0,
        ["ShadowedRectangle"] = 0,
        ["ScissorClip"] = 0,
        ["FractionalClip"] = 200,
        ["RotatedClip"] = 124,
        ["RoundedClip"] = 103,
        ["RotatedInScissor"] = 35,
        ["TextInRoundedClip"] = 144,
        ["TwoShaderClips"] = 268,
        ["GeometryClipCase"] = 6,
        ["ThinFills"] = 0,
    };

    private static readonly Color Background = Color.FromRgb(240, 240, 240);

    /// <summary>
    /// Gets the scaling of the view of a case: its pixels per logical unit.
    /// </summary>
    public static double GetScaling(string name) => name == "ScaledView" ? 1.5 : 1;

    /// <summary>
    /// Creates the case of a name.
    /// </summary>
    public static Control Create(string name) => name switch
    {
        "FilledRectangle" => Page((new Rectangle { Width = 120, Height = 72, Fill = Brushes.SteelBlue }, 60, 44)),
        "FractionalRectangle" => Page((new Rectangle { Width = 120.5, Height = 72.25, Fill = Brushes.SteelBlue, UseLayoutRounding = false }, 10.25, 20.5)),
        "ThinStroke" => Page((new Rectangle { Width = 120, Height = 72, Stroke = Brushes.Black, StrokeThickness = 0.5 }, 60, 44)),
        "RectangleStroke1" => Page((new Rectangle { Width = 120, Height = 72, Stroke = Brushes.Crimson, StrokeThickness = 1 }, 60, 44)),
        "RectangleStroke3" => Page((new Rectangle { Width = 120, Height = 72, Fill = Brushes.Gold, Stroke = Brushes.DarkSlateGray, StrokeThickness = 3 }, 60, 44)),
        "RoundedPerCorner" => Page((new Border { Width = 120, Height = 72, CornerRadius = new CornerRadius(4, 16, 32, 0), Background = Brushes.SteelBlue }, 60, 44)),
        "RoundedStroke" => Page((new Border { Width = 120, Height = 72, CornerRadius = new CornerRadius(36), BorderThickness = new Thickness(4), BorderBrush = Brushes.DarkOrange, Background = Brushes.Bisque }, 60, 44)),
        "RadiiTooLarge" => Page((new Border { Width = 120, Height = 40, CornerRadius = new CornerRadius(40), BorderThickness = new Thickness(2), BorderBrush = Brushes.DarkGreen, Background = Brushes.MediumSeaGreen }, 60, 60)),
        "EllipseFill" => Page((new Ellipse { Width = 120, Height = 72, Fill = Brushes.Orchid }, 60, 44)),
        "EllipseStroke" => Page((new Ellipse { Width = 120, Height = 48, Fill = Brushes.LightSkyBlue, Stroke = Brushes.Navy, StrokeThickness = 2 }, 60, 56)),
        "CircleStroke" => Page((new Ellipse { Width = 72, Height = 72, Stroke = Brushes.Teal, StrokeThickness = 5 }, 84, 44)),
        "DrawEllipseCall" => Page((new DrawingControl(context => context.DrawEllipse(Brushes.Orchid, new Pen(Brushes.Purple, 3), new Point(120, 80), 70, 44)), 0, 0)),
        "LineFlat" => Page((CreateLine(PenLineCap.Flat), 60, 80)),
        "LineSquare" => Page((CreateLine(PenLineCap.Square), 60, 80)),
        "LineRound" => Page((CreateLine(PenLineCap.Round), 60, 80)),
        "DiagonalLine" => Page((new Line { StartPoint = new Point(0, 0), EndPoint = new Point(184, 64), Stroke = Brushes.DarkGreen, StrokeThickness = 3 }, 28, 48)),
        "RotatedRectangle" => Page((new Rectangle { Width = 120, Height = 72, Fill = Brushes.Gold, Stroke = Brushes.DarkSlateGray, StrokeThickness = 3, RenderTransform = new RotateTransform(30) }, 60, 44)),
        "ScaledView" => Page(
            (new Rectangle { Width = 61, Height = 41, Stroke = Brushes.Crimson, StrokeThickness = 1 }, 10, 10),
            (new Ellipse { Width = 51, Height = 37, Fill = Brushes.LightSkyBlue, Stroke = Brushes.Navy, StrokeThickness = 2 }, 84, 10),
            (new Border { Width = 61, Height = 37, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), BorderBrush = Brushes.DarkOrange, Background = Brushes.Bisque }, 10, 60),
            (new Line { StartPoint = new Point(0, 0), EndPoint = new Point(64, 24), Stroke = Brushes.DarkGreen, StrokeThickness = 2 }, 84, 66)),
        "HalfOpaqueBrush" => Page(
            (new Rectangle { Width = 120, Height = 72, Fill = Brushes.Navy }, 40, 30),
            (new Border { Width = 120, Height = 72, Background = new SolidColorBrush(Colors.Crimson, 0.5) }, 80, 58)),
        "AliasedEllipse" => Page((WithAliasedEdges(new Ellipse { Width = 120, Height = 72, Fill = Brushes.Orchid, Stroke = Brushes.Purple, StrokeThickness = 3 }), 60, 44)),
        "Degenerate" => Page((new DrawingControl(DrawDegenerate), 0, 0)),
        "HugeRectangle" => Page((new Rectangle { Width = 120, Height = 100_000, Fill = Brushes.SteelBlue, Stroke = Brushes.Black, StrokeThickness = 2 }, 60, 80 - 50_000)),
        "GradientRectangle" => Page((new Rectangle
        {
            Width = 120,
            Height = 72,
            Fill = new LinearGradientBrush { GradientStops = { new GradientStop(Colors.SteelBlue, 0), new GradientStop(Colors.Gold, 1) } },
        }, 60, 44)),
        "ShadowedRectangle" => Page((new Border { Width = 120, Height = 72, Background = Brushes.White, BoxShadow = BoxShadows.Parse("6 6 12 0 #80000000") }, 60, 44)),
        "ScissorClip" => Page((Clipped(new Canvas { Width = 150, Height = 100, ClipToBounds = true }), 45, 30)),
        "FractionalClip" => Page((Clipped(new Canvas { Width = 150, Height = 100, ClipToBounds = true, UseLayoutRounding = false }), 10.5, 10.5)),
        "RotatedClip" => Page((Clipped(new Canvas { Width = 150, Height = 90, ClipToBounds = true, RenderTransform = new RotateTransform(25) }), 45, 35)),
        "RoundedClip" => Page((new Border { Width = 184, Height = 132, CornerRadius = new CornerRadius(32), ClipToBounds = true, Child = Stripes(184, 132) }, 28, 14)),
        "RotatedInScissor" => Page((Clipped(new Canvas { Width = 184, Height = 132, ClipToBounds = true, Background = Brushes.LightGray }, Clipped(RotatedClip(-30)), 70, 60), 28, 14)),
        "TextInRoundedClip" => Page((new Border
        {
            Width = 184,
            Height = 132,
            CornerRadius = new CornerRadius(32),
            ClipToBounds = true,
            Background = Brushes.Gold,
            Child = new TextBlock { Text = "Text cut by a rounded clip", FontSize = 40, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Navy, Margin = new Thickness(-8) },
        }, 28, 14)),
        "TwoShaderClips" => Page((Clipped(RotatedClip(25), new Border { Width = 120, Height = 70, CornerRadius = new CornerRadius(24), ClipToBounds = true, Child = Stripes(120, 70) }, 15, 10), 45, 35)),
        "ThinFills" => Page(
            (Thin(new Rectangle { Width = 120, Height = 0.5, Fill = Brushes.Black }), 60, 40.25),
            (Thin(new Rectangle { Width = 120, Height = 0.25, Fill = Brushes.Black }), 60, 60.5),
            (Thin(new Rectangle { Width = 0.5, Height = 60, Fill = Brushes.Black }), 100.25, 80),
            (Thin(new Rectangle { Width = 0.75, Height = 60, Fill = Brushes.Black }), 140.5, 80)),
        "GeometryClipCase" => Page((WithClip(Stripes(184, 132), Geometry.Parse("M 92,0 L 184,132 L 0,132 Z")), 28, 14)),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "No shape case has this name."),
    };

    // A canvas that fills the view, with the controls at their places
    private static Canvas Page(params (Control Control, double Left, double Top)[] content)
    {
        var page = new Canvas { Background = new SolidColorBrush(Background) };
        foreach (var (control, left, top) in content)
        {
            Canvas.SetLeft(control, left);
            Canvas.SetTop(control, top);
            page.Children.Add(control);
        }
        return page;
    }

    // A canvas of the size of the clip of RotatedClip, turned about its center
    private static Canvas RotatedClip(double angle) => new() { Width = 150, Height = 90, ClipToBounds = true, RenderTransform = new RotateTransform(angle) };

    // A clipping canvas over stripes that overflow it on every side
    private static Canvas Clipped(Canvas clip) => Clipped(clip, Stripes(260, 200), -50, -50);

    private static Canvas Clipped(Canvas clip, Control content, double left, double top)
    {
        clip.Children.Add(Placed(content, left, top));
        return clip;
    }

    // Vertical bands of three colors under a horizontal one, whose edges show what a clip does
    private static Canvas Stripes(double width, double height)
    {
        var colors = new[] { Brushes.Crimson, Brushes.Gold, Brushes.SteelBlue };
        var stripes = new Canvas { Width = width, Height = height, Background = Brushes.White };
        for (var i = 0; i * 20 < width; i++)
            stripes.Children.Add(Placed(new Rectangle { Width = 12, Height = height, Fill = colors[i % colors.Length] }, i * 20, 0));
        stripes.Children.Add(Placed(new Rectangle { Width = width, Height = 16, Fill = Brushes.Black }, 0, height / 2 - 8));
        return stripes;
    }

    private static Control Placed(Control control, double left, double top)
    {
        Canvas.SetLeft(control, left);
        Canvas.SetTop(control, top);
        return control;
    }

    // A control placed and sized at fractional pixels
    private static T Thin<T>(T control) where T : Control
    {
        control.UseLayoutRounding = false;
        return control;
    }

    private static T WithClip<T>(T control, Geometry clip) where T : Visual
    {
        control.Clip = clip;
        return control;
    }

    private static Line CreateLine(PenLineCap cap) => new()
    {
        StartPoint = new Point(0, 0),
        EndPoint = new Point(120, 0),
        Stroke = Brushes.SteelBlue,
        StrokeThickness = 16,
        StrokeLineCap = cap,
    };

    private static T WithAliasedEdges<T>(T control) where T : Visual
    {
        RenderOptions.SetEdgeMode(control, EdgeMode.Aliased);
        return control;
    }

    // A rectangle of no width with a pen, a pen of no thickness, a rectangle with neither brush nor pen, transparent
    // colors, an ellipse of no width and a line with a pen of no thickness: only the fill of the second one shows
    private static void DrawDegenerate(DrawingContext context)
    {
        var pen = new Pen(Brushes.Black, 2);
        context.DrawRectangle(Brushes.SteelBlue, pen, new Rect(20, 20, 0, 60));
        context.DrawRectangle(Brushes.SteelBlue, new Pen(Brushes.Black, 0), new Rect(40, 20, 60, 40));
        context.DrawRectangle(null, null, new Rect(110, 20, 60, 40));
        context.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Transparent, 3), new Rect(180, 20, 40, 40));
        context.DrawEllipse(Brushes.SteelBlue, pen, new Point(60, 110), 0, 30);
        context.DrawLine(new Pen(Brushes.Black, 0), new Point(100, 100), new Point(200, 140));
    }

    // A control that draws what a callback draws, over the whole case
    private sealed class DrawingControl : Control
    {
        private readonly Action<DrawingContext> draw;

        public DrawingControl(Action<DrawingContext> draw)
        {
            this.draw = draw;
            Width = ShapeCases.Width;
            Height = ShapeCases.Height;
        }

        public override void Render(DrawingContext context) => draw(context);
    }
}
