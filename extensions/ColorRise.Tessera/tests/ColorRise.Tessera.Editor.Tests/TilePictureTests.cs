using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Pictures;
using Stride.Assets.Sprite;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Quantum;
using Stride.Core.Serialization;
using Stride.Graphics;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// The pictures the editors draw a tile set with: its sources, made from their files once for the session.
/// </summary>
public sealed class TilePictureTests
{
    [Fact]
    public async Task A_grid_source_is_its_image_with_a_slot_per_tile_that_is_not_empty()
    {
        await using var session = await OpenTownAsync(TestTown.TileSet());
        var pictures = Pictures(session);

        var layout = Assert.Single(pictures.Sources);
        Assert.True(layout.IsGrid);
        Assert.Equal(new Int2(64, 32), layout.Size);
        Assert.Null(layout.Problem);
        Assert.Equal(7, layout.Slots.Count);

        var slot = layout.SlotOf(TileId.Parse("1:2,1"))!;
        Assert.Equal(new Rectangle(32, 16, 16, 16), slot.Bounds);
        Assert.Equal(new Rectangle(32, 16, 16, 16), slot.Region);
        Assert.Same(layout.Image, slot.Picture);
        Assert.Equal(TestTown.Flat(2, 1), slot.Picture![32, 16]);
        Assert.Null(slot.SpriteName);
        Assert.False(slot.IsMissing);

        Assert.Same(slot, layout.SlotAt(new Vector2(33, 17)));
        Assert.Null(layout.SlotAt(new Vector2(50, 20)));
        Assert.Null(layout.SlotAt(new Vector2(-1, 0)));
        Assert.Null(layout.SlotAt(new Vector2(64, 0)));
        Assert.Same(slot, pictures.Find(TileId.Parse("1:2,1")));
        Assert.Equal(new Int2(16, 16), pictures.LargestTile);
    }

