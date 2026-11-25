using System.Globalization;
using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Canvas;
using ColorRise.Tessera.Editor.Palette;
using ColorRise.Tessera.Editor.TileSetEditor;
using Stride.Core.Assets.Editor.Quantum.NodePresenters.Commands;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.Quantum.ViewModels;
using Stride.Core.Quantum;
using Stride.Core.Serialization;
using Stride.Graphics;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// Animation mode of the tile set editor: the tile it animates, the frames a click adds, the bar's commands, the durations in
/// the property grid, and the tiles that play in place.
/// </summary>
public sealed class TileSetEditorAnimationTests
{
    // The spike of the task (risk R11): the grid edits a field of a struct in a list of a copy outside the asset
    [Fact]
    public async Task The_grid_edits_the_duration_of_a_frame()
    {
        await using var session = await OpenTownAsync(Animated("1:0,0", ("1:0,0", 0.2f), ("1:1,0", 0.2f)));
        var editor = await session.EditTileSetAsync("Tiles/Town");
        var tileSet = session.TileSetAt("Tiles/Town");
        var data = new TileDataViewModel(editor.ServiceProvider, session.Session.AssetNodeContainer, Tile("1:0,0"), tileSet.Asset.GetDefinition(Tile("1:0,0")), showAnimation: true);
        var changes = 0;
        data.Changed += (_, _) => changes++;
        await ShowAsync(editor, data);

        Frame(editor, 1).Children.Single(child => child.Name == "Duration").NodeValue = 0.5f;

        Assert.True(changes > 0);
        Assert.Equal([0.2f, 0.5f], data.ToDefinition().Animation.Select(frame => frame.Duration));
        Assert.Equal([Tile("1:0,0"), Tile("1:1,0")], data.ToDefinition().Animation.Select(frame => frame.Tile));
        // Written as the editor writes tile data
        using (var transaction = session.UndoRedo.CreateTransaction())
        {
            TileSetGraph.SetDefinition(tileSet, data.ToDefinition());
            session.UndoRedo.SetName(transaction, "Tile data");
        }
        Assert.Equal([0.2f, 0.5f], Durations(session, "1:0,0"));
        Assert.Equal(["Tile data"], session.Steps);
        session.UndoRedo.Undo();
        Assert.Equal([0.2f, 0.2f], Durations(session, "1:0,0"));
        data.Destroy();
    }

    [Fact]
    public async Task The_grid_shows_the_durations_and_not_the_tiles_of_the_frames()
    {
        await using var session = await OpenTownAsync(Animated("1:0,0", ("1:0,0", 0.2f), ("1:1,0", 0.2f)));
        var editor = await session.EditTileSetAsync("Tiles/Town");
        var tileSet = session.TileSetAt("Tiles/Town");
        var data = new TileDataViewModel(editor.ServiceProvider, session.Session.AssetNodeContainer, Tile("1:0,0"), tileSet.Asset.GetDefinition(Tile("1:0,0")), showAnimation: true);
        await ShowAsync(editor, data);

        var animation = Child(editor, nameof(TileDefinition.Animation));
        Assert.Equal(2, animation.Children.Count);
        foreach (var frame in animation.Children)
        {
            Assert.Equal([nameof(TileAnimationFrame.Duration)], frame.Children.Select(child => child.Name));
            Assert.Null(frame.GetCommand(RemoveItemCommand.CommandName));
        }
        Assert.Null(animation.GetCommand(AddNewItemCommand.CommandName));
        Assert.Contains(nameof(TileDefinition.Kind), editor.EditorProperties.ViewModel!.RootNode.Children.Select(child => child.Name));
        data.Destroy();
    }

    [Fact]
    public async Task The_first_click_picks_the_tile_to_animate_and_adds_no_frame()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.Mode = TileSetEditorMode.Animation;
        Assert.Null(editor.AnimatedTile);

        Click(editor, "1:1,0");

