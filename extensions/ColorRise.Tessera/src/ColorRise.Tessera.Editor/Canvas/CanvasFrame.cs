using ColorRise.Tessera.Editor.Pictures;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.Canvas;

/// <summary>
/// A part of a picture drawn on the canvas.
/// </summary>
/// <param name="Picture">The picture.</param>
/// <param name="Source">The pixels of the picture that are drawn.</param>
/// <param name="Destination">Where they are drawn, in canvas units.</param>
/// <param name="Opacity">From 0, invisible, to 1, opaque.</param>
public readonly record struct CanvasSprite(TilePicture Picture, Rectangle Source, RectangleF Destination, float Opacity = 1);

/// <summary>
/// How a shape is drawn: its edge, its area, diagonal lines across it, or its two diagonals. Lines are one screen pixel wide.
/// </summary>
public enum CanvasMark
{
    /// <summary>
    /// The edge of the bounds.
    /// </summary>
    Outline,

    /// <summary>
    /// The whole of the bounds.
    /// </summary>
    Fill,

    /// <summary>
    /// Lines from the top left to the bottom right across the bounds.
    /// </summary>
    Hatch,

    /// <summary>
    /// The two diagonals of the bounds.
    /// </summary>
    Cross,
}

/// <summary>
/// A mark drawn over the sprites and the grid.
/// </summary>
/// <param name="Mark">How it is drawn.</param>
/// <param name="Bounds">Where, in canvas units.</param>
/// <param name="Color">Its colour, with its opacity.</param>
public readonly record struct CanvasShape(CanvasMark Mark, RectangleF Bounds, Color Color);

/// <summary>
/// What a scene gives the canvas to draw, once per frame.
/// </summary>
public sealed class CanvasFrame
{
    /// <summary>
    /// Drawn first, in order.
    /// </summary>
    public List<CanvasSprite> Sprites { get; } = [];

    /// <summary>
    /// Drawn over the sprites and the grid, in order.
    /// </summary>
    public List<CanvasShape> Shapes { get; } = [];

    /// <summary>
    /// Empties the frame for the next one.
    /// </summary>
    public void Clear()
    {
        Sprites.Clear();
        Shapes.Clear();
    }
}
