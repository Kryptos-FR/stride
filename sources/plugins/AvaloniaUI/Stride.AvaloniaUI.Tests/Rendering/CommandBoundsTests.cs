// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering;
using Stride.AvaloniaUI.Rendering.Drawing;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.AvaloniaUI.Rendering.Native;
using Xunit;

namespace Stride.AvaloniaUI.Tests.Rendering;

/// <summary>
/// Checks that the bounds the native renderer gives a fallback run hold what the fallback draws for its calls.
/// </summary>
public sealed class CommandBoundsTests
{
    private const int Size = 512;

    private static readonly (string Name, Matrix Transform)[] Transforms =
    [
        ("identity", Matrix.Identity),
        ("a 30 degree rotation", Matrix.CreateTranslation(-256, -256) * Matrix.CreateRotation(Math.PI / 6) * Matrix.CreateTranslation(256, 256)),
        ("a 1.5 scale", Matrix.CreateScale(1.5, 1.5)),
    ];

    private static readonly string[] Cases = ["Rectangle", "BoxShadows", "Ellipse", "Line", "MiterGeometry", "GlyphRun", "Bitmap", "OpacityGroup", "LayerGroup", "ClipGroup"];

    /// <summary>
    /// Gets the names of the cases, one test case each.
    /// </summary>
    public static IEnumerable<object[]> Commands => Cases.Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(Commands))]
    public void BoundsHoldWhatTheFallbackDraws(string name) => TestAvalonia.Run(() =>
    {
        var channel = SkiaOracle.CreateChannel();
        var core = new StrideRenderInterface(channel);
        using var resources = new Resources();
        var commands = CreateCase(name, core, resources);

        foreach (var (transformName, transform) in Transforms)
        {
            var ink = DrawAndMeasure(channel, commands, transform);
            Assert.True(ink != null, $"{name} draws nothing with {transformName}.");

            var bounds = commands.Length == 1 ? CommandBounds.Get(commands[0], transform) : CommandBounds.Get(commands, transform);
            if (bounds is not { } rect)
                continue;
            // A run covers whole pixels
            var pixels = new PixelRect(
                (int)Math.Floor(rect.Left), (int)Math.Floor(rect.Top),
                (int)Math.Ceiling(rect.Right) - (int)Math.Floor(rect.Left), (int)Math.Ceiling(rect.Bottom) - (int)Math.Floor(rect.Top));
            Assert.True(pixels.Contains(ink.Value), $"{name} with {transformName} draws {ink.Value}, outside its bounds {rect}.");
        }
    });

    private static DrawCommand[] CreateCase(string name, StrideRenderInterface core, Resources resources)
    {
        var fill = new ImmutableSolidColorBrush(Color.FromArgb(200, 30, 90, 160));
        var stroke = new ImmutableSolidColorBrush(Colors.Black);
        switch (name)
        {
            case "Rectangle":
                return [new DrawRectangleCommand(fill, new ImmutablePen(stroke, 6), new RoundedRect(new Rect(100, 120, 180, 90)), default)];
            case "BoxShadows":
                var outer = BoxShadow.Parse("14 18 8 6 #C0000000");
                var inset = BoxShadow.Parse("inset 5 5 6 2 #C0000000");
                return [new DrawRectangleCommand(fill, null, new RoundedRect(new Rect(110, 110, 160, 120), 12), new BoxShadows(outer, [inset]))];
            case "Ellipse":
                return [new DrawEllipseCommand(fill, new ImmutablePen(stroke, 5), new Rect(120, 100, 170, 130))];
            case "Line":
                return [new DrawLineCommand(new ImmutablePen(stroke, 8, lineCap: PenLineCap.Square), new Point(100, 120), new Point(300, 220))];
            case "MiterGeometry":
            {
                // A sharp corner at (200, 100), whose miter reaches well past the vertex
                var geometry = core.CreateStreamGeometry();
                using (var context = geometry.Open())
                using (var parser = new PathMarkupParser(context))
                    parser.Parse("M 100,300 L 200,100 L 210,300");
                return [new DrawGeometryCommand(null, new ImmutablePen(stroke, 6, lineJoin: PenLineJoin.Miter, miterLimit: 10), geometry)];
            }
            case "GlyphRun":
            {
                var (typeface, glyphs) = GlyphRunTests.Shape("Bounds", 40, FontWeight.Bold);
                var glyphRun = core.CreateGlyphRun(typeface, 40, glyphs, new Point(110, 200));
                resources.Add(glyphRun);
                return [new DrawGlyphRunCommand(stroke, glyphRun)];
            }
            case "Bitmap":
            {
                var pixels = new uint[4 * 4];
                Array.Fill(pixels, 0xFF20A040);
                var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
                try
                {
                    var bitmap = core.LoadBitmap(PixelFormat.Bgra8888, AlphaFormat.Premul, handle.AddrOfPinnedObject(), new PixelSize(4, 4), new Vector(96, 96), 16);
                    resources.Add(bitmap);
                    return [new DrawBitmapCommand(bitmap, 1, new Rect(0, 0, 4, 4), new Rect(100.5, 110.25, 190, 150))];
                }
                finally
                {
                    handle.Free();
                }
            }
            case "OpacityGroup":
                // The rectangle is larger than the group's bounds, which are only a hint
                return
                [
                    new PushOpacityCommand(0.5, new Rect(150, 150, 100, 100)),
                    new DrawRectangleCommand(fill, null, new RoundedRect(new Rect(100, 100, 220, 220)), default),
                    new PopOpacityCommand(),
                ];
            case "LayerGroup":
                return
                [
                    new PushLayerCommand(new Rect(150, 150, 100, 100)),
                    new DrawRectangleCommand(fill, null, new RoundedRect(new Rect(100, 100, 220, 220)), default),
                    new PopLayerCommand(),
                ];
            case "ClipGroup":
                // The clip bounds what the rectangle draws
                return
                [
                    new PushClipCommand(new Rect(150.5, 150.25, 100, 100)),
                    new DrawRectangleCommand(fill, null, new RoundedRect(new Rect(100, 100, 220, 220)), default),
                    new PopClipCommand(),
                ];
            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, null);
        }
    }

    // The bounding box of the pixels the fallback covers when it draws the commands with a transform, or null
    private static PixelRect? DrawAndMeasure(FallbackChannel channel, DrawCommand[] commands, Matrix transform)
    {
        var memory = GC.AllocateArray<byte>(Size * Size * 4, pinned: true);
        var target = new FallbackTarget(Marshal.UnsafeAddrOfPinnedArrayElement(memory, 0), new PixelSize(Size, Size), Size * 4, PixelFormat.Rgba8888, new Vector(96, 96), true);
        var list = new DrawCommandList();
        list.Add(new SetTransformCommand(transform));
        foreach (var command in commands)
            list.Add(command);
        using (var canvas = channel.Fallback.CreateCanvas(target))
            canvas.Draw(list);

        int left = Size, top = Size, right = -1, bottom = -1;
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                if (memory[(y * Size + x) * 4 + 3] == 0)
                    continue;
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }
        return right < 0 ? null : new PixelRect(left, top, right - left + 1, bottom - top + 1);
    }

    // What a case made, disposed when the test ends
    private sealed class Resources : IDisposable
    {
        private readonly List<IDisposable> items = [];

        public void Add(IDisposable item) => items.Add(item);

        public void Dispose()
        {
            foreach (var item in items)
                item.Dispose();
        }
    }
}
