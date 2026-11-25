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
/// <param name="Replace">Whether the canvas paints its background over the destination before the sprite, so that a
/// transparent pixel of the sprite shows the background and not what was drawn under it.</param>
public readonly record struct CanvasSprite(TilePicture Picture, Rectangle Source, RectangleF Destination, float Opacity = 1, bool Replace = false);

/// <summary>
/// How a shape is drawn: its edge, its area, diagonal lines across it, its two diagonals, or a triangle in its corner. Lines
/// are one screen pixel wide.
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

    /// <summary>
    /// A filled triangle in the top-left corner of the bounds, its sides a third of the smaller side of the bounds.
    /// </summary>
    Corner,
}

/// <summary>
/// A mark drawn over the sprites and the grid.
/// </summary>
/// <param name="Mark">How it is drawn.</param>
/// <param name="Bounds">Where, in canvas units.</param>
/// <param name="Color">Its colour, with its opacity.</param>
public readonly record struct CanvasShape(CanvasMark Mark, RectangleF Bounds, Color Color);

/// <summary>
/// A short text at the top-left inside its bounds, at a fixed size on screen (11 pixels), on a dark backing. It is not
/// drawn when the bounds are less than 14 pixels high on screen.
/// </summary>
/// <param name="Text">The text.</param>
/// <param name="Bounds">What it is written on, in canvas units.</param>
/// <param name="Color">The colour of the text.</param>
public readonly record struct CanvasLabel(string Text, RectangleF Bounds, Color Color);

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
    /// Drawn last, over the shapes, in order.
    /// </summary>
    public List<CanvasLabel> Labels { get; } = [];

    /// <summary>
    /// Empties the frame for the next one.
    /// </summary>
    public void Clear()
    {
        Sprites.Clear();
        Shapes.Clear();
        Labels.Clear();
    }
}
