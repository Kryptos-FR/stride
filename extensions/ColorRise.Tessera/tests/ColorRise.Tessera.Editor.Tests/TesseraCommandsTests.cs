using ColorRise.Tessera.Editor.Palette;
using ColorRise.Tessera.Editor.TileMapEditor;
using ColorRise.Tessera.Editor.TileSetEditor;
using Stride.Core.Assets.Editor.Commands;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Mathematics;
using static ColorRise.Tessera.Editor.Tests.TestTown;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// Tessera's commands: their ids, and the editor each one acts on.
/// </summary>
public sealed class TesseraCommandsTests
{
    private const string MapLocation = "Maps/Town";
    private const string TileSetLocation = "Tiles/Town";

    private static readonly string[] MapIds =
    [
        "Tessera.Map.Tool.Stamp", "Tessera.Map.Tool.Rectangle", "Tessera.Map.Tool.Fill", "Tessera.Map.Tool.Erase", "Tessera.Map.Tool.Pick", "Tessera.Map.Tool.Select",
        "Tessera.Map.FlipStampX", "Tessera.Map.FlipStampY", "Tessera.Map.RotateStampClockwise", "Tessera.Map.RotateStampCounterClockwise",
        "Tessera.Map.ToggleGrid", "Tessera.Map.ToggleDimOtherLayers", "Tessera.Map.AddLayer",
    ];

    private static readonly string[] TileSetIds = ["Tessera.TileSet.Mode.Select", "Tessera.TileSet.Mode.Solid", "Tessera.TileSet.Mode.Animation", "Tessera.TileSet.AddImage", "Tessera.TileSet.AddSpriteSheet"];

    [Fact]
    public void Every_command_has_its_id_a_scope_and_no_default_gesture()
    {
        var commands = TesseraCommands.Create();

        Assert.Equal([.. MapIds, .. TileSetIds], commands.Select(command => command.Id));
        Assert.All(commands, command =>
        {
            Assert.Equal("Tessera", command.Category);
            Assert.Null(command.DefaultGesture);
            Assert.NotNull(command.CanExecute);
            Assert.Equal(MapIds.Contains(command.Id) ? typeof(TileMapEditorViewModel) : typeof(TileSetEditorViewModel), command.GestureScope);
        });
        Assert.Equal("Stamp tool", Command("Tessera.Map.Tool.Stamp").Title());
        Assert.Equal("Flip stamp horizontally", Command("Tessera.Map.FlipStampX").Title());
        Assert.Contains("B", Command("Tessera.Map.Tool.Stamp").Keywords);
        Assert.Contains("Shift+Z", Command("Tessera.Map.RotateStampCounterClockwise").Keywords);
        Assert.All(MapIds.Take(6), id => Assert.NotNull(Command(id).IsChecked));
        Assert.Equal(18, commands.Count);
        Assert.Equal("Animation mode", Command("Tessera.TileSet.Mode.Animation").Title());
    }

    [Fact]
    public async Task A_tool_command_changes_the_tool_of_the_focused_map_editor_and_is_checked()
    {
        await using var session = await OpenAsync();
        var editor = await session.EditMapAsync(MapLocation);
        var context = In(session, editor);
        var fill = Command("Tessera.Map.Tool.Fill");

        Assert.True(fill.CanExecute!(context));
        Assert.False(fill.IsChecked!(context));
        await fill.ExecuteAsync(context);

        Assert.Equal(TileToolKind.Fill, editor.Tool);
        Assert.True(fill.IsChecked!(context));
        Assert.False(Command("Tessera.Map.Tool.Stamp").IsChecked!(context));

        await Command("Tessera.Map.AddLayer").ExecuteAsync(context);
        Assert.Equal(["Add layer"], session.Steps);
        await Command("Tessera.Map.ToggleDimOtherLayers").ExecuteAsync(context);
        Assert.True(editor.DimOtherLayers);
        await Command("Tessera.Map.ToggleDimOtherLayers").ExecuteAsync(context);
        Assert.False(editor.DimOtherLayers);
    }

