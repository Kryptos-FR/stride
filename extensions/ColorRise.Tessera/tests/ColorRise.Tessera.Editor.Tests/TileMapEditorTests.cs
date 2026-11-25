using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Canvas;
using ColorRise.Tessera.Editor.Palette;
using ColorRise.Tessera.Editor.TileMapEditor;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.Quantum;
using Stride.Core.Presentation.Quantum.ViewModels;
using Stride.Core.Presentation.Services;
using Stride.Core.Quantum;
using Stride.Core.Serialization;
using static ColorRise.Tessera.Editor.Tests.TestTown;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// The map editor around its canvas: its layers, its tile sets and their palettes, its settings, and its status line.
/// </summary>
public sealed class TileMapEditorTests
{
    private const string Location = "Maps/Town";
    private const string CaveLocation = "Tiles/Cave";

    [Fact]
    public async Task The_layers_are_listed_top_first_and_the_top_one_is_current()
    {
        await using var map = await OpenAsync([["."], ["."], ["."]], Names("Ground", "Details", "Roofs"));

        Assert.Equal(["Roofs", "Details", "Ground"], map.Editor.Layers.Select(layer => layer.Name));
        Assert.Equal(2, map.Editor.CurrentLayer);
        Assert.Same(map.Editor.Layers[0], map.Editor.SelectedLayer);

        map.Editor.SelectedLayer = map.Editor.Layers[2];
        Assert.Equal(0, map.Editor.CurrentLayer);
        map.Editor.CurrentLayer = 1;
        Assert.Same(map.Editor.Layers[1], map.Editor.SelectedLayer);
    }

    [Fact]
    public async Task Add_puts_a_layer_over_the_current_one_with_a_name_no_layer_has()
    {
        await using var map = await OpenAsync([["."], ["."]], Names("Ground", "Layer 1"));
        map.Editor.CurrentLayer = 0;

        map.Editor.AddLayerCommand.Execute();

        Assert.Equal(["Ground", "Layer 2", "Layer 1"], LayerNames(map));
        Assert.Equal(1, map.Editor.CurrentLayer);
        Assert.Equal("Layer 2", map.Editor.SelectedLayer!.Name);
        Assert.Equal(["Add layer"], map.Session.Steps);

        map.Session.UndoRedo.Undo();
        Assert.Equal(["Ground", "Layer 1"], LayerNames(map));
        Assert.Equal(["Layer 1", "Ground"], map.Editor.Layers.Select(layer => layer.Name));
    }

    [Fact]
    public async Task Remove_asks_when_the_layer_has_tiles()
    {
        await using var map = await OpenAsync([["."], ["0,0 1,0 2,0"]], Names("Ground", "Details"));

        map.Session.Dialogs.Answers.Enqueue(MessageBoxResult.No);
        map.Editor.RemoveLayerCommand.Execute();
        Assert.Equal("Remove the layer 'Details' and its 3 tiles?", Assert.Single(map.Session.Dialogs.Messages));
        Assert.Equal(["Ground", "Details"], LayerNames(map));
        Assert.Empty(map.Session.Steps);

        map.Session.Dialogs.Answers.Enqueue(MessageBoxResult.Yes);
        map.Editor.RemoveLayerCommand.Execute();
        Assert.Equal(["Ground"], LayerNames(map));
        Assert.Equal(["Remove layer"], map.Session.Steps);
        Assert.Equal(0, map.Editor.CurrentLayer);

        // A layer without a tile goes without a question
        map.Editor.RemoveLayerCommand.Execute();
        Assert.Empty(LayerNames(map));
        Assert.Equal(2, map.Session.Dialogs.Messages.Count);
        Assert.Equal(-1, map.Editor.CurrentLayer);
        Assert.Null(map.Editor.SelectedLayer);
    }

