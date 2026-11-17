// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Stride.Core.Assets.Editor.Components.Build;
using Stride.Core.Diagnostics;
using Stride.PlayMode.Protocol;

namespace Stride.Editor.PlayMode;

/// <summary>
/// Where a play session is.
/// </summary>
[Experimental("STREXP012")]
public enum PlaySessionState
{
    /// <summary>The game is starting; its agent has not said hello yet.</summary>
    Starting,

    /// <summary>The agent said hello: the session receives the tree, snapshots and the game's log.</summary>
    Connected,

    /// <summary>The game exited (<see cref="PlaySession.ExitCode"/>). The session ends here.</summary>
    GameExited,

    /// <summary>The agent did not connect, or its connection broke while the game still runs
    /// (<see cref="PlaySession.StateMessage"/>). The game keeps running without play mode.</summary>
    Failed,

    /// <summary>The game runs another Stride version than the editor's agent: it was started without the agent.</summary>
    VersionMismatch,
}

/// <summary>
/// The options of a play session.
/// </summary>
[Experimental("STREXP012")]
public sealed record PlaySessionOptions
{
    /// <summary>
    /// How long the game's agent has to connect once the game is started.
    /// </summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long <see cref="PlaySession.StopAsync"/> lets the game quit before it ends the process.
    /// </summary>
    public TimeSpan StopGracePeriod { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How long the session waits for the game to exit after the connection ends, before it says the connection failed.
    /// </summary>
    public TimeSpan ExitAfterDisconnectDelay { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Receives the errors of the session's event handlers, which never stop the session.
    /// </summary>
    public ILogger? Log { get; init; }
}

/// <summary>
/// The arguments of <see cref="PlaySession.StateChanged"/>.
/// </summary>
/// <param name="OldState">The state before.</param>
/// <param name="NewState">The state now.</param>
/// <param name="Message">Why, for <see cref="PlaySessionState.Failed"/> and <see cref="PlaySessionState.VersionMismatch"/>.</param>
[Experimental("STREXP012")]
public sealed record PlaySessionStateChange(PlaySessionState OldState, PlaySessionState NewState, string? Message);

/// <summary>
/// One run of a game with the play agent in it: starts the game through a transport, follows its states, and raises the
/// agent's tree, snapshots (read into mirrors) and log as events.
/// </summary>
/// <remarks>
/// Not a singleton: an editor can run several sessions, and tests make their own. The events are raised on the session's
/// reading task, one at a time, never on a UI thread: a view dispatches them itself. The state only goes forward:
/// <see cref="PlaySessionState.Starting"/>, then <see cref="PlaySessionState.Connected"/>,
/// <see cref="PlaySessionState.Failed"/> or <see cref="PlaySessionState.VersionMismatch"/>, then
/// <see cref="PlaySessionState.GameExited"/> once the game's process ends (a connected session can also fail first).
/// </remarks>
[Experimental("STREXP012")]
public sealed class PlaySession : IAsyncDisposable
{
    private readonly object @lock = new();
    private readonly IPlayTransport transport;
    private readonly PlayAgentFiles agentFiles;
    private readonly PlaySessionOptions options;
    private readonly LiveMirrorReader mirrorReader;
    private readonly CancellationTokenSource lifetime = new();
    private readonly TaskCompletionSource exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private PlayConnection? connection;
    private Task? exitWatch;
    private Task? connectionWatch;
    private bool started;
    private bool goodbye;
    private int disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaySession"/> class.
    /// </summary>
    /// <param name="transport">The channel the agent connects through. The session owns it.</param>
    /// <param name="agentFiles">The agent's files to load into the game.</param>
    /// <param name="options">The options; the defaults otherwise.</param>
    /// <param name="assemblies">Compares the game's assemblies with the editor's; a new one that looks among the assemblies
    /// loaded in the editor otherwise.</param>
    /// <param name="mirrorReader">Reads snapshots into mirrors; one over <paramref name="assemblies"/> otherwise.</param>
    public PlaySession(IPlayTransport transport, PlayAgentFiles agentFiles, PlaySessionOptions? options = null, PlayAssemblyCheck? assemblies = null, LiveMirrorReader? mirrorReader = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(agentFiles);
        this.transport = transport;
        this.agentFiles = agentFiles;
        this.options = options ?? new PlaySessionOptions();
        Assemblies = assemblies ?? new PlayAssemblyCheck();
        this.mirrorReader = mirrorReader ?? new LiveMirrorReader(Assemblies);
    }

    /// <summary>
    /// Raised when the state changes.
    /// </summary>
    public event EventHandler<PlaySessionStateChange>? StateChanged;

    /// <summary>
    /// Raised with the whole tree: once after the hello, and again when the game replaces its root scene.
    /// </summary>
    public event EventHandler<PlayTree>? TreeReceived;

    /// <summary>
    /// Raised with the changes of the tree of one frame.
    /// </summary>
    public event EventHandler<PlayTreeBatch>? TreeChanged;

    /// <summary>
    /// Raised with each snapshot of the subscribed entities, read into mirrors.
    /// </summary>
    public event EventHandler<LiveSnapshot>? SnapshotReceived;

    /// <summary>
    /// Raised with each message of the game's log.
    /// </summary>
    public event EventHandler<PlayLog>? LogReceived;

    /// <summary>
    /// Raised with each heartbeat (about one per second).
    /// </summary>
    public event EventHandler<PlayHeartbeat>? HeartbeatReceived;

    /// <summary>
    /// The state.
    /// </summary>
    public PlaySessionState State { get; private set; }

    /// <summary>
    /// Why the session is in its state, when it failed or the versions differ.
    /// </summary>
    public string? StateMessage { get; private set; }

    /// <summary>
    /// The game's process, once started.
    /// </summary>
    public IGameProcess? Game { get; private set; }

    /// <summary>
    /// The exit code of the game, once it exited.
    /// </summary>
    public int? ExitCode { get; private set; }

    /// <summary>
    /// The agent's hello, once connected.
    /// </summary>
    public PlayHello? Hello { get; private set; }

    /// <summary>
    /// The last tree received.
    /// </summary>
    public PlayTree? Tree { get; private set; }

    /// <summary>
    /// The last heartbeat received, and when (UTC).
    /// </summary>
    public (PlayHeartbeat Heartbeat, DateTime ReceivedUtc)? LastHeartbeat { get; private set; }

    /// <summary>
    /// The comparison of the game's assemblies with the editor's.
    /// </summary>
    public PlayAssemblyCheck Assemblies { get; }

    /// <summary>
    /// Completes when the game exited.
    /// </summary>
    public Task Exited => exited.Task;

    /// <summary>
    /// Starts the game with the agent, and waits for the agent to connect (or for the game to exit, or for the connection to
    /// time out). The state then tells what happened; the hello comes later, on the reading task.
    /// </summary>
    /// <param name="startGame">Starts the game with the given variables added to its environment (empty to start it without
    /// the agent): <see cref="PlayGameLauncher.Start"/> in the editor, a fake in tests.</param>
    /// <param name="gameStrideVersion">The Stride version the game runs (<see cref="PlayAgentFiles.GetGameStrideVersion"/>),
    /// checked before the start; <c>null</c> to leave the check to the agent's hook.</param>
    /// <param name="cancellationToken">Cancels the wait for the agent; the game keeps running.</param>
    public async Task StartAsync(Func<IReadOnlyDictionary<string, string>, IGameProcess> startGame, string? gameStrideVersion = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(startGame);
        ObjectDisposedException.ThrowIf(disposed != 0, this);
        lock (@lock)
        {
            if (started)
                throw new InvalidOperationException("The session was already started.");
            started = true;
        }

        if (gameStrideVersion is not null && !PlayAgentFiles.AreSameVersion(gameStrideVersion, agentFiles.StrideVersion))
        {
            StartWithoutAgent(startGame, PlaySessionState.VersionMismatch, $"The game runs Stride {gameStrideVersion} and the editor {agentFiles.StrideVersion}: play mode needs the same version. The game was started without it.");
            return;
        }
        if (!agentFiles.Exist)
        {
            StartWithoutAgent(startGame, PlaySessionState.Failed, $"The play agent's files are missing from {agentFiles.Directory}. The game was started without it.");
            return;
        }

        var environment = agentFiles.CreateGameEnvironment(transport.GameEnvironment, Environment.GetEnvironmentVariable(PlayProtocol.StartupHooksVariable));
        var game = startGame(environment);
        WatchExit(game);

        Stream stream;
        using (var connecting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token))
        {
            connecting.CancelAfter(options.ConnectTimeout);
            try
            {
                var accept = transport.AcceptAsync(connecting.Token);
                if (await Task.WhenAny(accept, exited.Task).ConfigureAwait(false) != accept)
                {
                    // The game exited before its agent connected: the exit ends the session
                    await connecting.CancelAsync().ConfigureAwait(false);
                    await accept.ContinueWith(static _ => { }, TaskScheduler.Default).ConfigureAwait(false);
                    return;
                }
                stream = await accept.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !lifetime.IsCancellationRequested)
            {
                SetState(PlaySessionState.Failed, $"The game's agent did not connect within {options.ConnectTimeout.TotalSeconds:0} s.");
                return;
            }
        }

        var newConnection = new PlayConnection(stream);
        lock (@lock)
            connection = newConnection;
        newConnection.Start(OnMessage);
        connectionWatch = WatchConnectionAsync(newConnection);
    }

    /// <summary>
    /// Asks for snapshots of entities, replacing the previous request; an empty list stops them.
    /// </summary>
    /// <param name="handles">The handles of the entities (<see cref="PlayEntityInfo.Handle"/>).</param>
    /// <param name="rateHz">How many snapshots per second; the agent caps it.</param>
    /// <returns><c>false</c> when the session is not connected or the request could not be queued.</returns>
    public bool Subscribe(IReadOnlyList<int> handles, double rateHz = 5)
    {
        ArgumentNullException.ThrowIfNull(handles);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rateHz);
        return SendIfConnected(new PlaySubscribe(handles, rateHz));
    }

