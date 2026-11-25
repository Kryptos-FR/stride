using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Canvas;
using ColorRise.Tessera.Editor.TileSetEditor;
using Stride.Assets.Presentation.ViewModels;
using Stride.Assets.Sprite;
using Stride.Core.Assets.Editor.Quantum.NodePresenters.Commands;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.Quantum.ViewModels;
using Stride.Core.Presentation.Services;
using Stride.Core.Quantum;
using Stride.Core.Serialization;
using Stride.Graphics;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// The tile set editor and the tiles of its tile set: their data in the property grid, the solid mode, and the rebinding of
/// a missing sprite.
/// </summary>
public sealed class TileSetEditorTilesTests
{
    // The spike of the task: the grid edits a string, a dictionary of abstract values and a flags enum of an object that is
    // not in the asset
    [Fact]
    public async Task The_property_grid_edits_the_data_of_a_tile()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        Click(editor, "1:1,0");
        await GridOfTilesAsync(editor, "1:1,0");

        Child(editor, "Kind").NodeValue = "door";
        await StepsAsync(session, 1);
        await Command(Child(editor, "Properties"), AddPrimitiveKeyCommand.CommandName).Invoke("locked");
        var locked = Assert.Single(Child(editor, "Properties").Children);
        await Command(locked, CreateNewInstanceCommand.CommandName).Invoke(new AbstractNodeType(typeof(TileBoolProperty)));
        await StepsAsync(session, 2);
        Child(editor, "Collision").NodeValue = TileCollision.Solid;
        await StepsAsync(session, 3);

