// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Diagnostics;
using Stride.Editor.PlayMode;
using Stride.PlayMode.Protocol;

namespace Stride.Editor.Tests.PlayMode;

/// <summary>
/// Tests <see cref="PlayModeService"/>, the editor's game launcher in play mode, with a fake transport and game.
/// </summary>
public sealed class PlayModeServiceTests : IAsyncLifetime
{
    private const string Version = "4.5.4-dev";

    private readonly string directory = Path.Combine(Path.GetTempPath(), $"stride-play-service-tests-{Guid.NewGuid():N}");
    private readonly List<FakePlayTransport> transports = [];
    private readonly List<FakeGame> games = [];
    private readonly List<FakeAgent> agents = [];
    private PlayModeService service = null!;
    private Func<IReadOnlyDictionary<string, string>, FakeGame> startGame = environment => new FakeGame(environment);

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(directory);
        foreach (var file in PlayAgentFiles.FileNames)
            File.WriteAllBytes(Path.Combine(directory, file), []);
        service = new PlayModeService(() =>
        {
            var transport = new FakePlayTransport();
            transports.Add(transport);
            return transport;
        }, new PlayAgentFiles(directory, Version), new PlaySessionOptions { StopGracePeriod = TimeSpan.FromMilliseconds(200) }, (_, _, environment) =>
        {
            var game = startGame(environment);
            games.Add(game);
            return game;
        });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await service.DisposeAsync();
        foreach (var agent in agents)
            await agent.DisposeAsync();
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void AStartedGameRunsWithTheAgent()
    {
        var started = new List<PlaySession>();
        service.SessionStarted += (_, x) => started.Add(x);
        var playing = 0;
        service.PlayingChanged += (_, _) => ++playing;

        var game = service.Start(Path.Combine(directory, "MyGame.dll"), new LoggerResult());

        var session = Assert.Single(started);
        Assert.Same(session, service.Session);
        Assert.Same(games.Single(), game);
        Assert.Same(game, session.Game);
        Assert.Equal("fake-pipe", games.Single().Environment[PlayProtocol.PipeVariable]);
        Assert.True(service.IsPlaying);
        Assert.Equal(1, playing);
        Assert.Equal(PlaySessionState.Starting, session.State);
    }

    [Fact]
    public async Task TheGamesExitEndsPlaying()
    {
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Start(Path.Combine(directory, "MyGame.dll"), new LoggerResult());
        service.PlayingChanged += (_, _) => exited.TrySetResult();

        games.Single().Exit(0);
        await exited.Task.WaitAsync(PlaySessionTests.Timeout);

        Assert.False(service.IsPlaying);
        // The last session stays, for its last tree
        Assert.NotNull(service.Session);
    }

    [Fact]
    public async Task ASecondStartEndsTheFirstGame()
    {
        service.Start(Path.Combine(directory, "MyGame.dll"), new LoggerResult());
        var first = service.Session!;
        agents.Add(transports[0].Connect());
        agents[0].Send(new PlayHello(1234, Version, Version, null, []));
        await WaitForStateAsync(first, PlaySessionState.Connected);

        service.Start(Path.Combine(directory, "MyGame.dll"), new LoggerResult());

        Assert.NotSame(first, service.Session);
        Assert.IsType<PlayQuit>(await agents[0].ReceiveAsync<PlayQuit>());
        // The first game did not quit: its process is ended
        Assert.Equal(-1, await games[0].Exited.WaitAsync(PlaySessionTests.Timeout));
        Assert.True(games[0].Killed);
        Assert.False(games[1].Exited.IsCompleted);
    }

    [Fact]
    public void EndNowEndsTheGame()
    {
        service.Start(Path.Combine(directory, "MyGame.dll"), new LoggerResult());

        // The agent never connected: nobody can ask the game to quit
        service.EndNow(TimeSpan.FromSeconds(1));

        Assert.True(games.Single().Killed);
    }

    [Fact]
    public void AGameThatCannotStartThrows()
    {
        startGame = _ => throw new FileNotFoundException("No game");

        Assert.Throws<FileNotFoundException>(() => service.Start(Path.Combine(directory, "MyGame.dll"), new LoggerResult()));
        Assert.False(service.IsPlaying);
    }

    [Fact]
    public void TheSettingOffStartsTheGameWithoutTheAgent()
    {
        service.ConnectToGame = () => false;

        var game = service.Start(Path.Combine(directory, "MyGame.dll"), new LoggerResult());

        Assert.Same(games.Single(), game);
        Assert.Empty(games.Single().Environment);
        Assert.Empty(transports);
        Assert.Equal(PlaySessionState.WithoutAgent, service.Session!.State);
        Assert.Contains("setting", service.Session.StateMessage);
        Assert.True(service.IsPlaying);
    }

