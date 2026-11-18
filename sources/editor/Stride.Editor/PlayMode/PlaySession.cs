// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Stride.Core.Assets.Editor.Components.Build;
using Stride.Core.Diagnostics;
using Stride.PlayMode.Protocol;
using Stride.PlayMode.Snapshots;

namespace Stride.Editor.PlayMode;

/// <summary>
/// An edit of a live value and its answer (<see cref="PlaySession.EditAnswered"/>).
/// </summary>
/// <param name="Edit">The edit sent.</param>
/// <param name="Result">The game's answer, or the session's when the game could not answer.</param>
[Experimental("STREXP012")]
public sealed record PlayEditAnswer(PlayEdit Edit, PlayEditResult Result);

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

    /// <summary>The game was started without the agent on purpose (<see cref="PlaySession.StartWithoutAgent"/>): it runs as a
    /// game started without play mode, and the session only follows its process.</summary>
    WithoutAgent,
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
    /// How long a connected game can send nothing (the agent sends a heartbeat every second) before the session says it does
    /// not respond (<see cref="PlaySession.IsResponding"/>).
    /// </summary>
    public TimeSpan NotRespondingAfter { get; init; } = TimeSpan.FromSeconds(5);

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
/// <see cref="PlaySessionState.GameExited"/> once the game's process ends (a connected session can also fail first). A session
/// started with <see cref="StartWithoutAgent"/> goes from <see cref="PlaySessionState.WithoutAgent"/> to
/// <see cref="PlaySessionState.GameExited"/>.
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
    // The edits sent and not answered yet, by id
    private readonly ConcurrentDictionary<int, (PlayEdit Edit, TaskCompletionSource<PlayEditResult> Completion)> pendingEdits = new();
    private int lastEditId;
    private PlayConnection? connection;
    private Task? exitWatch;
    private Task? connectionWatch;
    private Task? respondingWatch;
    // When the agent last sent something (Stopwatch ticks)
    private long lastReceived;
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
    /// Raised when <see cref="IsResponding"/> changes.
    /// </summary>
    public event EventHandler<bool>? RespondingChanged;

    /// <summary>
    /// Raised with each state of the game the agent sends (<see cref="GameState"/>): when it is paused, resumed or stepped, when
    /// its time scale changes, and after each heartbeat.
    /// </summary>
    public event EventHandler<PlayGameState>? GameStateChanged;

    /// <summary>
    /// Raised when a script of the game threw an exception that the engine caught (the game was started with
    /// <see cref="PlayLaunchOptions.PauseOnScriptException"/>): the game is paused for it (<see cref="LastScriptException"/>).
    /// </summary>
    public event EventHandler<PlayScriptException>? ScriptExceptionReceived;

    /// <summary>
    /// What the session asks of the game's agent when it starts it: start paused, pause on a script's exception, the key of the
    /// game's window. Given to the game as variables of its environment. None by default.
    /// </summary>
    public PlayLaunchOptions LaunchOptions { get; init; } = PlayLaunchOptions.None;

    /// <summary>
    /// The scene the game was asked to start on instead of its default scene (Play this scene); <c>null</c> for the default
    /// scene. Its URL is in <see cref="LaunchOptions"/>.
    /// </summary>
    public PlaySceneRequest? Scene { get; init; }

    /// <summary>
    /// The last script exception the game sent; <c>null</c> before the first.
    /// </summary>
    public PlayScriptException? LastScriptException { get; private set; }

    /// <summary>
    /// Why the connected game is paused, as its agent last said; <see cref="PlayPauseReason.None"/> while it plays.
    /// </summary>
    public PlayPauseReason PauseReason => IsPaused ? GameState!.PauseReason : PlayPauseReason.None;

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
    /// The last state of the game the agent sent: paused or playing, its time scale, frame rate and entities; <c>null</c> before
    /// the first.
    /// </summary>
    public PlayGameState? GameState { get; private set; }

    /// <summary>
    /// Whether the connected game is paused (<see cref="Pause"/>, <see cref="Step"/>), as its agent last said.
    /// </summary>
    public bool IsPaused => State == PlaySessionState.Connected && GameState is { Paused: true };

    /// <summary>
    /// The comparison of the game's assemblies with the editor's.
    /// </summary>
    public PlayAssemblyCheck Assemblies { get; }

    /// <summary>
    /// Whether the connected game sent something within <see cref="PlaySessionOptions.NotRespondingAfter"/>. A game that is
    /// busy, stuck, or stopped at a breakpoint of a debugger stops sending; it answers again when it goes on. <c>true</c>
    /// while the session is not connected.
    /// </summary>
    public bool IsResponding { get; private set; } = true;

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

        var environment = new Dictionary<string, string>(agentFiles.CreateGameEnvironment(transport.GameEnvironment, Environment.GetEnvironmentVariable(PlayProtocol.StartupHooksVariable)));
        foreach (var (name, value) in LaunchOptions.ToVariables())
            environment[name] = value;
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
                SetState(PlaySessionState.Failed, $"The game runs without the live view: its play agent did not connect within {options.ConnectTimeout.TotalSeconds:0} s. A trimmed or ahead-of-time compiled game, or one that sets its own startup hooks, cannot load the agent.");
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
    /// Starts the game without the agent, as a game started without play mode: the session only follows its process (its exit,
    /// and <see cref="StopAsync"/>, which ends it since it cannot be asked to quit).
    /// </summary>
    /// <param name="startGame">Starts the game; it is given no variable to add.</param>
    /// <param name="reason">Why the game runs without the live connection, as <see cref="StateMessage"/>.</param>
    public void StartWithoutAgent(Func<IReadOnlyDictionary<string, string>, IGameProcess> startGame, string reason)
    {
        ArgumentNullException.ThrowIfNull(startGame);
        ArgumentNullException.ThrowIfNull(reason);
        ObjectDisposedException.ThrowIf(disposed != 0, this);
        lock (@lock)
        {
            if (started)
                throw new InvalidOperationException("The session was already started.");
            started = true;
        }
        StartWithoutAgent(startGame, PlaySessionState.WithoutAgent, reason);
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
    /// Asks the game to pause: its time stops (animation, physics, particles), its scripts and the systems that follow the real
    /// time stop, its audio pauses, and it goes on drawing and sending snapshots. <see cref="GameStateChanged"/> follows.
    /// </summary>
    /// <returns><c>false</c> when the session is not connected or the request could not be queued.</returns>
    public bool Pause() => SendIfConnected(new PlayPause());

    /// <summary>
    /// Asks the paused game to go on as it was; the time paused is not counted.
    /// </summary>
    /// <returns><c>false</c> when the session is not connected or the request could not be queued.</returns>
    public bool Resume() => SendIfConnected(new PlayResume());

    /// <summary>
    /// Asks the game to run updates of its fixed step and stay paused; a game that plays is paused first.
    /// </summary>
    /// <param name="frames">How many updates.</param>
    /// <returns><c>false</c> when the session is not connected or the request could not be queued.</returns>
    public bool Step(int frames = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frames);
        return SendIfConnected(new PlayStep(frames));
    }

    /// <summary>
    /// Sets the speed of the game time, a multiple of the game's own time factor; it is kept through pauses.
    /// </summary>
    /// <param name="scale">The speed, 1 for the game's own; the agent keeps it within
    /// [<see cref="PlayGameState.MinTimeScale"/>, <see cref="PlayGameState.MaxTimeScale"/>].</param>
    /// <returns><c>false</c> when the session is not connected or the request could not be queued.</returns>
    public bool SetTimeScale(double scale)
    {
        if (!double.IsFinite(scale) || scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "The time scale must be a positive number.");
        return SendIfConnected(new PlayTimeScale(scale));
    }

    /// <summary>
    /// Turns on or off keeping the game's window on top while the editor or the game is the active app (the play toolbar's
    /// pin); the launch gives the first value (<see cref="PlayLaunchOptions.WindowOnTop"/>).
    /// </summary>
    /// <returns><c>false</c> when the session is not connected or the request could not be queued.</returns>
    public bool SetWindowOnTop(bool onTop) => SendIfConnected(new PlayWindowOnTop(onTop));

    /// <summary>
    /// Tells the game whether the editor is the active app: its window stays on top while the editor or the game is active.
    /// </summary>
    /// <returns><c>false</c> when the session is not connected or the request could not be queued.</returns>
    public bool SetEditorActive(bool active) => SendIfConnected(new PlayEditorActive(active));

    /// <summary>
    /// Asks the game to quit as its window's close button does.
    /// </summary>
    /// <returns><c>false</c> when the session is not connected or the request could not be queued.</returns>
    public bool RequestQuit() => SendIfConnected(new PlayQuit());

    /// <summary>
    /// Raised when an edit sent by <see cref="EditAsync"/> is answered, by the game or by the session when it ends first: on the
    /// session's reading task, or on the thread that ends the session.
    /// </summary>
    public event EventHandler<PlayEditAnswer>? EditAnswered;

    /// <summary>
    /// The local time the session was made: when the game started, once it was built. An asset changed since (its
    /// <see cref="Stride.Core.Assets.AssetItem.ModifiedTime"/> is later) is in the game's build as it was before.
    /// </summary>
    public DateTime StartTime { get; } = DateTime.Now;

    /// <summary>
    /// The URLs of the game's built content, which the agent sends when asked at the connection; <c>null</c> until then. A live
    /// reference to content can only be set to one of them: the game loads content from its build.
    /// </summary>
    public IReadOnlySet<string>? BuildContent { get; private set; }

    /// <summary>
    /// Raised when <see cref="BuildContent"/> is received, on the session's reading task.
    /// </summary>
    public event EventHandler<IReadOnlySet<string>>? BuildContentReceived;

    /// <summary>
    /// Makes the stand-in of a live entity or component for a reference the user picked (see
    /// <see cref="LiveMirrorReader.CreateStandIn"/>).
    /// </summary>
    public (object StandIn, IReadOnlyDictionary<object, PlayMirrorReference> Map)? CreateStandIn(PlayMirrorReference reference) => mirrorReader.CreateStandIn(reference);

    /// <summary>
    /// The edits sent and not answered yet.
    /// </summary>
    public int PendingEditCount => pendingEdits.Count;

    /// <summary>
    /// Gives the id of a new edit: each edit of a session has its own.
    /// </summary>
    public int NextEditId() => Interlocked.Increment(ref lastEditId);

    /// <summary>
    /// Sends an edit of a live value to the game, which applies it at the start of its next frame and answers with the value
    /// it reads back.
    /// </summary>
    /// <param name="edit">The edit, with an id from <see cref="NextEditId"/>.</param>
    /// <returns>The game's answer. When the session is not connected, or it ends before the game answers (the game stopped or
    /// its connection broke), a <see cref="PlayEditStatus.Failed"/> result that says so, made by the session.</returns>
    public Task<PlayEditResult> EditAsync(PlayEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        var completion = new TaskCompletionSource<PlayEditResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!pendingEdits.TryAdd(edit.Id, (edit, completion)))
            throw new ArgumentException($"An edit with the id {edit.Id} is already waiting for its result.", nameof(edit));
        // A session that ended between the check and the add is caught by the end's own sweep
        if (State != PlaySessionState.Connected || !SendIfConnected(edit))
            FailEdit(edit.Id, State == PlaySessionState.Connected ? "The edit could not be sent to the game (its queue is full)." : NotConnectedMessage());
        else if (State != PlaySessionState.Connected)
            FailPendingEdits();
        return completion.Task;
    }

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
        FailPendingEdits();
        foreach (var task in new[] { exitWatch, connectionWatch, respondingWatch })
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
        Volatile.Write(ref lastReceived, Stopwatch.GetTimestamp());
        if (!IsResponding && State == PlaySessionState.Connected)
            SetResponding(true);
        switch (message)
        {
            case PlayHello hello:
                Hello = hello;
                Assemblies.Add(hello.Assemblies);
                if (!PlayAgentFiles.AreSameVersion(hello.StrideVersion, agentFiles.StrideVersion))
                    SetState(PlaySessionState.VersionMismatch, $"The game runs Stride {hello.StrideVersion} and the editor {agentFiles.StrideVersion}: play mode needs the same version.");
                else if (SetState(PlaySessionState.Connected, null))
                {
                    respondingWatch = WatchRespondingAsync();
                    // The live pickers offer the content of the build: ask once, at the start
                    SendIfConnected(new PlayContentRequest());
                }
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
            case PlayGameState state:
                GameState = state;
                Raise(GameStateChanged, state);
                break;
            case PlayScriptException exception:
                LastScriptException = exception;
                Raise(ScriptExceptionReceived, exception);
                break;
            case PlayContentIndex index:
                var urls = index.Urls.ToHashSet(StringComparer.Ordinal);
                BuildContent = urls;
                Raise(BuildContentReceived, (IReadOnlySet<string>)urls);
                break;
            case PlayEditResult result:
                if (pendingEdits.TryRemove(result.Id, out var pending))
                {
                    Raise(EditAnswered, new PlayEditAnswer(pending.Edit, result));
                    pending.Completion.TrySetResult(result);
                }
                break;
        }
    }

    private string NotConnectedMessage() => State switch
    {
        PlaySessionState.GameExited => "The game stopped before the edit applied.",
        PlaySessionState.Connected => string.Empty,
        _ => "The game is not connected: the edit was not sent.",
    };

    private void FailEdit(int id, string error)
    {
        if (pendingEdits.TryRemove(id, out var pending))
        {
            var result = new PlayEditResult(id, 0, PlayEditStatus.Failed, PlayEditValue.Null, error);
            Raise(EditAnswered, new PlayEditAnswer(pending.Edit, result));
            pending.Completion.TrySetResult(result);
        }
    }

    /// <summary>
    /// The session is no longer connected: the edits sent will not be answered.
    /// </summary>
    private void FailPendingEdits()
    {
        var error = NotConnectedMessage();
        foreach (var id in pendingEdits.Keys)
            FailEdit(id, error.Length > 0 ? error : "The game stopped before the edit applied.");
    }

    private bool SetState(PlaySessionState state, string? message)
    {
        PlaySessionState old;
        lock (@lock)
        {
            old = State;
            // Forward only: an exited game ends the session; a failed, mismatched or agentless one does not connect again
            if (old == state || old == PlaySessionState.GameExited || (old is PlaySessionState.Failed or PlaySessionState.VersionMismatch or PlaySessionState.WithoutAgent && state != PlaySessionState.GameExited))
                return false;
            State = state;
            if (message is not null)
                StateMessage = message;
        }
        if (old == PlaySessionState.Connected)
            FailPendingEdits();
        Raise(StateChanged, new PlaySessionStateChange(old, state, message));
        return true;
    }

    /// <summary>
    /// While the session is connected, checks that the game keeps sending (the agent's heartbeat comes every second).
    /// </summary>
    private async Task WatchRespondingAsync()
    {
        var limit = options.NotRespondingAfter;
        var period = TimeSpan.FromTicks(Math.Max(limit.Ticks / 5, TimeSpan.FromMilliseconds(20).Ticks));
        using var timer = new PeriodicTimer(period);
        try
        {
            while (State == PlaySessionState.Connected && await timer.WaitForNextTickAsync(lifetime.Token).ConfigureAwait(false))
            {
                if (IsResponding && State == PlaySessionState.Connected && Stopwatch.GetElapsedTime(Volatile.Read(ref lastReceived)) > limit)
                    SetResponding(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The session ended
        }
        // A game that exits or loses its connection is not waited for any more
        if (!IsResponding)
            SetResponding(true);
    }

    private void SetResponding(bool responding)
    {
        lock (@lock)
        {
            if (IsResponding == responding)
                return;
            IsResponding = responding;
        }
        Raise(RespondingChanged, responding);
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
