using System.Text;
using ColorRise.Tessera.Tests.Assets;
using ColorRise.Tessera.Tests.Graphics;
using Stride.Core.Assets;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;

namespace ColorRise.Tessera.Tests;

/// <summary>
/// Tile maps and tile sets written as content and read back with a content manager. In the collection of the graphics tests:
/// one of them runs a game, and all share the process's content folders with the builds.
/// </summary>
[Collection(GraphicsCollection.Name)]
public sealed class ContentTests
{
    [Fact]
    public void A_tile_map_comes_back_as_it_was_saved()
    {
        using var build = new AssetBuild();
        var map = new TileMap { CellSize = new Vector2(2, 0.5f), Size = new Int2(40, 30) };
        map.SetTileSet(1, new TileSet { PixelsPerUnit = 8 });
        map.SetTileSet(7, new TileSet { PixelsPerUnit = 32 });
        var ground = map.Layers.Add("Ground");
        (ground.Visible, ground.Opacity, ground.Tint, ground.Offset, ground.Collision) = (false, 0.25f, new Color(10, 20, 30, 40), new Vector2(0.5f, -1), false);
        var top = map.Layers.Add("Top");
        (top.Opacity, top.Tint, top.Offset) = (0.75f, new Color(200, 100, 50, 255), new Vector2(-2, 3));
        ground.SetCell(new Int2(-1, -1), new TileCell(1, TileId.FromGrid(1, 3, 4), TileFlip.Diagonal | TileFlip.Horizontal));
        ground.SetCell(new Int2(-16, -16), new TileCell(7, TileId.FromNumber(2, 17)));
        ground.SetCell(new Int2(3 * 16 + 5, 2 * 16 + 7), new TileCell(7, TileId.FromGrid(3, 4095, 2047), TileFlip.Vertical));
        top.SetCell(new Int2(0, 0), new TileCell(1, TileId.FromGrid(1, 0, 0), TileFlip.Diagonal | TileFlip.Horizontal | TileFlip.Vertical));

        build.Save("Maps/Town", map);
        var loaded = build.Load<TileMap>("Maps/Town");

        Assert.NotSame(map, loaded);
        Assert.Equal(Describe(map), Describe(loaded));
        Assert.All(loaded.Layers, layer => Assert.Same(loaded, layer.Map));
    }

    [Fact]
    public void A_tile_set_comes_back_as_it_was_saved()
    {
        using var build = new AssetBuild();
        var set = new TileSet { PixelsPerUnit = 8, TileSize = new Int2(16, 24) };
        var water = new Tile { Id = TileId.FromGrid(1, 0, 0), Region = new Rectangle(0, 0, 16, 24), Collision = TileCollision.Solid, Kind = "water" };
        water.Animation.Add(new TileAnimationFrame { Tile = TileId.FromGrid(1, 1, 0), Duration = 0.25f });
        water.Animation.Add(new TileAnimationFrame { Tile = TileId.FromNumber(2, 17), Duration = 0.5f });
        water.Properties["depth"] = 3;
        water.Properties["speed"] = 0.5f;
        water.Properties["deep"] = true;
        water.Properties["name"] = "pond";
        water.Properties["tint"] = new Color(1, 2, 3, 4);
        set.Tiles[water.Id] = water;
        set.Tiles[TileId.FromGrid(1, 1, 0)] = new Tile { Id = TileId.FromGrid(1, 1, 0), Region = new Rectangle(16, 0, 16, 24) };
        set.Tiles[TileId.FromNumber(2, 17)] = new Tile { Id = TileId.FromNumber(2, 17), Region = new Rectangle(32, 0, 16, 48), Kind = "tall" };

        build.Save("Tiles/Pond", set);
        var loaded = build.Load<TileSet>("Tiles/Pond");

        Assert.NotSame(set, loaded);
        Assert.Equal(8, loaded.PixelsPerUnit);
        Assert.Equal(new Int2(16, 24), loaded.TileSize);
        Assert.Equal(set.Tiles.Keys.Order(), loaded.Tiles.Keys.Order());
        foreach (var (id, tile) in set.Tiles)
        {
            var back = loaded.Tiles[id];
            Assert.Equal((tile.Id, tile.Region, tile.Collision, tile.Kind), (back.Id, back.Region, back.Collision, back.Kind));
            Assert.Equal(tile.Animation, back.Animation);
        }
        var properties = loaded.Tiles[water.Id].Properties;
        Assert.Equal(5, properties.Count);
        Assert.Equal(3, Assert.IsType<int>(properties["depth"]));
        Assert.Equal(0.5f, Assert.IsType<float>(properties["speed"]));
        Assert.True(Assert.IsType<bool>(properties["deep"]));
        Assert.Equal("pond", Assert.IsType<string>(properties["name"]));
        Assert.Equal(new Color(1, 2, 3, 4), Assert.IsType<Color>(properties["tint"]));
    }

