using System.Diagnostics;
using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Canvas;
using ColorRise.Tessera.Editor.Palette;
using ColorRise.Tessera.Editor.Pictures;
using ColorRise.Tessera.Editor.TileMapEditor;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.Mathematics;
using Stride.Core.Quantum;
using Stride.Core.Serialization;
using Xunit.Abstractions;
using static ColorRise.Tessera.Editor.Tests.TestTown;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// A map as the canvas of its editor draws it: a picture per chunk and layer, composed once, and what goes over them.
/// </summary>
public sealed class TileMapSceneTests(ITestOutputHelper output)
{
    private const string Location = "Maps/Town";
    // A whole map of the tests, and more
    private static readonly RectangleF Everything = new(-10000, -10000, 20000, 20000);

    [Fact]
    public async Task The_cell_zero_is_above_the_origin_of_the_canvas()
    {
        await using var map = await OpenAsync([["0,0"]]);

        Assert.Equal(new Int2(16, 16), map.Scene.CellPixels);
        Assert.Equal(new Vector2(16, 16), map.Scene.GridSize);
        Assert.Equal(new RectangleF(0, -16, 16, 16), map.Scene.CellBounds(new Int2(0, 0)));
        Assert.Equal(new Int2(0, 0), map.Scene.CellAt(new Vector2(5, -3)));
        Assert.Equal(new Int2(-1, -1), map.Scene.CellAt(new Vector2(-1, 1)));
        Assert.Equal(new Int2(0, 0), map.Scene.CellAt(new Vector2(0, -16)));
        Assert.Equal(new Int2(0, -1), map.Scene.CellAt(new Vector2(0, 0)));
    }

    [Fact]
    public async Task A_map_is_one_sprite_per_chunk_and_layer_with_the_pixels_of_its_tiles()
    {
        await using var map = await OpenAsync([["0,0 1,0 2,1"], [". . 3,0"]]);

        var frame = Collect(map.Scene, Everything);

        Assert.Equal(2, frame.Sprites.Count);
        Assert.All(frame.Sprites, sprite => Assert.Equal(new RectangleF(0, -256, 256, 256), sprite.Destination));
        Assert.Equal(Flat(1, 0), PixelAt(frame.Sprites.First(sprite => sprite.Destination.Contains(Middle(map.Scene, 1, 0))), Middle(map.Scene, 1, 0)));
        Assert.Equal(Flat(2, 1), PixelAt(frame.Sprites[0], Middle(map.Scene, 2, 0)));
        Assert.Equal(Flat(3, 0), PixelAt(frame.Sprites[1], Middle(map.Scene, 2, 0)));
        Assert.Equal(Color.Transparent, PixelAt(frame.Sprites[1], Middle(map.Scene, 1, 0)));
        Assert.Equal(new RectangleF(0, -16, 48, 16), map.Scene.Bounds);
    }

    [Fact]
    public async Task Only_the_visible_chunks_are_collected()
    {
        await using var map = await OpenAsync([Rows((0, 0, "0,0"), (16, 0, "1,0"), (0, 16, "2,0"), (16, 16, "1,1"))]);

        Assert.Single(Collect(map.Scene, new RectangleF(10, -200, 100, 100)).Sprites);
        Assert.Equal(4, Collect(map.Scene, Everything).Sprites.Count);
    }

    [Fact]
    public async Task A_hidden_layer_is_not_drawn_and_the_others_can_be_dimmed()
    {
        await using var map = await OpenAsync([["0,0"], ["1,0"]]);
        var changed = 0;
        map.Scene.Changed += (_, _) => changed++;

        using (var transaction = map.Session.UndoRedo.CreateTransaction())
        {
            LayerNode(map, 1)[nameof(TileLayerAsset.Visible)].Update(false);
            map.Session.UndoRedo.SetName(transaction, "Hide");
        }

        Assert.True(changed > 0);
        var sprite = Assert.Single(Collect(map.Scene, Everything).Sprites);
        Assert.Equal(1, sprite.Opacity);
        Assert.Equal(Color.Red, PixelAt(sprite, new Vector2(4, -12)));

        map.Scene.CurrentLayer = 1;
        map.Scene.DimOthers = true;

        Assert.Equal(0.35f, map.Scene.DimOpacity);
        Assert.Equal(0.35f, Assert.Single(Collect(map.Scene, Everything).Sprites).Opacity);
    }