        Assert.Equal(Tile("1:1,0"), editor.AnimatedTile);
        Assert.Empty(editor.Frames);
        Assert.Null(editor.SelectedFrame);
        Assert.Equal([Tile("1:1,0")], editor.Scene.Selection);
        await GridOfAnimationAsync(editor, "1:1,0", 0);
        Assert.Empty(session.Steps);
    }

    [Fact]
    public async Task Entering_the_mode_with_one_tile_selected_animates_it()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        Click(editor, "1:2,0");
        Click(editor, "1:3,0", CanvasModifiers.Control);

        editor.Mode = TileSetEditorMode.Animation;
        Assert.Null(editor.AnimatedTile);
        Assert.Equal(TilePickMode.One, editor.Scene.Mode);
        Assert.Empty(editor.Scene.Selection);

        editor.Mode = TileSetEditorMode.Select;
        Click(editor, "1:2,0");
        editor.Mode = TileSetEditorMode.Animation;
        Assert.Equal(Tile("1:2,0"), editor.AnimatedTile);
        Assert.Equal([Tile("1:2,0")], editor.Scene.Selection);
        Assert.Empty(session.Steps);
    }

    [Fact]
    public async Task A_click_adds_a_frame_at_the_end_that_lasts_as_the_one_before()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.Mode = TileSetEditorMode.Animation;
        Click(editor, "1:0,0");

        Click(editor, "1:1,0");
        Click(editor, "1:2,0");
        Assert.Equal(Tiles("1:1,0", "1:2,0"), editor.Frames.Select(frame => frame.Tile));
        Assert.Equal([0.2f, 0.2f], editor.Frames.Select(frame => frame.Duration));
        Assert.Equal(["1", "2"], editor.Frames.Select(frame => frame.Label));
        Assert.Equal([0.2f, 0.2f], Durations(session, "1:0,0"));
        Assert.Same(editor.Frames[1], editor.SelectedFrame);

        // A duration set in the grid is the duration of the next frame
        await GridOfAnimationAsync(editor, "1:0,0", 2);
        Frame(editor, 1).Children.Single(child => child.Name == "Duration").NodeValue = 0.5f;
        await StepsAsync(session, 3);
        Assert.Equal([0.2f, 0.5f], Durations(session, "1:0,0"));
        Click(editor, "1:3,0");

        Assert.Equal([0.2f, 0.5f, 0.5f], Durations(session, "1:0,0"));
        Assert.Equal(["Add frame", "Add frame", "Tile data", "Add frame"], session.Steps);
    }

    [Fact]
    public async Task A_click_adds_after_the_selected_frame_and_selects_the_new_one()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.Mode = TileSetEditorMode.Animation;
        Click(editor, "1:0,0");
        Click(editor, "1:1,0");
        Click(editor, "1:2,0");

        editor.SelectedFrame = editor.Frames[0];
        Click(editor, "1:3,0");

        Assert.Equal(Tiles("1:1,0", "1:3,0", "1:2,0"), editor.Frames.Select(frame => frame.Tile));
        Assert.Equal(1, editor.SelectedFrame!.Index);
        Assert.Equal(Tile("1:3,0"), editor.SelectedFrame.Tile);
        Assert.Equal([Tile("1:0,0")], editor.Scene.Selection);
    }

    [Fact]
    public async Task A_tile_can_be_a_frame_twice_and_its_own_frame()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.Mode = TileSetEditorMode.Animation;
        Click(editor, "1:0,0");

        Click(editor, "1:0,0");
        Click(editor, "1:1,0");
        Click(editor, "1:0,0");

        Assert.Equal(Tiles("1:0,0", "1:1,0", "1:0,0"), editor.Frames.Select(frame => frame.Tile));
        Assert.Equal(Tiles("1:0,0", "1:1,0", "1:0,0"), FrameTiles(session, "1:0,0"));
        Assert.Equal(Tile("1:0,0"), editor.AnimatedTile);
    }

    [Fact]
    public async Task A_frame_can_come_from_another_source()
    {
        await using var session = await OpenWithSheetAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.Mode = TileSetEditorMode.Animation;
        Click(editor, "1:0,0");

        editor.SelectedSource = editor.Sources[1];
        Assert.Equal(Tile("1:0,0"), editor.AnimatedTile);
        Click(editor, "2:#2");

        Assert.Equal(Tile("1:0,0"), editor.AnimatedTile);
        Assert.Equal(Tiles("2:#2"), editor.Frames.Select(frame => frame.Tile));
        Assert.False(editor.Frames[0].IsMissing);
        editor.SelectedSource = editor.Sources[0];
        Assert.Equal([Tile("1:0,0")], editor.Scene.Selection);
    }

    [Fact]
    public async Task Remove_move_and_clear_are_one_step_each()
    {
        await using var session = await OpenTownAsync(Animated("1:0,0", ("1:0,0", 0.2f), ("1:1,0", 0.3f), ("1:2,0", 0.4f)));
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.Mode = TileSetEditorMode.Animation;
        Click(editor, "1:0,0");
        Assert.Equal(3, editor.Frames.Count);
        Assert.False(editor.RemoveFrameCommand.IsEnabled);
        Assert.True(editor.ClearFramesCommand.IsEnabled);

        editor.SelectedFrame = editor.Frames[1];
        Assert.True(editor.MoveFrameLeftCommand.IsEnabled);
        editor.RemoveFrameCommand.Execute();
        Assert.Equal(Tiles("1:0,0", "1:2,0"), FrameTiles(session, "1:0,0"));
        Assert.Equal(Tile("1:2,0"), editor.SelectedFrame?.Tile);
        session.UndoRedo.Undo();
        Assert.Equal(Tiles("1:0,0", "1:1,0", "1:2,0"), FrameTiles(session, "1:0,0"));

        editor.SelectedFrame = editor.Frames[0];
        Assert.False(editor.MoveFrameLeftCommand.IsEnabled);
        editor.MoveFrameRightCommand.Execute();
        Assert.Equal(Tiles("1:1,0", "1:0,0", "1:2,0"), FrameTiles(session, "1:0,0"));
        Assert.Equal([0.3f, 0.2f, 0.4f], Durations(session, "1:0,0"));
        Assert.Equal(1, editor.SelectedFrame?.Index);
        session.UndoRedo.Undo();
        Assert.Equal(Tiles("1:0,0", "1:1,0", "1:2,0"), FrameTiles(session, "1:0,0"));

        editor.ClearFramesCommand.Execute();
        Assert.Empty(editor.Frames);
        Assert.False(editor.ClearFramesCommand.IsEnabled);
        session.UndoRedo.Undo();
        Assert.Equal([0.2f, 0.3f, 0.4f], Durations(session, "1:0,0"));

        Assert.Equal(["Remove frame", "Move frame", "Clear frames"], session.Steps);
    }

    [Fact]
    public async Task Clear_removes_a_definition_left_with_nothing_and_keeps_a_solid_one()
    {
        var tileSet = Animated("1:0,0", ("1:1,0", 0.2f));
        tileSet.Tiles.Add(new TileDefinition { Tile = Tile("1:2,0"), Collision = TileCollision.Solid, Animation = { new TileAnimationFrame { Tile = Tile("1:3,0"), Duration = 0.2f } } });
        await using var session = await OpenTownAsync(tileSet);
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.Mode = TileSetEditorMode.Animation;

        Click(editor, "1:0,0");
        editor.ClearFramesCommand.Execute();
        Assert.Null(TileSet(session).GetDefinition(Tile("1:0,0")));

        editor.DoneAnimationCommand.Execute();
        Click(editor, "1:2,0");
        editor.ClearFramesCommand.Execute();
        var solid = TileSet(session).GetDefinition(Tile("1:2,0"));
        Assert.NotNull(solid);
        Assert.Equal(TileCollision.Solid, solid.Collision);
        Assert.Empty(solid.Animation);
    }

    [Fact]
    public async Task Delete_removes_the_selected_frame_and_Escape_lets_go()
    {
        await using var session = await OpenTownAsync(Animated("1:0,0", ("1:1,0", 0.2f), ("1:2,0", 0.2f)));
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.Mode = TileSetEditorMode.Animation;
        Click(editor, "1:0,0");
        editor.SelectedFrame = editor.Frames[0];

        Assert.True(editor.Scene.KeyPressed(CanvasKey.Delete, CanvasModifiers.None));
        Assert.Equal(Tiles("1:2,0"), FrameTiles(session, "1:0,0"));
        Assert.True(editor.Scene.KeyPressed(CanvasKey.Escape, CanvasModifiers.None));
        Assert.Null(editor.AnimatedTile);
        Assert.Empty(editor.Frames);
        Assert.Empty(editor.Scene.Selection);
        await ExclusiveDispatcherService.WaitUntilAsync(() => editor.EditorProperties.ViewModel is null, "an empty property grid");

        editor.Mode = TileSetEditorMode.Select;
        Assert.False(editor.Scene.KeyPressed(CanvasKey.Delete, CanvasModifiers.None));
        Assert.False(editor.Scene.KeyPressed(CanvasKey.Escape, CanvasModifiers.None));
        Assert.Equal(["Remove frame"], session.Steps);
    }

    [Fact]
    public async Task The_frames_carry_their_numbers_and_an_animated_tile_its_mark()
    {
        await using var session = await OpenTownAsync(Animated("1:0,0", ("1:1,0", 0.2f), ("1:2,0", 0.2f), ("1:2,0", 0.2f)));
        var editor = await session.EditTileSetAsync("Tiles/Town");

        var corner = Assert.Single(Collect(editor).Shapes, shape => shape.Mark == CanvasMark.Corner);
        Assert.Equal(new RectangleF(0, 0, 16, 16), corner.Bounds);
        Assert.Empty(Collect(editor).Labels);

        editor.Mode = TileSetEditorMode.Animation;
        Click(editor, "1:0,0");
        Assert.Equal(["1 on 16,0", "2, 3 on 32,0"], Labels(editor));

        Click(editor, "1:2,0");
        Click(editor, "1:2,0");
        Assert.Equal(["1 on 16,0", "2, 3, 4\u2026 on 32,0"], Labels(editor));
    }

    [Fact]
    public async Task An_animated_tile_plays_in_place()
    {
        await using var session = await OpenTownAsync(Animated("1:0,0", ("1:1,0", 0.2f), ("1:2,0", 0.2f)));
        var editor = await session.EditTileSetAsync("Tiles/Town");
        Assert.True(editor.Scene.Plays);
        Assert.True(editor.Scene.IsRunning);

        editor.Scene.Advance(0.25);

        var replaced = Assert.Single(Collect(editor).Sprites, sprite => sprite.Replace);
        Assert.Equal(new Rectangle(32, 0, 16, 16), replaced.Source);
        Assert.Equal(new RectangleF(0, 0, 16, 16), replaced.Destination);
    }

    [Fact]
    public async Task Leaving_the_mode_lets_go_and_the_grid_shows_the_selection_again()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        Click(editor, "1:2,0");
        Click(editor, "1:3,0", CanvasModifiers.Control);
        editor.Mode = TileSetEditorMode.Animation;
        Click(editor, "1:0,0");
        Click(editor, "1:1,0");
        await GridOfAnimationAsync(editor, "1:0,0", 1);

        editor.Mode = TileSetEditorMode.Select;

        Assert.Null(editor.AnimatedTile);
        Assert.Empty(editor.Frames);
        Assert.Equal(Tiles("1:2,0", "1:3,0"), editor.SelectedTiles);
        Assert.Equal(Tiles("1:2,0", "1:3,0"), editor.Scene.Selection);
        await ExclusiveDispatcherService.WaitUntilAsync(
            () => editor.EditorProperties.Selection?.OfType<TileDataViewModel>().Select(data => data.Tile).SequenceEqual(Tiles("1:2,0", "1:3,0")) == true
                  && editor.EditorProperties.ViewModel is { } grid
                  && grid.RootNode.Children.Any(child => child.Name == nameof(TileDefinition.Kind))
                  && grid.RootNode.Children.All(child => child.Name != nameof(TileDefinition.Animation)),
            "the data of the selected tiles, without their animation, in the property grid");
    }

    [Fact]
    public async Task SetAnimation_keeps_the_other_data_and_the_order_of_the_definitions()
    {
        var tileSet = TestTown.TileSet();
        tileSet.Tiles.Add(new TileDefinition { Tile = Tile("1:0,0"), Kind = "a" });
        tileSet.Tiles.Add(new TileDefinition { Tile = Tile("1:1,0"), Kind = "door", Collision = TileCollision.Solid, Properties = { ["locked"] = new TileBoolProperty { Value = true } } });
        tileSet.Tiles.Add(new TileDefinition { Tile = Tile("1:2,1"), Kind = "c" });
        await using var session = await OpenTownAsync(tileSet);

        Set(session, "1:1,0", ("1:3,0", 0.3f), ("1:0,0", 0.1f));
        var door = TileSet(session).Tiles[1];
        Assert.Equal(Tile("1:1,0"), door.Tile);
        Assert.Equal("door", door.Kind);
        Assert.Equal(TileCollision.Solid, door.Collision);
        Assert.True(Assert.IsType<TileBoolProperty>(door.Properties["locked"]).Value);
        Assert.Equal(Tiles("1:3,0", "1:0,0"), FrameTiles(session, "1:1,0"));
        Assert.Equal([0.3f, 0.1f], Durations(session, "1:1,0"));

        // A tile without data gets a definition at its place; one left with nothing goes away; one with data stays
        Set(session, "1:3,0", ("1:1,0", 0.2f));
        Assert.Equal(Tiles("1:0,0", "1:1,0", "1:3,0", "1:2,1"), TileSet(session).Tiles.Select(definition => definition.Tile));
        Set(session, "1:3,0");
        Set(session, "1:1,0");
        Assert.Equal(Tiles("1:0,0", "1:1,0", "1:2,1"), TileSet(session).Tiles.Select(definition => definition.Tile));
        Assert.Equal("door", TileSet(session).Tiles[1].Kind);
        Assert.Empty(TileSet(session).Tiles[1].Animation);
    }

    [Fact]
    public async Task An_undo_that_removes_the_selected_frame_moves_or_clears_the_selection()
    {
        await using var session = await OpenTownAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.Mode = TileSetEditorMode.Animation;
        Click(editor, "1:0,0");
        Click(editor, "1:1,0");
        Click(editor, "1:2,0");
        Assert.Equal(1, editor.SelectedFrame?.Index);

        session.UndoRedo.Undo();
        Assert.Equal(Tiles("1:1,0"), editor.Frames.Select(frame => frame.Tile));
        Assert.Same(editor.Frames[0], editor.SelectedFrame);

        session.UndoRedo.Undo();
        Assert.Empty(editor.Frames);
        Assert.Null(editor.SelectedFrame);
        Assert.Equal(Tile("1:0,0"), editor.AnimatedTile);
    }

    [Fact]
    public async Task A_removed_source_lets_go_of_its_animated_tile()
    {
        await using var session = await OpenWithSheetAsync();
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.Mode = TileSetEditorMode.Animation;
        editor.SelectedSource = editor.Sources[1];
        Click(editor, "2:#1");
        Click(editor, "2:#2");
        Assert.Single(editor.Frames);

        var viewModel = session.TileSetAt("Tiles/Town");
        using (var transaction = session.UndoRedo.CreateTransaction())
        {
            TileSetGraph.RemoveSource(viewModel, viewModel.Asset.Sources[1]);
            session.UndoRedo.SetName(transaction, "Remove source");
        }

        Assert.Null(editor.AnimatedTile);
        Assert.Empty(editor.Frames);
        Assert.Null(editor.SelectedFrame);
    }

    [Fact]
    public async Task The_frames_follow_a_change_made_outside_the_editor()
    {
        await using var session = await OpenTownAsync(Animated("1:0,0", ("1:1,0", 0.2f)));
        var editor = await session.EditTileSetAsync("Tiles/Town");
        editor.Mode = TileSetEditorMode.Animation;
        Click(editor, "1:0,0");
        editor.SelectedFrame = editor.Frames[0];

        var tiles = session.TileSetAt("Tiles/Town").PropertyGraph.RootNode[nameof(TileSetAsset.Tiles)].Target!;
        using (var transaction = session.UndoRedo.CreateTransaction())
        {
            tiles.IndexedTarget(new NodeIndex(0))![nameof(TileDefinition.Animation)].Target!.Add(new TileAnimationFrame { Tile = Tile("1:3,1"), Duration = 0.6f });
            session.UndoRedo.SetName(transaction, "Tile data");
        }

        Assert.Equal(Tiles("1:1,0", "1:3,1"), editor.Frames.Select(frame => frame.Tile));
        Assert.Equal(0.6f, editor.Frames[1].Duration);
        Assert.Same(editor.Frames[0], editor.SelectedFrame);
    }

    [Fact]
    public async Task A_frame_whose_tile_is_gone_is_marked_and_named_and_kept()
    {
        await using var session = await OpenTownAsync(Animated("1:3,0", ("1:0,0", 0.2f), ("1:9,9", 0.2f)));
        var editor = await session.EditTileSetAsync("Tiles/Town");
        Assert.Contains("The animation of the tile 1:3,0 uses the tile 1:9,9, which the tile set does not have.", editor.Problems);

        editor.Mode = TileSetEditorMode.Animation;
        Click(editor, "1:3,0");
        Assert.False(editor.Frames[0].IsMissing);
        Assert.NotNull(editor.Frames[0].Picture);
        Assert.Equal(new Rectangle(0, 0, 16, 16), editor.Frames[0].Region);
        Assert.True(editor.Frames[1].IsMissing);
        Assert.Null(editor.Frames[1].Picture);

        Click(editor, "1:1,0");
        Assert.Equal(Tiles("1:0,0", "1:9,9", "1:1,0"), FrameTiles(session, "1:3,0"));
    }

    private static async Task ShowAsync(TileSetEditorViewModel editor, TileDataViewModel data)
    {
        await editor.EditorProperties.GenerateSelectionPropertiesAsync([data]);
        await ExclusiveDispatcherService.WaitUntilAsync(
            () => editor.EditorProperties.ViewModel?.RootNode.Children.Any(child => child.Name == nameof(TileDefinition.Animation)) == true,
            "the animation of the tile in the property grid");
    }

    // Waits for the grid to show the data of the animated tile, with its frames
    private static Task GridOfAnimationAsync(TileSetEditorViewModel editor, string tile, int frames)
        => ExclusiveDispatcherService.WaitUntilAsync(
            () => editor.EditorProperties.Selection?.OfType<TileDataViewModel>().Select(data => data.Tile).SequenceEqual([Tile(tile)]) == true
                  && editor.EditorProperties.ViewModel?.RootNode.Children.FirstOrDefault(child => child.Name == nameof(TileDefinition.Animation)) is { } animation
                  && animation.Children.Count == frames,
            $"the {frames} frames of {tile} in the property grid");

    private static Task StepsAsync(TesseraSession session, int count)
        => ExclusiveDispatcherService.WaitUntilAsync(() => session.Steps.Count >= count, $"{count} steps in the history");

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

    private static CanvasFrame Collect(TileSetEditorViewModel editor)
    {
        var frame = new CanvasFrame();
        editor.Scene.Collect(new RectangleF(0, 0, 64, 32), 1, frame);
        return frame;
    }

    // "2, 3 on 32,0": the text of each label, and the corner of its bounds
    private static List<string> Labels(TileSetEditorViewModel editor)
        => [.. Collect(editor).Labels.Select(label => string.Create(CultureInfo.InvariantCulture, $"{label.Text} on {label.Bounds.X},{label.Bounds.Y}"))];

    // Writes the frames of a tile in a step of the test
    private static void Set(TesseraSession session, string tile, params (string Tile, float Duration)[] frames)
    {
        using var transaction = session.UndoRedo.CreateTransaction();
        TileSetGraph.SetAnimation(session.TileSetAt("Tiles/Town"), Tile(tile), [.. frames.Select(frame => new TileAnimationFrame { Tile = Tile(frame.Tile), Duration = frame.Duration })]);
        session.UndoRedo.SetName(transaction, "Animation");
    }

    private static TileSetAsset TileSet(TesseraSession session) => session.TileSetAt("Tiles/Town").Asset;

    private static List<TileId> Tiles(params string[] tiles) => [.. tiles.Select(TileId.Parse)];

    private static List<TileId> FrameTiles(TesseraSession session, string tile)
        => [.. TileSet(session).GetDefinition(Tile(tile))?.Animation.Select(frame => frame.Tile) ?? []];

    private static NodeViewModel Child(TileSetEditorViewModel editor, string member)
        => editor.EditorProperties.ViewModel!.RootNode.Children.Single(child => child.Name == member);

    private static NodeViewModel Frame(TileSetEditorViewModel editor, int index)
        => Child(editor, nameof(TileDefinition.Animation)).Children.ElementAt(index);

    private static TileId Tile(string tile) => TileId.Parse(tile);

    private static List<float> Durations(TesseraSession session, string tile)
        => [.. session.TileSetAt("Tiles/Town").Asset.GetDefinition(Tile(tile))?.Animation.Select(frame => frame.Duration) ?? []];

    // The town set, with a tile that has frames
    private static TileSetAsset Animated(string tile, params (string Tile, float Duration)[] frames)
    {
        var tileSet = TestTown.TileSet();
        var definition = new TileDefinition { Tile = Tile(tile) };
        foreach (var (frame, duration) in frames)
        {
            definition.Animation.Add(new TileAnimationFrame { Tile = Tile(frame), Duration = duration });
        }
        tileSet.Tiles.Add(definition);
        return tileSet;
    }

    // The town set, and the sheet "Tiles/Objects" in a source of id 2 with "grass", "water" and "wall" bound to 1, 2 and 3
    private static DispatcherTask<TesseraSession> OpenWithSheetAsync()
    {
        var tileSet = TestTown.TileSet();
        var sheet = TestTown.Objects();
        var source = tileSet.AddSource<SpriteSheetTileSetSource>("Objects");
        source.Sheet = AttachedReferenceManager.CreateProxyObject<SpriteSheet>(sheet.Id, "Tiles/Objects");
        foreach (var name in new[] { "grass", "water", "wall" })
        {
            source.Bindings.Add(new SpriteTileBinding { Number = ++source.LastNumber, SpriteName = name });
        }
        return OpenTownAsync(tileSet, ("Tiles/Objects", sheet));
    }

    private static async DispatcherTask<TesseraSession> OpenTownAsync(TileSetAsset? tileSet = null, params (string Location, Stride.Core.Assets.Asset Asset)[] others)
    {
        var session = await TesseraSession.OpenAsync([("Tiles/Town", tileSet ?? TestTown.TileSet()), .. others]);
        session.WriteImage("Tiles/town.png", 64, 32, TestTown.Pixel);
        return session;
    }
}