    [Fact]
    public async Task The_color_key_makes_its_pixels_transparent_and_can_empty_a_tile()
    {
        var tileSet = TestTown.TileSet();
        tileSet.ColorKeyEnabled = true;
        tileSet.ColorKeyColor = TestTown.Flat(1, 0);
        await using var session = await OpenTownAsync(tileSet);

        var layout = Assert.Single(Pictures(session).Sources);
        Assert.Equal(0, layout.Image![20, 4].A);
        Assert.Equal(6, layout.Slots.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not an image")]
    public async Task An_image_that_cannot_be_read_is_a_problem_of_its_source_only(string? content)
    {
        var tileSet = TestTown.TileSet();
        tileSet.AddSource<GridTileSetSource>("Cave").Image = new UFile("cave.png");
        await using var session = await TesseraSession.OpenAsync(("Tiles/Town", tileSet));
        session.WriteImage("Tiles/cave.png", 32, 16, (_, _) => Color.Gray);
        if (content is not null)
        {
            File.WriteAllText(session.FileOf("Tiles/town", ".png"), content);
        }

        var sources = Pictures(session).Sources;
        Assert.Equal(2, sources.Count);
        Assert.Contains("town.png", sources[0].Problem);
        Assert.Empty(sources[0].Slots);
        Assert.Null(sources[0].Image);
        Assert.Null(sources[1].Problem);
        Assert.Equal(2, sources[1].Slots.Count);
    }

    [Theory]
    [InlineData(0, 16)]
    [InlineData(16, 0)]
    [InlineData(128, 16)]
    [InlineData(-4, 16)]
    public async Task A_tile_size_that_makes_no_tile_gives_no_slot_and_does_not_throw(int width, int height)
    {
        var tileSet = TestTown.TileSet();
        ((GridTileSetSource)tileSet.Sources[0]).TileSize = new Int2(width, height);
        await using var session = await OpenTownAsync(tileSet);

        var pictures = Pictures(session);
        Assert.Empty(Assert.Single(pictures.Sources).Slots);
        Assert.Equal(new Int2(16, 16), pictures.LargestTile);
    }

    [Fact]
    public async Task A_sheet_source_is_a_box_per_bound_sprite_with_the_missing_ones_marked()
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
        source.Bindings.Add(new SpriteTileBinding { Number = 2, SpriteName = "gone" });
        source.LastNumber = 2;
        await using var session = await TesseraSession.OpenAsync(("Tiles/Town", tileSet), ("Tiles/Objects", sheet));
        session.WriteImage("Tiles/town.png", 64, 32, TestTown.Pixel);

        var layout = Assert.Single(Pictures(session).Sources);
        Assert.False(layout.IsGrid);
        var tree = layout.SlotOf(TileId.Parse("1:#1"))!;
        Assert.Equal(new Rectangle(0, 0, 16, 32), tree.Region);
        Assert.NotNull(tree.Picture);
        Assert.Equal((16, 32), (tree.Bounds.Width, tree.Bounds.Height));
        Assert.Equal("tree", tree.SpriteName);
        var gone = layout.SlotOf(TileId.Parse("1:#2"))!;
        Assert.True(gone.IsMissing);
        Assert.Equal("gone", gone.SpriteName);
        Assert.False(tree.Bounds.Intersects(gone.Bounds));
        foreach (var bounds in (Rectangle[])[tree.Bounds, gone.Bounds])
        {
            Assert.True(bounds is { Left: >= 0, Top: >= 0 } && bounds.Right <= layout.Size.X && bounds.Bottom <= layout.Size.Y, bounds.ToString());
        }
        Assert.Equal(["rock"], layout.UnboundSprites);
    }

    [Fact]
    public async Task A_change_of_the_asset_makes_the_pictures_again()
    {
        await using var session = await OpenTownAsync(TestTown.TileSet());
        var tileSet = session.TileSetAt("Tiles/Town");
        var pictures = Pictures(session);
        var changed = false;
        pictures.Changed += (_, _) => changed = true;

        using (var transaction = session.UndoRedo.CreateTransaction())
        {
            var source = tileSet.PropertyGraph.RootNode[nameof(TileSetAsset.Sources)].Target!.IndexedTarget(new NodeIndex(0))!;
            source[nameof(GridTileSetSource.TileSize)].Update(new Int2(32, 32));
            session.UndoRedo.SetName(transaction, "Tile size");
        }
        await ExclusiveDispatcherService.WaitUntilAsync(() => changed, "the pictures to be made again");

        Assert.Equal(2, Assert.Single(pictures.Sources).Slots.Count);
    }

    [Fact]
    public async Task A_changed_image_file_shows_after_a_refresh()
    {
        await using var session = await OpenTownAsync(TestTown.TileSet());
        var pictures = Pictures(session);
        Assert.Equal(TestTown.Flat(2, 0), pictures.Sources[0].Image![40, 4]);

        var file = session.WriteImage("Tiles/town.png", 64, 32, (_, _) => Color.White);
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(1));
        Assert.Equal(TestTown.Flat(2, 0), pictures.Sources[0].Image![40, 4]);

        var changed = false;
        pictures.Changed += (_, _) => changed = true;
        pictures.Refresh();
        Assert.True(changed);
        Assert.Equal(Color.White, pictures.Sources[0].Image![40, 4]);
    }

    [Fact]
    public async Task The_service_gives_one_object_per_tile_set_and_finds_it_by_its_reference()
    {
        await using var session = await OpenTownAsync(TestTown.TileSet());
        var service = TilePictureService.Of(session.Session);
        var tileSet = session.TileSetAt("Tiles/Town");

        var pictures = service.Get(tileSet);
        Assert.Same(pictures, service.Get(tileSet));
        Assert.Same(pictures, service.Get(AttachedReferenceManager.CreateProxyObject<TileSet>(tileSet.Id, "Tiles/Town")));
        Assert.Null(service.Get(AttachedReferenceManager.CreateProxyObject<TileSet>(AssetId.New(), "Tiles/Gone")));
        Assert.Null(service.Get((TileSet?)null));
    }

    private static async DispatcherTask<TesseraSession> OpenTownAsync(TileSetAsset tileSet)
    {
        var session = await TesseraSession.OpenAsync(("Tiles/Town", tileSet));
        session.WriteImage("Tiles/town.png", 64, 32, TestTown.Pixel);
        return session;
    }

    private static TileSetPictures Pictures(TesseraSession session) => TilePictureService.Of(session.Session).Get(session.TileSetAt("Tiles/Town"));
}
