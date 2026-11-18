// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Concurrent;
using Stride.Core.Diagnostics;
using Stride.Editor.PlayMode;
using Stride.Engine;
using Stride.PlayMode.Agent;
using Stride.PlayMode.Protocol;
using Stride.PlayMode.Snapshots;

namespace Stride.Editor.Tests.PlayMode;

/// <summary>
/// Tests the states and events of <see cref="PlaySession"/> against a fake transport, agent and game.
/// </summary>
public sealed class PlaySessionTests : IAsyncLifetime
{
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private const string Version = "4.5.4-dev";

    private readonly string directory = Path.Combine(Path.GetTempPath(), $"stride-play-tests-{Guid.NewGuid():N}");
    private readonly FakePlayTransport transport = new();
    private readonly ConcurrentQueue<PlaySessionStateChange> changes = new();
    private readonly LoggerResult log = new();
    private PlayAgentFiles agentFiles = null!;
    private PlaySession session = null!;
    private FakeGame? game;
    private FakeAgent? agent;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(directory);
        foreach (var file in PlayAgentFiles.FileNames)
            File.WriteAllBytes(Path.Combine(directory, file), []);
        agentFiles = new PlayAgentFiles(directory, Version);
        session = CreateSession(agentFiles);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await session.DisposeAsync();
        if (agent is not null)
            await agent.DisposeAsync();
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task TheGameStartsWithTheAgentsVariables()
    {
        var connected = WaitForState(PlaySessionState.Connected);

        await StartAndConnectAsync();
        agent!.Send(Hello());
        await connected;

        Assert.Equal(PlaySessionState.Connected, session.State);
        Assert.Equal("fake-pipe", game!.Environment[PlayProtocol.PipeVariable]);
        Assert.EndsWith(agentFiles.HookPath, game.Environment[PlayProtocol.StartupHooksVariable], StringComparison.Ordinal);
        Assert.Equal(Version, game.Environment[PlayProtocol.EngineVersionVariable]);
        Assert.Same(game, session.Game);
        Assert.Equal(1234, session.Hello!.ProcessId);
    }

    [Fact]
    public async Task TheAgentsMessagesAreRaised()
    {
        var tree = Wait<PlayTree>(x => session.TreeReceived += x);
        var batch = Wait<PlayTreeBatch>(x => session.TreeChanged += x);
        var snapshot = Wait<LiveSnapshot>(x => session.SnapshotReceived += x);
        var logged = Wait<PlayLog>(x => session.LogReceived += x);
        var heartbeat = Wait<PlayHeartbeat>(x => session.HeartbeatReceived += x);
        await ConnectAsync();
        var entity = new Entity("Player") { new PlayTestComponent { Health = 5 } };

        agent!.Send(new PlayTree(1, [], []));
        agent.Send(new PlayTreeBatch(2, [new PlayEntityRemoved(4)]));
        agent.Send(new PlaySnapshot(3, 0.5, [PlaySnapshotWriter.Write(entity, _ => 4)]));
        agent.Send(new PlayLog(3, PlayLogLevel.Info, "Game", "Hello", null));
        agent.Send(new PlayHeartbeat(4, 1, 60, new PlayAgentStats(60, 10, 20, 5, 100, 200, 1000, 0)));

        Assert.Equal(1, (await tree).Frame);
        Assert.Same(await tree, session.Tree);
        Assert.Equal(4, Assert.IsType<PlayEntityRemoved>(Assert.Single((await batch).Operations)).Handle);
        var mirror = Assert.Single((await snapshot).Entities);
        Assert.Equal(4, mirror.Handle);
        Assert.Equal(5, mirror.Entity.Get<PlayTestComponent>().Health);
        Assert.Equal("Hello", (await logged).Text);
        Assert.Equal(60, (await heartbeat).FramesPerSecond);
        Assert.Equal(60, session.LastHeartbeat!.Value.Heartbeat.FramesPerSecond);
    }

    [Fact]
    public async Task TheGamesAssembliesAreChecked()
    {
        await ConnectAsync(Hello(new PlayAssemblyInfo("MyGame", "1.0.0.0", Guid.NewGuid())));
        var logged = Wait<PlayLog>(x => session.LogReceived += x);

        agent!.Send(new PlayAssemblies([new PlayAssemblyInfo("MyPlugin", "1.0.0.0", Guid.NewGuid())]));
        agent.Send(new PlayLog(1, PlayLogLevel.Info, null, "after", null));
        await logged;

        Assert.Equal(PlayAssemblyStatus.Missing, session.Assemblies.GetStatus("MyGame"));
        Assert.Equal(PlayAssemblyStatus.Missing, session.Assemblies.GetStatus("MyPlugin"));
    }

