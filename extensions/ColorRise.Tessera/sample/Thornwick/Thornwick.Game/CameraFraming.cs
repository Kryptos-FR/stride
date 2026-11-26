using Stride.Core.Mathematics;

namespace Thornwick;

/// <summary>
/// Where an orthographic camera looks, and how much it shows.
/// </summary>
/// <param name="Center">The point of the world at the middle of the view.</param>
/// <param name="OrthographicSize">The height of the view in world units, for <c>CameraComponent.OrthographicSize</c>.</param>
/// <param name="Zoom">How many screen pixels one pixel of a tile takes, a whole number.</param>
public readonly record struct CameraFrame(Vector2 Center, float OrthographicSize, int Zoom);

/// <summary>
/// The view of a camera that follows a target over pixel art: a whole zoom, inside the frame of the place, on whole pixels.
/// </summary>
public static class CameraFraming
{
    /// <summary>
    /// How many units of height the view shows at least.
    /// </summary>
    public const float CellsHigh = 12;

    /// <summary>
    /// The largest whole zoom at which a view of that height still shows <see cref="CellsHigh"/> units; at least 1.
    /// </summary>
    public static int ZoomFor(int viewHeight, float pixelsPerUnit)
    {
        var zoom = (int)Math.Floor(Math.Max(viewHeight, 1) / (ValidPixelsPerUnit(pixelsPerUnit) * (double)CellsHigh));
        return Math.Max(zoom, 1);
    }

    /// <summary>
    /// The view that follows a target: inside the frame when there is one, centred on the frame along an axis where the
    /// frame is smaller than the view, and with its bottom-left corner on a whole pixel of the screen.
    /// </summary>
    public static CameraFrame Fit(Vector2 target, RectangleF? frame, Int2 viewPixels, float pixelsPerUnit)
    {
        var width = Math.Max(viewPixels.X, 1);
        var height = Math.Max(viewPixels.Y, 1);
        var zoom = ZoomFor(height, pixelsPerUnit);

        // The screen pixels of one unit, and the size of the view in units
        var unitPixels = (double)ValidPixelsPerUnit(pixelsPerUnit) * zoom;
        var viewWidth = width / unitPixels;
        var viewHeight = height / unitPixels;

        double x = target.X;
        double y = target.Y;
        if (frame is { } bounds)
        {
            x = Keep(x, bounds.X, bounds.Width, viewWidth);
            y = Keep(y, bounds.Y, bounds.Height, viewHeight);
        }

        // The bottom-left corner of the view on a whole screen pixel
        var left = Math.Round((x - viewWidth / 2) * unitPixels) / unitPixels;
        var bottom = Math.Round((y - viewHeight / 2) * unitPixels) / unitPixels;
        var center = new Vector2((float)(left + viewWidth / 2), (float)(bottom + viewHeight / 2));
        return new CameraFrame(center, (float)viewHeight, zoom);
    }

    // The centre of a view along an axis: inside the frame, or the frame's middle when the frame is the smaller one
    private static double Keep(double center, double start, double length, double view)
        => length <= view ? start + length / 2 : Math.Clamp(center, start + view / 2, start + length - view / 2);

    private static float ValidPixelsPerUnit(float pixelsPerUnit) => float.IsFinite(pixelsPerUnit) && pixelsPerUnit > 0 ? pixelsPerUnit : 16;
}
