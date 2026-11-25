using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.Canvas;

/// <summary>
/// What a canvas draws. Everything is in canvas units.
/// </summary>
public interface ICanvasScene
{
    /// <summary>
    /// What "fit" shows; null when there is nothing.
    /// </summary>
    RectangleF? Bounds { get; }

    /// <summary>
    /// The cell of the grid the canvas draws; zero for none.
    /// </summary>
    Vector2 GridSize { get; }

    /// <summary>
    /// Puts in <paramref name="frame"/> what is seen of the scene in <paramref name="visible"/> at a zoom.
    /// </summary>
    void Collect(RectangleF visible, float zoom, CanvasFrame frame);

    /// <summary>
    /// Raised when the scene must be drawn again.
    /// </summary>
    event EventHandler? Changed;
}