    [Fact]
    public async Task Up_and_down_move_the_layer_and_keep_it_current()
    {
        await using var map = await OpenAsync([["0,0"], ["1,0"], ["."]], Names("Ground", "Details", "Roofs"));
        map.Editor.CurrentLayer = 0;

        map.Editor.MoveLayerUpCommand.Execute();

        Assert.Equal(["Details", "Ground", "Roofs"], LayerNames(map));
        Assert.Equal(1, map.Editor.CurrentLayer);
        Assert.Equal("Ground", map.Editor.SelectedLayer!.Name);
        Assert.Equal(["Move layer"], map.Session.Steps);
        // Details is drawn first, and Ground over it
        var sprites = Collect(map.Editor.Scene).Sprites;
        var middle = map.Editor.Scene.CellBounds(Int2.Zero).Center;
        Assert.Equal(2, sprites.Count);
        Assert.Equal(Flat(1, 0), PixelAt(sprites[0], middle));
        Assert.Equal(Color.Yellow, PixelAt(sprites[1], middle));

        map.Editor.MoveLayerDownCommand.Execute();
        Assert.Equal(["Ground", "Details", "Roofs"], LayerNames(map));
        Assert.Equal(0, map.Editor.CurrentLayer);

        // The bottom layer goes no lower
        map.Editor.MoveLayerDownCommand.Execute();
        Assert.Equal(["Move layer", "Move layer"], map.Session.Steps);
    }

    [Fact]
    public async Task A_rename_is_one_step_and_a_name_in_use_or_empty_is_refused()
    {
        await using var map = await OpenAsync([["."], ["."]], Names("Ground", "Details"));
        var details = map.Editor.Layers[0];

        details.Name = "Trees";
        Assert.Equal(["Ground", "Trees"], LayerNames(map));
        Assert.Equal(["Rename layer"], map.Session.Steps);

        details.Name = "Ground";
        Assert.Equal("Trees", details.Name);
        Assert.Equal("Another layer is named 'Ground'.", map.Editor.Message);
        details.Name = " ";
        Assert.Equal("Trees", details.Name);
        Assert.Equal("A layer needs a name.", map.Editor.Message);
        Assert.Equal(["Rename layer"], map.Session.Steps);

        map.Session.UndoRedo.Undo();
        Assert.Equal("Details", details.Name);
    }

    [Fact]
    public async Task The_eye_is_the_asset_s_visible_and_an_undo_step()
    {
        await using var map = await OpenAsync([["0,0"]]);
        var layer = map.Editor.Layers[0];

        layer.IsVisible = false;

        Assert.False(MapAsset(map).Layers[0].Visible);
        Assert.Equal(["Layer visibility"], map.Session.Steps);
        Assert.Empty(Collect(map.Editor.Scene).Sprites);

        map.Session.UndoRedo.Undo();
        Assert.True(layer.IsVisible);
        Assert.Single(Collect(map.Editor.Scene).Sprites);
    }

