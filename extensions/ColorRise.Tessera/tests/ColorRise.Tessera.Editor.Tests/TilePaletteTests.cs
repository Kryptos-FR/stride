using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Canvas;
using ColorRise.Tessera.Editor.Palette;
using ColorRise.Tessera.Editor.Pictures;
using Stride.Assets.Sprite;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Quantum;
using Stride.Core.Serialization;
using Stride.Graphics;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// The palette of a map editor: what a click or a drag over a source picks, and the scene a source gives the canvas.
/// </summary>
public sealed class TilePaletteTests
{
    private const ushort Slot = 3;

    [Fact]
    public async Task A_click_picks_one_tile()
    {
        await using var session = await OpenTownAsync(TestTown.TileSet());
        var palette = Palette(session);
        Assert.Equal(TilePickMode.Block, palette.Scene.Mode);
        Assert.Null(palette.Picked);

        Click(palette.Scene, new Vector2(20, 4));

        Assert.Equal(new Int2(1, 1), palette.Picked!.Size);
        Assert.Equal(new TileCell(Slot, TileId.Parse("1:1,0")), palette.Picked[0, 0]);
        Assert.Equal([TileId.Parse("1:1,0")], palette.Scene.Selection);
    }

    [Fact]
    public async Task A_drag_picks_a_block_with_its_top_row_on_top()
    {
        await using var session = await OpenTownAsync(TestTown.TileSet());
        var palette = Palette(session);
        var picks = 0;
        palette.PickedChanged += (_, _) => picks++;

        (Vector2 From, Vector2 To)[] drags = [(new(4, 4), new(40, 20)), (new(40, 20), new(4, 4))];
        foreach (var (from, to) in drags)
        {
            Drag(palette.Scene, from, to);

            var picked = palette.Picked!;
            Assert.Equal(new Int2(3, 2), picked.Size);
            Assert.Equal(Cell("1:0,0"), picked[0, 1]);
            Assert.Equal(Cell("1:2,0"), picked[2, 1]);
            Assert.Equal(Cell("1:0,1"), picked[0, 0]);
            Assert.Equal(Cell("1:2,1"), picked[2, 0]);
        }
        Assert.Equal(2, picks);
        Assert.Equal(6, palette.Scene.Selection.Count);
    }

    // The pictures are made again when the image changes on disk, which may come in a drag
    [Fact]
    public async Task A_drag_goes_on_when_the_pictures_are_made_again()
    {
        await using var session = await OpenTownAsync(TestTown.TileSet());
        var palette = Palette(session);

        palette.Scene.PointerPressed(new Vector2(4, 4), CanvasButton.Left, CanvasModifiers.None);
        palette.Pictures.Refresh();
        palette.Scene.PointerMoved(new Vector2(40, 20), CanvasModifiers.None);
        palette.Scene.PointerReleased(new Vector2(40, 20), CanvasButton.Left, CanvasModifiers.None);

        Assert.Equal(new Int2(3, 2), palette.Picked!.Size);
    }

    [Fact]
    public async Task A_position_without_a_tile_is_a_hole_in_the_block()
    {
        await using var session = await OpenTownAsync(TestTown.TileSet());
        var palette = Palette(session);

        Drag(palette.Scene, new Vector2(36, 20), new Vector2(60, 20));

        var picked = palette.Picked!;
        Assert.Equal(new Int2(2, 1), picked.Size);
        Assert.Equal(Cell("1:2,1"), picked[0, 0]);
        Assert.True(picked[1, 0].IsEmpty);
        Assert.Equal([TileId.Parse("1:2,1")], palette.Scene.Selection);
    }

    [Fact]
    public async Task A_click_on_no_tile_keeps_what_was_picked()
    {
        await using var session = await OpenTownAsync(TestTown.TileSet());
        var palette = Palette(session);
        var picks = 0;
        palette.PickedChanged += (_, _) => picks++;
        Click(palette.Scene, new Vector2(20, 4));
        var picked = palette.Picked;

        Click(palette.Scene, new Vector2(60, 20));
        Click(palette.Scene, new Vector2(200, 200));

        Assert.Same(picked, palette.Picked);
        Assert.Equal(1, picks);
        Assert.Equal([TileId.Parse("1:1,0")], palette.Scene.Selection);
    }

