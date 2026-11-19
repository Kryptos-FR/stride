// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Concurrent;
using System.IO.Pipes;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using Stride.Assets;
using Stride.Assets.Editor.PlayMode;
using Stride.Assets.Entities;
using Stride.Assets.Materials;
using Stride.Assets.Sprite;
using Stride.Core;
using Stride.Core.Assets.Editor.Commands;
using Stride.Core.Assets.Editor.Avalonia.Commands;
using Stride.Core.Assets.Editor.Panels;
using Stride.Core.Serialization;
using Stride.Editor.PlayMode;
using Stride.Engine;
using Stride.GameStudio.Avalonia.Commands;
using Stride.GameStudio.Avalonia.Settings;
using Stride.GameStudio.Avalonia.Tests.Helpers;
using Stride.GameStudio.Avalonia.Views;
using Stride.GameStudio.Avalonia.Views.Panels;
using Stride.PlayMode.Agent;
using Stride.PlayMode.Protocol;

namespace Stride.GameStudio.Avalonia.Tests;

/// <summary>
/// Play mode from the shell: F5 starts the game of the editor's platform in a play session, Shift+F5 stops it, the Live tab
/// shows its scenes, and its selection shows in the property grid; Ctrl+F5 starts a game without the connection. The games are
/// fakes that connect to the session's real pipe, as the agent does.
/// </summary>
public sealed class PlayModeTests : IAsyncLifetime
{
    private const string Version = "1.0.0-test";
    private const string MaterialLocation = "Materials/Ground";
    private const int PlayerHandle = 2;

    private readonly string agentDirectory = Path.Combine(Path.GetTempPath(), $"stride-play-shell-tests-{Guid.NewGuid():N}");
    private readonly List<TestGame> games = [];
    private PlayModeService playMode = null!;

