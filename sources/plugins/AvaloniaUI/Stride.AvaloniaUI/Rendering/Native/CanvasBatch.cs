// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using System.Runtime.InteropServices;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace Stride.AvaloniaUI.Rendering.Native;

/// <summary>
/// A quad of the canvas, one instance of the canvas shader's draws.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CanvasInstance
{
    /// <summary>
    /// The size of an instance in the buffer, in bytes.
    /// </summary>
    public const int Size = 48;

    /// <summary>
    /// The per-instance streams of the canvas shader, all in the buffer of slot 0.
    /// </summary>
    public static readonly InputElementDescription[] InputElements =
    [
        Element("QUAD_RECT", 0),
        Element("QUAD_COLOR", 16),
        Element("QUAD_TEXTURE", 32),
    ];

    /// <summary>
    /// The quad's left, top, right and bottom edges, in pixels of the target.
    /// </summary>
    public Vector4 Rect;

    /// <summary>
    /// The quad's premultiplied color, which multiplies the texture's.
    /// </summary>
    public Vector4 Color;

    /// <summary>
    /// The texel of the texture at the quad's top left corner (x, y), and 1 in z to sample the texture.
    /// </summary>
    public Vector4 Texture;

    private static InputElementDescription Element(string semantic, int offset) => new()
    {
        SemanticName = semantic,
        Format = PixelFormat.R32G32B32A32_Float,
        InputSlot = 0,
        AlignedByteOffset = offset,
        InputSlotClass = InputClassification.Instance,
        InstanceDataStepRate = 1,
    };
}

/// <summary>
/// Consecutive instances drawn with one scissor, one texture and one blending.
/// </summary>
/// <param name="Texture">The texture the instances sample, or <c>null</c> for none.</param>
/// <param name="Opaque">Whether the instances replace the target's pixels rather than blend over them.</param>
internal readonly record struct CanvasSegment(int Start, int Count, Rectangle Scissor, Texture? Texture, bool Opaque);

/// <summary>
/// The quads of a frame of the canvas, in the order they are drawn.
/// </summary>
internal sealed class CanvasBatch
{
    private readonly List<CanvasInstance> instances = [];
    private readonly List<CanvasSegment> segments = [];

    /// <summary>
    /// Adds a quad of a premultiplied color that replaces the pixels of a rectangle.
    /// </summary>
    public void AddFill(Rectangle rectangle, Color4 premultipliedColor)
        => Add(new CanvasInstance { Rect = Edges(rectangle), Color = premultipliedColor.ToVector4() }, rectangle, null, opaque: true);

    /// <summary>
    /// Adds a quad that draws a texture over a rectangle of the target, one texel to one pixel from the texture's top left
    /// corner, blended over the target with premultiplied alpha, under a scissor.
    /// </summary>
    public void AddImage(Rectangle rectangle, Texture texture, Rectangle scissor)
        => Add(new CanvasInstance { Rect = Edges(rectangle), Color = Vector4.One, Texture = new Vector4(0, 0, 1, 0) }, scissor, texture, opaque: false);

    /// <summary>
    /// Draws the quads into a texture, then forgets them.
    /// </summary>
    public void Draw(NativeGraphics graphics, Texture target)
    {
        graphics.Draw(target, CollectionsMarshal.AsSpan(instances), CollectionsMarshal.AsSpan(segments));
        instances.Clear();
        segments.Clear();
    }

    private void Add(in CanvasInstance instance, Rectangle scissor, Texture? texture, bool opaque)
    {
        instances.Add(instance);
        if (segments.Count > 0 && segments[^1] is var last && last.Scissor == scissor && last.Texture == texture && last.Opaque == opaque)
            segments[^1] = last with { Count = last.Count + 1 };
        else
            segments.Add(new CanvasSegment(instances.Count - 1, 1, scissor, texture, opaque));
    }

    private static Vector4 Edges(Rectangle rectangle) => new(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);
}