    [Fact]
    public async Task A_sheet_source_picks_one_tile_whatever_the_drag()
    {
        var sheet = new SpriteSheetAsset
        {
            Sprites =
            {
                new SpriteInfo { Name = "tree", Source = new UFile("town.png"), TextureRegion = new Rectangle(0, 0, 16, 32) },
                new SpriteInfo { Name = "rock", Source = new UFile("town.png"), TextureRegion = new Rectangle(16, 16, 16, 16) },
            },
        };
        var tileSet = new TileSetAsset();
        var source = tileSet.AddSource<SpriteSheetTileSetSource>("Objects");
        source.Sheet = AttachedReferenceManager.CreateProxyObject<SpriteSheet>(sheet.Id, "Tiles/Objects");
        source.Bindings.Add(new SpriteTileBinding { Number = 1, SpriteName = "tree" });
        source.Bindings.Add(new SpriteTileBinding { Number = 2, SpriteName = "rock" });
        source.LastNumber = 2;
        await using var session = await TesseraSession.OpenAsync(("Tiles/Town", tileSet), ("Tiles/Objects", sheet));
        session.WriteImage("Tiles/town.png", 64, 32, TestTown.Pixel);
        var palette = Palette(session);
        Assert.Equal(TilePickMode.One, palette.Scene.Mode);
        var layout = palette.SelectedSource!;
        var tree = Middle(layout.SlotOf(TileId.Parse("1:#1"))!.Bounds);
        var rock = Middle(layout.SlotOf(TileId.Parse("1:#2"))!.Bounds);

        Drag(palette.Scene, tree, rock);

        Assert.Equal(TileStamp.Single(new TileCell(Slot, TileId.Parse("1:#1"))), palette.Picked);
        Assert.Equal([TileId.Parse("1:#1")], palette.Scene.Selection);
    }

    [Fact]
    public async Task Show_selects_the_source_and_the_tile_without_picking()
    {
        var tileSet = TestTown.TileSet();
        tileSet.AddSource<GridTileSetSource>("Cave").Image = new UFile("cave.png");
        await using var session = await OpenTownAsync(tileSet);
        session.WriteImage("Tiles/cave.png", 32, 16, (_, _) => Color.Gray);
        var palette = Palette(session);
        Assert.Equal(1, palette.SelectedSource!.Source.Id);
        var picks = 0;
        palette.PickedChanged += (_, _) => picks++;

        palette.Show(TileId.Parse("2:1,0"));

        Assert.Equal(2, palette.SelectedSource!.Source.Id);
        Assert.Same(palette.SelectedSource, palette.Scene.Layout);
        Assert.Equal([TileId.Parse("2:1,0")], palette.Scene.Selection);
        Assert.Null(palette.Picked);
        Assert.Equal(0, picks);
    }

    [Fact]
    public async Task The_sources_follow_the_tile_set()
    {
        await using var session = await OpenTownAsync(TestTown.TileSet());
        session.WriteImage("Tiles/cave.png", 32, 16, (_, _) => Color.Gray);
        var tileSet = session.TileSetAt("Tiles/Town");
        var palette = Palette(session);
        var sources = tileSet.PropertyGraph.RootNode[nameof(TileSetAsset.Sources)].Target!;

        using (var transaction = session.UndoRedo.CreateTransaction())
        {
            sources.Add(new GridTileSetSource { Id = 2, Name = "Cave", Image = new UFile("cave.png") });
            session.UndoRedo.SetName(transaction, "Add source");
        }
        await ExclusiveDispatcherService.WaitUntilAsync(() => palette.Sources.Count == 2, "the palette to show the new source");
        Assert.Equal(1, palette.SelectedSource!.Source.Id);

        palette.SelectedSource = palette.Sources[1];
        Assert.Same(palette.Sources[1], palette.Scene.Layout);
        using (var transaction = session.UndoRedo.CreateTransaction())
        {
            sources.Remove(tileSet.Asset.Sources[1], new NodeIndex(1));
            session.UndoRedo.SetName(transaction, "Remove source");
        }
        await ExclusiveDispatcherService.WaitUntilAsync(() => palette.Sources.Count == 1, "the palette to drop the source");

        Assert.Same(palette.Sources[0], palette.SelectedSource);
        Assert.Same(palette.Sources[0], palette.Scene.Layout);
    }