        var definition = Assert.Single(TileSet(session).Tiles);
        Assert.Equal(TileId.Parse("1:1,0"), definition.Tile);
        Assert.Equal("door", definition.Kind);
        Assert.Equal(TileCollision.Solid, definition.Collision);
        var property = Assert.Single(definition.Properties);
        Assert.Equal("locked", property.Key);
        Assert.IsType<TileBoolProperty>(property.Value);
        Assert.Equal(["Tile data", "Tile data", "Tile data"], session.Steps);
    }

    [Fact]
    public async Task A_click_selects_a_tile_and_control_adds_one()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");

        Click(editor, "1:1,0");
        Assert.Equal(Tiles("1:1,0"), editor.SelectedTiles);

        Click(editor, "1:2,1", CanvasModifiers.Control);
        Assert.Equal(Tiles("1:1,0", "1:2,1"), editor.SelectedTiles);
        await GridOfTilesAsync(editor, "1:1,0", "1:2,1");
        Assert.Empty(session.Steps);
    }

    [Fact]
    public async Task The_property_grid_shows_several_tiles_at_once()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        Click(editor, "1:1,0");
        Click(editor, "1:2,1", CanvasModifiers.Control);
        await GridOfTilesAsync(editor, "1:1,0", "1:2,1");

        Child(editor, "Kind").NodeValue = "water";
        await StepsAsync(session, 1);

        Assert.Equal(Tiles("1:1,0", "1:2,1"), TileSet(session).Tiles.Select(definition => definition.Tile));
        Assert.All(TileSet(session).Tiles, definition => Assert.Equal("water", definition.Kind));
        Assert.Equal(["Tile data"], session.Steps);
    }

    [Fact]
    public async Task Data_set_back_to_nothing_removes_the_definition()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        Click(editor, "1:1,0");
        await GridOfTilesAsync(editor, "1:1,0");

        Child(editor, "Kind").NodeValue = "door";
        await StepsAsync(session, 1);
        Assert.Single(TileSet(session).Tiles);
        Child(editor, "Kind").NodeValue = "";
        await StepsAsync(session, 2);

        Assert.Empty(TileSet(session).Tiles);
        Assert.Equal(["Tile data", "Tile data"], session.Steps);
    }

    [Fact]
    public async Task Definitions_stay_sorted_by_tile()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");

        Click(editor, "1:2,1");
        await GridOfTilesAsync(editor, "1:2,1");
        Child(editor, "Kind").NodeValue = "grass";
        await StepsAsync(session, 1);
        Click(editor, "1:0,0");
        await GridOfTilesAsync(editor, "1:0,0");
        Child(editor, "Kind").NodeValue = "path";
        await StepsAsync(session, 2);

        Assert.Equal(Tiles("1:0,0", "1:2,1"), TileSet(session).Tiles.Select(definition => definition.Tile));
    }

    [Fact]
    public async Task The_grid_follows_an_undo()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        Click(editor, "1:1,0");
        await GridOfTilesAsync(editor, "1:1,0");
        Child(editor, "Kind").NodeValue = "door";
        await StepsAsync(session, 1);

        session.UndoRedo.Undo();

        Assert.Empty(TileSet(session).Tiles);
        await ExclusiveDispatcherService.WaitUntilAsync(() => Shows(editor, "Kind", value => string.IsNullOrEmpty((string?)value)), "the grid to show no kind");
        // The grid edits the tile again from what the asset has
        Child(editor, "Collision").NodeValue = TileCollision.Solid;
        await StepsAsync(session, 2);
        var definition = Assert.Single(TileSet(session).Tiles);
        Assert.Null(definition.Kind);
        Assert.Equal(TileCollision.Solid, definition.Collision);
    }

    [Fact]
    public async Task A_solid_drag_is_one_step_and_takes_the_value_of_its_first_tile()
    {
        await using var session = await OpenTownAsync(Solid("1:2,0"));
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.Mode = TileSetEditorMode.Solid;

        Drag(editor, "1:0,0", "1:1,0", "1:2,0");

        Assert.Equal(Tiles("1:0,0", "1:1,0", "1:2,0"), SolidTiles(session));
        Assert.Equal(["Solid"], session.Steps);

        Drag(editor, "1:0,0", "1:1,0", "1:2,0");

        Assert.Empty(SolidTiles(session));
        Assert.Empty(TileSet(session).Tiles);
        Assert.Equal(["Solid", "Solid"], session.Steps);
        Assert.Empty(editor.SelectedTiles);
    }

    // The pictures are made again when the image changes on disk, which may come in a drag
    [Fact]
    public async Task A_solid_drag_goes_on_when_the_pictures_are_made_again()
    {
        await using var session = await OpenTownAsync(Solid("1:2,0"));
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.Mode = TileSetEditorMode.Solid;

        editor.Scene.PointerPressed(Middle(editor, "1:0,0"), CanvasButton.Left, CanvasModifiers.None);
        editor.Pictures.Refresh();
        editor.Scene.PointerMoved(Middle(editor, "1:1,0"), CanvasModifiers.None);
        editor.Scene.PointerReleased(Middle(editor, "1:1,0"), CanvasButton.Left, CanvasModifiers.None);

        Assert.Equal(Tiles("1:0,0", "1:1,0", "1:2,0"), SolidTiles(session));
        Assert.Equal(["Solid"], session.Steps);
    }

    [Fact]
    public async Task A_solid_drag_that_changes_nothing_makes_no_step()
    {
        await using var session = await OpenTownAsync(Solid("1:2,0"));
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.Mode = TileSetEditorMode.Solid;

        // Outside the image: no tile is under the pointer
        editor.Scene.PointerPressed(new Vector2(80, 40), CanvasButton.Left, CanvasModifiers.None);
        editor.Scene.PointerMoved(new Vector2(90, 50), CanvasModifiers.None);
        editor.Scene.PointerReleased(new Vector2(90, 50), CanvasButton.Left, CanvasModifiers.None);

        Assert.Equal(Tiles("1:2,0"), SolidTiles(session));
        Assert.Empty(session.Steps);
    }

    [Fact]
    public async Task Solid_keeps_the_other_data_of_a_tile()
    {
        var tileSet = TestTown.TileSet();
        tileSet.Tiles.Add(new TileDefinition { Tile = TileId.Parse("1:1,0"), Kind = "door" });
        await using var session = await OpenTownAsync(tileSet);
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.Mode = TileSetEditorMode.Solid;

        Click(editor, "1:1,0");
        var definition = Assert.Single(TileSet(session).Tiles);
        Assert.Equal((TileCollision.Solid, "door"), (definition.Collision, definition.Kind));

        Click(editor, "1:1,0");
        definition = Assert.Single(TileSet(session).Tiles);
        Assert.Equal((TileCollision.None, "door"), (definition.Collision, definition.Kind));
        Assert.Equal(["Solid", "Solid"], session.Steps);
    }

    [Fact]
    public async Task Solid_tiles_are_hatched()
    {
        await using var session = await OpenTownAsync(Solid("1:2,0"));
        var editor = await session.EditTileSetAsync("Tiles/Town");
        var bounds = (RectangleF)editor.Scene.Layout!.SlotOf(TileId.Parse("1:2,0"))!.Bounds;

        foreach (var mode in new[] { TileSetEditorMode.Select, TileSetEditorMode.Solid })
        {
            editor.Mode = mode;
            var frame = new CanvasFrame();
            editor.Scene.Collect(new RectangleF(0, 0, 64, 32), 1, frame);
            var hatch = Assert.Single(frame.Shapes, shape => shape.Mark == CanvasMark.Hatch);
            Assert.Equal(bounds, hatch.Bounds);
        }
    }

    // The marks come from the editor's look-up of the definitions, which must follow the asset
    [Fact]
    public async Task The_solid_marks_follow_a_change_made_outside_the_editor()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        var bounds = (RectangleF)editor.Scene.Layout!.SlotOf(TileId.Parse("1:2,0"))!.Bounds;

        using (var transaction = session.UndoRedo.CreateTransaction())
        {
            TileSetGraph.SetSolid(session.TileSetAt("Tiles/Town"), TileId.Parse("1:2,0"), true);
            session.UndoRedo.SetName(transaction, "Solid");
        }
        var hatch = Assert.Single(Collect(editor).Shapes, shape => shape.Mark == CanvasMark.Hatch);
        Assert.Equal(bounds, hatch.Bounds);

        session.UndoRedo.Undo();
        Assert.DoesNotContain(Collect(editor).Shapes, shape => shape.Mark == CanvasMark.Hatch);
    }

    [Fact]
    public async Task A_missing_tile_is_rebound_and_the_other_tile_goes_away()
    {
        await using var session = await OpenRenamedAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        await RenameWaterAsync(session, editor);
        editor.SelectedSource = editor.Sources[1];

        Click(editor, "2:#2");

        Assert.Equal(Tiles("2:#2"), editor.SelectedTiles);
        Assert.Equal("water", editor.MissingSpriteName);
        Assert.Contains("sea", editor.RebindCandidates);
        Assert.True(editor.RebindSpriteCommand.IsEnabled);
        session.Dialogs.Answers.Enqueue(MessageBoxResult.Yes);
        editor.RebindSpriteCommand.Execute("sea");

        Assert.Equal("Give the sprite 'sea' to the tile #2? The tile #4, which has it now, goes away: cells of maps that use #4 will show as missing.", Assert.Single(session.Dialogs.Messages));
        Assert.Equal(["1 grass", "2 sea", "3 wall"], Bindings(Sheet(session)));
        Assert.Equal(4, Sheet(session).LastNumber);
        Assert.DoesNotContain(TileSet(session).Tiles, definition => definition.Tile == TileId.Parse("2:#4"));
        Assert.Equal("Rebind sprite", session.Steps[^1]);
        await ExclusiveDispatcherService.WaitUntilAsync(() => editor.Sources[1].Layout?.SlotOf(TileId.Parse("2:#2")) is { IsMissing: false }, "the picture of the rebound tile");
    }

    [Fact]
    public async Task Rebind_answered_no_changes_nothing()
    {
        await using var session = await OpenRenamedAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        await RenameWaterAsync(session, editor);
        editor.SelectedSource = editor.Sources[1];
        Click(editor, "2:#2");
        var steps = session.Steps.ToList();

        session.Dialogs.Answers.Enqueue(MessageBoxResult.No);
        editor.RebindSpriteCommand.Execute("sea");

        Assert.Single(session.Dialogs.Messages);
        Assert.Equal(["1 grass", "2 water", "3 wall", "4 sea"], Bindings(Sheet(session)));
        Assert.Contains(TileSet(session).Tiles, definition => definition.Tile == TileId.Parse("2:#4"));
        Assert.Equal(steps, session.Steps);
    }

    [Fact]
    public async Task A_tile_that_is_not_missing_has_no_rebind()
    {
        await using var session = await OpenRenamedAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        await RenameWaterAsync(session, editor);

        Click(editor, "1:1,0");
        Assert.Empty(editor.RebindCandidates);
        Assert.False(editor.RebindSpriteCommand.IsEnabled);

        editor.SelectedSource = editor.Sources[1];
        Click(editor, "2:#4");
        Assert.Empty(editor.RebindCandidates);
        Assert.False(editor.RebindSpriteCommand.IsEnabled);
        Assert.Null(editor.MissingSpriteName);
    }

    // Review Focus 5
    [Fact]
    public async Task An_undo_that_removes_the_selected_tiles_clears_the_selection()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.ProposeImage(session.WriteImage("Tiles/cave.png", 64, 32, (_, _) => Color.Gray));
        editor.PendingImage!.ConfirmCommand.Execute();
        await ExclusiveDispatcherService.WaitUntilAsync(() => editor.Scene.Layout?.Source.Id == 2, "the pictures of the new source");
        Click(editor, "2:0,0");
        Click(editor, "2:1,0", CanvasModifiers.Control);
        await GridOfTilesAsync(editor, "2:0,0", "2:1,0");

        session.UndoRedo.Undo();

        Assert.Empty(editor.SelectedTiles);
        Assert.Same(editor.Sources[0], editor.SelectedSource);
        // With no tile selected, the grid shows the selected source again
        await ExclusiveDispatcherService.WaitUntilAsync(() => Members(editor).Contains(nameof(GridTileSetSource.Image)), "the source in the property grid");
        Assert.DoesNotContain(nameof(TileDefinition.Kind), Members(editor));
    }

    [Fact]
    public async Task A_new_grid_keeps_only_the_tiles_it_still_has()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        Click(editor, "1:1,0");
        Click(editor, "1:3,0", CanvasModifiers.Control);
        await GridOfTilesAsync(editor, "1:1,0", "1:3,0");

        using (var transaction = session.UndoRedo.CreateTransaction())
        {
            session.TileSetAt("Tiles/Town").PropertyGraph.RootNode[nameof(TileSetAsset.TileSize)].Update(new Int2(32, 32));
            session.UndoRedo.SetName(transaction, "Update property TileSize");
        }

        await ExclusiveDispatcherService.WaitUntilAsync(() => editor.Scene.Layout?.Slots.Count == 2, "the pictures of the new grid");
        Assert.Equal(Tiles("1:1,0"), editor.SelectedTiles);
        await GridOfTilesAsync(editor, "1:1,0");
    }

    [Fact]
    public async Task An_undo_in_the_middle_of_a_solid_drag_ends_the_drag()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.Mode = TileSetEditorMode.Solid;
        Click(editor, "1:0,0");
        Assert.Equal(["Solid"], session.Steps);

        editor.Scene.PointerPressed(Middle(editor, "1:1,0"), CanvasButton.Left, CanvasModifiers.None);
        editor.Scene.PointerMoved(Middle(editor, "1:2,0"), CanvasModifiers.None);
        session.UndoRedo.Undo();
        editor.Scene.PointerMoved(Middle(editor, "1:3,0"), CanvasModifiers.None);
        editor.Scene.PointerReleased(Middle(editor, "1:3,0"), CanvasButton.Left, CanvasModifiers.None);

        Assert.Empty(SolidTiles(session));
        Assert.Equal(["Solid"], session.Steps);
    }

    [Fact]
    public async Task A_lost_pointer_drops_the_solid_drag()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.Mode = TileSetEditorMode.Solid;

        editor.Scene.PointerPressed(Middle(editor, "1:1,0"), CanvasButton.Left, CanvasModifiers.None);
        editor.Scene.PointerMoved(Middle(editor, "1:2,0"), CanvasModifiers.None);
        editor.Scene.PointerCaptureLost();
        editor.Scene.PointerMoved(Middle(editor, "1:3,0"), CanvasModifiers.None);
        editor.Scene.PointerReleased(Middle(editor, "1:3,0"), CanvasButton.Left, CanvasModifiers.None);
        Assert.Empty(SolidTiles(session));
        Assert.Empty(session.Steps);

        Click(editor, "1:0,0");
        Assert.Equal(Tiles("1:0,0"), SolidTiles(session));
        Assert.Equal(["Solid"], session.Steps);
    }

    private static List<TileId> Tiles(params string[] tiles) => [.. tiles.Select(TileId.Parse)];

    private static CanvasFrame Collect(TileSetEditorViewModel editor)
    {
        var frame = new CanvasFrame();
        editor.Scene.Collect(new RectangleF(0, 0, 64, 32), 1, frame);
        return frame;
    }

    private static List<TileId> SolidTiles(TesseraSession session)
        => [.. TileSet(session).Tiles.Where(definition => (definition.Collision & TileCollision.Solid) != 0).Select(definition => definition.Tile)];

    private static Vector2 Middle(TileSetEditorViewModel editor, string tile)
    {
        var bounds = editor.Scene.Layout!.SlotOf(TileId.Parse(tile))!.Bounds;
        return new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
    }

    private static void Click(TileSetEditorViewModel editor, string tile, CanvasModifiers modifiers = CanvasModifiers.None)
    {
        var at = Middle(editor, tile);
        editor.Scene.PointerPressed(at, CanvasButton.Left, modifiers);
        editor.Scene.PointerReleased(at, CanvasButton.Left, modifiers);
    }

    // A press on the first tile, a move over each of the others, and a release on the last
    private static void Drag(TileSetEditorViewModel editor, params string[] tiles)
    {
        editor.Scene.PointerPressed(Middle(editor, tiles[0]), CanvasButton.Left, CanvasModifiers.None);
        foreach (var tile in tiles.Skip(1))
        {
            editor.Scene.PointerMoved(Middle(editor, tile), CanvasModifiers.None);
        }
        editor.Scene.PointerReleased(Middle(editor, tiles[^1]), CanvasButton.Left, CanvasModifiers.None);
    }

    // Waits for the grid to show the data of these tiles
    private static Task GridOfTilesAsync(TileSetEditorViewModel editor, params string[] tiles)
    {
        var expected = Tiles(tiles);
        return ExclusiveDispatcherService.WaitUntilAsync(
            () => editor.EditorProperties.ViewModel is not null
                  && editor.EditorProperties.Selection?.OfType<TileDataViewModel>().Select(data => data.Tile).SequenceEqual(expected) == true
                  && Members(editor).Contains(nameof(TileDefinition.Kind)),
            "the data of the tiles in the property grid");
    }

    private static Task StepsAsync(TesseraSession session, int count)
        => ExclusiveDispatcherService.WaitUntilAsync(() => session.Steps.Count >= count, $"{count} steps in the history");

    private static bool Shows(TileSetEditorViewModel editor, string member, Func<object?, bool> value)
        => editor.EditorProperties.ViewModel?.RootNode.Children.FirstOrDefault(child => child.Name == member) is { } node && value(node.NodeValue);

    // The property grid names a member "Name" as "Name_"
    private static List<string> Members(TileSetEditorViewModel editor)
        => editor.EditorProperties.ViewModel?.RootNode.Children.Select(child => child.Name.TrimEnd('_')).ToList() ?? [];

    private static NodeViewModel Child(TileSetEditorViewModel editor, string member)
        => editor.EditorProperties.ViewModel!.RootNode.Children.Single(child => child.Name == member);

    private static NodePresenterCommandWrapper Command(NodeViewModel node, string name)
        => Assert.IsAssignableFrom<NodePresenterCommandWrapper>(node.GetCommand(name));

    private static List<string> Bindings(SpriteSheetTileSetSource source) => [.. source.Bindings.Select(binding => $"{binding.Number} {binding.SpriteName}")];

    private static TileSetAsset TileSet(TesseraSession session) => session.TileSetAt("Tiles/Town").Asset;

    private static SpriteSheetTileSetSource Sheet(TesseraSession session) => (SpriteSheetTileSetSource)TileSet(session).Sources[1];

    private static TileSetAsset Solid(params string[] tiles)
    {
        var tileSet = TestTown.TileSet();
        foreach (var tile in tiles)
        {
            tileSet.Tiles.Add(new TileDefinition { Tile = TileId.Parse(tile), Collision = TileCollision.Solid });
        }
        return tileSet;
    }

    // "water", bound to 2:#2, is renamed "sea" in its sheet: 2:#2 is missing, and "sea" is bound to 2:#4, which has a kind
    private static async Task RenameWaterAsync(TesseraSession session, TileSetEditorViewModel editor)
    {
        var sheet = session.Kit.Asset<SpriteSheetViewModel>("Tiles/Objects");
        using (var transaction = session.UndoRedo.CreateTransaction())
        {
            sheet.PropertyGraph.RootNode[nameof(SpriteSheetAsset.Sprites)].Target!.IndexedTarget(new NodeIndex(1))![nameof(SpriteInfo.Name)].Update("sea");
            session.UndoRedo.SetName(transaction, "Rename sprite");
        }
        await ExclusiveDispatcherService.WaitUntilAsync(() => Sheet(session).Bindings.Count == 4, "the renamed sprite to be bound");
        await ExclusiveDispatcherService.WaitUntilAsync(() => editor.Sources[1].Layout?.Slots.Count == 4, "the pictures of the bound sprite");
        using (var transaction = session.UndoRedo.CreateTransaction())
        {
            var tiles = session.TileSetAt("Tiles/Town").PropertyGraph.RootNode[nameof(TileSetAsset.Tiles)].Target!;
            tiles.Add(new TileDefinition { Tile = TileId.Parse("2:#4"), Kind = "sea" });
            session.UndoRedo.SetName(transaction, "Tile data");
        }
    }

    // The town set, and the sheet "Tiles/Objects" in a source of id 2 with "grass", "water" and "wall" bound to 1, 2 and 3
    private static async DispatcherTask<TesseraSession> OpenRenamedAsync()
    {
        var tileSet = TestTown.TileSet();
        var sheet = TestTown.Objects();
        var source = tileSet.AddSource<SpriteSheetTileSetSource>("Objects");
        source.Sheet = AttachedReferenceManager.CreateProxyObject<SpriteSheet>(sheet.Id, "Tiles/Objects");
        foreach (var name in new[] { "grass", "water", "wall" })
        {
            source.Bindings.Add(new SpriteTileBinding { Number = ++source.LastNumber, SpriteName = name });
        }
        return await OpenTownAsync(tileSet, ("Tiles/Objects", sheet));
    }

    private static async DispatcherTask<TesseraSession> OpenTownAsync(TileSetAsset? tileSet = null, params (string Location, Stride.Core.Assets.Asset Asset)[] others)
    {
        var session = await TesseraSession.OpenAsync([("Tiles/Town", tileSet ?? TestTown.TileSet()), .. others]);
        session.WriteImage("Tiles/town.png", 64, 32, TestTown.Pixel);
        return session;
    }
}
