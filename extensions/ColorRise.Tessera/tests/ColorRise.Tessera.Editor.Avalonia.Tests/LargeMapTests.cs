using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ColorRise.Tessera.Editor.Avalonia.Controls;
using ColorRise.Tessera.Editor.Pictures;
using ColorRise.Tessera.Editor.Tests;
using ColorRise.Tessera.Editor.TileMapEditor;
using Stride.Assets.Editor.Testing;
using Stride.Core.Presentation.Avalonia.Services;
using Int2 = Stride.Core.Mathematics.Int2;
using Vector2 = Stride.Core.Mathematics.Vector2;

namespace ColorRise.Tessera.Editor.Avalonia.Tests;

/// <summary>
/// A large map on the canvas, zoomed out and panned (risk R4).
/// </summary>
public sealed class LargeMapTests(ITestOutputHelper output)
{
    [AvaloniaFact]
    public async Task A_large_map_draws_and_pans()
    {
        var options = new StrideEditorTestSessionOptions { Dispatcher = DispatcherService.Create(), Plugins = [new TesseraEditorAvaloniaPlugin()] };
        var tileSet = TestTown.TileSet();
        await using var session = await TesseraSession.OpenAsync(options, ("Tiles/Town", tileSet), ("Maps/Town", TestTown.LargeMap(tileSet, "Tiles/Town", 256, 4)));
        session.WriteImage("Tiles/town.png", 64, 32, TestTown.Pixel);
        using var document = new TileMapDocument(session.MapAt("Maps/Town"));
        using var scene = new TileMapScene(document, TilePictureService.Of(session.Session));
        var canvas = new TileCanvas { Scene = scene, ShowGrid = false };
        canvas.View.Zoom = 0.125f;
        canvas.View.Origin = new Vector2(0, -4096);
        var window = CanvasProbe.Show(canvas, 1280, 720);
        try
        {
            // The cell (1, 0) has the flat tile (1, 0) in every layer: the top one is drawn last
            var cell = scene.CellBounds(new Int2(1, 0)).Center;
            var screen = canvas.View.ToScreen(cell);
            var point = canvas.TranslatePoint(new Point(screen.X, screen.Y), window)!.Value;
            Assert.Equal(TestTown.Flat(1, 0), CanvasProbe.Pixel(window, (int)point.X, (int)point.Y));

            var watch = Stopwatch.StartNew();
            for (var pan = 0; pan < 30; pan++)
            {
                canvas.View.Pan(new Vector2(-8, 8));
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
                Assert.NotNull(window.CaptureRenderedFrame());
            }
            var mean = watch.Elapsed / 30;
            output.WriteLine($"Mean frame of the pan: {mean.TotalMilliseconds:0.0} ms");
            Assert.True(mean < TimeSpan.FromMilliseconds(100), $"A frame of the pan took {mean.TotalMilliseconds} ms.");
        }
        finally
        {
            window.Close();
        }
    }
}