    [Fact]
    public async Task A_layer_s_tint_opacity_and_offset_show()
    {
        await using var map = await OpenAsync([["0,0 1,0"]], asset =>
        {
            var layer = asset.Layers[0];
            layer.Tint = new Color(128, 255, 255, 255);
            layer.Opacity = 0.5f;
            layer.Offset = new Vector2(0.5f, 0.25f);
        });

        var sprite = Assert.Single(Collect(map.Scene, Everything).Sprites);

        Assert.Equal(0.5f, sprite.Opacity);
        // Half a cell to the right and a quarter up; the canvas has Y down
        Assert.Equal(new RectangleF(8, -260, 256, 256), sprite.Destination);
        // The cells of the current layer are where its tiles are drawn
        Assert.Equal(new RectangleF(24, -20, 16, 16), map.Scene.CellBounds(new Int2(1, 0)));
        Assert.Equal(new Int2(1, 0), map.Scene.CellAt(new Vector2(25, -19)));
        var flat = Flat(1, 0);
        Assert.Equal(new Color((byte)((flat.R * 128 + 127) / 255), flat.G, flat.B, flat.A), PixelAt(sprite, Middle(map.Scene, 1, 0)));
    }

    [Fact]
    public async Task A_change_of_a_cell_makes_one_picture_again()
    {
        await using var map = await OpenAsync([["0,0 1,0"]]);
        Collect(map.Scene, Everything);
        var composed = map.Scene.ComposedCount;
        var changed = 0;
        map.Scene.Changed += (_, _) => changed++;

        Assert.True(map.Document.Write(0, new Dictionary<Int2, TileCell> { [new Int2(1, 0)] = Cell(2, 1) }, "Stamp"));
        var frame = Collect(map.Scene, Everything);

        Assert.True(changed > 0);
        Assert.Equal(composed + 1, map.Scene.ComposedCount);
        Assert.Equal(Flat(2, 1), PixelAt(Assert.Single(frame.Sprites), Middle(map.Scene, 1, 0)));
        Collect(map.Scene, Everything);
        Assert.Equal(composed + 1, map.Scene.ComposedCount);
    }

    [Fact]
    public async Task A_tile_larger_than_a_cell_reaches_into_the_next_chunk()
    {
        var tall = TileSet();
        tall.TileSize = new Int2(16, 32);
        await using var map = await OpenAsync([[]], asset =>
        {
            asset.TileSets.Add(new TileSetSlot { Slot = 2, TileSet = AttachedReferenceManager.CreateProxyObject<TileSet>(tall.Id, "Tiles/Tall") });
            asset.LastSlot = 2;
        }, ("Tiles/Tall", tall));

        // The tile 1:0,0 of the tall set: the quarters over the flat tile (0, 1)
        Assert.True(map.Document.Write(0, new Dictionary<Int2, TileCell> { [new Int2(0, 15)] = new TileCell(2, TileId.Parse("1:0,0")) }, "Stamp"));
        var frame = Collect(map.Scene, Everything);

        Assert.Equal(2, frame.Sprites.Count);
        var below = frame.Sprites.Single(sprite => sprite.Destination == new RectangleF(0, -256, 256, 256));
        var above = frame.Sprites.Single(sprite => sprite.Destination == new RectangleF(0, -512, 256, 256));
        var top = map.Scene.CellBounds(new Int2(0, 16));
        Assert.Equal(Color.Red, PixelAt(above, new Vector2(top.X + 4, top.Y + 4)));
        Assert.Equal(Color.Yellow, PixelAt(above, new Vector2(top.X + 12, top.Y + 12)));
        Assert.Equal(Flat(0, 1), PixelAt(below, Middle(map.Scene, 0, 15)));
        Assert.Equal(new RectangleF(0, -256, 16, 16), map.Scene.Bounds);
    }

    [Fact]
    public async Task Zoomed_out_pictures_are_smaller()
    {
        await using var map = await OpenAsync([["0,0 1,0"]]);

        var sprite = Assert.Single(Collect(map.Scene, Everything, zoom: 0.25f).Sprites);

        Assert.Equal((64, 64), (sprite.Picture.Width, sprite.Picture.Height));
        Assert.Equal(new Rectangle(0, 0, 64, 64), sprite.Source);
        Assert.Equal(new RectangleF(0, -256, 256, 256), sprite.Destination);
        Assert.Equal(Flat(1, 0), PixelAt(sprite, Middle(map.Scene, 1, 0)));

        // Between two levels, the larger one
        Assert.Equal(128, Assert.Single(Collect(map.Scene, Everything, zoom: 0.3f).Sprites).Picture.Width);
        Assert.Equal(32, Assert.Single(Collect(map.Scene, Everything, zoom: 0.05f).Sprites).Picture.Width);
        Assert.Equal(256, Assert.Single(Collect(map.Scene, Everything, zoom: 4).Sprites).Picture.Width);
    }