    public ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(agentDirectory);
        foreach (var file in PlayAgentFiles.FileNames)
            File.WriteAllBytes(Path.Combine(agentDirectory, file), []);
        playMode = new PlayModeService(() => new PipePlayTransport(), new PlayAgentFiles(agentDirectory, Version),
            new PlaySessionOptions { StopGracePeriod = TimeSpan.FromSeconds(2) }, (_, _, environment) =>
            {
                var game = new TestGame(environment);
                lock (games)
                    games.Add(game);
                return game;
            });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await playMode.DisposeAsync();
        foreach (var game in games)
            await game.DisposeAsync();
        Directory.Delete(agentDirectory, recursive: true);
    }

    [AvaloniaFact]
    public async Task TheProjectMenuAndTheToolbarHaveStopGame()
    {
        using var fixture = await ShellSessionFixture.OpenExecutableAsync(Platform.Type, playMode);
        var window = new MainWindow { DataContext = fixture.Main };
        await window.ShowAsync();
        try
        {
            var stop = MenuItem(window, "StopGameMenuItem");
            Assert.Equal(new KeyGesture(Key.F5, KeyModifiers.Shift), stop.InputGesture);
            Assert.Equal(GameStudioCommands.StopGameId, Assert.IsType<CommandReference>(stop.Command).Id);
            Assert.Equal(GameStudioCommands.StopGameId, Assert.IsType<CommandReference>(Button(window, "StopGameButton").Command).Id);
            Assert.Contains(window.View.CommandSlots!.Shortcuts, x => x.Gesture == new KeyGesture(Key.F5, KeyModifiers.Shift) && x.Descriptor.Id == GameStudioCommands.StopGameId);
            // No game plays
            Assert.False(await IsEnabledInOpenMenuAsync(stop));

            // The Live tab is a panel of its own, hidden until a game connects
            var live = fixture.Main.Panels.FindDockable(LivePanel.Id)!;
            Assert.Equal("Live", live.Title);
            Assert.False(live.ViewModel.IsShown);
        }
        finally
        {
            await window.CloseAsync();
        }
    }

    [AvaloniaFact]
    public async Task F5PlaysTheGameAndTheLiveTabShowsItsScenesUntilShiftF5StopsIt()
    {
        using var fixture = await ShellSessionFixture.OpenExecutableAsync(Platform.Type, playMode);
        fixture.ReplaceProjectBuilder();
        fixture.ReplaceDialogService();
        var window = new MainWindow { DataContext = fixture.Main };
        await window.ShowAsync();
        try
        {
            var live = fixture.Main.Panels.FindDockable(LivePanel.Id)!;

            Press(window, Key.F5, RawInputModifiers.None, PhysicalKey.F5);
            await WindowTestHelper.WaitUntilAsync(() => Count(games) == 1, "the start of the game");
            var game = games[0];
            await game.ConnectAsync();
            game.Send(new PlayHello(game.Id, Version, Version, null, []));
            game.Send(Tree());

            // The Live tab comes forward with the running scenes
            await WindowTestHelper.WaitUntilAsync(() => live.ViewModel.IsShown, "the Live tab in front");
            var view = window.GetVisualDescendants().OfType<LiveView>().Single();
            var list = view.GetVisualDescendants().OfType<ListBox>().Single(x => x.Name == "LiveTree");
            await WindowTestHelper.WaitUntilAsync(() => list.ItemCount == 3, "the rows of the tree");
            var viewModel = Assert.IsType<LivePanelViewModel>(view.DataContext);
            Assert.Equal(["MainScene", "Player", "Ground"], viewModel.Tree.Rows.Select(x => x.Name));
            Assert.Equal("Playing", view.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "StateText").Text);
            Assert.False(list.Classes.Contains("stopped"));
            await WindowTestHelper.WaitUntilAsync(() => fixture.Main.StopGameCommand.IsEnabled, "Stop game enabled");

            // Shift+F5 asks the game to quit; it does
            Press(window, Key.F5, RawInputModifiers.Shift, PhysicalKey.F5);
            await game.Quit.WaitAsync(TimeSpan.FromSeconds(10));
            game.Exit(0);
            await WindowTestHelper.WaitUntilAsync(() => viewModel.State == LiveTabState.Stopped, "the stop of the game");

            // The last tree stays, greyed
            Assert.Equal("Stopped (exit code 0)", viewModel.StateText);
            Assert.Equal(3, list.ItemCount);
            await WindowTestHelper.WaitUntilAsync(() => list.Classes.Contains("stopped"), "the greyed tree");
            await WindowTestHelper.WaitUntilAsync(() => !fixture.Main.StopGameCommand.IsEnabled, "Stop game disabled");
            Assert.False(game.Killed);
        }
        finally
        {
            await window.CloseAsync();
        }
    }

    [AvaloniaFact]
    public async Task ASecondF5AsksToStopTheGameThatPlays()
    {
        using var fixture = await ShellSessionFixture.OpenExecutableAsync(Platform.Type, playMode);
        var builder = fixture.ReplaceProjectBuilder();
        var dialogs = fixture.ReplaceDialogService();
        var window = new MainWindow { DataContext = fixture.Main };
        await window.ShowAsync();
        try
        {
            Press(window, Key.F5, RawInputModifiers.None, PhysicalKey.F5);
            await WindowTestHelper.WaitUntilAsync(() => Count(games) == 1 && !fixture.Main.Build.BuildInProgress, "the start of the first game");
            var first = games[0];
            await first.ConnectAsync();
            first.Send(new PlayHello(first.Id, Version, Version, null, []));
            await WindowTestHelper.WaitUntilAsync(() => playMode.Session?.State == PlaySessionState.Connected, "the first game connected");
            // Ends as the agent does when it is asked to quit
            first.ExitOnQuit = true;

            Press(window, Key.F5, RawInputModifiers.None, PhysicalKey.F5);
            await WindowTestHelper.WaitUntilAsync(() => Count(games) == 2, "the start of the second game", 10000);

            // The user was asked (the recording dialogs answer OK), the first game quit, then the project was built again
            Assert.Contains(dialogs.Messages, x => x.Contains("only one game plays at a time"));
            Assert.Equal(0, await first.Exited.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.False(first.Killed);
            Assert.Equal(2, builder.Requests.Count);
            Assert.True(playMode.IsPlaying);
        }
        finally
        {
            await window.CloseAsync();
        }
    }

    [AvaloniaFact]
    public async Task TheLiveTabsSelectionShowsInThePropertyGridWhileItIsShownAndGoesBackOnStop()
    {
        using var fixture = await ShellSessionFixture.OpenExecutableAsync(Platform.Type, playMode, (MaterialLocation, new MaterialAsset()));
        fixture.ReplaceProjectBuilder();
        fixture.ReplaceDialogService();
        var window = new MainWindow { DataContext = fixture.Main };
        await window.ShowAsync();
        try
        {
            // The asset's properties are shown when the game starts
            var material = fixture.Asset(MaterialLocation);
            fixture.Session.AssetCollection.SelectAssets([material]);
            await WindowTestHelper.WaitUntilAsync(() => fixture.Session.ActiveProperties.Name == material.Url, "the material's properties");
            var assetProperties = fixture.Session.ActiveProperties;

            var game = await PlayAsync(window, fixture);
            var live = (LivePanelViewModel)fixture.Main.Panels.FindDockable(LivePanel.Id)!.ViewModel;
            await WindowTestHelper.WaitUntilAsync(() => live.EntityCount == 3, "the tree");
            // The status bar says it, in green
            var status = window.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "PlayStatus");
            await WindowTestHelper.WaitUntilAsync(() => status.IsEffectivelyVisible && status.Classes.Contains("playing"), "the status bar");
            Assert.Equal(Color.Parse("#FF388934"), Assert.IsAssignableFrom<ISolidColorBrush>(status.Background).Color);

            // Selecting the player shows it in the grid, which asks the game for its snapshots
            live.SelectedItems.Add(live.Tree.FindEntity(PlayerHandle)!);
            await WindowTestHelper.WaitUntilAsync(() => fixture.Session.ActiveProperties is LivePropertiesViewModel, "the live grid");
            var properties = (LivePropertiesViewModel)fixture.Session.ActiveProperties;
            Assert.Equal([PlayerHandle], (await game.NextSubscribeAsync()).Handles);
            game.Send(Snapshot(1));
            await WindowTestHelper.WaitUntilAsync(() => properties.ViewModel is not null && properties.Name == "Player", "the player's live properties");
            Assert.Equal(1, properties.BuildCount);

            // A selection in the asset view takes the grid back: the game stops sending
            fixture.Session.AssetCollection.SelectAssets([]);
            fixture.Session.AssetCollection.SelectAssets([material]);
            await WindowTestHelper.WaitUntilAsync(() => fixture.Session.ActiveProperties == assetProperties, "the material's properties again");
            Assert.Empty((await game.NextSubscribeAsync()).Handles);
            Assert.False(properties.IsReceiving);
            // and settle: the generation of the empty selection can end after the material's
            await Task.Delay(200);
            await WindowTestHelper.PumpAsync();

            // Back to the Live tab: the same selection, applied in place from the next snapshot
            live.ShowSelectionCommand.Execute();
            Assert.Same(properties, fixture.Session.ActiveProperties);
            Assert.Equal([PlayerHandle], (await game.NextSubscribeAsync()).Handles);
            game.Send(Snapshot(2));
            await WindowTestHelper.WaitUntilAsync(() => properties.LiveStatus.StartsWith("Frame 2", StringComparison.Ordinal), "the next snapshot");
            Assert.Equal(1, properties.BuildCount);

            // The property grid hidden: nothing shows the values, the game stops sending; shown again, it sends again
            fixture.Main.Panels.Hide(BuiltInPanels.PropertyGrid);
            Assert.Empty((await game.NextSubscribeAsync()).Handles);
            fixture.Main.Panels.Show(BuiltInPanels.PropertyGrid);
            Assert.Equal([PlayerHandle], (await game.NextSubscribeAsync()).Handles);

            // Stop: the grid goes back to the material, the live grid ends
            game.ExitOnQuit = true;
            Press(window, Key.F5, RawInputModifiers.Shift, PhysicalKey.F5);
            await WindowTestHelper.WaitUntilAsync(() => live.State == LiveTabState.Stopped, "the stop of the game");
            await WindowTestHelper.WaitUntilAsync(() => fixture.Session.ActiveProperties == assetProperties, "the material's properties after the stop");
            Assert.False(properties.IsReceiving);
            Assert.Null(live.Selection);
        }
        finally
        {
            await window.CloseAsync();
        }
    }

    [AvaloniaFact]
    public async Task OnceTheGameStoppedTheLiveTabSaysSoAndARowShowsAStoppedGrid()
    {
        using var fixture = await ShellSessionFixture.OpenExecutableAsync(Platform.Type, playMode, (MaterialLocation, new MaterialAsset()));
        fixture.ReplaceProjectBuilder();
        fixture.ReplaceDialogService();
        var window = new MainWindow { DataContext = fixture.Main };
        await window.ShowAsync();
        try
        {
            var material = fixture.Asset(MaterialLocation);
            fixture.Session.AssetCollection.SelectAssets([material]);
            await WindowTestHelper.WaitUntilAsync(() => fixture.Session.ActiveProperties.Name == material.Url, "the material's properties");
            var assetProperties = fixture.Session.ActiveProperties;
            var game = await PlayAsync(window, fixture);
            var live = (LivePanelViewModel)fixture.Main.Panels.FindDockable(LivePanel.Id)!.ViewModel;
            await WindowTestHelper.WaitUntilAsync(() => live.EntityCount == 3, "the tree");
            live.SelectedItems.Add(live.Tree.FindEntity(PlayerHandle)!);
            await WindowTestHelper.WaitUntilAsync(() => fixture.Session.ActiveProperties is LivePropertiesViewModel, "the live grid");

            // The game's window is closed: the game exits by itself
            game.Exit(0);
            await WindowTestHelper.WaitUntilAsync(() => live.State == LiveTabState.Stopped, "the exit of the game");
            await WindowTestHelper.WaitUntilAsync(() => fixture.Session.ActiveProperties == assetProperties, "the material's properties after the exit");
            Assert.Empty(live.SelectedItems);

            // The tab stays (the setting is on by default), greyed, and says that its tree is the last one
            Assert.True(fixture.Main.Panels.FindDockable(LivePanel.Id)!.ViewModel.IsShown);
            var view = window.GetVisualDescendants().OfType<LiveView>().Single();
            var notice = view.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "StoppedNotice");
            await WindowTestHelper.WaitUntilAsync(() => notice.IsEffectivelyVisible, "the stopped notice");
            Assert.Contains("The game has stopped", view.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "StoppedNoticeText").Text);
            Assert.Contains("stopped", view.GetVisualDescendants().OfType<ListBox>().Single(x => x.Name == "LiveTree").Classes);
            Assert.False(view.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "HeaderText").IsVisible);
            Assert.True(view.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "StoppedHeaderText").IsVisible);

            // A row selected now: the grid says that the game stopped, under the entity's name, with a grey "Stopped" badge
            live.SelectedItems.Add(live.Tree.FindEntity(PlayerHandle)!);
            await WindowTestHelper.WaitUntilAsync(() => fixture.Session.ActiveProperties is LivePropertiesViewModel { IsStopped: true, FallbackMessage: not null }, "the stopped grid");
            var stopped = (LivePropertiesViewModel)fixture.Session.ActiveProperties;
            Assert.Contains("The game has stopped", stopped.FallbackMessage);
            Assert.Equal("Player", stopped.Name);
            Assert.Null(stopped.ViewModel);
            var grid = window.GetVisualDescendants().OfType<PropertyGridView>().Single();
            await WindowTestHelper.WaitUntilAsync(() => grid.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "StoppedBadgeText").IsEffectivelyVisible, "the Stopped badge");
            Assert.False(grid.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "LiveBadgeText").IsEffectivelyVisible);
            var badge = grid.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "LiveBadge");
            Assert.Contains("stopped", badge.Classes);
            // No lock of the edits: there is nothing to edit any more
            Assert.False(grid.GetVisualDescendants().OfType<Control>().Single(x => x.Name == "LiveEditsToggle").IsEffectivelyVisible);
            Assert.Equal(global::Avalonia.Media.Color.Parse("#808080"), Assert.IsAssignableFrom<global::Avalonia.Media.ISolidColorBrush>(badge.Background).Color);

            // Nothing selected: the grid goes back
            live.SelectedItems.Clear();
            await WindowTestHelper.WaitUntilAsync(() => fixture.Session.ActiveProperties == assetProperties, "the material's properties again");
        }
        finally
        {
            await window.CloseAsync();
        }
    }

    [AvaloniaFact]
    public async Task WithTheSettingOffTheLiveTabClosesWhenTheGameExitsButStaysAfterACrash()
    {
        using var fixture = await ShellSessionFixture.OpenExecutableAsync(Platform.Type, playMode);
        fixture.ReplaceProjectBuilder();
        fixture.ReplaceDialogService();
        var window = new MainWindow { DataContext = fixture.Main };
        await window.ShowAsync();
        GameStudioEditorSettings.PlayModeKeepLiveTabAfterExit.SetValue(false);
        try
        {
            var liveDockable = fixture.Main.Panels.FindDockable(LivePanel.Id)!;
            var game = await PlayAsync(window, fixture);
            await WindowTestHelper.WaitUntilAsync(() => liveDockable.ViewModel.IsShown, "the Live tab in front");

            // The game exits normally: the Live tab closes (alone in its group by default, the group goes with it)
            game.Exit(0);
            await WindowTestHelper.WaitUntilAsync(() => !fixture.Main.Panels.IsVisible(LivePanel.Id), "the Live tab closed");

            // Docked behind the solution explorer: the next game brings it forward, and its exit gives the solution explorer back
            fixture.Main.Panels.Show(LivePanel.Id);
            var solution = fixture.Main.Panels.FindDockable(BuiltInPanels.SolutionExplorer)!;
            var group = Assert.IsAssignableFrom<Dock.Model.Core.IDock>(solution.Owner);
            fixture.Main.Panels.Factory.MoveDockable(Assert.IsAssignableFrom<Dock.Model.Core.IDock>(liveDockable.Owner), group, liveDockable, solution);
            fixture.Main.Panels.Factory.SetActiveDockable(solution);
            await WindowTestHelper.PumpAsync();
            Assert.Same(group, liveDockable.Owner);
            Assert.Same(solution, group.ActiveDockable);
            Press(window, Key.F5, RawInputModifiers.None, PhysicalKey.F5);
            await WindowTestHelper.WaitUntilAsync(() => Count(games) == 2 && !fixture.Main.Build.BuildInProgress, "the start of the second game", 10000);
            var second = games[1];
            await second.ConnectAsync();
            second.Send(new PlayHello(second.Id, Version, Version, null, []));
            await WindowTestHelper.WaitUntilAsync(() => group.ActiveDockable == liveDockable, "the Live tab in front of the solution explorer");
            Assert.Same(solution, liveDockable.Covered);
            second.Exit(0);
            await WindowTestHelper.WaitUntilAsync(() => !fixture.Main.Panels.IsVisible(LivePanel.Id), "the Live tab closed again");
            Assert.Same(solution, group.ActiveDockable);

            // The next game crashes: the tab stays, with the exit code
            Press(window, Key.F5, RawInputModifiers.None, PhysicalKey.F5);
            await WindowTestHelper.WaitUntilAsync(() => Count(games) == 3 && !fixture.Main.Build.BuildInProgress, "the start of the third game", 10000);
            var third = games[2];
            await third.ConnectAsync();
            third.Send(new PlayHello(third.Id, Version, Version, null, []));
            await WindowTestHelper.WaitUntilAsync(() => liveDockable.ViewModel.IsShown, "the Live tab in front again");
            third.Exit(134);
            var live = (LivePanelViewModel)liveDockable.ViewModel;
            await WindowTestHelper.WaitUntilAsync(() => live.State == LiveTabState.Stopped, "the crash of the game");
            await WindowTestHelper.PumpAsync();
            Assert.True(fixture.Main.Panels.IsVisible(LivePanel.Id));
            Assert.Equal("Stopped (exit code 134)", live.StateText);
        }
        finally
        {
            GameStudioEditorSettings.PlayModeKeepLiveTabAfterExit.SetValue(true);
            await window.CloseAsync();
        }
    }

    [AvaloniaFact]
    public async Task ThePinKeepsTheGameWindowOnTopAndTheGameLearnsWhenTheEditorIsActive()
    {
        playMode.WindowOnTop = true;
        using var fixture = await ShellSessionFixture.OpenExecutableAsync(Platform.Type, playMode);
        fixture.ReplaceProjectBuilder();
        fixture.ReplaceDialogService();
        var window = new MainWindow { DataContext = fixture.Main };
        await window.ShowAsync();
        try
        {
            playMode.IsEditorActive = true;
            var game = await PlayAsync(window, fixture);
            Assert.Equal("1", game.Environment[PlayProtocol.WindowOnTopVariable]);
            Assert.Equal(new PlayEditorActive(true), await game.NextWindowMessageAsync());

            // The pin of the Live tab's toolbar, checked: a click lets the game's window go behind the editor
            var view = window.GetVisualDescendants().OfType<LiveView>().Single();
            var pin = view.GetVisualDescendants().OfType<ToggleButton>().Single(x => x.Name == "WindowOnTopButton");
            Assert.True(pin.IsChecked);
            pin.IsChecked = false;
            Assert.Equal(new PlayWindowOnTop(false), await game.NextWindowMessageAsync());
            Assert.False(playMode.WindowOnTop);
            Assert.Contains("Keep the game window on top", ToolTip.GetTip(pin)?.ToString());

            // The editor deactivated: the game is told
            playMode.IsEditorActive = false;
            Assert.Equal(new PlayEditorActive(false), await game.NextWindowMessageAsync());
        }
        finally
        {
            await window.CloseAsync();
        }
    }

    [AvaloniaFact]
    public async Task TheLiveTabsToolbarMenuKeysAndDropChangeTheGamesStructure()
    {
        using var fixture = await ShellSessionFixture.OpenExecutableAsync(Platform.Type, playMode);
        fixture.ReplaceProjectBuilder();
        fixture.ReplaceDialogService();
        var window = new MainWindow { DataContext = fixture.Main };
        await window.ShowAsync();
        try
        {
            var game = await PlayAsync(window, fixture);
            var view = window.GetVisualDescendants().OfType<LiveView>().Single();
            var list = view.GetVisualDescendants().OfType<ListBox>().Single(x => x.Name == "LiveTree");
            await WindowTestHelper.WaitUntilAsync(() => list.ItemCount == 3, "the rows of the tree");
            var viewModel = Assert.IsType<LivePanelViewModel>(view.DataContext);
            var (player, ground) = (viewModel.Tree.FindEntity(PlayerHandle)!, viewModel.Tree.FindEntity(4)!);

            // The toolbar: create is on, duplicate and delete need a selected entity
            var create = view.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "CreateEntityButton");
            var duplicate = view.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "DuplicateEntityButton");
            var delete = view.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "DeleteEntityButton");
            await WindowTestHelper.WaitUntilAsync(() => create.IsEffectivelyEnabled, "Create enabled");
            Assert.False(duplicate.IsEffectivelyEnabled);
            viewModel.SelectedItems.Add(ground);
            await WindowTestHelper.WaitUntilAsync(() => duplicate.IsEffectivelyEnabled && delete.IsEffectivelyEnabled, "Duplicate and Delete enabled");

            // Ctrl+D and Delete act on the running game while the tree has the focus (a row, as after a click)
            Assert.True(list.ContainerFromItem(ground)?.Focus());
            await WindowTestHelper.PumpAsync();
            Press(window, Key.D, RawInputModifiers.Control, PhysicalKey.D);
            var copy = await game.NextStructureEditAsync();
            Assert.Equal((PlayStructureOperation.DuplicateEntity, 4), (copy.Operation, copy.EntityHandle));
            Press(window, Key.Delete, RawInputModifiers.None, PhysicalKey.Delete);
            var deleted = await game.NextStructureEditAsync();
            Assert.Equal((PlayStructureOperation.DeleteEntity, 4), (deleted.Operation, deleted.EntityHandle));

            // The context menu has the scene editor's words
            var menu = list.ContextMenu!;
            var names = menu.Items.OfType<MenuItem>().Select(x => x.Name).ToList();
            Assert.Equal(["CreateEntityMenuItem", "SpawnPrefabMenuItem", "DuplicateEntityMenuItem", "DeleteEntityMenuItem", "ShowInSceneEditorMenuItem", "ApplyToAssetMenuItem", "CopyNameMenuItem", "CopyIdMenuItem"], names);
            Assert.Equal("Empty entity", menu.Items.OfType<MenuItem>().First().Header);

            // The ground dropped on the player goes under it; Alt keeps its place in the world
            Assert.True(view.Drop([ground], player, KeyModifiers.Alt));
            var move = await game.NextStructureEditAsync();
            Assert.Equal((PlayStructureOperation.MoveEntity, 4, PlayerHandle, true), (move.Operation, move.EntityHandle, move.ParentHandle, move.KeepWorldTransform));
            // Once the game moved it, its row is under the player and still selected, as in the scene editor
            game.Send(new PlayStructureResult(move.Id, 5, PlayEditStatus.Applied, [], Guid.Empty, null));
            game.Send(new PlayTreeBatch(5, [new PlayEntityMoved(4, PlayerHandle, 1)]));
            await WindowTestHelper.WaitUntilAsync(() => ground.Parent == player, "the ground under the player");
            Assert.Equal([ground], list.SelectedItems!.Cast<object>());
            // Not under its own child
            Assert.False(view.Drop([player], viewModel.Tree.FindEntity(3), KeyModifiers.None));
            // The camera moved to the root: the ground's row moves up in the list, and stays selected
            game.Send(new PlayTreeBatch(6, [new PlayEntityMoved(3, 0, 1)]));
            await WindowTestHelper.WaitUntilAsync(() => viewModel.Tree.FindEntity(3)!.Parent is LiveSceneItem, "the camera at the root");
            Assert.Equal(["MainScene", "Player", "Ground", "Camera"], viewModel.Tree.Rows.Select(x => x.Name));
            Assert.Equal([ground], list.SelectedItems!.Cast<object>());
            Assert.Equal([ground], viewModel.SelectedItems);

            // An entity the editor added is marked, and not offered to the scene editor
            game.Send(new PlayTreeBatch(7, [new PlayEntityAdded(new PlayEntityInfo(20, Guid.NewGuid(), "Entity", true, 0, 1, []))]));
            await WindowTestHelper.WaitUntilAsync(() => viewModel.Tree.FindEntity(20) is not null, "the new entity");
            Assert.False(viewModel.Tree.FindEntity(20)!.IsAddedWhilePlaying);
        }
        finally
        {
            await window.CloseAsync();
        }
    }

    [AvaloniaFact]
    public void ThePinAndTheSettingAreOne()
    {
        var saves = 0;
        var service = new PlayModeService(new PlaySessionOptions());
        try
        {
            GameStudioEditorSettings.FollowWindowOnTop(service, () => ++saves);
            // On by default
            Assert.True(service.WindowOnTop);

            // A click on the pin changes and saves the setting
            service.WindowOnTop = false;
            Assert.False(GameStudioEditorSettings.PlayModeGameWindowOnTop.GetValue());
            Assert.Equal(1, saves);
            Assert.Equal(PlayLaunchOptions.None with { Hotkey = new PlayHotkey("F7") }, GameStudioEditorSettings.GetPlayLaunchOptions());
        }
        finally
        {
            GameStudioEditorSettings.PlayModeGameWindowOnTop.SetValue(true);
            service.DisposeAsync().AsTask().Wait();
        }
    }

    [AvaloniaFact]
    public async Task CtrlF5StartsTheGameWithoutTheLiveConnection()
    {
        using var fixture = await ShellSessionFixture.OpenExecutableAsync(Platform.Type, playMode);
        fixture.ReplaceProjectBuilder();
        fixture.ReplaceDialogService();
        var window = new MainWindow { DataContext = fixture.Main };
        await window.ShowAsync();
        try
        {
            var item = MenuItem(window, "StartWithoutLiveViewMenuItem");
            Assert.Equal(GameStudioCommands.StartWithoutLiveViewId, Assert.IsType<CommandReference>(item.Command).Id);
            Assert.Contains(window.View.CommandSlots!.Shortcuts, x => x.Gesture == new KeyGesture(Key.F5, KeyModifiers.Control) && x.Descriptor.Id == GameStudioCommands.StartWithoutLiveViewId);

            fixture.Main.StartWithoutLiveViewCommand.Execute();
            await WindowTestHelper.WaitUntilAsync(() => Count(games) == 1 && !fixture.Main.Build.BuildInProgress, "the start of the game");

            // No pipe for the agent: the game runs as without play mode, and the status bar says so
            Assert.False(games[0].Environment.ContainsKey(PlayProtocol.PipeVariable));
            Assert.Equal(PlaySessionState.WithoutAgent, playMode.Session!.State);
            var status = window.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "PlayStatusText");
            await WindowTestHelper.WaitUntilAsync(() => status.Text == "Game: Not connected" && status.IsEffectivelyVisible, "the status bar");
            Assert.False(fixture.Main.Panels.FindDockable(LivePanel.Id)!.ViewModel.IsShown);
            // The Live tab, if the user opens it, says why there is no live view
            var live = (LivePanelViewModel)fixture.Main.Panels.FindDockable(LivePanel.Id)!.ViewModel;
            await WindowTestHelper.WaitUntilAsync(() => live.State == LiveTabState.NotConnected, "the Live tab not connected");
            Assert.Contains("Start without the live view", live.Message);
            await WindowTestHelper.WaitUntilAsync(() => fixture.Main.StopGameCommand.IsEnabled, "Stop game enabled");

            // The next F5 connects again: it asks to stop this game first (the recording dialogs answer OK)
            Press(window, Key.F5, RawInputModifiers.None, PhysicalKey.F5);
            await WindowTestHelper.WaitUntilAsync(() => Count(games) == 2, "the start of the second game", 10000);
            Assert.True(games[0].Killed);
            Assert.True(games[1].Environment.ContainsKey(PlayProtocol.PipeVariable));
            await WindowTestHelper.WaitUntilAsync(() => status.Text == "Game: Connecting", "the status bar");
        }
        finally
        {
            await window.CloseAsync();
        }
    }

    [AvaloniaFact]
    public async Task StartProjectPausedStartsTheGamePausedOnce()
    {
        using var fixture = await ShellSessionFixture.OpenExecutableAsync(Platform.Type, playMode);
        fixture.ReplaceProjectBuilder();
        fixture.ReplaceDialogService();
        var window = new MainWindow { DataContext = fixture.Main };
        await window.ShowAsync();
        try
        {
            var item = MenuItem(window, "StartPausedMenuItem");
            Assert.Equal(GameStudioCommands.StartPausedId, Assert.IsType<CommandReference>(item.Command).Id);
            Assert.DoesNotContain(window.View.CommandSlots!.Shortcuts, x => x.Descriptor.Id == GameStudioCommands.StartPausedId);

            fixture.Main.StartPausedCommand.Execute();
            await WindowTestHelper.WaitUntilAsync(() => Count(games) == 1 && !fixture.Main.Build.BuildInProgress, "the start of the game");
            Assert.Equal("1", games[0].Environment[PlayProtocol.StartPausedVariable]);
            Assert.True(games[0].Environment.ContainsKey(PlayProtocol.PipeVariable));
            Assert.False(playMode.NextStartPaused);

            // The next F5 starts as the settings say (the recording dialogs answer OK to stop this game)
            Press(window, Key.F5, RawInputModifiers.None, PhysicalKey.F5);
            await WindowTestHelper.WaitUntilAsync(() => Count(games) == 2, "the start of the second game", 10000);
            Assert.False(games[1].Environment.ContainsKey(PlayProtocol.StartPausedVariable));
        }
        finally
        {
            await window.CloseAsync();
        }
    }

    [AvaloniaFact]
    public async Task PlayThisSceneStartsTheGameOnTheActiveSceneOnce()
    {
        var main = new SceneAsset();
        var gameSettings = new GameSettingsAsset { DefaultScene = AttachedReferenceManager.CreateProxyObject<Scene>(main.Id, "MainScene") };
        using var fixture = await ShellSessionFixture.OpenExecutableAsync(Platform.Type, playMode, ("MainScene", main), ("Scenes/Level2", new SceneAsset()),
            (GameSettingsAsset.GameSettingsLocation, gameSettings), ("Sheet", new SpriteSheetAsset()));
        fixture.ReplaceProjectBuilder();
        var dialogs = fixture.ReplaceDialogService();
        var window = new MainWindow { DataContext = fixture.Main };
        await window.ShowAsync();
        try
        {
            var item = MenuItem(window, "PlaySceneMenuItem");
            Assert.Equal(GameStudioCommands.PlaySceneId, Assert.IsType<CommandReference>(item.Command).Id);
            Assert.Equal(new KeyGesture(Key.F8), item.InputGesture);
            Assert.Contains(window.View.CommandSlots!.Shortcuts, x => x.Gesture == new KeyGesture(Key.F8) && x.Descriptor.Id == GameStudioCommands.PlaySceneId);

            // The session opens on the default scene: it plays as with Start project
            await WindowTestHelper.WaitUntilAsync(() => fixture.Session.EditorCollection.ActiveEditor?.Asset == fixture.Asset("MainScene"), "the default scene's editor");
            await WindowTestHelper.PumpAsync();
            Assert.Null(fixture.Main.PlaySceneDisabledReason);
            Assert.True(fixture.Main.PlaySceneCommand.IsEnabled);
            var button = Button(window, "PlaySceneButton");
            Assert.True(button.IsVisible);
            Assert.Equal(GameStudioCommands.PlaySceneId, Assert.IsType<CommandReference>(button.Command).Id);
            fixture.Main.PlaySceneCommand.Execute();
            await WindowTestHelper.WaitUntilAsync(() => Count(games) == 1 && !fixture.Main.Build.BuildInProgress, "the start of the game");
            Assert.False(games[0].Environment.ContainsKey(PlayProtocol.SceneVariable));
            Assert.Empty(dialogs.Messages);

            // Another document than a scene: the reason says it
            await OpenEditorAsync(fixture, fixture.Asset("Sheet"));
            Assert.False(fixture.Main.PlaySceneCommand.IsEnabled);
            Assert.Contains("is not a scene", fixture.Main.PlaySceneDisabledReason, StringComparison.Ordinal);

            // A scene that the build does not have: it asks to make it a root asset, then the game starts on it (the
            // recording dialogs answer OK, also to stop the game that plays)
            var level2 = fixture.Asset("Scenes/Level2");
            await OpenEditorAsync(fixture, level2);
            Assert.True(fixture.Main.PlaySceneCommand.IsEnabled);
            Assert.False(level2.Dependencies.IsRoot);
            fixture.Main.PlaySceneCommand.Execute();
            await WindowTestHelper.WaitUntilAsync(() => Count(games) == 2 && !fixture.Main.Build.BuildInProgress, "the start of the game on Level2", 10000);
            Assert.True(level2.Dependencies.IsRoot);
            Assert.Contains(dialogs.Messages, x => x.Contains("Level2 is not in the build", StringComparison.Ordinal));
            Assert.Equal(level2.Url, games[1].Environment[PlayProtocol.SceneVariable]);
            Assert.Null(playMode.NextStartScene);
            Assert.Equal("Level2", playMode.Session?.Scene?.Name);

            // In the build now: it plays without a question
            var questions = dialogs.Messages.Count(x => x.Contains("is not in the build", StringComparison.Ordinal));
            fixture.Main.PlaySceneCommand.Execute();
            await WindowTestHelper.WaitUntilAsync(() => Count(games) == 3 && !fixture.Main.Build.BuildInProgress, "the second start on Level2", 10000);
            Assert.Equal(questions, dialogs.Messages.Count(x => x.Contains("is not in the build", StringComparison.Ordinal)));
            Assert.Equal(level2.Url, games[2].Environment[PlayProtocol.SceneVariable]);

            // The next F5 starts the default scene
            Press(window, Key.F5, RawInputModifiers.None, PhysicalKey.F5);
            await WindowTestHelper.WaitUntilAsync(() => Count(games) == 4, "the start of the game with F5", 10000);
            Assert.False(games[3].Environment.ContainsKey(PlayProtocol.SceneVariable));
        }
        finally
        {
            await window.CloseAsync();
        }
    }

    [AvaloniaFact]
    public async Task PlayThisSceneIsDisabledWithAReasonWithoutAScene()
    {
        using var fixture = await ShellSessionFixture.OpenExecutableAsync(Platform.Type, playMode, ("Sheet", new SpriteSheetAsset()));
        var window = new MainWindow { DataContext = fixture.Main };
        await window.ShowAsync();
        try
        {
            Assert.False(fixture.Main.PlaySceneCommand.IsEnabled);
            Assert.Equal("No scene is open: open a scene to play it.", fixture.Main.PlaySceneDisabledReason);
            Assert.False(await IsEnabledInOpenMenuAsync(MenuItem(window, "PlaySceneMenuItem")));

            // The palette shows it greyed, with its reason
            window.View.ShowCommandPalette(null);
            await WindowTestHelper.PumpAsync();
            var palette = Assert.IsType<CommandPaletteViewModel>(window.View.CommandPalette.DataContext);
            palette.SearchText = "play this scene";
            var entry = palette.Results.First();
            Assert.Equal(GameStudioCommands.PlaySceneId, entry.Id);
            Assert.False(entry.IsEnabled);
            Assert.Equal(fixture.Main.PlaySceneDisabledReason, entry.DisabledReason);
            Assert.Equal("F8", entry.Gesture);
        }
        finally
        {
            await window.CloseAsync();
        }
    }

    [AvaloniaFact]
    public void TheSettingsGiveTheLaunchOptions()
    {
        Assert.Equal(PlayLaunchOptions.None with { Hotkey = new PlayHotkey("F7"), WindowOnTop = true }, GameStudioEditorSettings.GetPlayLaunchOptions());
        Assert.All([GameStudioEditorSettings.PlayModeStartPaused, GameStudioEditorSettings.PlayModePauseOnScriptException],
            x => Assert.StartsWith(GameStudioEditorSettings.PlayMode + "/", x.DisplayName.ToString()));
        Assert.StartsWith(GameStudioEditorSettings.PlayMode + "/", GameStudioEditorSettings.PlayModeGameHotkey.DisplayName.ToString());
        try
        {
            GameStudioEditorSettings.PlayModeStartPaused.SetValue(true);
            GameStudioEditorSettings.PlayModePauseOnScriptException.SetValue(true);
            GameStudioEditorSettings.PlayModeGameHotkey.SetValue("ctrl+shift+F8");
            Assert.Equal(new PlayLaunchOptions(true, true, new PlayHotkey("F8", PlayHotkeyModifiers.Ctrl | PlayHotkeyModifiers.Shift), WindowOnTop: true), GameStudioEditorSettings.GetPlayLaunchOptions());

            // "None" (or nothing) turns the key off
            GameStudioEditorSettings.PlayModeGameHotkey.SetValue("None");
            Assert.Null(GameStudioEditorSettings.GetPlayLaunchOptions().Hotkey);
            GameStudioEditorSettings.PlayModeGameHotkey.SetValue("");
            Assert.Null(GameStudioEditorSettings.GetPlayLaunchOptions().Hotkey);
        }
        finally
        {
            GameStudioEditorSettings.PlayModeStartPaused.SetValue(false);
            GameStudioEditorSettings.PlayModePauseOnScriptException.SetValue(false);
            GameStudioEditorSettings.PlayModeGameHotkey.SetValue("F7");
        }
    }

    [AvaloniaFact]
    public async Task TheSettingOffStartsTheGameWithoutTheLiveConnection()
    {
        playMode.ConnectToGame = () => false;
        using var fixture = await ShellSessionFixture.OpenExecutableAsync(Platform.Type, playMode);
        fixture.ReplaceProjectBuilder();
        var window = new MainWindow { DataContext = fixture.Main };
        await window.ShowAsync();
        try
        {
            Press(window, Key.F5, RawInputModifiers.None, PhysicalKey.F5);
            await WindowTestHelper.WaitUntilAsync(() => Count(games) == 1 && !fixture.Main.Build.BuildInProgress, "the start of the game");

            Assert.False(games[0].Environment.ContainsKey(PlayProtocol.PipeVariable));
            Assert.Equal(PlaySessionState.WithoutAgent, playMode.Session!.State);
            Assert.Contains("Connect to the game when starting the project", playMode.Session.StateMessage);
        }
        finally
        {
            await window.CloseAsync();
        }
    }

    [AvaloniaFact]
    public async Task F7PausesF10StepsAndTheLiveTabAndTheStatusBarSayPaused()
    {
        using var fixture = await ShellSessionFixture.OpenExecutableAsync(Platform.Type, playMode);
        fixture.ReplaceProjectBuilder();
        fixture.ReplaceDialogService();
        var window = new MainWindow { DataContext = fixture.Main };
        await window.ShowAsync();
        try
        {
            // The Project menu has both, with their shortcuts; no game plays
            var pause = MenuItem(window, "PauseGameMenuItem");
            var step = MenuItem(window, "StepGameMenuItem");
            Assert.Equal(new KeyGesture(Key.F7), pause.InputGesture);
            Assert.Equal(new KeyGesture(Key.F10), step.InputGesture);
            Assert.Equal(GameStudioCommands.PauseGameId, Assert.IsType<CommandReference>(pause.Command).Id);
            Assert.Equal(GameStudioCommands.StepGameId, Assert.IsType<CommandReference>(step.Command).Id);
            Assert.Contains(window.View.CommandSlots!.Shortcuts, x => x.Gesture == new KeyGesture(Key.F7) && x.Descriptor.Id == GameStudioCommands.PauseGameId);
            Assert.Contains(window.View.CommandSlots!.Shortcuts, x => x.Gesture == new KeyGesture(Key.F10) && x.Descriptor.Id == GameStudioCommands.StepGameId);
            Assert.False(await IsEnabledInOpenMenuAsync(pause));
            Assert.False(await IsEnabledInOpenMenuAsync(step));

            var game = await PlayAsync(window, fixture);
            game.Send(GameState(paused: false, timeScale: 1));
            var controls = fixture.Main.PlayControls!;
            await WindowTestHelper.WaitUntilAsync(() => controls.CanControl && controls.FramesPerSecond == 60, "the controls enabled");
            var view = window.GetVisualDescendants().OfType<LiveView>().Single();
            var live = Assert.IsType<LivePanelViewModel>(view.DataContext);
            var fps = view.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "FramesPerSecond");
            await WindowTestHelper.WaitUntilAsync(() => fps.Text == "60 fps" && fps.IsEffectivelyVisible, "the frame rate");
            Assert.True(await IsEnabledInOpenMenuAsync(pause));

            // F7 pauses: the game says it is paused, the Live tab and the status bar say it
            Press(window, Key.F7, RawInputModifiers.None, PhysicalKey.F7);
            Assert.IsType<PlayPause>(await game.NextControlAsync());
            game.Send(GameState(paused: true, timeScale: 1));
            await WindowTestHelper.WaitUntilAsync(() => live.State == LiveTabState.Paused, "the Live tab paused");
            Assert.Equal("Paused", view.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "StateText").Text);
            var status = window.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "PlayStatus");
            await WindowTestHelper.WaitUntilAsync(() => status.Classes.Contains("paused"), "the status bar paused");
            Assert.Equal("Game: Paused", window.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "PlayStatusText").Text);
            Assert.Equal(Color.Parse("#FF2B6CB0"), Assert.IsAssignableFrom<ISolidColorBrush>(status.Background).Color);
            Assert.Equal("Resume", controls.PauseText);

            // F10 and the Step button step one frame each
            Press(window, Key.F10, RawInputModifiers.None, PhysicalKey.F10);
            Assert.Equal(1, Assert.IsType<PlayStep>(await game.NextControlAsync()).Frames);
            Button(window, "StepButton").Command!.Execute(null);
            Assert.Equal(1, Assert.IsType<PlayStep>(await game.NextControlAsync()).Frames);

            // The time scale goes to the game; the game's state does not send it back
            var scale = view.GetVisualDescendants().OfType<ComboBox>().Single(x => x.Name == "TimeScale");
            Assert.Equal(1.0, scale.SelectedItem);
            scale.SelectedItem = 0.25;
            Assert.Equal(0.25, Assert.IsType<PlayTimeScale>(await game.NextControlAsync()).Scale);
            game.Send(GameState(paused: true, timeScale: 0.25));

            // F7 again resumes
            Press(window, Key.F7, RawInputModifiers.None, PhysicalKey.F7);
            Assert.IsType<PlayResume>(await game.NextControlAsync());
            game.Send(GameState(paused: false, timeScale: 0.25));
            await WindowTestHelper.WaitUntilAsync(() => live.State == LiveTabState.Playing && status.Classes.Contains("playing"), "playing again");
            Assert.Equal(0.25, controls.TimeScale);
            Assert.True(game.Controls.IsEmpty);

            // Stopped: nothing to pause
            game.ExitOnQuit = true;
            Press(window, Key.F5, RawInputModifiers.Shift, PhysicalKey.F5);
            await WindowTestHelper.WaitUntilAsync(() => live.State == LiveTabState.Stopped, "the stop of the game");
            await WindowTestHelper.WaitUntilAsync(() => !controls.CanControl, "the controls disabled");
            Assert.False(await IsEnabledInOpenMenuAsync(pause));
        }
        finally
        {
            await window.CloseAsync();
        }
    }

    /// <summary>
    /// F5, then the game connects as its agent does and sends its tree.
    /// </summary>
    private async Task<TestGame> PlayAsync(MainWindow window, ShellSessionFixture fixture)
    {
        Press(window, Key.F5, RawInputModifiers.None, PhysicalKey.F5);
        await WindowTestHelper.WaitUntilAsync(() => Count(games) == 1 && !fixture.Main.Build.BuildInProgress, "the start of the game");
        var game = games[0];
        await game.ConnectAsync();
        game.Send(new PlayHello(game.Id, Version, Version, null, []));
        game.Send(Tree());
        await WindowTestHelper.WaitUntilAsync(() => playMode.Session?.State == PlaySessionState.Connected, "the game connected");
        return game;
    }

    /// <summary>
    /// A snapshot of the player, as the agent writes it.
    /// </summary>
    private static PlaySnapshot Snapshot(long frame)
    {
        var player = new Entity("Player");
        player.Transform.Position = new Stride.Core.Mathematics.Vector3(frame, 0, 0);
        return new PlaySnapshot(frame, frame / 60.0, [PlaySnapshotWriter.Write(player, x => x == player ? PlayerHandle : 0)]);
    }

    /// <summary>
    /// The state the agent sends: 60 frames a second, the three entities of <see cref="Tree"/>.
    /// </summary>
    private static PlayGameState GameState(bool paused, double timeScale)
        => new(120, 100, paused, timeScale, 1, 60, 3, 0);

    private static PlayTree Tree()
    {
        const string transform = "Stride.Engine.TransformComponent, Stride.Engine";
        return new PlayTree(1,
            [new PlaySceneInfo(1, Guid.NewGuid(), "MainScene", "MainScene", 0)],
            [
                new PlayEntityInfo(PlayerHandle, Guid.NewGuid(), "Player", true, 0, 1, [new PlayComponentInfo(Guid.NewGuid(), transform)]),
                new PlayEntityInfo(3, Guid.NewGuid(), "Camera", true, 2, 1, [new PlayComponentInfo(Guid.NewGuid(), "Stride.Engine.CameraComponent, Stride.Engine")]),
                new PlayEntityInfo(4, Guid.NewGuid(), "Ground", true, 0, 1, [new PlayComponentInfo(Guid.NewGuid(), transform), new PlayComponentInfo(Guid.NewGuid(), "Stride.Engine.ModelComponent, Stride.Engine")]),
            ]);
    }

    private static int Count(List<TestGame> games)
    {
        lock (games)
            return games.Count;
    }

    private static void Press(Window window, Key key, RawInputModifiers modifiers, PhysicalKey physicalKey)
    {
        window.KeyPress(key, modifiers, physicalKey, null);
        window.KeyRelease(key, modifiers, physicalKey, null);
    }

    private static async Task OpenEditorAsync(ShellSessionFixture fixture, Stride.Core.Assets.Presentation.ViewModels.AssetViewModel asset)
    {
        fixture.Session.AssetCollection.SelectAssets([asset]);
        await WindowTestHelper.PumpAsync();
        fixture.Session.EditSelectedContentCommand.Execute();
        await WindowTestHelper.WaitUntilAsync(() => fixture.Session.EditorCollection.ActiveEditor?.Asset == asset, "the editor of " + asset.Name);
        await WindowTestHelper.PumpAsync();
    }

    private static MenuItem MenuItem(Window window, string name) => window.GetLogicalDescendants().OfType<MenuItem>().Single(x => x.Name == name);

    private static Button Button(Window window, string name) => window.GetVisualDescendants().OfType<Button>().Single(x => x.Name == name);

    /// <summary>
    /// Whether the item is enabled when the user opens its menu: Avalonia evaluates the command of an item of a closed menu
    /// only when the menu opens.
    /// </summary>
    private static async Task<bool> IsEnabledInOpenMenuAsync(MenuItem item)
    {
        var menu = Assert.IsType<MenuItem>(item.Parent);
        menu.Open();
        await WindowTestHelper.PumpAsync();
        try
        {
            return item.IsEffectivelyEnabled;
        }
        finally
        {
            menu.Close();
            await WindowTestHelper.PumpAsync();
        }
    }

    /// <summary>
    /// A game's process that the test drives: it connects to the session's pipe as the agent does, and ends by hand.
    /// </summary>
    private sealed class TestGame(IReadOnlyDictionary<string, string> environment) : IPlayGameProcess, IAsyncDisposable
    {
        private readonly TaskCompletionSource<int> exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource quit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentQueue<PlaySubscribe> subscribes = new();
        private PlayConnection? connection;

        public int Id => 4321;

        public IReadOnlyDictionary<string, string> Environment => environment;

        public Task<int> Exited => exited.Task;

        /// <summary>
        /// Completes when the editor asks the game to quit.
        /// </summary>
        public Task Quit => quit.Task;

        public bool ExitOnQuit { get; set; }

        public bool Killed { get; private set; }

        public async Task ConnectAsync()
        {
            var pipe = new NamedPipeClientStream(".", environment[PlayProtocol.PipeVariable], PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            connection = new PlayConnection(pipe);
            connection.Start(message =>
            {
                if (message is PlaySubscribe subscribe)
                    subscribes.Enqueue(subscribe);
                if (message is PlayPause or PlayResume or PlayStep or PlayTimeScale)
                    Controls.Enqueue(message);
                if (message is PlayWindowOnTop or PlayEditorActive)
                    WindowMessages.Enqueue(message);
                if (message is PlayStructureEdit command)
                    StructureEdits.Enqueue(command);
                if (message is not PlayQuit)
                    return;
                quit.TrySetResult();
                if (ExitOnQuit)
                    Exit(0);
            });
        }

        public void Send(PlayMessage message) => Assert.True(connection?.Send(message));

        /// <summary>
        /// The next request for snapshots the editor sent.
        /// </summary>
        public async Task<PlaySubscribe> NextSubscribeAsync()
        {
            PlaySubscribe? next = null;
            await WindowTestHelper.WaitUntilAsync(() => subscribes.TryDequeue(out next), "a request for snapshots");
            return next!;
        }

        /// <summary>
        /// The pause, resume, step and time scale messages the editor sent, not read yet.
        /// </summary>
        public ConcurrentQueue<PlayMessage> Controls { get; } = new();

        /// <summary>
        /// The next pause, resume, step or time scale message the editor sent.
        /// </summary>
        public async Task<PlayMessage> NextControlAsync()
        {
            PlayMessage? next = null;
            await WindowTestHelper.WaitUntilAsync(() => Controls.TryDequeue(out next), "a pause, resume, step or time scale message");
            return next!;
        }

        /// <summary>
        /// The structure commands the editor sent, not read yet.
        /// </summary>
        public ConcurrentQueue<PlayStructureEdit> StructureEdits { get; } = new();

        /// <summary>
        /// The next structure command the editor sent.
        /// </summary>
        public async Task<PlayStructureEdit> NextStructureEditAsync()
        {
            PlayStructureEdit? next = null;
            await WindowTestHelper.WaitUntilAsync(() => StructureEdits.TryDequeue(out next), "a structure command");
            return next!;
        }

        /// <summary>
        /// The window on top and editor activation messages the editor sent, not read yet.
        /// </summary>
        public ConcurrentQueue<PlayMessage> WindowMessages { get; } = new();

        /// <summary>
        /// The next window on top or editor activation message the editor sent.
        /// </summary>
        public async Task<PlayMessage> NextWindowMessageAsync()
        {
            PlayMessage? next = null;
            await WindowTestHelper.WaitUntilAsync(() => WindowMessages.TryDequeue(out next), "a window on top or editor activation message");
            return next!;
        }

        public void Exit(int exitCode) => exited.TrySetResult(exitCode);

        public void Kill()
        {
            Killed = true;
            Exit(-1);
        }

        public async ValueTask DisposeAsync()
        {
            Exit(-1);
            if (connection is not null)
                await connection.DisposeAsync();
        }
    }
}
