// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Concurrent;
using Stride.Core.Assets.Editor.Components.Build;
using Stride.Core.Assets.Editor.Panels;
using Stride.Core.Assets.Editor.Tests.Helpers;
using Stride.Core.Diagnostics;
using Stride.Core.Presentation.Commands;
using Stride.Core.Presentation.Services;
using Stride.Core.Presentation.ViewModels;
using Stride.Editor.PlayMode;
using Stride.PlayMode.Protocol;
using static Stride.Editor.Tests.PlayMode.LiveTreeTests;

namespace Stride.Editor.Tests.PlayMode;

/// <summary>
/// Tests the Live tab (<see cref="LivePanelViewModel"/>) and the play mode service it follows, with a fake transport, agent
/// and game: the tree and its batches, the rate of the updates, the states, the selection handed to its view, a second start
/// while a game plays, the notices (not responding, stale code), and the status bar's state.
/// </summary>
public sealed class LivePanelTests : IAsyncLifetime
{
    private const string Version = "4.5.4-dev";
    private static readonly TimeSpan Timeout = PlaySessionTests.Timeout;

    private readonly string directory = Path.Combine(Path.GetTempPath(), $"stride-live-tests-{Guid.NewGuid():N}");
    private readonly ConcurrentQueue<FakePlayTransport> transports = new();
    private readonly ConcurrentQueue<FakeGame> games = new();
    private readonly List<FakeAgent> agents = [];
    private readonly ExclusiveDispatcherService dispatcher = new();
    private readonly FakeClipboardService clipboard = new();
    private readonly FakeSceneAssets sceneAssets = new();
    private readonly FakeSelectionView selectionView = new();
    private readonly ICommandBase reloadCode = new AnonymousCommand(ViewModelServiceProvider.NullServiceProvider, () => { });
    private PlayModeService playMode = null!;
    private LivePanelViewModel panel = null!;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(directory);
        foreach (var file in PlayAgentFiles.FileNames)
            File.WriteAllBytes(Path.Combine(directory, file), []);
        playMode = CreatePlayMode(DefaultOptions());
        panel = CreatePanel();
        return Task.CompletedTask;
    }

    private static PlaySessionOptions DefaultOptions() => new()
    {
        ConnectTimeout = Timeout,
        StopGracePeriod = TimeSpan.FromMilliseconds(200),
        ExitAfterDisconnectDelay = TimeSpan.FromMilliseconds(200),
        // Longer than any test: a fake agent sends no heartbeat
        NotRespondingAfter = TimeSpan.FromMinutes(5),
        Log = new LoggerResult(),
    };

    /// <summary>
    /// Replaces the service and the tab by ones whose sessions have other options.
    /// </summary>
    private async Task RecreateAsync(PlaySessionOptions options)
    {
        dispatcher.Invoke(panel.Destroy);
        await panel.WhenIdleAsync();
        await playMode.DisposeAsync();
        playMode = CreatePlayMode(options);
        panel = CreatePanel();
    }

    private PlayModeService CreatePlayMode(PlaySessionOptions options)
    {
        return new PlayModeService(() =>
        {
            var transport = new FakePlayTransport();
            transports.Enqueue(transport);
            return transport;
        }, new PlayAgentFiles(directory, Version), options, (_, _, environment) =>
        {
            var game = new FakeGame(environment);
            games.Enqueue(game);
            return game;
        });
    }

    public async Task DisposeAsync()
    {
        dispatcher.Invoke(panel.Destroy);
        await panel.WhenIdleAsync();
        await playMode.DisposeAsync();
        foreach (var agent in agents)
            await agent.DisposeAsync();
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task TheTabShowsTheStatesOfTheGame()
    {
        Assert.Equal(LiveTabState.NotPlaying, Read(() => panel.State));
        Assert.Equal("Not playing", Read(() => panel.StateText));
        Assert.NotNull(Read(() => panel.Message));

        var game = Start();
        await WaitAsync(() => panel.State == LiveTabState.Connecting, "connecting");
        Assert.Equal("Connecting", Read(() => panel.StateText));
        Assert.True(playMode.IsPlaying);

        await ConnectAsync();
        await WaitAsync(() => panel.State == LiveTabState.Playing, "playing");
        Assert.Equal("Playing", Read(() => panel.StateText));
        Assert.True(Read(() => panel.IsLive));
        Assert.Null(Read(() => panel.Message));

        game.Exit(3);
        await WaitAsync(() => panel.State == LiveTabState.Stopped, "stopped");
        Assert.Equal("Stopped (exit code 3)", Read(() => panel.StateText));
        Assert.False(Read(() => panel.IsLive));
        Assert.False(playMode.IsPlaying);
    }

    [Fact]
    public async Task AGameWithoutTheLiveConnectionSaysWhy()
    {
        Start();
        await ConnectAsync(new PlayHello(1234, "4.4.0", Version, "MyGame.Game", []));

        await WaitAsync(() => panel.State == LiveTabState.NotConnected, "not connected");
        Assert.Equal("Not connected", Read(() => panel.StateText));
        Assert.Contains("4.4.0", Read(() => panel.Message));
    }

    [Fact]
    public async Task TheTreeFollowsTheGame()
    {
        Start();
        var agent = await ConnectAsync();

        agent.Send(Tree());
        await WaitAsync(() => panel.EntityCount == 3, "the tree");
        Assert.Equal(["MainScene", "Player", "Ground"], Read(() => panel.Tree.Rows.Select(x => x.Name).ToList()));

        agent.Send(new PlayTreeBatch(2, [new PlayEntityAdded(Entity(5, BulletId, "Bullet", 0, Model))]));
        agent.Send(new PlayTreeBatch(3, [new PlayEntityStateChanged(4, "Floor", false)]));
        await WaitAsync(() => panel.EntityCount == 4 && panel.Tree.FindEntity(4)!.Name == "Floor", "the batches");
        Assert.Equal(["MainScene", "Player", "Floor", "Bullet"], Read(() => panel.Tree.Rows.Select(x => x.Name).ToList()));
        Assert.False(Read(() => panel.Tree.FindEntity(4)!.IsEnabled));
        // The scene asset has no bullet
        Assert.True(Read(() => panel.Tree.FindEntity(5)!.IsSpawned));
        Assert.False(Read(() => panel.Tree.FindEntity(2)!.IsSpawned));

        // Once the game exits, the last tree stays
        games.Single().Exit(0);
        await WaitAsync(() => panel.State == LiveTabState.Stopped, "stopped");
        Assert.Equal(4, Read(() => panel.Tree.Rows.Count));
    }

    [Fact]
    public async Task TheBatchesAreAppliedAtMostTenTimesASecond()
    {
        Start();
        var agent = await ConnectAsync();
        agent.Send(Tree());
        await WaitAsync(() => panel.EntityCount == 3, "the tree");
        var before = Read(() => panel.ApplyCount);

        // 60 batches, one every 10 ms
        var started = DateTime.UtcNow;
        for (var i = 0; i < 60; ++i)
        {
            agent.Send(new PlayTreeBatch(10 + i, [new PlayEntityAdded(Entity(100 + i, Guid.NewGuid(), $"Bullet{i}", 0))]));
            await Task.Delay(10);
        }
        await WaitAsync(() => panel.EntityCount == 63, "every batch");
        var elapsed = DateTime.UtcNow - started;

        // At most one update every 100 ms, the first one at once
        var applies = Read(() => panel.ApplyCount) - before;
        var allowed = (int)(elapsed.TotalMilliseconds / LivePanelViewModel.MinimumApplyInterval.TotalMilliseconds) + 1;
        Assert.InRange(applies, 2, allowed);
        Assert.Equal(63, Read(() => panel.Tree.Rows.Count));
    }

    [Fact]
    public async Task SelectingEntitiesHandsThemToTheView()
    {
        Start();
        var agent = await ConnectAsync();
        agent.Send(Tree());
        await WaitAsync(() => panel.EntityCount == 3, "the tree");

        dispatcher.Invoke(() =>
        {
            panel.SelectedItems.Add(panel.Tree.FindEntity(2)!);
            panel.SelectedItems.Add(panel.Tree.FindEntity(4)!);
        });

        var selection = Read(() => panel.Selection);
        Assert.NotNull(selection);
        Assert.Same(playMode.Session, selection.Session);
        Assert.Equal([2, 4], selection.Handles);
        // The view shows it (and asks the game for its snapshots)
        Assert.Same(selection, Read(() => selectionView.Shown.Last()));

        // A scene row is not an entity: nothing changes
        var shown = Read(() => selectionView.Shown.Count);
        dispatcher.Invoke(() => panel.SelectedItems.Add(panel.Tree.Roots[0]));
        Assert.Equal([2, 4], Read(() => panel.Selection!.Handles));
        Assert.Equal(shown, Read(() => selectionView.Shown.Count));

        // Coming back to the tab shows the same selection again
        dispatcher.Invoke(() => panel.ShowSelectionCommand.Execute());
        Assert.Same(selection, Read(() => selectionView.Shown.Last()));

        dispatcher.Invoke(panel.SelectedItems.Clear);
        Assert.Null(Read(() => panel.Selection));
        Assert.Null(Read(() => selectionView.Shown.Last()));
    }

    [Fact]
    public async Task ADestroyedSelectedEntityLeavesTheSelection()
    {
        Start();
        var agent = await ConnectAsync();
        agent.Send(Tree());
        await WaitAsync(() => panel.EntityCount == 3, "the tree");
        dispatcher.Invoke(() =>
        {
            panel.Tree.FindEntity(2)!.IsExpanded = true;
            panel.SelectedItems.Add(panel.Tree.FindEntity(3)!);
            panel.SelectedItems.Add(panel.Tree.FindEntity(4)!);
        });
        Assert.Equal([3, 4], Read(() => panel.Selection!.Handles));

        // The player goes with its camera
        agent.Send(new PlayTreeBatch(5, [new PlayEntityRemoved(2)]));

        await WaitAsync(() => panel.Selection!.Handles.SequenceEqual([4]), "the selection without the camera");
        Assert.Equal([4], Read(() => selectionView.Shown.Last()!.Handles));
        Assert.Equal(["Ground"], Read(() => panel.SelectedItems.OfType<LiveEntityItem>().Select(x => x.Name).ToList()));
    }

    [Fact]
    public async Task StoppingKeepsTheTreeAndEndsTheSelection()
    {
        Start();
        var agent = await ConnectAsync();
        agent.Send(Tree());
        await WaitAsync(() => panel.EntityCount == 3, "the tree");
        dispatcher.Invoke(() => panel.SelectedItems.Add(panel.Tree.FindEntity(2)!));
        Assert.NotNull(Read(() => panel.Selection));

        // Stop asks the game to quit; this one does not, so its process is ended
        await playMode.StopAsync();

        Assert.IsType<PlayQuit>(await agent.ReceiveAsync<PlayQuit>());
        Assert.True(games.Single().Killed);
        await WaitAsync(() => panel.State == LiveTabState.Stopped, "stopped");
        Assert.Null(Read(() => panel.Selection));
        // The view gives its place back
        Assert.Null(Read(() => selectionView.Shown.Last()));
        Assert.Equal(3, Read(() => panel.Tree.Rows.Count));
        Assert.Equal("Stopped (exit code -1)", Read(() => panel.StateText));
        // The rows are no longer selected, and the tab says the tree is the last one
        Assert.Empty(Read(() => panel.SelectedItems.ToList()));
        Assert.Contains("The game has stopped", Read(() => panel.StoppedNotice));
    }

    [Fact]
    public async Task ARowSelectedAfterTheStopIsAStoppedSelection()
    {
        Start();
        var agent = await ConnectAsync();
        agent.Send(Tree());
        await WaitAsync(() => panel.EntityCount == 3, "the tree");
        Assert.Null(Read(() => panel.StoppedNotice));

        games.Single().Exit(0);
        await WaitAsync(() => panel.State == LiveTabState.Stopped, "stopped");

        // The last tree's rows can be selected: the view learns that the game stopped, with the names
        dispatcher.Invoke(() => panel.SelectedItems.Add(panel.Tree.FindEntity(2)!));
        var selection = Read(() => panel.Selection);
        Assert.NotNull(selection);
        Assert.True(selection.IsStopped);
        Assert.Equal([2], selection.Handles);
        Assert.Equal(["Player"], selection.Names);
        Assert.Same(selection, Read(() => selectionView.Shown.Last()));

        dispatcher.Invoke(panel.SelectedItems.Clear);
        Assert.Null(Read(() => selectionView.Shown.Last()));

        // A new game: the notice goes
        Start();
        await WaitAsync(() => panel.State == LiveTabState.Connecting, "the next game");
        Assert.Null(Read(() => panel.StoppedNotice));
    }

    [Fact]
    public async Task AGameThatExitsBeforeItsTreeHasNoStoppedNotice()
    {
        Start();
        await ConnectAsync();

        games.Single().Exit(0);
        await WaitAsync(() => panel.State == LiveTabState.Stopped, "stopped");

        Assert.Null(Read(() => panel.StoppedNotice));
    }

    [Fact]
    public async Task ASecondStartEndsTheFirstGameAndShowsTheNewOne()
    {
        var first = Start();
        var firstAgent = await ConnectAsync();
        firstAgent.Send(Tree());
        await WaitAsync(() => panel.EntityCount == 3, "the first tree");
        var firstSession = playMode.Session;

        var second = Start();

        // The first game is asked to quit, then ended
        Assert.IsType<PlayQuit>(await firstAgent.ReceiveAsync<PlayQuit>());
        await first.Exited.WaitAsync(Timeout);
        Assert.NotSame(firstSession, playMode.Session);
        await WaitAsync(() => panel.State == LiveTabState.Connecting && panel.EntityCount == 0, "the new session");
        Assert.Empty(Read(() => panel.Tree.Rows));

        var secondAgent = await ConnectAsync();
        secondAgent.Send(new PlayTree(1, [new PlaySceneInfo(1, SceneId, "MainScene", "MainScene", 0)], [Entity(2, PlayerId, "Player", 0, Transform)]));
        await WaitAsync(() => panel.State == LiveTabState.Playing && panel.EntityCount == 1, "the second tree");
        Assert.False(second.Exited.IsCompleted);
    }

    [Fact]
    public async Task TheTabOpenedWhileAGamePlaysShowsIt()
    {
        Start();
        var agent = await ConnectAsync();
        agent.Send(Tree());
        await WaitUntilAsync(() => playMode.Session!.Tree is not null, "the session's tree");

        var late = CreatePanel();
        try
        {
            await WaitAsync(() => late.State == LiveTabState.Playing && late.EntityCount == 3, "the late tab");
        }
        finally
        {
            dispatcher.Invoke(late.Destroy);
            await late.WhenIdleAsync();
        }
    }

    [Fact]
    public async Task TheContextMenuCopiesAndShowsInTheSceneEditor()
    {
        Start();
        var agent = await ConnectAsync();
        agent.Send(Tree());
        agent.Send(new PlayTreeBatch(2, [new PlayEntityAdded(Entity(5, BulletId, "Bullet", 0, Model))]));
        await WaitAsync(() => panel.EntityCount == 4, "the tree");
        var player = Read(() => panel.Tree.FindEntity(2)!);
        var bullet = Read(() => panel.Tree.FindEntity(5)!);

        // The clipboard and the scene assets of the test answer at once
        dispatcher.Invoke(() => panel.CopyNameCommand.Execute(player));
        Assert.Equal("Player", clipboard.Text);
        dispatcher.Invoke(() => panel.CopyIdCommand.Execute(player));
        Assert.Equal(PlayerId.ToString(), clipboard.Text);

        // Only an entity with a counterpart in a scene asset can be shown in its scene editor
        Assert.True(Read(() => panel.ShowInSceneEditorCommand.CanExecute(player)));
        Assert.False(Read(() => panel.ShowInSceneEditorCommand.CanExecute(bullet)));
        dispatcher.Invoke(() => panel.ShowInSceneEditorCommand.Execute(player));
        Assert.Equal((SceneId, PlayerId), Assert.Single(sceneAssets.Shown));
    }

    [Fact]
    public async Task TheSearchFiltersTheRows()
    {
        Start();
        var agent = await ConnectAsync();
        agent.Send(Tree());
        await WaitAsync(() => panel.EntityCount == 3, "the tree");

        dispatcher.Invoke(() => panel.SearchText = "GROUND");

        Assert.Equal(["MainScene", "Ground"], Read(() => panel.Tree.Rows.Select(x => x.Name).ToList()));
    }

    [Fact]
    public async Task AGameThatStopsAnsweringIsNotRespondingUntilWaitOrEndGame()
    {
        await RecreateAsync(DefaultOptions() with { NotRespondingAfter = TimeSpan.FromMilliseconds(300) });
        var game = Start();
        var agent = await ConnectAsync();
        agent.Send(Tree());
        await WaitAsync(() => panel.EntityCount == 3, "the tree");

        // Nothing comes for a while: a busy game, or a breakpoint
        await WaitAsync(() => panel.State == LiveTabState.NotResponding, "not responding");
        Assert.Equal("Not responding", Read(() => panel.StateText));
        Assert.True(Read(() => panel.ShowsNotResponding));
        Assert.True(Read(() => panel.IsLive));

        // Wait hides the notice; the game answers again, then stops answering again: the notice comes back
        dispatcher.Invoke(() => panel.WaitCommand.Execute());
        Assert.False(Read(() => panel.ShowsNotResponding));
        Assert.Equal(LiveTabState.NotResponding, Read(() => panel.State));
        agent.Send(Heartbeat(10));
        await WaitAsync(() => panel.State == LiveTabState.Playing, "playing again");
        Assert.False(Read(() => panel.ShowsNotResponding));
        await WaitAsync(() => panel.ShowsNotResponding, "not responding again");

        // End game ends the process at once
        dispatcher.Invoke(() => panel.EndGameCommand.Execute());
        Assert.True(game.Killed);
        await WaitAsync(() => panel.State == LiveTabState.Stopped, "stopped");
        Assert.False(Read(() => panel.ShowsNotResponding));
    }

    [Fact]
    public async Task AnAgentThatDoesNotConnectLeavesTheGameRunningAndSaysWhy()
    {
        await RecreateAsync(DefaultOptions() with { ConnectTimeout = TimeSpan.FromMilliseconds(200) });
        var game = Start();

        await WaitAsync(() => panel.State == LiveTabState.NotConnected, "not connected");

        Assert.Contains("did not connect within", Read(() => panel.Message));
        Assert.False(game.Exited.IsCompleted);
        Assert.True(playMode.IsPlaying);
    }

    [Fact]
    public async Task AGameThatRunsAnotherBuildOfItsCodeSaysSomeValuesArePlaceholders()
    {
        Start();
        // An assembly the editor has, in another build
        var assembly = typeof(FactAttribute).Assembly;
        var stale = new PlayAssemblyInfo(assembly.GetName().Name!, assembly.GetName().Version?.ToString(), Guid.NewGuid());
        await ConnectAsync(new PlayHello(1234, Version, Version, "MyGame.Game", [stale]));

        await WaitAsync(() => panel.StaleNotice is not null, "the notice");
        Assert.Contains(stale.Name, Read(() => panel.StaleNotice));
        Assert.Contains("placeholders", Read(() => panel.StaleNotice));
        Assert.Same(reloadCode, Read(() => panel.ReloadCodeCommand));
    }

    [Fact]
    public async Task AGameStartedWithoutTheConnectionSaysWhy()
    {
        playMode.ConnectToGame = () => false;

        var game = Start();

        Assert.Empty(game.Environment);
        await WaitAsync(() => panel.State == LiveTabState.NotConnected, "not connected");
        Assert.Contains("Connect to the game when starting the project", Read(() => panel.Message));
        Assert.Empty(transports);

        // Stop ends it: it cannot be asked to quit
        await playMode.StopAsync();
        Assert.True(game.Killed);
        await WaitAsync(() => panel.State == LiveTabState.Stopped, "stopped");
    }

    [Fact]
    public async Task TheStatusBarFollowsTheGame()
    {
        await RecreateAsync(DefaultOptions() with { NotRespondingAfter = TimeSpan.FromMilliseconds(300) });
        var status = dispatcher.Invoke(() => new PlayStatusViewModel(new ViewModelServiceProvider([dispatcher]), playMode));
        try
        {
            Assert.False(Read(() => status.IsVisible));

            var game = Start();
            await WaitAsync(() => status.State == LiveTabState.Connecting, "connecting");
            Assert.True(Read(() => status.IsVisible));
            Assert.Equal("Game: Connecting", Read(() => status.Text));

            var agent = await ConnectAsync();
            agent.Send(Heartbeat(1));
            await WaitAsync(() => status.State == LiveTabState.Playing, "playing");
            Assert.Equal("Game: Playing", Read(() => status.Text));
            Assert.True(Read(() => status.IsPlaying));

            await WaitAsync(() => status.State == LiveTabState.NotResponding, "not responding");
            Assert.Equal("Game: Not responding", Read(() => status.Text));
            agent.Send(Heartbeat(2));
            await WaitAsync(() => status.State == LiveTabState.Playing, "playing again");

            game.Exit(3);
            await WaitAsync(() => status.State == LiveTabState.Stopped, "stopped");
            Assert.Equal("Game: Stopped (exit code 3)", Read(() => status.Text));

            // A game started without the connection: not connected, and why
            playMode.ConnectToGame = () => false;
            Start();
            await WaitAsync(() => status.State == LiveTabState.NotConnected, "not connected");
            Assert.Contains("Connect to the game", Read(() => status.Message));
        }
        finally
        {
            dispatcher.Invoke(status.Destroy);
        }
    }

    [Fact]
    public async Task PauseStepAndTheTimeScaleGoToTheGameAndTheTabSaysPaused()
    {
        var status = dispatcher.Invoke(() => new PlayStatusViewModel(new ViewModelServiceProvider([dispatcher]), playMode));
        try
        {
            var controls = panel.Controls;
            Assert.False(Read(() => controls.CanControl));
            Assert.False(Read(() => controls.PauseCommand.IsEnabled));

            var game = Start();
            var agent = await ConnectAsync();
            agent.Send(new PlayGameState(10, 10, false, 1, 1, 59.6, 3, 0));
            await WaitAsync(() => controls.CanControl && controls.FramesPerSecond == 59.6, "the controls");
            Assert.True(Read(() => controls.StepCommand.IsEnabled));
            Assert.Equal(3, Read(() => controls.EntityCount));
            Assert.Equal("Pause", Read(() => controls.PauseText));

            dispatcher.Invoke(() => controls.PauseCommand.Execute());
            await agent.ReceiveAsync<PlayPause>();
            agent.Send(new PlayGameState(11, 10, true, 1, 1, 59.6, 3, 0));
            await WaitAsync(() => panel.State == LiveTabState.Paused, "paused");
            Assert.Equal("Paused", Read(() => panel.StateText));
            Assert.True(Read(() => panel.IsPaused));
            // The tree still follows the paused game
            Assert.True(Read(() => panel.IsLive));
            Assert.Equal("Resume", Read(() => controls.PauseText));
            await WaitAsync(() => status.State == LiveTabState.Paused, "the status bar");
            Assert.Equal("Game: Paused", Read(() => status.Text));
            Assert.True(Read(() => status.IsPaused));

            dispatcher.Invoke(() => controls.StepCommand.Execute());
            Assert.Equal(1, (await agent.ReceiveAsync<PlayStep>()).Frames);
            agent.Send(new PlayGameState(12, 11, true, 1, 1, 59.6, 3, 0));
            await WaitAsync(() => controls.PlayedFrames == 11, "the step");

            dispatcher.Invoke(() => controls.TimeScale = 0.25);
            Assert.Equal(0.25, (await agent.ReceiveAsync<PlayTimeScale>()).Scale);
            // The game's state does not send the scale back
            agent.Send(new PlayGameState(13, 11, true, 0.25, 1, 59.6, 3, 0));
            await WaitAsync(() => controls.TimeScale == 0.25 && controls.PlayedFrames == 11, "the scale");

            dispatcher.Invoke(() => controls.PauseCommand.Execute());
            await agent.ReceiveAsync<PlayResume>();
            agent.Send(new PlayGameState(14, 12, false, 0.25, 1, 59.6, 3, 0));
            await WaitAsync(() => panel.State == LiveTabState.Playing && !controls.IsPaused, "playing again");
            agent.Send(new PlayHeartbeat(15, 1, 60, new PlayAgentStats(60, 1, 1, 0, 0, 0, 0, 0)));
            await agent.ReceiveAsync<PlayQuit>().WaitAsync(TimeSpan.FromMilliseconds(100)).ContinueWith(x => Assert.True(x.IsFaulted, "nothing else was sent"));

            game.Exit(0);
            await WaitAsync(() => !controls.CanControl && panel.State == LiveTabState.Stopped, "stopped");
            Assert.False(Read(() => controls.PauseCommand.IsEnabled));
            Assert.False(Read(() => controls.IsPaused));
        }
        finally
        {
            dispatcher.Invoke(status.Destroy);
        }
    }

    [Fact]
    public async Task AScriptExceptionPausesWithANoticeThatSelectsItsEntity()
    {
        var status = dispatcher.Invoke(() => new PlayStatusViewModel(new ViewModelServiceProvider([dispatcher]), playMode));
        try
        {
            playMode.LaunchOptions = () => new PlayLaunchOptions(PauseOnScriptException: true);
            var game = Start();
            var agent = await ConnectAsync();
            agent.Send(Tree());
            await WaitAsync(() => panel.EntityCount == 3, "the tree");

            // As the agent sends it: the exception, then the paused state with its reason
            agent.Send(new PlayScriptException(20, PlayScriptExceptionKind.Update, "MyGame.PlayerController", 2, PlayerId, "Player",
                "System.InvalidOperationException", "No weapon.", "   at MyGame.PlayerController.Update()", Paused: true));
            agent.Send(new PlayGameState(21, 20, true, 1, 1, 60, 3, 0, PlayPauseReason.ScriptException));

            await WaitAsync(() => panel.State == LiveTabState.Paused && panel.ExceptionNotice is not null, "the notice");
            Assert.Equal("PlayerController (entity Player) threw InvalidOperationException in Update: No weapon.", Read(() => panel.ExceptionNotice));
            Assert.Equal("Paused: script exception in PlayerController", Read(() => panel.PauseText));
            await WaitAsync(() => status.State == LiveTabState.Paused, "the status bar");
            Assert.Equal("Game: Paused: script exception in PlayerController", Read(() => status.Text));

            // The entity of the script, even behind a search that hides it
            dispatcher.Invoke(() => panel.SearchText = "Ground");
            Assert.True(Read(() => panel.SelectExceptionEntityCommand.IsEnabled));
            dispatcher.Invoke(() => panel.SelectExceptionEntityCommand.Execute());
            Assert.Equal([2], Read(() => panel.Selection!.Handles));
            Assert.Equal(string.Empty, Read(() => panel.SearchText));

            // Dismissed, the reason stays; the same exception does not come back with the next state
            dispatcher.Invoke(() => panel.DismissExceptionCommand.Execute());
            agent.Send(new PlayGameState(22, 20, true, 1, 1, 60, 3, 0, PlayPauseReason.ScriptException));
            await Task.Delay(100);
            Assert.Null(Read(() => panel.ExceptionNotice));
            Assert.Equal("Paused: script exception in PlayerController", Read(() => panel.PauseText));

            dispatcher.Invoke(() => panel.Controls.PauseCommand.Execute());
            await agent.ReceiveAsync<PlayResume>();
            agent.Send(new PlayGameState(23, 21, false, 1, 1, 60, 3, 0));
            await WaitAsync(() => panel.State == LiveTabState.Playing && panel.PauseText is null, "playing again");
            await WaitAsync(() => status.Text == "Game: Playing", "the status bar again");

            game.Exit(0);
            await WaitAsync(() => panel.State == LiveTabState.Stopped, "stopped");
        }
        finally
        {
            dispatcher.Invoke(status.Destroy);
        }
    }

    [Fact]
    public async Task TheTabAndTheStatusBarSayWhichScenePlays()
    {
        var status = dispatcher.Invoke(() => new PlayStatusViewModel(new ViewModelServiceProvider([dispatcher]), playMode));
        try
        {
            playMode.NextStartScene = new PlaySceneRequest("/MyGame/Scenes/Level2", "Level2", "/MyGame/MainScene", "MainScene");
            Start();
            var agent = await ConnectAsync();
            await WaitAsync(() => panel.SceneText == "Playing Level2 instead of MainScene", "the scene in the tab");

            // The game loaded it without the package's namespace: it is the scene asked for
            agent.Send(new PlayTree(1, [new PlaySceneInfo(1, Guid.NewGuid(), "Level2", "Scenes/Level2", 0)], []));
            agent.Send(Heartbeat(1));
            await WaitAsync(() => panel.ApplyCount > 0 && status.State == LiveTabState.Playing, "the tree");
            Assert.Equal("Playing Level2 instead of MainScene", Read(() => panel.SceneText));
            Assert.Equal("Game: Playing Level2", Read(() => status.Text));
            agent.Send(new PlayGameState(2, 1, true, 1, 1, 60, 0, 0, PlayPauseReason.Editor));
            await WaitAsync(() => status.State == LiveTabState.Paused, "paused");
            Assert.Equal("Game: Paused (Level2)", Read(() => status.Text));

            // The next start plays the default scene: no words about the scene
            Start();
            await WaitAsync(() => panel.State == LiveTabState.Connecting && panel.SceneText is null, "the default scene");
            await WaitAsync(() => status.State == LiveTabState.Connecting, "the status bar");
            Assert.Equal("Game: Connecting", Read(() => status.Text));
        }
        finally
        {
            dispatcher.Invoke(status.Destroy);
        }
    }

    [Fact]
    public async Task ASceneThatIsNotInTheBuiltDataSaysTheGameStartedOnTheDefaultScene()
    {
        playMode.NextStartScene = new PlaySceneRequest("/MyGame/Scenes/Level3", "Level3", "/MyGame/MainScene", "MainScene");
        Start();
        var agent = await ConnectAsync();

        // The agent found no such scene in the game's data: the game started on its default scene
        agent.Send(new PlayTree(1, [new PlaySceneInfo(1, SceneId, "MainScene", "/MyGame/MainScene", 0)], []));
        await WaitAsync(() => panel.SceneText == "Level3 is not in the game's built data: the game started on MainScene.", "the scene that did not play");

        // A scene the game loads itself later does not change it
        agent.Send(new PlayTree(2, [new PlaySceneInfo(2, Guid.NewGuid(), "Level3", "/MyGame/Scenes/Level3", 0)], []));
        await WaitAsync(() => panel.ApplyCount > 1, "the second tree");
        Assert.Equal("Level3 is not in the game's built data: the game started on MainScene.", Read(() => panel.SceneText));
    }

    [Fact]
    public async Task TheTabSaysWhyTheGameIsPaused()
    {
        playMode.LaunchOptions = () => new PlayLaunchOptions(StartPaused: true, Hotkey: new PlayHotkey("F7"));
        Start();
        var agent = await ConnectAsync();

        agent.Send(new PlayGameState(1, 0, true, 1, 1, 60, 3, 0, PlayPauseReason.StartPaused));
        await WaitAsync(() => panel.PauseText == "Paused: started paused", "started paused");

        agent.Send(new PlayGameState(2, 1, false, 1, 1, 60, 3, 0));
        await WaitAsync(() => panel.PauseText is null && panel.State == LiveTabState.Playing, "playing");

        agent.Send(new PlayGameState(3, 2, true, 1, 1, 60, 3, 0, PlayPauseReason.Hotkey));
        await WaitAsync(() => panel.PauseText == "Paused: F7 in the game window", "the key");

        // A pause of the editor's needs no words: the badge says paused
        agent.Send(new PlayGameState(4, 2, false, 1, 1, 60, 3, 0));
        agent.Send(new PlayGameState(5, 2, true, 1, 1, 60, 3, 0, PlayPauseReason.Editor));
        await WaitAsync(() => panel.State == LiveTabState.Paused && panel.PauseText is null, "the editor's pause");
        Assert.Null(Read(() => panel.ExceptionNotice));
    }

    [Fact]
    public void ClosingTheTabDisposesTheViewOfTheSelection()
    {
        dispatcher.Invoke(panel.Destroy);

        Assert.True(selectionView.Disposed);
    }

    private static PlayHeartbeat Heartbeat(long frame) => new(frame, frame / 60.0, 60, new PlayAgentStats(60, 10, 20, 0, 0, 0, 0, 0));

    private LivePanelViewModel CreatePanel()
    {
        var services = new ViewModelServiceProvider([dispatcher, clipboard]);
        var descriptor = LivePanel.Create(_ => throw new NotSupportedException());
        return dispatcher.Invoke(() => new LivePanelViewModel(new PanelContext { Descriptor = descriptor, Session = new FakeSession(), ServiceProvider = services }, playMode, sceneAssets, selectionView, reloadCode));
    }

    private FakeGame Start()
    {
        var count = games.Count;
        var game = Assert.IsType<FakeGame>(dispatcher.Invoke(() => playMode.Start(Path.Combine(directory, "MyGame.dll"), new LoggerResult())));
        Assert.Equal(count + 1, games.Count);
        return game;
    }

    private async Task<FakeAgent> ConnectAsync(PlayHello? hello = null)
    {
        var transport = transports.Last();
        var agent = transport.Connect();
        agents.Add(agent);
        agent.Send(hello ?? new PlayHello(1234, Version, Version, "MyGame.Game", []));
        await WaitUntilAsync(() => playMode.Session!.State != PlaySessionState.Starting, "the hello");
        return agent;
    }

    private T Read<T>(Func<T> read) => dispatcher.Invoke(read);

    private static Task WaitAsync(Func<bool> condition, string what) => ExclusiveDispatcherService.WaitUntilAsync(condition, what, Timeout);

    private static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Timed out waiting for {what}.");
            await Task.Delay(10);
        }
    }

    /// <summary>
    /// The view of the selection: records what it is asked to show.
    /// </summary>
    private sealed class FakeSelectionView : ILiveSelectionView
    {
        public List<LiveSelection?> Shown { get; } = [];

        public bool Disposed { get; private set; }

        public void Show(LiveSelection? selection) => Shown.Add(selection);

        public void Dispose() => Disposed = true;
    }

    /// <summary>
    /// The scene assets of a session that has the scene of <see cref="LiveTreeTests.Tree"/> with its player, camera and ground.
    /// </summary>
    private sealed class FakeSceneAssets : ILiveSceneAssets
    {
        public List<(Guid Scene, Guid Entity)> Shown { get; } = [];

        public IReadOnlyCollection<Guid>? GetEntityIds(PlaySceneInfo scene) => scene.Id == SceneId ? [PlayerId, CameraId, GroundId] : null;

        public Task<bool> ShowInSceneEditorAsync(PlaySceneInfo scene, Guid entityId)
        {
            Shown.Add((scene.Id, entityId));
            return Task.FromResult(true);
        }
    }
}