    [Fact]
    public async Task TheBuildsContentIsAskedOnceConnectedAndKept()
    {
        var received = Wait<IReadOnlySet<string>>(x => session.BuildContentReceived += x);
        Assert.Null(session.BuildContent);
        await ConnectAsync();

        await agent!.ReceiveAsync<PlayContentRequest>();
        agent.Send(new PlayContentIndex(["Models/Box", "Scenes/Main"]));

        Assert.Equal(["Models/Box", "Scenes/Main"], (await received).Order());
        Assert.True(session.BuildContent!.Contains("Models/Box"));
        Assert.False(session.BuildContent.Contains("models/box"));
        Assert.True(session.StartTime <= DateTime.Now);
    }

    [Fact]
    public void AReferencePickedInTheEditorGetsAStandInLikeThoseOfTheSnapshots()
    {
        var entityId = Guid.NewGuid();
        var componentId = Guid.NewGuid();

        var entity = session.CreateStandIn(new PlayMirrorReference(PlayMirrorReferenceKind.Entity, 7, entityId, "Camera", Guid.Empty, null))!.Value;
        var component = session.CreateStandIn(new PlayMirrorReference(PlayMirrorReferenceKind.Component, 7, entityId, "Camera", componentId, PlayMirrorSerializer.TypeNameOf(typeof(PlayTestComponent))))!.Value;
        var unknown = session.CreateStandIn(new PlayMirrorReference(PlayMirrorReferenceKind.Component, 7, entityId, "Camera", componentId, "Some.Game.Component, Some.Game"));

        var standIn = Assert.IsType<Entity>(entity.StandIn);
        Assert.Equal(("Camera", entityId), (standIn.Name, standIn.Id));
        Assert.Equal(7, entity.Map[standIn].EntityHandle);
        var componentStandIn = Assert.IsType<PlayTestComponent>(component.StandIn);
        Assert.Equal(componentId, componentStandIn.Id);
        // The component sits on a stand-in of its entity, which the map knows too
        Assert.Equal(PlayMirrorReferenceKind.Entity, component.Map[componentStandIn.Entity!].Kind);
        Assert.Equal(componentId, component.Map[componentStandIn].ComponentId);
        Assert.Null(unknown);
    }

    [Fact]
    public async Task ASubscriptionIsSentToTheAgent()
    {
        Assert.False(session.Subscribe([1]));
        await ConnectAsync();

        Assert.True(session.Subscribe([3, 4], 10));

        var subscribe = await agent!.ReceiveAsync<PlaySubscribe>();
        Assert.Equal([3, 4], subscribe.Handles);
        Assert.Equal(10, subscribe.RateHz);
    }