    [Fact]
    public async Task The_lock_is_not_in_the_asset_and_comes_back_when_the_editor_opens_again()
    {
        await using var map = await OpenAsync([["."], ["."]], Names("Ground", "Details"));

        map.Editor.Layers[1].IsLocked = true;

        Assert.True(map.Editor.Layers[1].IsLocked);
        Assert.False(map.Session.MapAt(Location).IsDirty);
        Assert.Empty(map.Session.Steps);
        var second = await map.Session.EditMapAsync(Location);
        Assert.True(second.Layers[1].IsLocked);
        Assert.False(second.Layers[0].IsLocked);

        // A rename keeps the lock, and a layer removed leaves nothing behind
        map.Editor.Layers[1].Name = "Floor";
        Assert.True(map.Editor.Layers[1].IsLocked);
        Assert.True(second.Layers[1].IsLocked);
        map.Editor.CurrentLayer = 0;
        map.Editor.RemoveLayerCommand.Execute();
        var id = map.Session.MapAt(Location).Id.ToString();
        Assert.DoesNotContain(TesseraEditorSettings.LockedLayers.GetValue(), key => key.StartsWith(id, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_locked_layer_takes_no_stroke()
    {
        await using var map = await OpenAsync([["."]], Names("Ground"));
        map.Editor.Stamp = TileStamp.Single(Cell(1, 0));

        map.Editor.Layers[0].IsLocked = true;
        Click(map.Editor, 0, 0);

        Assert.Equal("The layer 'Ground' is locked.", map.Editor.Message);
        Assert.Equal("Ground (locked)", map.Editor.StatusLayer);
        Assert.Empty(map.Session.Steps);
    }

    [Fact]
    public async Task The_property_grid_shows_the_selected_layer_then_the_map()
    {
        await using var map = await OpenAsync([["."]]);

        await ExclusiveDispatcherService.WaitUntilAsync(() => Members(map.Editor).Contains(nameof(TileLayerAsset.Opacity)), "the layer in the property grid");
        Assert.Equal(
            [nameof(TileLayerAsset.Collision), nameof(TileLayerAsset.Name), nameof(TileLayerAsset.Offset), nameof(TileLayerAsset.Opacity), nameof(TileLayerAsset.Tint), nameof(TileLayerAsset.Visible)],
            Members(map.Editor).Order());

        map.Editor.SelectedLayer = null;
        await ExclusiveDispatcherService.WaitUntilAsync(() => Members(map.Editor).Contains(nameof(TileMapAsset.CellSize)), "the map in the property grid");
        Assert.DoesNotContain(nameof(TileLayerAsset.Opacity), Members(map.Editor));
        Assert.Equal(0, map.Editor.CurrentLayer);
    }

    [Fact]
    public async Task The_property_grid_has_no_node_for_the_chunks()
    {
        await using var map = await OpenAsync([["0,0"]]);
        var asset = map.Session.MapAt(Location);
        var layer = asset.PropertyGraph.RootNode[nameof(TileMapAsset.Layers)].Target!.IndexedTarget(new NodeIndex(0))!;

        // The grid is built on another thread, and a node of the chunks would be rebuilt by every stroke
        Assert.False(((IPropertyProviderViewModel)asset).ShouldConstructMember(layer[nameof(TileLayerAsset.Chunks)]));
        Assert.True(((IPropertyProviderViewModel)asset).ShouldConstructMember(layer[nameof(TileLayerAsset.Opacity)]));
    }

    [Fact]
    public async Task An_opacity_set_in_the_property_grid_shows_on_the_canvas()
    {
        await using var map = await OpenAsync([["0,0"]]);
        await ExclusiveDispatcherService.WaitUntilAsync(() => Members(map.Editor).Contains(nameof(TileLayerAsset.Opacity)), "the layer in the property grid");

        Child(map.Editor, nameof(TileLayerAsset.Opacity)).NodeValue = 0.5f;
        await ExclusiveDispatcherService.WaitUntilAsync(() => map.Session.Steps.Count > 0, "the change in the history");

        Assert.Equal(0.5f, Assert.Single(Collect(map.Editor.Scene).Sprites).Opacity);
    }

    [Fact]
    public async Task A_tile_set_is_added_in_a_new_slot()
    {
        await using var map = await OpenWithCaveAsync([["."]]);
        map.Session.Dialogs.PickAsset = _ => map.Session.TileSetAt(CaveLocation);

        map.Editor.AddTileSetCommand.Execute();

        Assert.Equal([1, 2], MapAsset(map).TileSets.Select(slot => slot.Slot));
        Assert.Equal(2, MapAsset(map).LastSlot);
        Assert.Equal(["Add tile set"], map.Session.Steps);
        Assert.Equal([(ushort)1, (ushort)2], map.Editor.TileSets.Select(slot => slot.Slot));
        Assert.Equal("Cave", map.Editor.TileSets[1].Name);
        Assert.NotNull(map.Editor.TileSets[1].Palette);
        Assert.Same(map.Editor.TileSets[1], map.Editor.SelectedTileSet);
    }

    [Fact]
    public async Task A_tile_set_the_map_has_is_refused()
    {
        await using var map = await OpenAsync([["."]]);

        Assert.False(map.Editor.AddTileSet(map.Session.TileSetAt("Tiles/Town")));

        Assert.Equal("The map already has the tile set 'Town'.", map.Editor.Message);
        Assert.Empty(map.Session.Steps);
    }

    [Fact]
    public async Task Removing_a_tile_set_in_use_asks_with_the_count_and_keeps_the_cells()
    {
        await using var map = await OpenAsync([["0,0 1,0", "2,0 ."]]);
        map.Session.Dialogs.Answers.Enqueue(MessageBoxResult.Yes);

        map.Editor.RemoveTileSetCommand.Execute();

        Assert.Equal(
            "Remove the tile set 'Town'? 3 cells use it. They stay in the map and show as missing. This cannot be put right by adding the tile set again; use Undo.",
            Assert.Single(map.Session.Dialogs.Messages));
        Assert.Empty(MapAsset(map).TileSets);
        Assert.Empty(map.Editor.TileSets);
        Assert.Equal(["Remove tile set"], map.Session.Steps);
        Assert.Equal(3, map.Editor.Document.CountCells(1));
        var sprite = Assert.Single(Collect(map.Editor.Scene).Sprites);
        Assert.Equal(TileComposer.MissingColor, PixelAt(sprite, map.Editor.Scene.CellBounds(Int2.Zero).Center + new Vector2(-6, 0)));
    }

    [Fact]
    public async Task A_slot_removed_and_the_set_added_again_gets_a_new_slot()
    {
        await using var map = await OpenAsync([["."]]);

        map.Editor.RemoveTileSetCommand.Execute();
        Assert.Empty(map.Session.Dialogs.Messages);
        Assert.True(map.Editor.AddTileSet(map.Session.TileSetAt("Tiles/Town")));

        Assert.Equal(2, Assert.Single(MapAsset(map).TileSets).Slot);
        Assert.Equal(["Remove tile set", "Add tile set"], map.Session.Steps);
    }

    [Fact]
    public async Task A_pick_in_the_palette_makes_the_stamp_and_the_stamp_tool()
    {
        await using var map = await OpenAsync([["."]]);
        map.Editor.Tool = TileToolKind.Erase;
        var palette = map.Editor.SelectedTileSet!.Palette!;

        var tile = palette.SelectedSource!.SlotOf(TileId.Parse("1:2,1"))!.Bounds;
        var middle = new Vector2(tile.X + tile.Width / 2f, tile.Y + tile.Height / 2f);
        palette.Scene.PointerPressed(middle, CanvasButton.Left, CanvasModifiers.None);
        palette.Scene.PointerReleased(middle, CanvasButton.Left, CanvasModifiers.None);

        Assert.Equal(TileStamp.Single(Cell(2, 1)), map.Editor.Stamp);
        Assert.Equal(TileToolKind.Stamp, map.Editor.Tool);
    }

    [Fact]
    public async Task The_pick_tool_shows_the_tile_in_its_palette()
    {
        await using var map = await OpenWithCaveAsync([["2,1 0,1"]], inSlot2: true);
        map.Editor.SelectedTileSet = map.Editor.TileSets[1];

        map.Editor.Tool = TileToolKind.Pick;
        Click(map.Editor, 0, 0);

        Assert.Same(map.Editor.TileSets[0], map.Editor.SelectedTileSet);
        Assert.Equal([TileId.Parse("1:2,1")], map.Editor.TileSets[0].Palette!.Scene.Selection);

        // A capture of one tile shows it too
        map.Editor.SelectedTileSet = map.Editor.TileSets[1];
        Drag(map.Editor, (1, 0), (1, 0), CanvasButton.Right);
        Assert.Same(map.Editor.TileSets[0], map.Editor.SelectedTileSet);
        Assert.Equal([TileId.Parse("1:0,1")], map.Editor.TileSets[0].Palette!.Scene.Selection);
    }

    [Fact]
    public async Task A_map_with_an_empty_chunk_item_opens_and_names_it()
    {
        // As a hand edit or a merge leaves "- ~" in the list of chunks
        await using var map = await OpenAsync([["0,0"]], asset => asset.Layers[0].Chunks.Add(null!));

        Assert.Contains(map.Editor.Problems, line => line.Contains("empty item", StringComparison.Ordinal));
        map.Editor.Stamp = TileStamp.Single(Cell(1, 0));
        Click(map.Editor, 1, 0);
        Assert.Equal(["Stamp"], map.Session.Steps);
    }

    [Fact]
    public async Task A_tile_set_whose_pixels_per_unit_is_not_above_zero_is_named()
    {
        // As a hand edit leaves it: the map editor draws its tiles as missing boxes
        var tileSet = TileSet();
        tileSet.PixelsPerUnit = 0;
        var session = await TesseraSession.OpenAsync([("Tiles/Town", tileSet), (Location, Map(tileSet, "Tiles/Town", [["0,0"]]))]);
        session.WriteImage("Tiles/town.png", 64, 32, Pixel);
        await using var map = new OpenEditor(session, await session.EditMapAsync(Location));

        Assert.Contains("The tile set 'Town' has a pixels per unit of 0: its tiles show as missing boxes.", map.Editor.Problems);
    }

    [Fact]
    public async Task A_tile_set_deleted_from_the_session_shows_as_missing()
    {
        await using var map = await OpenAsync([["0,0"]]);
        map.Session.Dialogs.FixAssetReferences = _ => Task.FromResult(true);

        Assert.True(await map.Session.Session.DeleteItems([map.Session.TileSetAt("Tiles/Town")], skipConfirmation: true));

        await ExclusiveDispatcherService.WaitUntilAsync(() => map.Editor.TileSets is [{ IsMissing: true }], "the tile set to show as missing");
        var slot = map.Editor.TileSets[0];
        Assert.Equal("missing (slot 1)", slot.Name);
        Assert.Null(slot.Palette);
        Assert.Contains(map.Editor.Problems, line => line.Contains("slot 1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_status_line_tells_the_cell_the_layer_the_tile_and_the_zoom()
    {
        await using var map = await OpenAsync([Rows((12, 7, "3,0"))], Names("Ground"));
        map.Editor.View.Zoom = 1;

        Move(map.Editor, 12, 7);

        Assert.Equal("cell 12, 7", map.Editor.StatusCell);
        Assert.Equal("Ground", map.Editor.StatusLayer);
        Assert.Equal("Town 1:3,0", map.Editor.StatusTile);
        Assert.Equal("100%", map.Editor.StatusZoom);

        map.Editor.View.Zoom = 3;
        Assert.Equal("300%", map.Editor.StatusZoom);
        Move(map.Editor, 11, 7);
        Assert.Equal("", map.Editor.StatusTile);
        map.Editor.Input.PointerLeft();
        Assert.Equal("", map.Editor.StatusCell);
        Assert.Equal("", map.Editor.StatusTile);
    }

    [Fact]
    public async Task The_grid_the_dimming_and_the_last_zoom_are_kept_in_the_settings()
    {
        await using var map = await OpenAsync([["."], ["."]]);
        try
        {
            map.Editor.ShowGrid = false;
            map.Editor.DimOtherLayers = true;
            map.Editor.View.Zoom = 4;
            Assert.True(map.Editor.Scene.DimOthers);

            var second = await map.Session.EditMapAsync(Location);

            Assert.False(second.ShowGrid);
            Assert.True(second.DimOtherLayers);
            Assert.True(second.Scene.DimOthers);
            Assert.Equal(4, second.View.Zoom);
            Assert.False(TesseraEditorSettings.ShowGrid.GetValue());
        }
        finally
        {
            // The settings live as long as the process: the other tests open with the defaults
            TesseraEditorSettings.ShowGrid.SetValue(true);
            TesseraEditorSettings.DimOtherLayers.SetValue(false);
            TesseraEditorSettings.LastZoom.SetValue(0);
        }
    }

    [Fact]
    public async Task The_play_toggle_follows_its_setting_and_writes_it()
    {
        await using var map = await OpenAsync([["0,0"]]);
        try
        {
            Assert.True(TesseraEditorSettings.PlayAnimations.GetValue());
            Assert.True(map.Editor.PlayAnimations);
            Assert.True(map.Editor.Scene.PlayAnimations);

            map.Editor.PlayAnimations = false;

            Assert.False(map.Editor.Scene.PlayAnimations);
            Assert.False(TesseraEditorSettings.PlayAnimations.GetValue());
            var second = await map.Session.EditMapAsync(Location);
            Assert.False(second.PlayAnimations);
            Assert.False(second.Scene.PlayAnimations);
        }
        finally
        {
            // The settings live as long as the process: the other tests open with the defaults
            TesseraEditorSettings.PlayAnimations.SetValue(true);
        }
    }

    private static async DispatcherTask<OpenEditor> OpenAsync(string[][] layers, Action<TileMapAsset>? change = null)
    {
        var tileSet = TileSet();
        var asset = Map(tileSet, "Tiles/Town", layers);
        change?.Invoke(asset);
        var session = await TesseraSession.OpenAsync([("Tiles/Town", tileSet), (Location, asset)]);
        session.WriteImage("Tiles/town.png", 64, 32, Pixel);
        return new OpenEditor(session, await session.EditMapAsync(Location));
    }

    // With a second tile set, "Tiles/Cave", over the same image; in the slot 2 of the map, or in no slot
    private static async DispatcherTask<OpenEditor> OpenWithCaveAsync(string[][] layers, bool inSlot2 = false)
    {
        var (tileSet, cave) = (TileSet(), TileSet());
        var asset = Map(tileSet, "Tiles/Town", layers);
        if (inSlot2)
        {
            asset.TileSets.Add(new TileSetSlot { Slot = 2, TileSet = AttachedReferenceManager.CreateProxyObject<TileSet>(cave.Id, CaveLocation) });
            asset.LastSlot = 2;
        }
        var session = await TesseraSession.OpenAsync([("Tiles/Town", tileSet), (CaveLocation, cave), (Location, asset)]);
        session.WriteImage("Tiles/town.png", 64, 32, Pixel);
        return new OpenEditor(session, await session.EditMapAsync(Location));
    }

    private static Action<TileMapAsset> Names(params string[] names) => asset =>
    {
        for (var index = 0; index < names.Length; index++)
        {
            asset.Layers[index].Name = names[index];
        }
    };

    private static TileMapAsset MapAsset(OpenEditor map) => map.Session.MapAt(Location).Asset;

    private static List<string> LayerNames(OpenEditor map) => [.. MapAsset(map).Layers.Select(layer => layer.Name)];

    private static Vector2 Middle(TileMapEditorViewModel editor, int x, int y) => editor.Scene.CellBounds(new Int2(x, y)).Center;

    private static void Move(TileMapEditorViewModel editor, int x, int y) => editor.Input.PointerMoved(Middle(editor, x, y), CanvasModifiers.None);

    private static void Click(TileMapEditorViewModel editor, int x, int y) => Drag(editor, (x, y), (x, y));

    private static void Drag(TileMapEditorViewModel editor, (int X, int Y) from, (int X, int Y) to, CanvasButton button = CanvasButton.Left)
    {
        Move(editor, from.X, from.Y);
        editor.Input.PointerPressed(Middle(editor, from.X, from.Y), button, CanvasModifiers.None);
        Move(editor, to.X, to.Y);
        editor.Input.PointerReleased(Middle(editor, to.X, to.Y), button, CanvasModifiers.None);
    }

    private static CanvasFrame Collect(TileMapScene scene)
    {
        var frame = new CanvasFrame();
        scene.Collect(new RectangleF(-10000, -10000, 20000, 20000), 1, frame);
        return frame;
    }

    private static Color PixelAt(CanvasSprite sprite, Vector2 canvas)
    {
        var (destination, source) = (sprite.Destination, sprite.Source);
        Assert.True(destination.Contains(canvas), $"{canvas} is not in {destination}.");
        var x = source.X + (int)((canvas.X - destination.X) / destination.Width * source.Width);
        var y = source.Y + (int)((canvas.Y - destination.Y) / destination.Height * source.Height);
        return sprite.Picture[x, y];
    }

    // The property grid names a member "Name" as "Name_"
    private static List<string> Members(TileMapEditorViewModel editor)
        => editor.EditorProperties.ViewModel?.RootNode.Children.Select(child => child.Name.TrimEnd('_')).ToList() ?? [];

    private static NodeViewModel Child(TileMapEditorViewModel editor, string member)
        => editor.EditorProperties.ViewModel!.RootNode.Children.Single(child => child.Name.TrimEnd('_') == member);

    private sealed class OpenEditor(TesseraSession session, TileMapEditorViewModel editor) : IAsyncDisposable
    {
        public TesseraSession Session { get; } = session;

        public TileMapEditorViewModel Editor { get; } = editor;

        public ValueTask DisposeAsync() => Session.DisposeAsync();
    }
}