    [Fact]
    public async Task The_cache_keeps_under_its_budget()
    {
        await using var map = await OpenAsync([[]]);
        var cells = Enumerable.Range(0, 40).ToDictionary(i => new Int2(i * 16, 0), _ => Cell(1, 0));
        Assert.True(map.Document.Write(0, cells, "Stamp"));
        map.Scene.PictureBudget = 1024 * 1024;
        const long picture = 256 * 256 * 4;

        foreach (var cell in cells.Keys)
        {
            var frame = Collect(map.Scene, new RectangleF(cell.X * 16 + 10, -200, 100, 100));
            Assert.Single(frame.Sprites);
            Assert.InRange(map.Scene.PictureBytes, picture, 1024 * 1024 + picture);
        }
        Assert.Equal(40, map.Scene.ComposedCount);
    }

    [Fact]
    public async Task The_ghost_and_the_overlay_are_in_the_frame()
    {
        await using var map = await OpenAsync([["0,0"]]);
        var changed = 0;
        map.Scene.Changed += (_, _) => changed++;

        map.Scene.SetGhost(TileStamp.Single(Cell(1, 0)), new Int2(2, 3));
        var frame = Collect(map.Scene, Everything);

        Assert.Equal(1, changed);
        Assert.Equal(2, frame.Sprites.Count);
        var ghost = frame.Sprites[^1];
        Assert.Equal(0.6f, ghost.Opacity);
        Assert.Equal(map.Scene.CellBounds(new Int2(2, 3)), ghost.Destination);
        Assert.Equal(Flat(1, 0), PixelAt(ghost, Middle(map.Scene, 2, 3)));

        var outline = new CanvasShape(CanvasMark.Outline, new RectangleF(0, -16, 32, 16), Color.White);
        map.Scene.Overlay.Add(outline);
        map.Scene.Invalidate();
        Assert.Equal(2, changed);
        Assert.Contains(outline, Collect(map.Scene, Everything).Shapes);

        map.Scene.SetGhost(null, Int2.Zero);
        Assert.Single(Collect(map.Scene, Everything).Sprites);
    }

    [Fact]
    public async Task The_frame_of_the_map_is_outlined_and_is_its_bounds()
    {
        await using var map = await OpenAsync([["0,0"]], asset => asset.Size = new Int2(20, 10));

        var frame = Collect(map.Scene, Everything);

        Assert.Equal(new RectangleF(0, -160, 320, 160), map.Scene.Bounds);
        Assert.Contains(frame.Shapes, shape => shape.Mark == CanvasMark.Outline && shape.Bounds == new RectangleF(0, -160, 320, 160));
    }

    // Review Focus 2 and 3
    [Fact]
    public async Task A_cell_whose_tile_set_or_tile_is_gone_is_a_marked_box()
    {
        await using var map = await OpenAsync([Rows((0, 0, "0,0"), (3, 3, "1,0"), (5, 5, "2,0"))], asset =>
        {
            var chunk = asset.Layers[0].Chunks[0];
            chunk.Cells = chunk.Cells.Replace("1/1:1,0", "7/9:3,3", StringComparison.Ordinal).Replace("1/1:2,0", "1/1:9,9", StringComparison.Ordinal);
        });

        var sprite = Assert.Single(Collect(map.Scene, Everything).Sprites);
        Assert.True(IsMarked(PixelAt(sprite, Middle(map.Scene, 3, 3))));
        Assert.True(IsMarked(PixelAt(sprite, Middle(map.Scene, 5, 5))));
        Assert.True(IsMarked(PixelAt(sprite, Middle(map.Scene, 3, 3) + new Vector2(-6, 0))));
        Assert.Equal(Color.Red, PixelAt(sprite, map.Scene.CellBounds(Int2.Zero).TopLeft + new Vector2(2, 2)));

        // The tile set asset of the slot 1 deleted: its references are cleared, and every cell of the slot is a marked box
        map.Session.Dialogs.FixAssetReferences = _ => Task.FromResult(true);
        Assert.True(await map.Session.Session.DeleteItems([map.Session.TileSetAt("Tiles/Town")], skipConfirmation: true));

        sprite = Assert.Single(Collect(map.Scene, Everything).Sprites);
        Assert.True(IsMarked(PixelAt(sprite, Middle(map.Scene, 0, 0))));
        Assert.True(IsMarked(PixelAt(sprite, Middle(map.Scene, 0, 0) + new Vector2(-6, 0))));
    }

    [Fact]
    public async Task An_unreadable_chunk_is_hatched()
    {
        await using var map = await OpenAsync([Rows((0, 0, "0,0"), (16, 0, "1,0"))], asset =>
            asset.Layers[0].Chunks.Single(chunk => chunk.Position == new Int2(1, 0)).Cells = "x\n");

        var frame = Collect(map.Scene, Everything);

        var hatch = Assert.Single(frame.Shapes, shape => shape.Mark == CanvasMark.Hatch);
        Assert.Equal(new RectangleF(256, -256, 256, 256), hatch.Bounds);
        Assert.True(hatch.Color.R > 200 && hatch.Color.G < 100 && hatch.Color.B < 100, $"{hatch.Color}");
        Assert.Single(frame.Sprites);
    }

