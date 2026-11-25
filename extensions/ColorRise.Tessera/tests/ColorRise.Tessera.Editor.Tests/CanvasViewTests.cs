using ColorRise.Tessera.Editor.Canvas;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// The zoom and the pan of a canvas, in steps, around the cursor.
/// </summary>
public sealed class CanvasViewTests
{
    [Fact]
    public void Zooming_keeps_the_point_under_the_cursor()
    {
        var view = new CanvasView();
        view.ZoomAt(new Vector2(100, 50), 1);
        Assert.Equal(2, view.Zoom);
        Assert.Equal(new Vector2(50, 25), view.Origin);
        Assert.Equal(new Vector2(100, 50), view.ToCanvas(new Vector2(100, 50)));
        Assert.Equal(new Vector2(100, 50), view.ToScreen(new Vector2(100, 50)));
    }

    [Fact]
    public void Zoom_stops_at_the_first_and_the_last_step()
    {
        var view = new CanvasView();
        view.ZoomAt(default, -20);
        Assert.Equal(0.125f, view.Zoom);
        view.ZoomAt(default, 40);
        Assert.Equal(32, view.Zoom);
        view.Zoom = 0.01f;
        Assert.Equal(0.125f, view.Zoom);
        view.Zoom = 1000;
        Assert.Equal(32, view.Zoom);
    }

    [Fact]
    public void A_zoom_between_two_steps_goes_to_the_next_step()
    {
        var view = new CanvasView { Zoom = 2.5f };
        view.ZoomAt(default, 1);
        Assert.Equal(3, view.Zoom);
        view.Zoom = 2.5f;
        view.ZoomAt(default, -1);
        Assert.Equal(2, view.Zoom);
    }

    [Fact]
    public void Pan_moves_the_origin_by_the_screen_distance_over_the_zoom()
    {
        var view = new CanvasView { Zoom = 4 };
        view.Pan(new Vector2(8, -12));
        Assert.Equal(new Vector2(-2, 3), view.Origin);
    }

    [Fact]
    public void Visible_is_the_view_in_canvas_units()
    {
        var view = new CanvasView { Zoom = 2, Origin = new Vector2(10, 20) };
        Assert.Equal(new RectangleF(10, 20, 100, 50), view.Visible(new Vector2(200, 100)));
    }

    [Fact]
    public void Bindings_hear_of_the_zoom_and_the_origin()
    {
        var view = new CanvasView();
        var changed = new List<string?>();
        ((System.ComponentModel.INotifyPropertyChanged)view).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        view.ZoomAt(new Vector2(100, 50), 1);
        Assert.Equal([nameof(CanvasView.Zoom), nameof(CanvasView.Origin)], changed);

        changed.Clear();
        view.Pan(new Vector2(4, 0));
        Assert.Equal([nameof(CanvasView.Origin)], changed);
    }

    [Fact]
    public void Fit_shows_all_of_the_bounds_at_the_largest_step_centred()
    {
        var view = new CanvasView();
        view.Fit(new RectangleF(0, 0, 64, 32), new Vector2(640, 480));
        // 12 would need 768 pixels
        Assert.Equal(8, view.Zoom);
        Assert.Equal(new Vector2(-8, -14), view.Origin);
    }

    [Fact]
    public void Changed_is_raised_once_per_change()
    {
        var view = new CanvasView();
        var count = 0;
        view.Changed += (_, _) => count++;

        view.ZoomAt(new Vector2(10, 10), 1);
        Assert.Equal(1, count);
        view.Pan(new Vector2(4, 4));
        Assert.Equal(2, count);
        view.Fit(new RectangleF(0, 0, 64, 32), new Vector2(640, 480));
        Assert.Equal(3, count);
        view.Zoom = 3;
        Assert.Equal(4, count);
        view.Zoom = 3;
        view.Origin = view.Origin;
        Assert.Equal(4, count);
    }
}
