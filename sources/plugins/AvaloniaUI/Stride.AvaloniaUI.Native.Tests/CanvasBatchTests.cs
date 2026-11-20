// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Media;
using Stride.AvaloniaUI.Rendering.Native;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Xunit;
using AvaloniaMatrix = Avalonia.Matrix;
using Rect = Avalonia.Rect;

namespace Stride.AvaloniaUI.Native.Tests;

/// <summary>
/// Tests how the canvas batch splits its quads into draws.
/// </summary>
public sealed class CanvasBatchTests
{
    private static readonly Rectangle Scissor = new(0, 0, 200, 100);
    private static readonly CanvasShape Shape = new(AvaloniaMatrix.Identity, new RoundedRect(new Rect(10, 10, 40, 20)), null, Colors.SteelBlue, false);

    [Fact]
    public void TextAndShapesOnOnePageAreOneSegment()
    {
        var page = new Texture();
        var batch = new CanvasBatch();
        batch.AddShape(Shape, Scissor, null);
        batch.AddGlyph(new CanvasGlyph(new Rectangle(60, 20, 8, 10), page, new Int2(0, 0), Color4.Black), Scissor, null);
        batch.AddShape(Shape, Scissor, null);
        batch.AddGlyph(new CanvasGlyph(new Rectangle(70, 20, 8, 10), page, new Int2(8, 0), Color4.Black), Scissor, null);

        var segment = Assert.Single(batch.Segments);
        Assert.Same(page, segment.Texture);
        Assert.Equal(0, segment.Start);
        Assert.Equal(4, segment.Count);
    }

    [Fact]
    public void GlyphsOfAnotherPageStartASegment()
    {
        var first = new Texture();
        var second = new Texture();
        var batch = new CanvasBatch();
        batch.AddGlyph(new CanvasGlyph(new Rectangle(60, 20, 8, 10), first, new Int2(0, 0), Color4.Black), Scissor, null);
        batch.AddShape(Shape, Scissor, null);
        batch.AddGlyph(new CanvasGlyph(new Rectangle(70, 20, 8, 10), second, new Int2(0, 0), Color4.Black), Scissor, null);

        Assert.Collection(batch.Segments,
            segment => Assert.Equal((first, 0, 2), (segment.Texture, segment.Start, segment.Count)),
            segment => Assert.Equal((second, 2, 1), (segment.Texture, segment.Start, segment.Count)));
    }
}