    // R4
    [Fact]
    public async Task A_large_map_is_composed_once()
    {
        var tileSet = TileSet();
        await using var map = await OpenAsync(tileSet, LargeMap(tileSet, "Tiles/Town", 256, 4));
        var whole = new RectangleF(0, -4096, 4096, 4096);

        var watch = Stopwatch.StartNew();
        var frame = Collect(map.Scene, whole, zoom: 0.125f);
        var first = watch.Elapsed;
        output.WriteLine($"First collect of 1024 pictures at the zoom 0.125: {first.TotalMilliseconds:0} ms");

        Assert.Equal(1024, frame.Sprites.Count);
        Assert.Equal(1024, map.Scene.ComposedCount);
        Assert.Equal(Flat(1, 0), PixelAt(frame.Sprites.First(sprite => sprite.Destination.Contains(Middle(map.Scene, 1, 0))), Middle(map.Scene, 1, 0)));

        watch.Restart();
        frame = Collect(map.Scene, whole, zoom: 0.125f);
        var second = watch.Elapsed;
        output.WriteLine($"Second collect: {second.TotalMilliseconds:0.0} ms");
        Assert.Equal(1024, frame.Sprites.Count);
        Assert.Equal(1024, map.Scene.ComposedCount);
        Assert.True(second < TimeSpan.FromMilliseconds(50), $"The second collect took {second.TotalMilliseconds} ms.");

        Assert.True(map.Document.Write(2, new Dictionary<Int2, TileCell> { [new Int2(100, 100)] = Cell(2, 1) }, "Stamp"));
        Collect(map.Scene, whole, zoom: 0.125f);
        Assert.Equal(1025, map.Scene.ComposedCount);
    }

    private static async DispatcherTask<OpenScene> OpenAsync(string[][] layers, Action<TileMapAsset>? change = null, params (string Location, Asset Asset)[] others)
    {
        var tileSet = TileSet();
        var asset = Map(tileSet, "Tiles/Town", layers);
        change?.Invoke(asset);
        return await OpenAsync(tileSet, asset, others);
    }

    private static async DispatcherTask<OpenScene> OpenAsync(TileSetAsset tileSet, TileMapAsset asset, params (string Location, Asset Asset)[] others)
    {
        var session = await TesseraSession.OpenAsync([("Tiles/Town", tileSet), (Location, asset), .. others]);
        session.WriteImage("Tiles/town.png", 64, 32, Pixel);
        var document = new TileMapDocument(session.MapAt(Location));
        return new OpenScene(session, document, new TileMapScene(document, TilePictureService.Of(session.Session)));
    }

    private static CanvasFrame Collect(TileMapScene scene, RectangleF visible, float zoom = 1)
    {
        var frame = new CanvasFrame();
        scene.Collect(visible, zoom, frame);
        return frame;
    }

    private static Vector2 Middle(TileMapScene scene, int x, int y) => scene.CellBounds(new Int2(x, y)).Center;

    // The pixel of a sprite's picture drawn at a point of the canvas
    private static Color PixelAt(CanvasSprite sprite, Vector2 canvas)
    {
        var (destination, source) = (sprite.Destination, sprite.Source);
        Assert.True(destination.Contains(canvas), $"{canvas} is not in {destination}.");
        var x = source.X + (int)((canvas.X - destination.X) / destination.Width * source.Width);
        var y = source.Y + (int)((canvas.Y - destination.Y) / destination.Height * source.Height);
        return sprite.Picture[x, y];
    }

    // A pixel of the box of a missing tile, or of its cross
    private static bool IsMarked(Color pixel) => pixel == TileComposer.MissingColor || pixel == Color.Black;

    private static IObjectNode LayerNode(OpenScene map, int layer)
        => map.Session.MapAt(Location).PropertyGraph.RootNode[nameof(TileMapAsset.Layers)].Target!.IndexedTarget(new NodeIndex(layer))!;

    private sealed class OpenScene(TesseraSession session, TileMapDocument document, TileMapScene scene) : IAsyncDisposable
    {
        public TesseraSession Session { get; } = session;

        public TileMapDocument Document { get; } = document;

        public TileMapScene Scene { get; } = scene;

        public async ValueTask DisposeAsync()
        {
            Scene.Dispose();
            Document.Dispose();
            await Session.DisposeAsync();
        }
    }
}