    [Fact]
    public void StartingOnceWithoutTheConnectionLeavesTheNextStartsAlone()
    {
        service.NextStartWithoutConnection = true;

        service.Start(Path.Combine(directory, "MyGame.dll"), new LoggerResult());

        Assert.Equal(PlaySessionState.WithoutAgent, service.Session!.State);
        Assert.Contains("Start without the live view", service.Session.StateMessage);
        Assert.False(service.NextStartWithoutConnection);

        // The next start connects again (and ends this game)
        service.Start(Path.Combine(directory, "MyGame.dll"), new LoggerResult());
        Assert.Equal(PlaySessionState.Starting, service.Session!.State);
        Assert.Equal("fake-pipe", games[1].Environment[PlayProtocol.PipeVariable]);
        Assert.Single(transports);
    }

    [Fact]
    public void TheLaunchOptionsGoToTheGame()
    {
        service.LaunchOptions = () => new PlayLaunchOptions(PauseOnScriptException: true, Hotkey: new PlayHotkey("F8", PlayHotkeyModifiers.Shift));

        service.Start(Path.Combine(directory, "MyGame.dll"), new LoggerResult());

        var environment = games.Single().Environment;
        Assert.Equal("1", environment[PlayProtocol.PauseOnScriptExceptionVariable]);
        Assert.Equal("Shift+F8", environment[PlayProtocol.HotkeyVariable]);
        Assert.False(environment.ContainsKey(PlayProtocol.StartPausedVariable));
        Assert.True(service.Session!.LaunchOptions.PauseOnScriptException);
    }

    [Fact]
    public void StartingPausedOnceLeavesTheNextStartsAlone()
    {
        service.NextStartPaused = true;

        service.Start(Path.Combine(directory, "MyGame.dll"), new LoggerResult());

        Assert.Equal("1", games[0].Environment[PlayProtocol.StartPausedVariable]);
        Assert.True(service.Session!.LaunchOptions.StartPaused);
        Assert.False(service.NextStartPaused);

        service.Start(Path.Combine(directory, "MyGame.dll"), new LoggerResult());
        Assert.False(games[1].Environment.ContainsKey(PlayProtocol.StartPausedVariable));
    }

    [Fact]
    public async Task PlayingASceneOnceStartsTheGameOnItAndSaysSo()
    {
        var output = new LoggerResult();
        service.NextStartScene = new PlaySceneRequest("/MyGame/Scenes/Level2", "Level2", "/MyGame/MainScene", "MainScene");

        service.Start(Path.Combine(directory, "MyGame.dll"), output);

        Assert.Equal("/MyGame/Scenes/Level2", games[0].Environment[PlayProtocol.SceneVariable]);
        Assert.Equal("/MyGame/Scenes/Level2", service.Session!.LaunchOptions.Scene);
        Assert.Equal("Level2", service.Session.Scene?.Name);
        Assert.Null(service.NextStartScene);
        // The Output pane says which scene plays, once the agent connects (the pane follows the game's logger after Start)
        Assert.DoesNotContain(output.Messages, x => x.Text.Contains("Playing", StringComparison.Ordinal));
        agents.Add(transports[0].Connect());
        agents[0].Send(new PlayHello(1234, Version, Version, null, []));
        await WaitForStateAsync(service.Session, PlaySessionState.Connected);
        Assert.Contains(output.Messages, x => x.Type == LogMessageType.Info && x.Text.Contains("Playing Level2 instead of MainScene", StringComparison.Ordinal));

        // The next start plays the default scene again
        service.Start(Path.Combine(directory, "MyGame.dll"), new LoggerResult());
        Assert.False(games[1].Environment.ContainsKey(PlayProtocol.SceneVariable));
        Assert.Null(service.Session!.Scene);
    }

    [Fact]
    public async Task PlayingASceneConnectsEvenWhenTheSettingIsOff()
    {
        var output = new LoggerResult();
        service.ConnectToGame = () => false;
        service.NextStartScene = new PlaySceneRequest("Scenes/Level2", "Level2");

        service.Start(Path.Combine(directory, "MyGame.dll"), output);

        // Only the agent can change the scene: this game connects, and the Output says why
        Assert.Equal("fake-pipe", games[0].Environment[PlayProtocol.PipeVariable]);
        Assert.Equal("Scenes/Level2", games[0].Environment[PlayProtocol.SceneVariable]);
        Assert.Equal(PlaySessionState.Starting, service.Session!.State);
        agents.Add(transports[0].Connect());
        agents[0].Send(new PlayHello(1234, Version, Version, null, []));
        await WaitForStateAsync(service.Session, PlaySessionState.Connected);
        Assert.Contains(output.Messages, x => x.Text.Contains("Connect to the game when starting the project", StringComparison.Ordinal));

        // The next start follows the setting again
        service.Start(Path.Combine(directory, "MyGame.dll"), new LoggerResult());
        Assert.Equal(PlaySessionState.WithoutAgent, service.Session!.State);
    }

