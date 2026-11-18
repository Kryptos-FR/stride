// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.IO.Pipes;
using System.Reflection;
using System.Threading.Channels;
using Scheduler = Stride.Core.MicroThreading.Scheduler;
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

    private PlayAgentSystem CreateAgent(PlayLaunchOptions options)
    {
        var agent = new PlayAgentSystem(game, options);
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

        public void Dispose() => connection.DisposeAsync().AsTask().Wait(Timeout);
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
