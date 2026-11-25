using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using ColorRise.Tessera.Editor.Avalonia.Controls;
using ColorRise.Tessera.Editor.Pictures;
using ColorRise.Tessera.Editor.TileMapEditor;
using Rectangle = Stride.Core.Mathematics.Rectangle;
using StrideColor = Stride.Core.Mathematics.Color;

namespace ColorRise.Tessera.Editor.Avalonia.Tests;

/// <summary>
/// The control that shows one picture, or a part of it: in the bar of frames, the strip and the preview.
/// </summary>
public sealed class PictureViewTests
{
    // The colour around the control
    private static readonly StrideColor Side = new(10, 20, 30, 255);

    [AvaloniaFact]
    public void A_region_is_drawn_with_hard_pixels_and_its_proportions()
    {
        // The region is 2 pixels wide and 3 high, from (1, 0): red, green; blue, yellow; white, black
        var picture = new TilePicture(4, 3);
        (picture[1, 0], picture[2, 0]) = (StrideColor.Red, StrideColor.Lime);
        (picture[1, 1], picture[2, 1]) = (StrideColor.Blue, StrideColor.Yellow);
        (picture[1, 2], picture[2, 2]) = (StrideColor.White, StrideColor.Black);
        var window = Show(new PictureView { Picture = picture, Region = new Rectangle(1, 0, 2, 3) });

        // 20 pixels a pixel: 40 by 60, from x = 10
        Assert.Equal(StrideColor.Red, CanvasProbe.Pixel(window, 10, 0));
        Assert.Equal(StrideColor.Red, CanvasProbe.Pixel(window, 29, 19));
        Assert.Equal(StrideColor.Lime, CanvasProbe.Pixel(window, 30, 19));
        Assert.Equal(StrideColor.Blue, CanvasProbe.Pixel(window, 29, 20));
        Assert.Equal(StrideColor.Yellow, CanvasProbe.Pixel(window, 49, 39));
        Assert.Equal(StrideColor.White, CanvasProbe.Pixel(window, 10, 59));
        Assert.Equal(StrideColor.Black, CanvasProbe.Pixel(window, 49, 59));
        Assert.Equal(Side, CanvasProbe.Pixel(window, 9, 30));
        Assert.Equal(Side, CanvasProbe.Pixel(window, 50, 30));
        window.Close();
    }

    [AvaloniaFact]
    public void A_picture_that_changed_is_drawn_again()
    {
        var picture = new TilePicture(1, 1);
        picture[0, 0] = StrideColor.Red;
        var window = Show(new PictureView { Picture = picture });
        Assert.Equal(StrideColor.Red, CanvasProbe.Pixel(window, 30, 30));

        picture[0, 0] = StrideColor.Blue;
        picture.Touch();

        Assert.Equal(StrideColor.Blue, CanvasProbe.Pixel(window, 30, 30));
        window.Close();
    }

    [AvaloniaFact]
    public void A_missing_picture_is_a_marked_box()
    {
        var window = Show(new PictureView { IsMissing = true });

        Assert.Equal(TileComposer.MissingColor, CanvasProbe.Pixel(window, 30, 10));
        Assert.Equal(TileComposer.MissingColor, CanvasProbe.Pixel(window, 2, 30));
        Assert.Equal(StrideColor.Black, CanvasProbe.Pixel(window, 30, 30));
        window.Close();
    }

    // The control fills a window of 60 by 60 pixels, over a known colour
    private static Window Show(PictureView view)
        => CanvasProbe.Show(new Border { Background = new SolidColorBrush(Color.FromRgb(Side.R, Side.G, Side.B)), Child = view }, 60, 60);
}