    [Fact]
    public void A_flipbook_comes_back_as_it_was_saved()
    {
        using var build = new AssetBuild();
        build.Save("Tiles/Hero", new TileSet { PixelsPerUnit = 8 });
        var flipbook = new Flipbook { TileSet = Reference("Tiles/Hero"), Pivot = new Vector2(0.25f, 0.5f) };
        var walk = new FlipbookAnimation { Name = "walk", LoopMode = FlipbookLoopMode.PingPong };
        var down = new FlipbookClip { Direction = FlipbookDirection.Down };
        var frame = new FlipbookFrame { Duration = 0.15f };
        frame.Cells.Add(new FlipbookCell { Tile = TileId.Parse("1:0,0") });
        frame.Cells.Add(new FlipbookCell { Tile = TileId.Parse("1:1,0"), Offset = new Int2(0, 16), Flip = TileFlip.Horizontal });
        down.Frames.Add(frame);
        walk.Clips.Add(down);
        walk.Clips.Add(new FlipbookClip { Direction = FlipbookDirection.Left });
        flipbook.Animations.Add(walk);

        build.Save("Flipbooks/Hero", flipbook);
        var loaded = build.Load<Flipbook>("Flipbooks/Hero");

        Assert.NotSame(flipbook, loaded);
        Assert.Equal(new Vector2(0.25f, 0.5f), loaded.Pivot);
        Assert.Equal(8, loaded.TileSet!.PixelsPerUnit);
        var back = Assert.Single(loaded.Animations);
        Assert.Equal(("walk", FlipbookLoopMode.PingPong), (back.Name, back.LoopMode));
        Assert.Equal([FlipbookDirection.Down, FlipbookDirection.Left], back.Clips.Select(clip => clip.Direction));
        Assert.Empty(back.Clips[1].Frames);
        var backFrame = Assert.Single(back.Clips[0].Frames);
        Assert.Equal(0.15f, backFrame.Duration);
        Assert.Equal(
            [(TileId.Parse("1:0,0"), Int2.Zero, TileFlip.None), (TileId.Parse("1:1,0"), new Int2(0, 16), TileFlip.Horizontal)],
            backFrame.Cells.Select(cell => (cell.Tile, cell.Offset, cell.Flip)));
    }

    [Fact]
    public void A_map_read_again_keeps_its_object_and_tells()
    {
        using var build = new AssetBuild();
        build.Save("Tiles/Town", new TileSet());
        build.Save("Maps/Town", OneCell(Reference("Tiles/Town"), TileId.FromGrid(1, 0, 0)));

        var map = build.Content.Load<TileMap>("Maps/Town");
        var raised = 0;
        map.Reloaded += (sender, _) =>
        {
            Assert.Same(map, sender);
            raised++;
        };
        build.Save("Maps/Town", OneCell(Reference("Tiles/Town"), TileId.FromGrid(1, 1, 0)));
        build.Content.Reload(map);

        Assert.Equal(TileId.FromGrid(1, 1, 0), map.Layers["Ground"].GetCell(Int2.Zero).Tile);
        Assert.Single(map.Layers);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void A_reloaded_map_shows_its_new_cells_in_the_next_frame()
    {
        using var build = new AssetBuild();
        build.Save("Tiles/Flat", new TileSet { PixelsPerUnit = 4, TileSize = new Int2(4, 4) });
        build.Save("Maps/Town", OneCell(Reference("Tiles/Flat"), TileId.FromGrid(1, 0, 0)));
        var map = build.Content.Load<TileMap>("Maps/Town");

        var frames = GraphicsHarness.Run(new HarnessOptions(), scene =>
        {
            // The loaded set gets the pixels of the game: red, then green; a reload of the map reads the same set
            var flat = TestTiles.Flat(scene.Game.GraphicsDevice, new Color(255, 0, 0, 255), new Color(0, 255, 0, 255));
            var set = map.TileSets[1];
            foreach (var (id, tile) in flat.Tiles)
            {
                set.Tiles[id] = tile;
            }
            scene.Add("map", new TileMapComponent { Map = map });
            scene.SetView(new Vector2(0.5f, 0.5f), 1);
        }, _ => { }, _ =>
        {
            build.Save("Maps/Town", OneCell(Reference("Tiles/Flat"), TileId.FromGrid(1, 1, 0)));
            build.Content.Reload(map);
        });

        Assert.Equal(new Color(255, 0, 0, 255), frames[0][32, 32]);
        Assert.Equal(new Color(0, 255, 0, 255), frames[1][32, 32]);
    }

    // The tile set at a URL, as an asset names it: written as a reference, never as content of its own
    private static TileSet Reference(string url) => AttachedReferenceManager.CreateProxyObject<TileSet>(AssetId.New(), url);

    // A map of one layer, Ground, with one tile of slot 1 in the cell (0, 0)
    private static TileMap OneCell(TileSet set, TileId tile)
    {
        var map = new TileMap();
        map.SetTileSet(1, set);
        map.Layers.Add("Ground").SetCell(Int2.Zero, set, tile);
        return map;
    }

    // Every value of a map, its tile sets by slot and pixels per unit, its cells in order
    internal static string Describe(TileMap map)
    {
        var text = new StringBuilder();
        text.Append($"{map.CellSize} {map.Size}\n");
        foreach (var (slot, set) in map.TileSets.OrderBy(pair => pair.Key))
        {
            text.Append($"slot {slot}: {set.PixelsPerUnit}\n");
        }
        foreach (var layer in map.Layers)
        {
            text.Append($"{layer.Name} {layer.Visible} {layer.Opacity} {layer.Tint} {layer.Offset} {layer.Collision}\n");
            foreach (var chunk in layer.Chunks.OrderBy(c => c.Position.Y).ThenBy(c => c.Position.X))
            {
                for (var i = 0; i < chunk.Cells.Length; i++)
                {
                    if (!chunk.Cells[i].IsEmpty)
                    {
                        var cell = chunk.Cells[i];
                        text.Append($"  ({chunk.Position.X * TileChunk.Size + i % TileChunk.Size}, {chunk.Position.Y * TileChunk.Size + i / TileChunk.Size}) {cell.Slot}/{cell.Tile} {cell.Flip}\n");
                    }
                }
            }
        }
        return text.ToString();
    }
}
