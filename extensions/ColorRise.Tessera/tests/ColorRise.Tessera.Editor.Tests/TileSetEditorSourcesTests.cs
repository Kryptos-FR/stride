using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Canvas;
using ColorRise.Tessera.Editor.TileSetEditor;
using Stride.Assets.Presentation.ViewModels;
using Stride.Assets.Sprite;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.Services;
using Stride.Core.Quantum;
using Stride.Core.Serialization;
using Stride.Graphics;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// The tile set editor and the sources of its tile set: images cut on a grid, and sprite sheets.
/// </summary>
public sealed class TileSetEditorSourcesTests
{
    [Fact]
    public async Task The_editor_opens_on_the_first_source_and_makes_no_step()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");

        var source = Assert.Single(editor.Sources);
        Assert.Equal("Town", source.Name);
        Assert.True(source.IsGrid);
        Assert.Same(source, editor.SelectedSource);
        Assert.Same(source.Layout, editor.Scene.Layout);
        Assert.NotNull(source.Layout);
        Assert.Empty(session.Steps);
        Assert.False(session.TileSetAt("Tiles/Town").IsDirty);
        Assert.Empty(editor.Problems);
    }

    [Fact]
    public async Task An_image_is_proposed_with_the_tile_sets_tile_size_and_nothing_is_added()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");

        editor.ProposeImage(Cave(session));

        var pending = editor.PendingImage!;
        Assert.Equal("cave", pending.Name);
        Assert.Equal(new Int2(64, 32), pending.ImageSize);
        Assert.Equal((16, 16), (pending.TileWidth, pending.TileHeight));
        Assert.Equal((4, 2), (pending.Columns, pending.Rows));
        Assert.Null(pending.Problem);
        Assert.True(pending.ConfirmCommand.IsEnabled);
        var frame = new CanvasFrame();
        pending.Scene.Collect(new RectangleF(0, 0, 64, 32), 1, frame);
        Assert.Single(frame.Sprites);
        Assert.Equal(8, frame.Shapes.Count(shape => shape.Mark == CanvasMark.Outline));
        Assert.Single(TileSet(session).Sources);
        Assert.Empty(session.Steps);
    }

    [Theory]
    [InlineData(32, 32, 0, 0, 2, 1)]
    [InlineData(16, 16, 8, 0, 3, 2)]
    [InlineData(15, 15, 0, 1, 4, 2)]
    public async Task The_numbers_change_the_grid(int tile, int tileY, int margin, int spacing, int columns, int rows)
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.ProposeImage(Cave(session));
        var pending = editor.PendingImage!;
        var changed = 0;
        pending.Scene.Changed += (_, _) => changed++;

        pending.TileWidth = tile;
        pending.TileHeight = tileY;
        pending.MarginX = margin;
        pending.SpacingX = spacing;
        pending.SpacingY = spacing;

        Assert.Equal((columns, rows), (pending.Columns, pending.Rows));
        Assert.Null(pending.Problem);
        Assert.True(changed > 0);
        var frame = new CanvasFrame();
        pending.Scene.Collect(new RectangleF(0, 0, 64, 32), 1, frame);
        Assert.Equal(columns * rows, frame.Shapes.Count(shape => shape.Mark == CanvasMark.Outline));
    }

    [Fact]
    public async Task A_grid_of_too_many_tiles_is_counted_and_not_outlined()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.ProposeImage(session.WriteImage("Tiles/big.png", 512, 512, (_, _) => Color.White));
        var pending = editor.PendingImage!;

        pending.TileWidth = 2;
        pending.TileHeight = 2;

        Assert.Equal((256, 256), (pending.Columns, pending.Rows));
        Assert.Null(pending.Problem);
        var frame = new CanvasFrame();
        pending.Scene.Collect(new RectangleF(0, 0, 512, 512), 1, frame);
        Assert.Empty(frame.Shapes);

        pending.TileWidth = 16;
        pending.TileHeight = 16;

        frame.Clear();
        pending.Scene.Collect(new RectangleF(0, 0, 512, 512), 1, frame);
        Assert.Equal(1024, frame.Shapes.Count(shape => shape.Mark == CanvasMark.Outline));
    }

    [Theory]
    [InlineData(0, 16)]
    [InlineData(16, -1)]
    [InlineData(128, 16)]
    public async Task A_grid_with_no_tile_is_a_problem_and_cannot_be_added(int width, int height)
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.ProposeImage(Cave(session));
        var pending = editor.PendingImage!;

        pending.TileWidth = width;
        pending.TileHeight = height;

        Assert.NotNull(pending.Problem);
        Assert.False(pending.ConfirmCommand.IsEnabled);
        pending.ConfirmCommand.Execute();
        Assert.Single(TileSet(session).Sources);
        Assert.Empty(session.Steps);
    }

    [Fact]
    public async Task A_file_that_is_not_an_image_is_proposed_with_its_problem()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        var notes = session.WriteImage("Tiles/notes.png", 1, 1, (_, _) => Color.White);
        File.WriteAllText(notes, "These are notes, not an image.");

        editor.ProposeImage(notes);

        var pending = editor.PendingImage!;
        Assert.Contains("notes.png", pending.Problem);
        Assert.Equal(Int2.Zero, pending.ImageSize);
        Assert.False(pending.ConfirmCommand.IsEnabled);
    }

    [Fact]
    public async Task Add_makes_the_source_in_one_step_and_undo_takes_it_back()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.ProposeImage(Cave(session));

        editor.PendingImage!.ConfirmCommand.Execute();

        var tileSet = TileSet(session);
        Assert.Equal(["Town", "cave"], editor.Sources.Select(source => source.Name));
        Assert.Equal(2, editor.Sources[1].Id);
        Assert.Equal(2, tileSet.LastSourceId);
        Assert.Same(editor.Sources[1], editor.SelectedSource);
        Assert.Null(editor.PendingImage);
        Assert.Equal(["Add image"], session.Steps);
        // The tile size of the tile set is stored as (0, 0), so that the source follows it
        Assert.Equal(Int2.Zero, ((GridTileSetSource)tileSet.Sources[1]).TileSize);

        session.UndoRedo.Undo();
        Assert.Single(editor.Sources);
        Assert.Equal(1, tileSet.LastSourceId);
        Assert.Same(editor.Sources[0], editor.SelectedSource);

        session.UndoRedo.Redo();
        Assert.Equal(2, editor.Sources.Count);
        Assert.Equal(2, tileSet.LastSourceId);
        await ExclusiveDispatcherService.WaitUntilAsync(() => editor.Pictures.Sources.Count == 2, "the pictures of the two sources");
        Assert.NotNull(editor.Sources[1].Layout);
        Assert.Equal(8, editor.Sources[1].Layout!.Slots.Count);
    }

    [Fact]
    public async Task Cancel_leaves_nothing()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.ProposeImage(Cave(session));

        editor.PendingImage!.CancelCommand.Execute();

        Assert.Null(editor.PendingImage);
        Assert.Single(TileSet(session).Sources);
        Assert.Empty(session.Steps);
    }

    [Fact]
    public async Task A_second_image_of_the_same_name_gets_a_number()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");

        editor.ProposeImage(new UFile(session.FileOf("Tiles/town", ".png")));
        Assert.Equal("town 2", editor.PendingImage!.Name);
        editor.PendingImage.ConfirmCommand.Execute();

        Assert.Equal(["Town", "town 2"], editor.Sources.Select(source => source.Name));
    }

    [Fact]
    public async Task The_saved_asset_names_the_image_relative_to_its_folder()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.ProposeImage(Cave(session));
        editor.PendingImage!.ConfirmCommand.Execute();

        Assert.True(await session.Session.SaveSessionAsync());

        var text = File.ReadAllText(session.FileOf("Tiles/Town", TileSetAsset.FileExtension));
        // The session writes the path relative to the asset's folder, with the tag of a file
        Assert.True(text.Split('\n').Select(line => line.Trim()).Contains("Image: !file cave.png"), text);
    }

    [Fact]
    public async Task A_sprite_sheet_is_added_with_all_its_sprites_bound()
    {
        await using var session = await OpenTownAsync(objects: true);
        var editor = await session.EditTileSetAsync("Tiles/Town");

        Assert.True(editor.AddSpriteSheet(Objects(session)));

        var source = Assert.IsType<SpriteSheetTileSetSource>(TileSet(session).Sources[1]);
        Assert.Equal("Objects", source.Name);
        Assert.Equal(2, source.Id);
        Assert.Equal(["1 grass", "2 water", "3 wall"], Bindings(source));
        Assert.Equal(3, source.LastNumber);
        Assert.Equal(["Add sprite sheet"], session.Steps);
        Assert.Same(editor.Sources[1], editor.SelectedSource);
        Assert.False(editor.SelectedSource!.IsGrid);
    }

    [Fact]
    public async Task A_sheet_the_set_already_has_is_refused_with_a_message()
    {
        await using var session = await OpenTownAsync(objects: true);
        var editor = await session.EditTileSetAsync("Tiles/Town");
        Assert.True(editor.AddSpriteSheet(Objects(session)));

        Assert.False(editor.AddSpriteSheet(Objects(session)));

        Assert.Contains("Objects", editor.Message);
        Assert.Equal(2, TileSet(session).Sources.Count);
        Assert.Equal(["Add sprite sheet"], session.Steps);
    }

    [Fact]
    public async Task Dropped_sprite_sheets_are_added_and_other_assets_refused()
    {
        await using var session = await OpenTownAsync(objects: true, map: true);
        var editor = await session.EditTileSetAsync("Tiles/Town");
        var sheet = Objects(session);

        Assert.True(editor.CanAddChildren([sheet], AddChildModifiers.None, out _));
        Assert.False(editor.CanAddChildren([session.MapAt("Maps/Town")], AddChildModifiers.None, out var message));
        Assert.False(string.IsNullOrEmpty(message));

        editor.AddChildren([sheet], AddChildModifiers.None);

        Assert.IsType<SpriteSheetTileSetSource>(TileSet(session).Sources[1]);
        Assert.Equal(["Add sprite sheet"], session.Steps);
    }

    [Fact]
    public async Task Opening_binds_the_new_sprites_of_a_sheet_in_one_step()
    {
        await using var session = await OpenTownAsync(objects: true, bound: ["grass", "water"]);

        await session.EditTileSetAsync("Tiles/Town");

        Assert.Equal(["1 grass", "2 water", "3 wall"], Bindings(Sheet(session)));
        Assert.Equal(["Bind sprites"], session.Steps);
    }

    [Fact]
    public async Task A_sprite_added_to_the_sheet_while_the_editor_is_open_is_bound()
    {
        await using var session = await OpenTownAsync(objects: true, bound: ["grass", "water", "wall"]);
        var editor = await session.EditTileSetAsync("Tiles/Town");
        Assert.Empty(session.Steps);

        using (var transaction = session.UndoRedo.CreateTransaction())
        {
            Sprites(session).Add(new SpriteInfo { Name = "door", Source = new UFile("town.png"), TextureRegion = new Rectangle(0, 16, 16, 16) });
            session.UndoRedo.SetName(transaction, "Add sprite");
        }
        await ExclusiveDispatcherService.WaitUntilAsync(() => Sheet(session).Bindings.Count == 4, "the new sprite to be bound");

        Assert.Equal(["1 grass", "2 water", "3 wall", "4 door"], Bindings(Sheet(session)));
        Assert.Equal(["Add sprite", "Bind sprites"], session.Steps);
        Assert.Equal(2, editor.Sources.Count);
    }

    [Fact]
    public async Task A_renamed_sprite_leaves_a_missing_tile_and_a_new_one()
    {
        await using var session = await OpenTownAsync(objects: true, bound: ["grass", "water", "wall"]);
        var editor = await session.EditTileSetAsync("Tiles/Town");

        using (var transaction = session.UndoRedo.CreateTransaction())
        {
            Sprites(session).IndexedTarget(new NodeIndex(1))![nameof(SpriteInfo.Name)].Update("sea");
            session.UndoRedo.SetName(transaction, "Rename sprite");
        }
        await ExclusiveDispatcherService.WaitUntilAsync(() => Sheet(session).Bindings.Count == 4, "the renamed sprite to be bound");
        await ExclusiveDispatcherService.WaitUntilAsync(() => editor.Sources[1].Layout?.Slots.Count == 4, "the pictures of the bound sprite");

        Assert.Equal(["1 grass", "2 water", "3 wall", "4 sea"], Bindings(Sheet(session)));
        var layout = editor.Sources[1].Layout!;
        Assert.True(layout.SlotOf(TileId.Parse("2:#2"))!.IsMissing);
        Assert.False(layout.SlotOf(TileId.Parse("2:#4"))!.IsMissing);
        Assert.Contains(editor.Problems, line => line.Contains("water"));
    }

    [Fact]
    public async Task A_removed_sprite_leaves_a_missing_tile_and_the_editor_stays_open()
    {
        await using var session = await OpenTownAsync(objects: true, bound: ["grass", "water", "wall"]);
        var editor = await session.EditTileSetAsync("Tiles/Town");

        using (var transaction = session.UndoRedo.CreateTransaction())
        {
            Sprites(session).Remove(Objects(session).Asset.Sprites[1], new NodeIndex(1));
            session.UndoRedo.SetName(transaction, "Remove sprite");
        }
        await ExclusiveDispatcherService.WaitUntilAsync(() => editor.Sources[1].Layout?.SlotOf(TileId.Parse("2:#2"))?.IsMissing == true, "the tile to be missing");

        Assert.Equal(["1 grass", "2 water", "3 wall"], Bindings(Sheet(session)));
        Assert.Contains(editor.Problems, line => line.Contains("water"));
        Assert.Equal(2, editor.Sources.Count);
        Assert.Equal(["Remove sprite"], session.Steps);
    }

    [Fact]
    public async Task An_image_that_is_gone_is_a_problem_line_and_the_source_can_still_be_removed()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        File.Delete(session.FileOf("Tiles/town", ".png"));

        editor.RefreshCommand.Execute();

        Assert.Contains(editor.Problems, line => line.Contains("town.png"));
        Assert.Empty(editor.Scene.Layout!.Slots);
        session.Dialogs.Answers.Enqueue(MessageBoxResult.Yes);
        editor.RemoveSourceCommand.Execute();
        Assert.Empty(TileSet(session).Sources);
        Assert.Null(editor.SelectedSource);
    }

    [Fact]
    public async Task Remove_asks_and_removes_the_source_and_its_tile_data_in_one_step()
    {
        var tileSet = TestTown.TileSet();
        tileSet.Tiles.Add(new TileDefinition { Tile = TileId.Parse("1:0,0"), Collision = TileCollision.Solid });
        await using var session = await OpenTownAsync(tileSet);
        var editor = await session.EditTileSetAsync("Tiles/Town");
        session.Dialogs.Answers.Enqueue(MessageBoxResult.Yes);

        editor.RemoveSourceCommand.Execute();

        var asset = TileSet(session);
        Assert.Empty(asset.Sources);
        Assert.Empty(asset.Tiles);
        Assert.Empty(editor.Sources);
        Assert.Equal(["Remove source"], session.Steps);
        Assert.Contains("Town", Assert.Single(session.Dialogs.Messages));

        session.UndoRedo.Undo();
        Assert.Single(asset.Sources);
        Assert.Equal(TileCollision.Solid, Assert.Single(asset.Tiles).Collision);
        Assert.Single(editor.Sources);
    }

    [Fact]
    public async Task Remove_answered_no_changes_nothing()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        session.Dialogs.Answers.Enqueue(MessageBoxResult.No);

        editor.RemoveSourceCommand.Execute();

        Assert.Single(TileSet(session).Sources);
        Assert.Single(editor.Sources);
        Assert.Empty(session.Steps);
        Assert.Single(session.Dialogs.Messages);
    }

    [Fact]
    public async Task The_property_grid_shows_the_selected_source_then_the_asset()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");

        editor.SelectedSource = editor.Sources[0];
        await ExclusiveDispatcherService.WaitUntilAsync(() => Members(editor).Contains(nameof(GridTileSetSource.Image)), "the source in the property grid");
        Assert.Equal(
            [nameof(GridTileSetSource.Image), nameof(GridTileSetSource.Margin), nameof(TileSetSource.Name), nameof(GridTileSetSource.Spacing), nameof(GridTileSetSource.TileSize)],
            Members(editor).Order());

        editor.SelectedSource = null;
        await ExclusiveDispatcherService.WaitUntilAsync(() => Members(editor).Contains(nameof(TileSetAsset.PixelsPerUnit)), "the asset in the property grid");
        Assert.DoesNotContain(nameof(GridTileSetSource.Image), Members(editor));
    }

    // The property grid names a member "Name" as "Name_"
    private static List<string> Members(TileSetEditorViewModel editor)
        => editor.EditorProperties.ViewModel?.RootNode.Children.Select(child => child.Name.TrimEnd('_')).ToList() ?? [];

    private static List<string> Bindings(SpriteSheetTileSetSource source) => [.. source.Bindings.Select(binding => $"{binding.Number} {binding.SpriteName}")];

    private static TileSetAsset TileSet(TesseraSession session) => session.TileSetAt("Tiles/Town").Asset;

    private static SpriteSheetTileSetSource Sheet(TesseraSession session) => (SpriteSheetTileSetSource)TileSet(session).Sources[1];

    private static SpriteSheetViewModel Objects(TesseraSession session) => session.Kit.Asset<SpriteSheetViewModel>("Tiles/Objects");

    private static IObjectNode Sprites(TesseraSession session) => Objects(session).PropertyGraph.RootNode[nameof(SpriteSheetAsset.Sprites)].Target!;

    // "cave.png" beside the tile set: 64 by 32 grey pixels, four by two tiles
    private static UFile Cave(TesseraSession session) => session.WriteImage("Tiles/cave.png", 64, 32, (_, _) => Color.Gray);

    // The town set with its image; with the sheet "Tiles/Objects", and a sheet source over it with the sprites given bound;
    // with a map "Maps/Town"
    private static DispatcherTask<TesseraSession> OpenTownAsync(bool objects = false, string[]? bound = null, bool map = false)
    {
        var tileSet = TestTown.TileSet();
        var assets = new List<(string Location, Stride.Core.Assets.Asset Asset)>();
        if (objects)
        {
            var sheet = TestTown.Objects();
            assets.Add(("Tiles/Objects", sheet));
            if (bound is not null)
            {
                var source = tileSet.AddSource<SpriteSheetTileSetSource>("Objects");
                source.Sheet = AttachedReferenceManager.CreateProxyObject<SpriteSheet>(sheet.Id, "Tiles/Objects");
                foreach (var name in bound)
                {
                    source.Bindings.Add(new SpriteTileBinding { Number = ++source.LastNumber, SpriteName = name });
                }
            }
        }
        if (map)
        {
            assets.Add(("Maps/Town", new TileMapAsset()));
        }
        return OpenTownAsync(tileSet, [.. assets]);
    }

    private static async DispatcherTask<TesseraSession> OpenTownAsync(TileSetAsset tileSet, params (string Location, Stride.Core.Assets.Asset Asset)[] others)
    {
        var session = await TesseraSession.OpenAsync([("Tiles/Town", tileSet), .. others]);
        session.WriteImage("Tiles/town.png", 64, 32, TestTown.Pixel);
        return session;
    }
}
