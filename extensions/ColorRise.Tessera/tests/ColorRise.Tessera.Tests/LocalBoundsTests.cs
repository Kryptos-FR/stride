using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;

namespace ColorRise.Tessera.Tests;

/// <summary>
/// The volume a map and a flipbook give in their entity's space, which the scene editor frames when it focuses on the
/// entity.
/// </summary>
public sealed class LocalBoundsTests
{
    private const float Tolerance = 1e-5f;

    private static readonly TileId Grass = TileId.FromGrid(1, 0, 0);

    [Fact]
    public void A_map_with_a_frame_gives_its_frame_through_its_layers()
    {
        var tileMap = new TileMap { Size = new Int2(60, 40), CellSize = new Vector2(2, 0.5f) };
        tileMap.Layers.Add("Ground");
        tileMap.Layers.Add("Details");
        tileMap.Layers.Add("Overhead");
        // In the entity's space: where the entity stands changes nothing
        var map = Place(new TileMapComponent { Map = tileMap }, new Vector3(10, 20, 3));

        AssertBox(Bounds(map), new Vector3(0, 0, 0), new Vector3(120, 20, 0.2f));
    }

    [Fact]
    public void A_negative_spacing_puts_the_layers_under_the_first()
    {
        var tileMap = new TileMap { Size = new Int2(4, 4) };
        tileMap.Layers.Add("Ground");
        tileMap.Layers.Add("Details");
        var map = new TileMapComponent { Map = tileMap, LayerSpacing = -0.5f };

        AssertBox(Bounds(map), new Vector3(0, 0, -0.5f), new Vector3(4, 4, 0));
    }

    [Fact]
    public void A_map_without_a_frame_gives_its_painted_cells_with_the_offsets_of_their_layers()
    {
        var set = new TileSet();
        set.Tiles[Grass] = new Tile { Id = Grass };
        var tileMap = new TileMap { CellSize = new Vector2(2, 0.5f) };
        tileMap.AddTileSet(set);
        var ground = tileMap.Layers.Add("Ground");
        ground.SetCell(new Int2(2, 3), set, Grass);
        ground.SetCell(new Int2(-5, 7), set, Grass);
        var top = tileMap.Layers.Add("Top");
        top.Offset = new Vector2(0.5f, 0.25f);
        top.SetCell(new Int2(10, 0), set, Grass);
        var map = new TileMapComponent { Map = tileMap };

        // Cells -5 to 10 across and 0 to 7 up; the cell (10, 0) of Top is moved by its layer's offset
        AssertBox(Bounds(map), new Vector3(-10, 0.25f, 0), new Vector3(22.5f, 4, 0.1f));
    }

    [Fact]
    public void The_bounds_of_a_large_map_without_a_frame_read_about_its_outline()
    {
        // The editor's orthographic view asks every frame. Read cell by cell, 1024 by 1024 cells take tens of milliseconds;
        // through the chunks and the cells of the chunks on the edges, well under one
        var set = new TileSet();
        set.Tiles[Grass] = new Tile { Id = Grass };
        var tileMap = new TileMap();
        tileMap.AddTileSet(set);
        var ground = tileMap.Layers.Add("Ground");
        for (var y = 0; y < 1024; y++)
        {
            for (var x = 0; x < 1024; x++)
            {
                ground.SetCell(new Int2(x, y), set, Grass);
            }
        }
        var map = new TileMapComponent { Map = tileMap };

        var fastest = TimeSpan.MaxValue;
        for (var run = 0; run < 5; run++)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            AssertBox(Bounds(map), Vector3.Zero, new Vector3(1024, 1024, 0));
            fastest = TimeSpan.FromTicks(Math.Min(fastest.Ticks, watch.Elapsed.Ticks));
        }
        Assert.True(fastest < TimeSpan.FromMilliseconds(10), $"The bounds took {fastest.TotalMilliseconds} ms.");
    }

    [Fact]
    public void A_map_with_nothing_to_frame_gives_the_empty_box()
    {
        Assert.Equal(BoundingBox.Empty, Bounds(new TileMapComponent()));

        var tileMap = new TileMap();
        tileMap.Layers.Add("Ground");
        Assert.Equal(BoundingBox.Empty, Bounds(new TileMapComponent { Map = tileMap }));
    }

    [Fact]
    public void A_flipbook_gives_the_frame_it_shows_around_its_pivot()
    {
        var flipbook = Place(new FlipbookComponent { Flipbook = Flipbook(), Animation = "idle" }, new Vector3(10, 20, 3));

        // A tile of 16 by 24 pixels at 16 a unit, the pivot at the middle of its bottom
        AssertBox(Bounds(flipbook), new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 1.5f, 0));
    }

    [Fact]
    public void A_flipbook_mirrored_gives_its_mirrored_frame()
    {
        var book = Flipbook();
        book.Pivot = Vector2.Zero;
        var flipbook = new FlipbookComponent { Flipbook = book, Animation = "idle", FlipX = true };

        AssertBox(Bounds(flipbook), new Vector3(-1, 0, 0), new Vector3(0, 1.5f, 0));
    }

    [Fact]
    public void A_flipbook_that_shows_nothing_gives_the_empty_box()
    {
        Assert.Equal(BoundingBox.Empty, Bounds(new FlipbookComponent()));
        Assert.Equal(BoundingBox.Empty, Bounds(new FlipbookComponent { Flipbook = Flipbook(), Animation = "run" }));

        var book = Flipbook();
        book.TileSet!.Tiles.Clear();
        Assert.Equal(BoundingBox.Empty, Bounds(new FlipbookComponent { Flipbook = book, Animation = "idle" }));
    }

    private static BoundingBox Bounds(EntityComponent component) => Assert.IsAssignableFrom<IEntityComponentBounds>(component).LocalBounds;

    private static T Place<T>(T component, Vector3 position) where T : EntityComponent
    {
        var entity = new Entity { component };
        entity.Transform.Position = position;
        entity.Transform.UpdateWorldMatrix();
        return component;
    }

    // A flipbook with the animation idle, of one frame whose cell is a tile of 16 by 24 pixels, at 16 pixels a unit
    private static Flipbook Flipbook()
    {
        var set = new TileSet { PixelsPerUnit = 16, TileSize = new Int2(16, 16) };
        set.Tiles[Grass] = new Tile { Id = Grass, Texture = new Texture(), Region = new Rectangle(0, 0, 16, 24) };
        var frame = new FlipbookFrame();
        frame.Cells.Add(new FlipbookCell { Tile = Grass });
        var clip = new FlipbookClip();
        clip.Frames.Add(frame);
        var idle = new FlipbookAnimation { Name = "idle" };
        idle.Clips.Add(clip);
        var flipbook = new Flipbook { TileSet = set };
        flipbook.Animations.Add(idle);
        return flipbook;
    }

    private static void AssertBox(BoundingBox box, Vector3 minimum, Vector3 maximum)
    {
        Assert.True(Vector3.Distance(minimum, box.Minimum) < Tolerance, $"Expected the minimum {minimum}, got {box.Minimum}.");
        Assert.True(Vector3.Distance(maximum, box.Maximum) < Tolerance, $"Expected the maximum {maximum}, got {box.Maximum}.");
    }
}
