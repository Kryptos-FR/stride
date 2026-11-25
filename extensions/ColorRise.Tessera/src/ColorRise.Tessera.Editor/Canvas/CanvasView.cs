using System.ComponentModel;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.Canvas;

/// <summary>
/// The zoom and the pan of a canvas. One canvas unit is one pixel of a tile image, and Y grows down. A view binds to its
/// <see cref="Zoom"/> and <see cref="Origin"/>.
/// </summary>
public sealed class CanvasView : INotifyPropertyChanged
{
    private int updating;
    // The properties changed by the change in progress, in the order of their first change
    private readonly List<string> changedWhileUpdating = [];

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
                RaiseChanged(nameof(Zoom));
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
                RaiseChanged(nameof(Origin));
            }
        }
    }

    /// <summary>
    /// Raised once for each change of the zoom or the origin, or of both together.
    /// </summary>
    public event EventHandler? Changed;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

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

    /// <summary>
    /// Puts the middle of <paramref name="bounds"/> in the middle of a view of the given size, at the zoom the view has.
    /// </summary>
    public void Centre(RectangleF bounds, Vector2 viewSize)
        => Origin = new Vector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2) - viewSize / (2 * Zoom);

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
        if (updating == 0 && changedWhileUpdating.Count > 0)
        {
            var changed = changedWhileUpdating.ToList();
            changedWhileUpdating.Clear();
            Raise(changed);
        }
    }

    private void RaiseChanged(string property)
    {
        if (updating == 0)
        {
            Raise([property]);
        }
        else if (!changedWhileUpdating.Contains(property))
        {
            changedWhileUpdating.Add(property);
        }
    }

    private void Raise(IReadOnlyList<string> properties)
    {
        foreach (var property in properties)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