    [Fact]
    public async Task The_scene_gives_the_picture_the_outlines_and_the_selection()
    {
        await using var session = await OpenTownAsync(TestTown.TileSet());
        var scene = Palette(session).Scene;
        Assert.Equal(new RectangleF(0, 0, 64, 32), scene.Bounds);
        Assert.Equal(Vector2.Zero, scene.GridSize);

        var frame = new CanvasFrame();
        scene.Collect(new RectangleF(0, 0, 64, 32), 1, frame);
        var sprite = Assert.Single(frame.Sprites);
        Assert.Equal(new Rectangle(0, 0, 64, 32), sprite.Source);
        Assert.Equal(new RectangleF(0, 0, 64, 32), sprite.Destination);
        Assert.Equal(7, frame.Shapes.Count(shape => shape.Mark == CanvasMark.Outline));
        Assert.DoesNotContain(frame.Shapes, shape => shape.Mark == CanvasMark.Fill);

        Click(scene, new Vector2(20, 4));
        frame.Clear();
        scene.Collect(new RectangleF(0, 0, 64, 32), 1, frame);
        var fill = Assert.Single(frame.Shapes, shape => shape.Mark == CanvasMark.Fill);
        Assert.Equal(new RectangleF(16, 0, 16, 16), fill.Bounds);

        // Only what is seen: the tiles of the left half
        frame.Clear();
        scene.Collect(new RectangleF(0, 0, 30, 32), 1, frame);
        Assert.Equal(4, frame.Shapes.Where(shape => shape.Mark == CanvasMark.Outline).Select(shape => shape.Bounds).Distinct().Count());
    }

    [Fact]
    public async Task Many_adds_and_removes_with_control_and_paints_in_paint_mode()
    {
        await using var session = await OpenTownAsync(TestTown.TileSet());
        var scene = Palette(session).Scene;
        scene.Mode = TilePickMode.Many;

        Click(scene, new Vector2(4, 4));
        Click(scene, new Vector2(20, 4), CanvasModifiers.Control);
        Assert.Equal([TileId.Parse("1:0,0"), TileId.Parse("1:1,0")], scene.Selection);
        Click(scene, new Vector2(4, 4), CanvasModifiers.Control);
        Assert.Equal([TileId.Parse("1:1,0")], scene.Selection);
        Drag(scene, new Vector2(4, 4), new Vector2(40, 20));
        Assert.Equal(6, scene.Selection.Count);
        Click(scene, new Vector2(60, 20));
        Assert.Empty(scene.Selection);

        scene.Mode = TilePickMode.Paint;
        var painted = new List<TileId>();
        var ended = 0;
        scene.Painted += (_, tile) => painted.Add(tile);
        scene.PaintEnded += (_, _) => ended++;
        scene.PointerPressed(new Vector2(4, 4), CanvasButton.Left, CanvasModifiers.None);
        foreach (var x in (float[])[20, 36, 20, 4])
        {
            scene.PointerMoved(new Vector2(x, 4), CanvasModifiers.None);
        }
        scene.PointerReleased(new Vector2(4, 4), CanvasButton.Left, CanvasModifiers.None);

        Assert.Equal([TileId.Parse("1:0,0"), TileId.Parse("1:1,0"), TileId.Parse("1:2,0")], painted);
        Assert.Equal(1, ended);
        Assert.Empty(scene.Selection);
    }

    private static TileCell Cell(string tile) => new(Slot, TileId.Parse(tile));

    private static Vector2 Middle(Rectangle bounds) => new(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);

    private static void Click(ICanvasInput input, Vector2 at, CanvasModifiers modifiers = CanvasModifiers.None)
    {
        input.PointerPressed(at, CanvasButton.Left, modifiers);
        input.PointerReleased(at, CanvasButton.Left, modifiers);
    }

    private static void Drag(ICanvasInput input, Vector2 from, Vector2 to)
    {
        input.PointerPressed(from, CanvasButton.Left, CanvasModifiers.None);
        input.PointerMoved((from + to) / 2, CanvasModifiers.None);
        input.PointerMoved(to, CanvasModifiers.None);
        input.PointerReleased(to, CanvasButton.Left, CanvasModifiers.None);
    }

    private static TilePaletteViewModel Palette(TesseraSession session)
    {
        var pictures = TilePictureService.Of(session.Session).Get(session.TileSetAt("Tiles/Town"));
        return new TilePaletteViewModel(session.Session.ServiceProvider, pictures, Slot);
    }

    private static async DispatcherTask<TesseraSession> OpenTownAsync(TileSetAsset tileSet)
    {
        var session = await TesseraSession.OpenAsync(("Tiles/Town", tileSet));
        session.WriteImage("Tiles/town.png", 64, 32, TestTown.Pixel);
        return session;
    }
}