    /// <summary>
    /// Asks the game to quit as its window's close button does.
    /// </summary>
    /// <returns><c>false</c> when the session is not connected or the request could not be queued.</returns>
    public bool RequestQuit() => SendIfConnected(new PlayQuit());

    /// <summary>
    /// Ends the game: asks it to quit, and ends its process if it still runs after <see cref="PlaySessionOptions.StopGracePeriod"/>.
    /// </summary>
    public async Task StopAsync()
    {
        var game = Game;
        if (game is null || exited.Task.IsCompleted)
            return;
        if (RequestQuit())
        {
            var finished = await Task.WhenAny(exited.Task, Task.Delay(options.StopGracePeriod)).ConfigureAwait(false);
            if (finished == exited.Task)
                return;
        }
        if (game is IPlayGameProcess process)
        {
            process.Kill();
            await Task.WhenAny(exited.Task, Task.Delay(options.StopGracePeriod)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Ends the game (<see cref="StopAsync"/>), the connection and the transport.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        await StopAsync().ConfigureAwait(false);
        await lifetime.CancelAsync().ConfigureAwait(false);
        PlayConnection? toClose;
        lock (@lock)
            toClose = connection;
        if (toClose is not null)
            await toClose.DisposeAsync().ConfigureAwait(false);
        await transport.DisposeAsync().ConfigureAwait(false);
        foreach (var task in new[] { exitWatch, connectionWatch })
        {
            if (task is null)
                continue;
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The session ended before the game
            }
        }
        lifetime.Dispose();
    }

    private void StartWithoutAgent(Func<IReadOnlyDictionary<string, string>, IGameProcess> startGame, PlaySessionState state, string message)
    {
        var game = startGame(new Dictionary<string, string>());
        SetState(state, message);
        WatchExit(game);
    }

    private void WatchExit(IGameProcess game)
    {
        Game = game;
        exitWatch = WatchExitAsync(game);
    }

    private async Task WatchExitAsync(IGameProcess game)
    {
        var exitCode = await game.Exited.WaitAsync(lifetime.Token).ConfigureAwait(false);
        ExitCode = exitCode;
        SetState(PlaySessionState.GameExited, null);
        exited.TrySetResult();
    }

    private async Task WatchConnectionAsync(PlayConnection watched)
    {
        string? error = null;
        try
        {
            await watched.Completion.WaitAsync(lifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            return;
        }
        catch (Exception e)
        {
            error = e.Message;
        }

        // The game usually exits right after its agent closes the connection
        try
        {
            await exited.Task.WaitAsync(goodbye ? Timeout.InfiniteTimeSpan : options.ExitAfterDisconnectDelay, lifetime.Token).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            SetState(PlaySessionState.Failed, error is null ? "The game's agent closed the connection, but the game still runs." : $"The connection to the game's agent broke: {error}");
        }
        catch (OperationCanceledException)
        {
            // The session ended
        }
    }

    private bool SendIfConnected(PlayMessage message)
    {
        PlayConnection? current;
        lock (@lock)
            current = State == PlaySessionState.Connected ? connection : null;
        return current?.Send(message) ?? false;
    }

    private void OnMessage(PlayMessage message)
    {
        switch (message)
        {
            case PlayHello hello:
                Hello = hello;
                Assemblies.Add(hello.Assemblies);
                if (!PlayAgentFiles.AreSameVersion(hello.StrideVersion, agentFiles.StrideVersion))
                    SetState(PlaySessionState.VersionMismatch, $"The game runs Stride {hello.StrideVersion} and the editor {agentFiles.StrideVersion}: play mode needs the same version.");
                else
                    SetState(PlaySessionState.Connected, null);
                return;
            case PlayGoodbye:
                goodbye = true;
                return;
        }

        // Only a connected session reads what the agent sends
        if (State != PlaySessionState.Connected)
            return;
        switch (message)
        {
            case PlayAssemblies assemblies:
                Assemblies.Add(assemblies.Assemblies);
                break;
            case PlayTree tree:
                Tree = tree;
                Raise(TreeReceived, tree);
                break;
            case PlayTreeBatch batch:
                Raise(TreeChanged, batch);
                break;
            case PlaySnapshot snapshot when SnapshotReceived is not null:
                Raise(SnapshotReceived, mirrorReader.Read(snapshot));
                break;
            case PlayLog log:
                Raise(LogReceived, log);
                break;
            case PlayHeartbeat heartbeat:
                LastHeartbeat = (heartbeat, DateTime.UtcNow);
                Raise(HeartbeatReceived, heartbeat);
                break;
        }
    }

    private void SetState(PlaySessionState state, string? message)
    {
        PlaySessionState old;
        lock (@lock)
        {
            old = State;
            // Forward only: an exited game ends the session; a failed or mismatched one does not connect again
            if (old == state || old == PlaySessionState.GameExited || (old is PlaySessionState.Failed or PlaySessionState.VersionMismatch && state != PlaySessionState.GameExited))
                return;
            State = state;
            if (message is not null)
                StateMessage = message;
        }
        Raise(StateChanged, new PlaySessionStateChange(old, state, message));
    }

    private void Raise<T>(EventHandler<T>? handler, T args)
    {
        if (handler is null)
            return;
        try
        {
            handler(this, args);
        }
        catch (Exception e)
        {
            options.Log?.Error($"A play session handler failed on {typeof(T).Name}.", e);
        }
    }
}
