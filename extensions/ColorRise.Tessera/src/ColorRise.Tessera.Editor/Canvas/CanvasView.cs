using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.Canvas;

/// <summary>
/// The zoom and the pan of a canvas. One canvas unit is one pixel of a tile image, and Y grows down.
/// </summary>
public sealed class CanvasView
{
    private int updating;
    private bool changedWhileUpdating;

    /// <summary>
    /// The zooms the wheel goes through, in screen pixels per canvas unit.
    /// </summary>
    public static IReadOnlyList<float> ZoomSteps { get; } = [0.125f, 0.25f, 0.5f, 1, 2, 3, 4, 6, 8, 12, 16, 32];

    /// <summary>
    /// Screen pixels per canvas unit; 1 at first, and kept between the first and the last of <see cref="ZoomSteps"/>.
    /// </summary>
    public float Zoom
    {
        get;
        set
        {
            var zoom = float.IsFinite(value) ? Math.Clamp(value, ZoomSteps[0], ZoomSteps[^1]) : 1;
            if (field != zoom)
            {
                field = zoom;
                RaiseChanged();
            }
        }
    } = 1;

    /// <summary>
    /// The canvas point at the top-left corner of the view.
    /// </summary>
    public Vector2 Origin
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                RaiseChanged();
            }
        }
    }

    /// <summary>
    /// Raised once for each change of the zoom or the origin, or of both together.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// The canvas point at a point of the view, in screen pixels from its top-left corner.
    /// </summary>
    public Vector2 ToCanvas(Vector2 screen) => Origin + screen / Zoom;

    /// <summary>
    /// The point of the view, in screen pixels from its top-left corner, at a canvas point.
    /// </summary>
    public Vector2 ToScreen(Vector2 canvas) => (canvas - Origin) * Zoom;

    /// <summary>
    /// Zooms to the next step up (positive steps) or down (negative); the canvas point under <paramref name="screen"/> stays under it.
    /// </summary>
    public void ZoomAt(Vector2 screen, int steps)
    {
        var zoom = Zoom;
        for (var i = 0; i < Math.Abs(steps); i++)
        {
            zoom = steps > 0
                ? ZoomSteps.FirstOrDefault(step => step > zoom, ZoomSteps[^1])
                : ZoomSteps.LastOrDefault(step => step < zoom, ZoomSteps[0]);
        }
        var anchor = ToCanvas(screen);
        Update(() =>
        {
            Zoom = zoom;
            Origin = anchor - screen / Zoom;
        });
    }

    /// <summary>
    /// Moves the view by a distance in screen pixels: what was under the pointer follows it.
    /// </summary>
    public void Pan(Vector2 screenDelta) => Origin -= screenDelta / Zoom;

    /// <summary>
    /// The part of the canvas a view of the given size shows, in canvas units.
    /// </summary>
    public RectangleF Visible(Vector2 viewSize) => new(Origin.X, Origin.Y, viewSize.X / Zoom, viewSize.Y / Zoom);

    /// <summary>
    /// Shows all of <paramref name="bounds"/> at the largest step that holds them in a view of the given size, centred.
    /// </summary>
    public void Fit(RectangleF bounds, Vector2 viewSize)
    {
        var zoom = ZoomSteps[0];
        foreach (var step in ZoomSteps)
        {
            if (bounds.Width * step <= viewSize.X && bounds.Height * step <= viewSize.Y)
            {
                zoom = step;
            }
        }
        var centre = new Vector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        Update(() =>
        {
            Zoom = zoom;
            Origin = centre - viewSize / (2 * Zoom);
        });
    }

    // Raises Changed once for all the changes the action makes
    private void Update(Action change)
    {
        updating++;
        try
        {
            change();
        }
        finally
        {
            updating--;
        }
        if (updating == 0 && changedWhileUpdating)
        {
            changedWhileUpdating = false;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RaiseChanged()
    {
        if (updating > 0)
        {
            changedWhileUpdating = true;
        }
        else
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