    [Fact]
    public async Task A_map_command_cannot_run_without_a_map_editor()
    {
        await using var session = await OpenAsync();
        var tileSetEditor = await session.EditTileSetAsync(TileSetLocation);
        var mapEditor = await session.EditMapAsync(MapLocation);
        var nowhere = new CommandContext { Session = session.Session, Location = CommandLocations.Shortcut };

        Assert.All(TesseraCommands.Create(), command => Assert.False(command.CanExecute!(nowhere), command.Id));
        Assert.All(MapIds, id => Assert.False(Command(id).CanExecute!(In(session, tileSetEditor)), id));
        Assert.All(TileSetIds, id => Assert.False(Command(id).CanExecute!(In(session, mapEditor)), id));
        Assert.All(TileSetIds, id => Assert.True(Command(id).CanExecute!(In(session, tileSetEditor)), id));

        // The plugin registers them all
        var host = Assert.IsAssignableFrom<EditorPluginHost>(session.Session.ServiceProvider.Get<IAssetsPluginService>());
        Assert.Equal(TesseraCommands.Create().Select(command => command.Id), host.Commands.Items.Select(command => command.Id).Where(id => id.StartsWith("Tessera.", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task The_flip_commands_flip_the_stamp()
    {
        await using var session = await OpenAsync();
        var editor = await session.EditMapAsync(MapLocation);
        var context = In(session, editor);
        var stamp = new TileStamp(new Int2(2, 1), [Cell(0, 0), Cell(1, 0, TileFlip.Diagonal)]);
        editor.Stamp = stamp;

        await Command("Tessera.Map.FlipStampX").ExecuteAsync(context);
        Assert.Equal(stamp.FlipX(), editor.Stamp);
        await Command("Tessera.Map.FlipStampX").ExecuteAsync(context);
        await Command("Tessera.Map.FlipStampY").ExecuteAsync(context);
        Assert.Equal(stamp.FlipY(), editor.Stamp);
        await Command("Tessera.Map.FlipStampY").ExecuteAsync(context);
        await Command("Tessera.Map.RotateStampClockwise").ExecuteAsync(context);
        Assert.Equal(stamp.RotateClockwise(), editor.Stamp);
        await Command("Tessera.Map.RotateStampCounterClockwise").ExecuteAsync(context);
        Assert.Equal(stamp, editor.Stamp);
    }

    [Fact]
    public async Task The_tile_set_commands_reach_the_tile_set_editor()
    {
        await using var session = await OpenAsync();
        var editor = await session.EditTileSetAsync(TileSetLocation);
        var context = In(session, editor);
        var solid = Command("Tessera.TileSet.Mode.Solid");

        await solid.ExecuteAsync(context);
        Assert.Equal(TileSetEditorMode.Solid, editor.Mode);
        Assert.True(solid.IsChecked!(context));
        await Command("Tessera.TileSet.Mode.Select").ExecuteAsync(context);
        Assert.Equal(TileSetEditorMode.Select, editor.Mode);
        var animation = Command("Tessera.TileSet.Mode.Animation");
        Assert.False(animation.IsChecked!(context));
        await animation.ExecuteAsync(context);
        Assert.Equal(TileSetEditorMode.Animation, editor.Mode);
        Assert.True(animation.IsChecked!(context));
        Assert.False(solid.IsChecked!(context));

        var asked = false;
        session.Dialogs.PickFile = _ =>
        {
            asked = true;
            return null;
        };
        await Command("Tessera.TileSet.AddImage").ExecuteAsync(context);
        Assert.True(asked);
        await Command("Tessera.TileSet.AddSpriteSheet").ExecuteAsync(context);
        Assert.Single(session.Dialogs.AssetPickers);
    }

    private static CommandDescriptor Command(string id) => TesseraCommands.Create().Single(command => command.Id == id);

    private static CommandContext In(TesseraSession session, IAssetEditorViewModel editor)
        => new() { Session = session.Session, Location = CommandLocations.Shortcut, Editor = editor };

    private static async DispatcherTask<TesseraSession> OpenAsync()
    {
        var tileSet = TileSet();
        var session = await TesseraSession.OpenAsync((TileSetLocation, tileSet), (MapLocation, Map(tileSet, TileSetLocation, [["."]])));
        session.WriteImage("Tiles/town.png", 64, 32, Pixel);
        return session;
    }
}
