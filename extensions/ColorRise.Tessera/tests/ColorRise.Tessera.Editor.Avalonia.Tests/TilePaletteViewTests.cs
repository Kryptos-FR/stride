using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using ColorRise.Tessera.Editor.Avalonia.Controls;
using ColorRise.Tessera.Editor.Palette;
using ColorRise.Tessera.Editor.Pictures;
using ColorRise.Tessera.Editor.Tests;
using Stride.Assets.Editor.Testing;
using Stride.Core.Presentation.Avalonia.Services;
using Int2 = Stride.Core.Mathematics.Int2;
using Vector2 = Stride.Core.Mathematics.Vector2;

namespace ColorRise.Tessera.Editor.Avalonia.Tests;

/// <summary>
/// The palette of the map editor, as a person drags in it.
/// </summary>
public sealed class TilePaletteViewTests
{
    [AvaloniaFact]
    public async Task A_drag_in_the_palette_makes_a_stamp()
    {
        await using var session = await TesseraSession.OpenAsync(new StrideEditorTestSessionOptions { Dispatcher = DispatcherService.Create() }, ("Tiles/Town", TestTown.TileSet()));
        session.WriteImage("Tiles/town.png", 64, 32, TestTown.Pixel);
        var pictures = TilePictureService.Of(session.Session).Get(session.TileSetAt("Tiles/Town"));
        var palette = new TilePaletteViewModel(session.Session.ServiceProvider, pictures, 1);
        var view = new TilePalette { DataContext = palette };
        var window = CanvasProbe.Show(view, 300, 200);
        try
        {
            var canvas = view.GetVisualDescendants().OfType<TileCanvas>().Single();
            Point At(int column, int row)
            {
                var screen = palette.View.ToScreen(new Vector2(column * 16 + 8, row * 16 + 8));
                return canvas.TranslatePoint(new Point(screen.X, screen.Y), window)!.Value;
            }

            window.MouseDown(At(0, 0), MouseButton.Left);
            window.MouseMove(At(1, 0));
            window.MouseUp(At(1, 0), MouseButton.Left);

            Assert.Equal(new Int2(2, 1), palette.Picked!.Size);
            var middle = At(2, 1);
            Assert.Equal(TestTown.Flat(2, 1), CanvasProbe.Pixel(window, (int)middle.X, (int)middle.Y));
        }
        finally
        {
            window.Close();
        }
    }
}