    [Fact]
    public async Task PauseResumeStepAndTheTimeScaleAreSentToTheAgent()
    {
        Assert.False(session.Pause());
        Assert.False(session.Step());
        await ConnectAsync();

        Assert.True(session.Pause());
        Assert.True(session.Step(3));
        Assert.True(session.SetTimeScale(0.25));
        Assert.True(session.Resume());

        await agent!.ReceiveAsync<PlayPause>();
        Assert.Equal(3, (await agent.ReceiveAsync<PlayStep>()).Frames);
        Assert.Equal(0.25, (await agent.ReceiveAsync<PlayTimeScale>()).Scale);
        await agent.ReceiveAsync<PlayResume>();
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Step(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetTimeScale(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetTimeScale(double.NaN));
    }

    [Fact]
    public async Task TheGameStateIsKeptAndRaised()
    {
        await ConnectAsync();
        Assert.Null(session.GameState);
        Assert.False(session.IsPaused);

        var raised = Wait<PlayGameState>(x => session.GameStateChanged += x);
        agent!.Send(new PlayGameState(40, 30, true, 0.5, 1, 59, 12, 0));
        var state = await raised;

        Assert.Equal(new PlayGameState(40, 30, true, 0.5, 1, 59, 12, 0), state);
        Assert.Equal(state, session.GameState);
        Assert.True(session.IsPaused);

        // An exited game is not paused any more
        var exited = WaitForState(PlaySessionState.GameExited);
        game!.Exit(0);
        await exited;
        Assert.False(session.IsPaused);
        Assert.False(session.Resume());
    }

    [Fact]
    public async Task TheGamesExitEndsTheSession()
    {
        await ConnectAsync();
        var exited = WaitForState(PlaySessionState.GameExited);

        game!.Exit(3);
        await exited;

        Assert.Equal(3, session.ExitCode);
        Assert.True(session.Exited.IsCompleted);
        Assert.False(session.Subscribe([1]));
        Assert.Equal([PlaySessionState.Connected, PlaySessionState.GameExited], changes.Select(x => x.NewState));
    }

    [Fact]
    public async Task AGameOfAnotherVersionStartsWithoutTheAgent()
    {
        await session.StartAsync(StartGame, gameStrideVersion: "4.4.0").WaitAsync(Timeout);

        Assert.Equal(PlaySessionState.VersionMismatch, session.State);
        Assert.Contains("4.4.0", session.StateMessage);
        Assert.Empty(game!.Environment);

        var exited = WaitForState(PlaySessionState.GameExited);
        game.Exit(0);
        await exited;
    }

    [Fact]
    public async Task TheSameVersionWithOtherMetadataStartsWithTheAgent()
    {
        var start = session.StartAsync(StartGame, gameStrideVersion: $"{Version}+abcdef");
        agent = transport.Connect();
        await start.WaitAsync(Timeout);

        Assert.Equal(PlaySessionState.Starting, session.State);
        Assert.NotEmpty(game!.Environment);
    }

    [Fact]
    public async Task MissingAgentFilesStartTheGameWithoutTheAgent()
    {
        await using var noFiles = CreateSession(new PlayAgentFiles(Path.Combine(directory, "missing"), Version));

        await noFiles.StartAsync(StartGame).WaitAsync(Timeout);

        Assert.Equal(PlaySessionState.Failed, noFiles.State);
        Assert.Contains("missing", noFiles.StateMessage);
        Assert.Empty(game!.Environment);
    }

    [Fact]
    public async Task AnAgentThatDoesNotConnectFails()
    {
        await using var slow = CreateSession(agentFiles, new PlaySessionOptions { ConnectTimeout = TimeSpan.FromMilliseconds(100), StopGracePeriod = TimeSpan.FromMilliseconds(100) });

        await slow.StartAsync(StartGame).WaitAsync(Timeout);

        Assert.Equal(PlaySessionState.Failed, slow.State);
        Assert.Contains("did not connect", slow.StateMessage);
        // The game keeps running until the session stops it
        Assert.False(game!.Exited.IsCompleted);
        await slow.StopAsync().WaitAsync(Timeout);
        Assert.True(game.Killed);
    }

    [Fact]
    public async Task AGameThatExitsBeforeItsAgentConnectsEndsTheSession()
    {
        var start = session.StartAsync(StartGame);
        await WaitUntilAsync(() => game is not null);

        game!.Exit(1);
        await start.WaitAsync(Timeout);
        await session.Exited.WaitAsync(Timeout);

        Assert.Equal(PlaySessionState.GameExited, session.State);
        Assert.Equal(1, session.ExitCode);
    }

    [Fact]
    public async Task AnAgentOfAnotherVersionIsNotRead()
    {
        var mismatch = WaitForState(PlaySessionState.VersionMismatch);
        await StartAndConnectAsync();
        var trees = 0;
        session.TreeReceived += (_, _) => trees++;

        agent!.Send(Hello() with { StrideVersion = "4.4.0" });
        await mismatch;
        agent.Send(new PlayTree(1, [], []));
        await agent.CloseAsync();

        Assert.Equal(0, trees);
        Assert.False(session.Subscribe([1]));
    }

    [Fact]
    public async Task AnEditGoesToTheGameAndItsResultAnswersIt()
    {
        await ConnectAsync();
        var answered = Wait<PlayEditAnswer>(x => session.EditAnswered += x);
        var edit = Edit(PlayEditValue.Of(new byte[] { 1, 2, 3 }));

        var result = session.EditAsync(edit);
        var received = await agent!.ReceiveAsync<PlayEdit>();
        Assert.Equal(edit.Encode(), received.Encode());
        Assert.Equal(1, session.PendingEditCount);
        agent.Send(new PlayEditResult(edit.Id, 7, PlayEditStatus.Applied, PlayEditValue.Of(new byte[] { 4 }), null));

        var answer = await result.WaitAsync(Timeout);
        Assert.Equal(PlayEditStatus.Applied, answer.Status);
        Assert.Equal(7, answer.Frame);
        Assert.Equal([4], answer.ReadBack.Payload.ToArray());
        Assert.Same(edit, (await answered).Edit);
        Assert.Equal(0, session.PendingEditCount);
    }

    [Fact]
    public async Task AnEditOfAnEntityGoneIsAnsweredGone()
    {
        await ConnectAsync();
        var edit = Edit(PlayEditValue.Null);

        var result = session.EditAsync(edit);
        await agent!.ReceiveAsync<PlayEdit>();
        agent.Send(new PlayEditResult(edit.Id, 3, PlayEditStatus.Gone, PlayEditValue.Null, "The entity is no longer in the game."));

        var answer = await result.WaitAsync(Timeout);
        Assert.Equal(PlayEditStatus.Gone, answer.Status);
        Assert.Equal("The entity is no longer in the game.", answer.Error);
    }

    [Fact]
    public async Task AnEditWithoutTheGameFailsAtOnce()
    {
        var answer = await session.EditAsync(Edit(PlayEditValue.Null)).WaitAsync(Timeout);

        Assert.Equal(PlayEditStatus.Failed, answer.Status);
        Assert.Contains("not connected", answer.Error);
        Assert.Equal(0, session.PendingEditCount);
    }

    [Fact]
    public async Task AnEditOnItsWayWhenTheGameStopsFailsWithoutAnException()
    {
        await ConnectAsync();
        var edit = Edit(PlayEditValue.Of(new byte[] { 1 }));
        var result = session.EditAsync(edit);
        await agent!.ReceiveAsync<PlayEdit>();
        agent.Received += x =>
        {
            if (x is PlayQuit)
                game!.Exit(0);
        };

        await session.StopAsync().WaitAsync(Timeout);

        var answer = await result.WaitAsync(Timeout);
        Assert.Equal(PlayEditStatus.Failed, answer.Status);
        Assert.Equal("The game stopped before the edit applied.", answer.Error);
        Assert.Equal(0, session.PendingEditCount);
    }

    [Fact]
    public async Task AConnectionThatEndsWhileTheGameRunsFails()
    {
        await ConnectAsync();
        var failed = WaitForState(PlaySessionState.Failed);

        await agent!.CloseAsync();
        await failed;

        Assert.Contains("closed the connection", session.StateMessage);
        Assert.False(game!.Exited.IsCompleted);
    }

    [Fact]
    public async Task AGoodbyeWaitsForTheExit()
    {
        await ConnectAsync();
        var exited = WaitForState(PlaySessionState.GameExited);

        agent!.Send(new PlayGoodbye("The game exits."));
        await agent.CloseAsync();
        await Task.Delay(300);
        game!.Exit(0);
        await exited;

        Assert.Equal([PlaySessionState.Connected, PlaySessionState.GameExited], changes.Select(x => x.NewState));
    }

    [Fact]
    public async Task StopAsksTheGameToQuit()
    {
        await ConnectAsync();
        agent!.Received += x =>
        {
            if (x is PlayQuit)
                game!.Exit(0);
        };

        await session.StopAsync().WaitAsync(Timeout);

        Assert.True(session.Exited.IsCompleted);
        Assert.False(game!.Killed);
        Assert.Equal(0, session.ExitCode);
    }

    [Fact]
    public async Task StopEndsAGameThatDoesNotQuit()
    {
        await ConnectAsync();

        await session.StopAsync().WaitAsync(Timeout);

        Assert.IsType<PlayQuit>(await agent!.ReceiveAsync<PlayQuit>());
        Assert.True(game!.Killed);
        Assert.Equal(PlaySessionState.GameExited, session.State);
    }

    [Fact]
    public async Task AFailingHandlerDoesNotStopTheSession()
    {
        await ConnectAsync();
        session.TreeReceived += (_, _) => throw new InvalidOperationException("A view failed.");
        var heartbeat = Wait<PlayHeartbeat>(x => session.HeartbeatReceived += x);

        agent!.Send(new PlayTree(1, [], []));
        agent.Send(new PlayHeartbeat(2, 1, 60, new PlayAgentStats(0, 0, 0, 0, 0, 0, 0, 0)));
        await heartbeat;

        Assert.Equal(PlaySessionState.Connected, session.State);
        Assert.Contains(log.Messages, x => x.Type == LogMessageType.Error && x.Text.Contains(nameof(PlayTree)));
    }

    [Fact]
    public async Task DisposingEndsTheGameAndTheTransport()
    {
        await ConnectAsync();

        await session.DisposeAsync();

        Assert.True(game!.Killed);
        Assert.True(transport.IsDisposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.StartAsync(StartGame));
    }

    [Fact]
    public async Task AGameThatSendsNothingDoesNotRespondUntilItSendsAgain()
    {
        await using var watched = CreateSession(agentFiles, new PlaySessionOptions { NotRespondingAfter = TimeSpan.FromMilliseconds(200), StopGracePeriod = TimeSpan.FromMilliseconds(100) });
        var responding = new ConcurrentQueue<bool>();
        watched.RespondingChanged += (_, x) => responding.Enqueue(x);
        var start = watched.StartAsync(StartGame);
        agent = transport.Connect();
        await start.WaitAsync(Timeout);
        Assert.True(watched.IsResponding);
        agent.Send(Hello());
        await WaitUntilAsync(() => watched.State == PlaySessionState.Connected);

        await WaitUntilAsync(() => !watched.IsResponding);
        Assert.Equal(PlaySessionState.Connected, watched.State);
        agent.Send(new PlayHeartbeat(1, 1, 60, new PlayAgentStats(60, 10, 20, 0, 0, 0, 0, 0)));
        await WaitUntilAsync(() => watched.IsResponding);
        Assert.Equal([false, true], responding.ToArray());

        // An exited game is not waited for
        await WaitUntilAsync(() => !watched.IsResponding);
        game!.Exit(0);
        await WaitUntilAsync(() => watched.State == PlaySessionState.GameExited && watched.IsResponding);
    }

    [Fact]
    public async Task AGameStartedWithoutTheAgentOnlyFollowsItsProcess()
    {
        session.StartWithoutAgent(StartGame, "Started without the live view.");

        Assert.Equal(PlaySessionState.WithoutAgent, session.State);
        Assert.Equal("Started without the live view.", session.StateMessage);
        Assert.Empty(game!.Environment);
        Assert.False(session.Subscribe([1]));
        Assert.Throws<InvalidOperationException>(() => session.StartWithoutAgent(StartGame, "Again"));

        // It cannot be asked to quit: Stop ends it
        await session.StopAsync().WaitAsync(Timeout);
        Assert.True(game.Killed);
        await WaitUntilAsync(() => session.State == PlaySessionState.GameExited);
        Assert.Equal(-1, session.ExitCode);
    }

    private PlaySession CreateSession(PlayAgentFiles files, PlaySessionOptions? options = null)
    {
        var created = new PlaySession(transport, files, options ?? new PlaySessionOptions
        {
            StopGracePeriod = TimeSpan.FromMilliseconds(200),
            ExitAfterDisconnectDelay = TimeSpan.FromMilliseconds(200),
            Log = log,
        });
        created.StateChanged += (_, e) => changes.Enqueue(e);
        return created;
    }

    private FakeGame StartGame(IReadOnlyDictionary<string, string> environment) => game = new FakeGame(environment);

    private async Task StartAndConnectAsync()
    {
        var start = session.StartAsync(StartGame);
        agent = transport.Connect();
        await start.WaitAsync(Timeout);
    }

    private async Task ConnectAsync(PlayHello? hello = null)
    {
        var connected = WaitForState(PlaySessionState.Connected);
        await StartAndConnectAsync();
        agent!.Send(hello ?? Hello());
        await connected;
    }

    private PlayEdit Edit(PlayEditValue value)
        => new(session.NextEditId(), 4, Guid.NewGuid(), [PlayPathSegment.Member("Speed")], PlayEditOperation.Set, value);

    private static PlayHello Hello(params PlayAssemblyInfo[] assemblies) => new(1234, Version, Version, "MyGame.Game", assemblies);

    private Task WaitForState(PlaySessionState state)
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.StateChanged += (_, e) =>
        {
            if (e.NewState == state)
                reached.TrySetResult();
        };
        return reached.Task.WaitAsync(Timeout);
    }

    private static Task<T> Wait<T>(Action<EventHandler<T>> subscribe)
    {
        var raised = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        subscribe((_, e) => raised.TrySetResult(e));
        return raised.Task.WaitAsync(Timeout);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        while (!condition())
            await Task.Delay(10, timeout.Token);
    }
}