    [Fact]
    public void AGameStartedWithoutTheConnectionHasNoLaunchOptions()
    {
        service.ConnectToGame = () => false;
        service.NextStartPaused = true;
        service.LaunchOptions = () => new PlayLaunchOptions(StartPaused: true, PauseOnScriptException: true, Hotkey: new PlayHotkey("F7"));

        service.Start(Path.Combine(directory, "MyGame.dll"), new LoggerResult());

        // Nothing would resume it, and nothing would catch its exceptions
        Assert.Empty(games.Single().Environment);
        Assert.Equal(PlayLaunchOptions.None, service.Session!.LaunchOptions);
    }

    [Fact]
    public async Task AScriptExceptionGoesToTheOutput()
    {
        var output = new LoggerResult();
        service.Start(Path.Combine(directory, "MyGame.dll"), output);
        var agent = transports.Single().Connect();
        agents.Add(agent);
        agent.Send(new PlayHello(1234, Version, Version, "MyGame.Game", []));
        var received = new TaskCompletionSource<PlayScriptException>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Session!.ScriptExceptionReceived += (_, x) => received.TrySetResult(x);

        agent.Send(new PlayScriptException(42, PlayScriptExceptionKind.Update, "MyGame.PlayerController", 2, Guid.NewGuid(), "Player",
            "System.InvalidOperationException", "No weapon", "   at MyGame.PlayerController.Update()", Paused: true));

        var exception = await received.Task.WaitAsync(PlaySessionTests.Timeout);
        Assert.Same(exception, service.Session.LastScriptException);
        var message = Assert.Single(output.Messages, x => x.Type == LogMessageType.Error);
        Assert.Contains("PlayerController (entity Player) threw InvalidOperationException in Update: No weapon", message.Text);
        Assert.Contains("No weapon. Play mode paused the game", message.Text);
        Assert.Contains("at MyGame.PlayerController.Update()", message.Text);
    }

    [Fact]
    public async Task AnEditTheGameRefusesGoesToTheOutputButNotOneAStopCutShort()
    {
        var output = new LoggerResult();
        service.Start(Path.Combine(directory, "MyGame.dll"), output);
        var agent = transports.Single().Connect();
        agents.Add(agent);
        agent.Send(new PlayHello(1234, Version, Version, "MyGame.Game", []));
        await WaitForStateAsync(service.Session!, PlaySessionState.Connected);
        agent.Send(new PlayTree(1, [], [new PlayEntityInfo(4, Guid.NewGuid(), "Player", true, 0, 0, [])]));
        await WaitUntilAsync(() => service.Session!.Tree is not null);
        var session = service.Session!;

        var refused = new PlayEdit(session.NextEditId(), 4, Guid.NewGuid(), [PlayPathSegment.Member("Count")], PlayEditOperation.Set, PlayEditValue.Null);
        var answer = session.EditAsync(refused);
        await agent.ReceiveAsync<PlayEdit>();
        agent.Send(new PlayEditResult(refused.Id, 2, PlayEditStatus.Failed, PlayEditValue.Null, "The setter threw ArgumentOutOfRangeException: the count cannot be negative."));
        await answer.WaitAsync(PlaySessionTests.Timeout);
        var cut = session.EditAsync(refused with { Id = session.NextEditId() });
        await agent.ReceiveAsync<PlayEdit>();
        await agent.CloseAsync();
        await cut.WaitAsync(PlaySessionTests.Timeout);

        var message = Assert.Single(output.Messages, x => x.Type == LogMessageType.Warning);
        Assert.Equal("The game refused the edit of Player > Count: The setter threw ArgumentOutOfRangeException: the count cannot be negative. The game goes on with the value it had.", message.Text);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(PlaySessionTests.Timeout);
        while (!condition())
            await Task.Delay(10, timeout.Token);
    }

    private static Task WaitForStateAsync(PlaySession session, PlaySessionState state)
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.StateChanged += (_, e) =>
        {
            if (e.NewState == state)
                reached.TrySetResult();
        };
        if (session.State == state)
            reached.TrySetResult();
        return reached.Task.WaitAsync(PlaySessionTests.Timeout);
    }
}
