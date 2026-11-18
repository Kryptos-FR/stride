// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.IO.Pipes;
using System.Reflection;
using System.Threading.Channels;
using Scheduler = Stride.Core.MicroThreading.Scheduler;
using Stride.Core.Diagnostics;
using Stride.Engine;
using Stride.Games;
using Stride.PlayMode.Agent;
using Stride.PlayMode.Protocol;

namespace Stride.PlayMode.Tests;

/// <summary>
/// Tests the launch options of the agent (start paused, pause on script exception) on a game that is not run (no graphics):
/// its script system runs by hand, and the agent's update follows it as in a frame.
/// </summary>
public sealed class PlayAgentOptionsTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly PropertyInfo PropagateExceptions = typeof(Scheduler).GetProperty("PropagateExceptions", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly Game game = new();
    private readonly List<IDisposable> disposables = [];

    public void Dispose()
    {
        foreach (var disposable in disposables)
            disposable.Dispose();
        game.Dispose();
    }

    [Fact]
    public void StartPausedPausesBeforeTheFirstUpdate()
    {
        var agent = CreateAgent(new PlayLaunchOptions(StartPaused: true));
        var script = new CountingScript();
        game.Script.Add(script);

        Frame(agent, times: 3);

        Assert.True(agent.PauseController.IsPaused);
        Assert.Equal(PlayPauseReason.StartPaused, agent.PauseController.PauseReason);
        Assert.False(game.Script.Enabled);
        Assert.Equal(0, script.Starts);
        Assert.Equal(0, game.UpdateTime.Factor);
    }

    [Fact]
    public void AGameNoEditorConnectsToPlays()
    {
        var agent = CreateAgent(new PlayLaunchOptions(StartPaused: true, PauseOnScriptException: true));
        Assert.True(agent.PausesOnScriptException);

        agent.NoEditor();
        Frame(agent);

        Assert.False(agent.PauseController.IsPaused);
        Assert.True(game.Script.Enabled);
        // Its scripts' exceptions end it again, as without play mode
        Assert.False(agent.PausesOnScriptException);
        Assert.True((bool)PropagateExceptions.GetValue(game.Script.Scheduler)!);
    }

    [Fact]
    public void WithNoOptionTheGamePlaysAndItsExceptionsAreNotCaught()
    {
        var agent = CreateAgent(PlayLaunchOptions.None);

        Frame(agent);

        Assert.False(agent.PauseController.IsPaused);
        Assert.False(agent.PausesOnScriptException);
        Assert.True((bool)PropagateExceptions.GetValue(game.Script.Scheduler)!);
        game.Script.Add(new ThrowingUpdateScript());
        Assert.Throws<InvalidOperationException>(() => game.Script.Update(new GameTime()));
    }

    [Fact]
    public async Task ASyncScriptThatThrowsPausesTheGameAndTheExceptionIsSent()
    {
        var agent = CreateAgent(new PlayLaunchOptions(PauseOnScriptException: true));
        var editor = await ConnectAsync(agent);
        var thrower = new ThrowingUpdateScript();
        var entity = new Entity("Thrower") { thrower };
        game.Script.Add(thrower);

        game.Script.Update(new GameTime());
        agent.Update(new GameTime());

        Assert.True(agent.PauseController.IsPaused);
        Assert.Equal(PlayPauseReason.ScriptException, agent.PauseController.PauseReason);
        var exception = await editor.ReadAsync<PlayScriptException>();
        Assert.Equal(PlayScriptExceptionKind.Update, exception.Kind);
        Assert.Equal(typeof(ThrowingUpdateScript).FullName, exception.ScriptType);
        Assert.Equal(entity.Id, exception.EntityId);
        Assert.Equal("Thrower", exception.EntityName);
        Assert.Equal(typeof(InvalidOperationException).FullName, exception.ExceptionType);
        Assert.Equal("Update failed.", exception.Message);
        Assert.Contains(nameof(ThrowingUpdateScript.Update), exception.Details);
        Assert.True(exception.Paused);
        var state = await editor.ReadAsync<PlayGameState>(x => x.Paused);
        Assert.Equal(PlayPauseReason.ScriptException, state.PauseReason);
        // The engine stopped the script: the game went on, and goes on without it once resumed
        Assert.Equal(1, thrower.Updates);
    }

    [Fact]
    public async Task AnAsyncScriptThatThrowsPausesTheGameAndTheExceptionIsSent()
    {
        var agent = CreateAgent(new PlayLaunchOptions(PauseOnScriptException: true));
        var editor = await ConnectAsync(agent);
        var thrower = new ThrowingAsyncScript();
        _ = new Entity("AsyncThrower") { thrower };
        game.Script.Add(thrower);

        for (var i = 0; i < 4 && !agent.PauseController.IsPaused; i++)
        {
            game.Script.Update(new GameTime());
            agent.Update(new GameTime());
        }

        Assert.Equal(PlayPauseReason.ScriptException, agent.PauseController.PauseReason);
        var exception = await editor.ReadAsync<PlayScriptException>();
        Assert.Equal(PlayScriptExceptionKind.Async, exception.Kind);
        Assert.Equal(typeof(ThrowingAsyncScript).FullName, exception.ScriptType);
        Assert.Equal("AsyncThrower", exception.EntityName);
        Assert.Equal("Execute failed.", exception.Message);
    }

    [Fact]
    public void AStartThatThrowsIsCaughtAsAStart()
    {
        using var watcher = new PlayScriptExceptionWatcher(game.Script);
        Assert.True(watcher.Start());
        var thrower = new ThrowingStartScript();
        game.Script.Add(thrower);

        game.Script.Update(new GameTime());

        var caught = Assert.Single(watcher.TakeCaught([]));
        Assert.Equal(PlayScriptExceptionKind.Start, caught.Kind);
        Assert.Same(thrower, caught.Script);
        Assert.Empty(watcher.TakeCaught([]));
    }

    [Fact]
    public void TheWatcherGivesTheScriptSystemBackItsWay()
    {
        var watcher = new PlayScriptExceptionWatcher(game.Script);
        Assert.True(PlayScriptExceptionWatcher.IsSupported);

        Assert.True(watcher.Start());
        Assert.False((bool)PropagateExceptions.GetValue(game.Script.Scheduler)!);
        watcher.Dispose();

        Assert.False(watcher.IsWatching);
        Assert.True((bool)PropagateExceptions.GetValue(game.Script.Scheduler)!);
    }

    [Fact]
    public void TheScriptTypeComesFromTheStackWhenTheScriptIsNotKnown()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new ThrowingUpdateScript().Update());

        Assert.Equal(typeof(ThrowingUpdateScript), PlayScriptExceptionWatcher.FindScriptType(exception));
        Assert.Null(PlayScriptExceptionWatcher.FindScriptType(new InvalidOperationException()));
    }

    [Fact]
    public async Task AnEditorThatConnectsAfterTheExceptionLearnsWhichOne()
    {
        var agent = CreateAgent(new PlayLaunchOptions(PauseOnScriptException: true));
        var thrower = new ThrowingUpdateScript();
        _ = new Entity("EarlyThrower") { thrower };
        game.Script.Add(thrower);
        game.Script.Update(new GameTime());
        agent.Update(new GameTime());
        Assert.True(agent.PauseController.IsPaused);

        var editor = await ConnectAsync(agent);

        var exception = await editor.ReadAsync<PlayScriptException>();
        Assert.Equal("EarlyThrower", exception.EntityName);
    }

    [Fact]
    public void TheGameStartsOnTheSceneOfTheLaunchOptions()
    {
        game.SceneSystem.InitialSceneUrl = "MainScene";

        var agent = CreateAgent(new PlayLaunchOptions(Scene: "Scenes/Level2"), url => url is "Scenes/Level2" or "MainScene");

        // Before the scene system loads its content: it loads this scene instead of the default one
        Assert.True(agent.StartsOnScene);
        Assert.Equal("Scenes/Level2", game.SceneSystem.InitialSceneUrl);
        // The other options are not changed by it
        Assert.False(agent.PauseController.IsPaused);
    }

    [Fact]
    public void ASceneThatIsNotInTheBuiltDataLeavesTheDefaultScene()
    {
        game.SceneSystem.InitialSceneUrl = "MainScene";
        var messages = new List<ILogMessage>();
        void OnLogged(ILogMessage message) => messages.Add(message);
        GlobalLogger.GlobalMessageLogged += OnLogged;
        try
        {
            var agent = CreateAgent(new PlayLaunchOptions(StartPaused: true, Scene: "Scenes/Level3"), url => url == "MainScene");

            Assert.False(agent.StartsOnScene);
            Assert.Equal("MainScene", game.SceneSystem.InitialSceneUrl);
            // The editor's Output gets why, from the agent's log
            var warning = Assert.Single(messages, x => x.Module == "PlayMode" && x.Type == LogMessageType.Warning);
            Assert.Contains("Scenes/Level3", warning.Text);
            Assert.Contains("MainScene", warning.Text);
            // The game still starts paused
            Assert.True(agent.PauseController.IsPaused);
        }
        finally
        {
            GlobalLogger.GlobalMessageLogged -= OnLogged;
        }
    }

    [Fact]
    public void ThePlayModeTurnsOffTheCapOfAnUnfocusedGameUntilNoEditorFollowsIt()
    {
        Assert.True(game.TreatNotFocusedLikeMinimized);

        var agent = CreateAgent(PlayLaunchOptions.None);

        Assert.False(game.TreatNotFocusedLikeMinimized);
        agent.NoEditor();
        Frame(agent);
        Assert.True(game.TreatNotFocusedLikeMinimized);
    }

    [Fact]
    public void TheDestroyedAgentGivesTheCapBack()
    {
        game.TreatNotFocusedLikeMinimized = false;
        var agent = CreateAgent(PlayLaunchOptions.None);
        game.TreatNotFocusedLikeMinimized = true;
        // The game's own value from before the agent comes back, even when it was off
        agent.Dispose();

        Assert.False(game.TreatNotFocusedLikeMinimized);
    }

    [Fact]
    public async Task TheEditorKeepsTheWindowOnTopWhileItIsActiveAndThePinTurnsItOff()
    {
        var window = new AgentWindow();
        var agent = CreateAgent(new PlayLaunchOptions(WindowOnTop: true), window: window);
        Assert.True(agent.WindowController.IsEnabled);
        Assert.False(window.OnTop);
        var editor = await ConnectAsync(agent);

        editor.Send(new PlayEditorActive(true));
        await FrameUntilAsync(agent, () => window.OnTop, "the window on top");
        Assert.True(agent.WindowController.IsEditorActive);

        editor.Send(new PlayWindowOnTop(false));
        await FrameUntilAsync(agent, () => !window.OnTop, "the window back among the others");
        Assert.False(agent.WindowController.IsEnabled);

        editor.Send(new PlayWindowOnTop(true));
        await FrameUntilAsync(agent, () => window.OnTop, "the window on top again");

        // The editor leaves: the window goes back, and the game is capped again when unfocused
        editor.Dispose();
        await FrameUntilAsync(agent, () => agent.SubscriberCount == 0, "the editor gone");
        Assert.False(window.OnTop);
        Assert.True(agent.WindowController.IsReleased);
        Assert.True(game.TreatNotFocusedLikeMinimized);
    }

    [Fact]
    public void AGameWithoutAWindowIsNotKeptOnTop()
    {
        // A game that is not run has no window: the agent says it once, and the game goes on
        var warnings = new List<string>();
        void OnLogged(ILogMessage message)
        {
            if (message.Module == "PlayMode" && message.Type == LogMessageType.Warning)
                warnings.Add(message.Text);
        }
        GlobalLogger.GlobalMessageLogged += OnLogged;
        try
        {
            var agent = CreateAgent(new PlayLaunchOptions(WindowOnTop: true));
            agent.WindowController.SetEditorActive(true);
            agent.WindowController.SetEditorActive(false);
            agent.WindowController.SetEditorActive(true);

            Assert.False(agent.WindowController.IsOnTop);
            Assert.Single(warnings, x => x.Contains("on top", StringComparison.Ordinal));
        }
        finally
        {
            GlobalLogger.GlobalMessageLogged -= OnLogged;
        }
    }

    [Fact]
    public void WithoutASceneTheDefaultSceneStays()
    {
        game.SceneSystem.InitialSceneUrl = "MainScene";

        var agent = CreateAgent(PlayLaunchOptions.None, _ => throw new InvalidOperationException("No scene is looked for."));

        Assert.False(agent.StartsOnScene);
        Assert.Equal("MainScene", game.SceneSystem.InitialSceneUrl);
    }

    private PlayAgentSystem CreateAgent(PlayLaunchOptions options, Func<string, bool>? sceneExists = null, IPlayWindow? window = null)
    {
        var agent = new PlayAgentSystem(game, options, sceneExists, window);
        disposables.Add(agent);
        return agent;
    }

    private void Frame(PlayAgentSystem agent, int times = 1)
    {
        for (var i = 0; i < times; i++)
        {
            if (game.Script.Enabled)
                game.Script.Update(new GameTime());
            agent.Update(new GameTime());
            agent.Draw(new GameTime());
        }
    }

    /// <summary>
    /// Runs frames until the condition holds: the editor's messages arrive on the connection's task.
    /// </summary>
    private async Task FrameUntilAsync(PlayAgentSystem agent, Func<bool> condition, string what)
    {
        var start = DateTime.UtcNow;
        while (!condition())
        {
            if (DateTime.UtcNow - start > Timeout)
                Assert.Fail($"Timed out waiting for {what}.");
            Frame(agent);
            await Task.Delay(10);
        }
    }

    /// <summary>
    /// Connects an editor's end to the agent, and runs the agent's update that lets it join.
    /// </summary>
    private async Task<EditorEnd> ConnectAsync(PlayAgentSystem agent)
    {
        var name = $"stride-play-test-{Guid.NewGuid():N}";
        var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await Task.WhenAll(server.WaitForConnectionAsync(), client.ConnectAsync()).WaitAsync(Timeout);
        var editor = new EditorEnd(server);
        disposables.Add(editor);
        agent.AddSubscriber(client);
        agent.Update(new GameTime());
        return editor;
    }

    /// <summary>
    /// The editor's end of a connection: the messages the agent sends.
    /// </summary>
    private sealed class EditorEnd : IDisposable
    {
        private readonly PlayConnection connection;
        private readonly Channel<PlayMessage> received = Channel.CreateUnbounded<PlayMessage>();

        public EditorEnd(Stream stream)
        {
            connection = new PlayConnection(stream);
            connection.Start(x => received.Writer.TryWrite(x));
        }

        public async Task<T> ReadAsync<T>(Func<T, bool>? match = null)
            where T : PlayMessage
        {
            using var timeout = new CancellationTokenSource(Timeout);
            while (true)
            {
                var message = await received.Reader.ReadAsync(timeout.Token);
                if (message is T typed && match?.Invoke(typed) != false)
                    return typed;
            }
        }

        public void Send(PlayMessage message) => Assert.True(connection.Send(message));

        public void Dispose() => connection.DisposeAsync().AsTask().Wait(Timeout);
    }

    /// <summary>
    /// The game's window, as the agent keeps it on top: a game that is not run has none.
    /// </summary>
    private sealed class AgentWindow : IPlayWindow
    {
        public event EventHandler<EventArgs>? Activated
        {
            add { }
            remove { }
        }

        public event EventHandler<EventArgs>? Deactivated
        {
            add { }
            remove { }
        }

        public bool IsActive => false;

        public bool OnTop { get; private set; }

        public bool TrySetOnTop(bool onTop)
        {
            OnTop = onTop;
            return true;
        }
    }

    private sealed class CountingScript : SyncScript
    {
        public int Starts { get; private set; }

        public override void Start() => Starts++;

        public override void Update()
        {
        }
    }

    private sealed class ThrowingUpdateScript : SyncScript
    {
        public int Updates { get; private set; }

        public override void Update()
        {
            Updates++;
            throw new InvalidOperationException("Update failed.");
        }
    }

    private sealed class ThrowingStartScript : StartupScript
    {
        public override void Start() => throw new InvalidOperationException("Start failed.");
    }

    private sealed class ThrowingAsyncScript : AsyncScript
    {
        public override async Task Execute()
        {
            await Script.NextFrame();
            throw new InvalidOperationException("Execute failed.");
        }
    }
}
