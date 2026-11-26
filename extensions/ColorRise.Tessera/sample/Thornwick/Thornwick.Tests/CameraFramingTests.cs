using Stride.Core.Mathematics;

namespace Thornwick.Tests;

/// <summary>
/// The camera's frame: a whole zoom that shows twelve cells of height, inside the map's frame, on whole screen pixels, and
/// finite numbers for any window (Review Focus 4).
/// </summary>
public sealed class CameraFramingTests
{
    private static readonly RectangleF Town = new(0, 0, 60, 40);
    private static readonly Int2 Window = new(1280, 720);

    [Theory]
    [InlineData(720, 3)]
    [InlineData(1080, 5)]
    [InlineData(480, 2)]
    [InlineData(192, 1)]
    [InlineData(100, 1)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(2160, 11)]
    public void The_zoom_is_the_largest_whole_number_that_shows_twelve_cells(int height, int zoom)
        => Assert.Equal(zoom, CameraFraming.ZoomFor(height, 16));

    [Fact]
    public void It_follows_the_target_inside_the_frame()
    {
        var frame = CameraFraming.Fit(new Vector2(30, 20), Town, Window, 16);

        Assert.Equal(new CameraFrame(new Vector2(30, 20), 15, 3), frame);
    }

    [Fact]
    public void It_stops_at_the_frame()
    {
        AssertNear(new Vector2(13.3333f, 7.5f), CameraFraming.Fit(new Vector2(1, 1), Town, Window, 16).Center);
        AssertNear(new Vector2(46.6667f, 32.5f), CameraFraming.Fit(new Vector2(59, 39), Town, Window, 16).Center);
    }

    [Fact]
    public void A_place_smaller_than_the_view_is_centred()
    {
        var house = new RectangleF(100, 0, 12, 9);

        foreach (var target in new[] { new Vector2(101, 1), new Vector2(111, 8), new Vector2(0, 0) })
        {
            AssertNear(new Vector2(106, 4.5f), CameraFraming.Fit(target, house, Window, 16).Center);
        }
    }

    [Fact]
    public void Without_a_frame_it_follows_the_target()
    {
        var frame = CameraFraming.Fit(new Vector2(-500, 900), null, Window, 16);

        AssertNear(new Vector2(-500, 900), frame.Center);
        Assert.Equal(3, frame.Zoom);
    }

    [Fact]
    public void The_view_starts_on_a_whole_pixel()
    {
        var frame = CameraFraming.Fit(new Vector2(30.0101f, 20.0077f), Town, Window, 16);

        // 48 screen pixels a unit at a zoom of 3; the view is 1280 by 720 pixels
        var corner = (frame.Center - new Vector2(1280, 720) / 48 / 2) * 48;
        Assert.Equal(Math.Round(corner.X), corner.X, 0.001);
        Assert.Equal(Math.Round(corner.Y), corner.Y, 0.001);
    }

    // Review Focus 4
    [Fact]
    public void A_view_without_height_or_a_pixels_per_unit_of_zero_gives_finite_numbers()
    {
        var cases = new (Int2 View, float PixelsPerUnit)[]
        {
            (new Int2(0, 0), 16), (new Int2(1280, 0), 16), (Window, 0), (Window, -1), (Window, float.NaN),
        };
        foreach (var (view, pixelsPerUnit) in cases)
        {
            var frame = CameraFraming.Fit(new Vector2(30, 20), Town, view, pixelsPerUnit);

            Assert.True(frame.Zoom >= 1, $"{view} at {pixelsPerUnit}");
            Assert.True(float.IsFinite(frame.OrthographicSize) && frame.OrthographicSize > 0, $"{view} at {pixelsPerUnit}");
            Assert.True(float.IsFinite(frame.Center.X) && float.IsFinite(frame.Center.Y), $"{view} at {pixelsPerUnit}");
        }
    }

    private static void AssertNear(Vector2 expected, Vector2 actual)
    {
        Assert.Equal(expected.X, actual.X, 0.001f);
        Assert.Equal(expected.Y, actual.Y, 0.001f);
    }
}
