// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Concurrent;
using Stride.Core.Assets.Editor.Components.Build;
using Stride.Core.Assets.Editor.Panels;
using Stride.Core.Assets.Editor.Tests.Helpers;
using Stride.Core.Diagnostics;
using Stride.Core.Presentation.Services;
using Stride.Core.Presentation.ViewModels;
using Stride.Editor.PlayMode;
using Stride.PlayMode.Protocol;
using static Stride.Editor.Tests.PlayMode.LiveTreeTests;

namespace Stride.Editor.Tests.PlayMode;

/// <summary>
/// Tests the Live tab (<see cref="LivePanelViewModel"/>) and the play mode service it follows, with a fake transport, agent
/// and game: the tree and its batches, the rate of the updates, the states, the selection and its subscriptions, and a second
/// start while a game plays.
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
    private PlayModeService playMode = null!;
    private LivePanelViewModel panel = null!;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(directory);
        foreach (var file in PlayAgentFiles.FileNames)
            File.WriteAllBytes(Path.Combine(directory, file), []);
        var options = new PlaySessionOptions
        {
            ConnectTimeout = Timeout,
            StopGracePeriod = TimeSpan.FromMilliseconds(200),
            ExitAfterDisconnectDelay = TimeSpan.FromMilliseconds(200),
            Log = new LoggerResult(),
        };
        playMode = new PlayModeService(() =>
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
        panel = CreatePanel();
        return Task.CompletedTask;
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
    public async Task SelectingEntitiesSubscribesToThem()
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

        var subscribe = await ReceiveSubscribeAsync(agent, 2);
        Assert.Equal([2, 4], subscribe.Handles);
        Assert.Equal(LivePanelViewModel.SnapshotRateHz, subscribe.RateHz);
        var selection = Read(() => panel.Selection);
        Assert.NotNull(selection);
        Assert.Same(playMode.Session, selection.Session);
        Assert.Equal([2, 4], selection.Handles);

        // A scene row is not an entity: nothing changes
        dispatcher.Invoke(() => panel.SelectedItems.Add(panel.Tree.Roots[0]));
        Assert.Equal([2, 4], Read(() => panel.Selection!.Handles));

        dispatcher.Invoke(panel.SelectedItems.Clear);
        subscribe = await ReceiveSubscribeAsync(agent, 0);
        Assert.Empty(subscribe.Handles);
        Assert.Null(Read(() => panel.Selection));
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
        await ReceiveSubscribeAsync(agent, 2);

        // The player goes with its camera
        agent.Send(new PlayTreeBatch(5, [new PlayEntityRemoved(2)]));

        var subscribe = await ReceiveSubscribeAsync(agent, 1);
        Assert.Equal([4], subscribe.Handles);
        Assert.Equal([4], Read(() => panel.Selection!.Handles));
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
        await ReceiveSubscribeAsync(agent, 1);

        // Stop asks the game to quit; this one does not, so its process is ended
        await playMode.StopAsync();

        Assert.IsType<PlayQuit>(await agent.ReceiveAsync<PlayQuit>());
        Assert.True(games.Single().Killed);
        await WaitAsync(() => panel.State == LiveTabState.Stopped, "stopped");
        Assert.Null(Read(() => panel.Selection));
        Assert.Equal(3, Read(() => panel.Tree.Rows.Count));
        Assert.Equal("Stopped (exit code -1)", Read(() => panel.StateText));
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

    private LivePanelViewModel CreatePanel()
    {
        var services = new ViewModelServiceProvider([dispatcher, clipboard]);
        var descriptor = LivePanel.Create(_ => throw new NotSupportedException());
        return dispatcher.Invoke(() => new LivePanelViewModel(new PanelContext { Descriptor = descriptor, Session = new FakeSession(), ServiceProvider = services }, playMode, sceneAssets));
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

    private static async Task<PlaySubscribe> ReceiveSubscribeAsync(FakeAgent agent, int count)
    {
        // A selection made of several changes can send several requests: the last one counts
        while (true)
        {
            var subscribe = await agent.ReceiveAsync<PlaySubscribe>();
            if (subscribe.Handles.Count == count)
                return subscribe;
        }
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
