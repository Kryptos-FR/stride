// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Stride.Core.Assets.Editor.Components.Build;
using Stride.Core.Diagnostics;
using Stride.Core.Translation;
using Stride.PlayMode.Protocol;

namespace Stride.Editor.PlayMode;

/// <summary>
/// Starts the games of the editor's own platform as play sessions: registered as the editor's <see cref="IGameLauncher"/>, it
/// makes Start project (F5) start the game with the play agent, so that the Live tab can follow it.
/// </summary>
/// <remarks>
/// One session at a time: starting a game while one plays ends the first. The last session stays in <see cref="Session"/>
/// after its game exits, until the next start replaces it, so that its last tree can still be shown.
/// </remarks>
[Experimental("STREXP012")]
public sealed class PlayModeService : IGameLauncher, IAsyncDisposable
{
    private readonly Lock @lock = new();
    private readonly Func<IPlayTransport> createTransport;
    private readonly PlayAgentFiles agentFiles;
    private readonly PlaySessionOptions options;
    private readonly Func<string, ILogger, IReadOnlyDictionary<string, string>, IGameProcess> startGame;
    // The ends of the sessions that were replaced or failed to start, awaited by DisposeAsync
    private readonly List<Task> endings = [];
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayModeService"/> class, which starts games with the agent next to the
    /// editor, over a named pipe.
    /// </summary>
    /// <param name="options">The options of the sessions; the defaults otherwise.</param>
    public PlayModeService(PlaySessionOptions? options = null)
        : this(() => new PipePlayTransport(), PlayAgentFiles.NextToEditor, options, PlayGameLauncher.Start)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayModeService"/> class.
    /// </summary>
    /// <param name="createTransport">Creates the transport of each session.</param>
    /// <param name="agentFiles">The agent's files to load into the games.</param>
    /// <param name="options">The options of the sessions; the defaults otherwise.</param>
    /// <param name="startGame">Starts a game: its assembly, the logger of its output, and the variables to add to its
    /// environment (<see cref="PlayGameLauncher.Start"/>).</param>
    public PlayModeService(Func<IPlayTransport> createTransport, PlayAgentFiles agentFiles, PlaySessionOptions? options, Func<string, ILogger, IReadOnlyDictionary<string, string>, IGameProcess> startGame)
    {
        ArgumentNullException.ThrowIfNull(createTransport);
        ArgumentNullException.ThrowIfNull(agentFiles);
        ArgumentNullException.ThrowIfNull(startGame);
        this.createTransport = createTransport;
        this.agentFiles = agentFiles;
        this.options = options ?? new PlaySessionOptions();
        this.startGame = startGame;
    }

    /// <summary>
    /// Raised when a session is created, before its game starts: a view attaches to its events there, so that it misses none.
    /// Raised on the thread that starts the game.
    /// </summary>
    public event EventHandler<PlaySession>? SessionStarted;

    /// <summary>
    /// Raised when <see cref="IsPlaying"/> changes: when a game starts, and when it exits. Raised on the thread of the change
    /// (the session's when the game exits).
    /// </summary>
    public event EventHandler? PlayingChanged;

    /// <summary>
    /// Whether a game starts with the live connection: the editor's setting "Connect to the game when starting the project".
    /// Read at each start; <c>true</c> by default.
    /// </summary>
    public Func<bool> ConnectToGame { get; set; } = static () => true;

    /// <summary>
    /// Whether the next game starts without the live connection, whatever <see cref="ConnectToGame"/> says (Start without the
    /// live view). The next start clears it; clear it too when the start does not happen (a failed build).
    /// </summary>
    public bool NextStartWithoutConnection { get; set; }

    /// <summary>
    /// What the games start with: start paused, pause on a script's exception, the key of the game's window. The editor's play
    /// mode settings; read at each start, none by default.
    /// </summary>
    public Func<PlayLaunchOptions> LaunchOptions { get; set; } = static () => PlayLaunchOptions.None;

    /// <summary>
    /// Whether the next game starts paused, whatever <see cref="LaunchOptions"/> says (Start project paused). The next start
    /// clears it; clear it too when the start does not happen (a failed build).
    /// </summary>
    public bool NextStartPaused { get; set; }

    /// <summary>
    /// The scene the next game starts on instead of its default scene (Play this scene); <c>null</c> for the default scene. The
    /// next start clears it; clear it too when the start does not happen (a failed build). The agent changes the scene, so the
    /// game starts with the live connection, whatever <see cref="ConnectToGame"/> says.
    /// </summary>
    public PlaySceneRequest? NextStartScene { get; set; }

    /// <summary>
    /// The current session, or the last one once its game exited; <c>null</c> before the first start.
    /// </summary>
    public PlaySession? Session { get; private set; }

    /// <summary>
    /// Whether the game of the current session still runs.
    /// </summary>
    public bool IsPlaying => Session is { Game: not null } session && !session.Exited.IsCompleted;

    /// <inheritdoc/>
    /// <remarks>
    /// Ends the game that plays, if any, then starts this one with the agent. The game starts before this method returns; its
    /// agent connects later (<see cref="PlaySession.StateChanged"/>). Without the live connection (<see cref="ConnectToGame"/>,
    /// <see cref="NextStartWithoutConnection"/>), the game starts as it would without play mode, in a session that only
    /// follows its process (<see cref="PlaySessionState.WithoutAgent"/>).
    /// </remarks>
    public IGameProcess Start(string assemblyPath, ILogger output)
    {
        ArgumentNullException.ThrowIfNull(assemblyPath);
        ArgumentNullException.ThrowIfNull(output);

        var once = NextStartWithoutConnection;
        var paused = NextStartPaused;
        var scene = NextStartScene;
        NextStartWithoutConnection = NextStartPaused = false;
        NextStartScene = null;
        // Only the agent can start the game on another scene
        var settingConnects = ConnectToGame();
        var connect = scene is not null || (!once && settingConnects);
        var launch = connect ? LaunchOptions() : PlayLaunchOptions.None;
        if (paused && connect)
            launch = launch with { StartPaused = true };
        if (scene is not null)
            launch = launch with { Scene = scene.Url };
        var session = new PlaySession(connect ? createTransport() : new NoPlayTransport(), agentFiles, options) { LaunchOptions = launch, Scene = scene };
        if (scene is not null)
        {
            // Once the agent connects: the Output pane follows the game's output only after this call returns
            var text = settingConnects
                ? scene.Description + "."
                : string.Format(Tr._p("Play", "{0}. The game connects to the editor this time, though the setting \"Connect to the game when starting the project\" is off: the play agent starts it on that scene."), scene.Description);
            var logged = 0;
            session.StateChanged += (_, change) =>
            {
                if (change.NewState == PlaySessionState.Connected && Interlocked.Exchange(ref logged, 1) == 0)
                    output.Info(text);
            };
        }
        // The exception that paused the game, with its stack, next to the game's own output
        session.ScriptExceptionReceived += (_, exception) => output.Error(PlayPauseText.DescribeForOutput(exception));
        PlaySession? previous;
        lock (@lock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            previous = Session;
            Session = session;
        }
        if (previous is not null)
            Retire(previous);

        SessionStarted?.Invoke(this, session);
        if (!connect)
            return StartWithoutAgent(session, assemblyPath, output, once);

        // The game starts within this call: the session awaits only once it waits for the agent
        var start = session.StartAsync(environment => startGame(assemblyPath, output, environment), PlayAgentFiles.GetGameStrideVersion(assemblyPath));
        if (start.IsFaulted || session.Game is null)
        {
            Retire(session);
            // The reason the game did not start, for the Output pane
            start.GetAwaiter().GetResult();
            throw new InvalidOperationException("The game did not start.");
        }

        Watch(start);
        PlayingChanged?.Invoke(this, EventArgs.Empty);
        session.Exited.ContinueWith(_ => PlayingChanged?.Invoke(this, EventArgs.Empty), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return session.Game;
    }

    private IGameProcess StartWithoutAgent(PlaySession session, string assemblyPath, ILogger output, bool once)
    {
        var reason = once
            ? "The game was started without the live view (Start without the live view)."
            : "The game was started without the live view: the setting \"Connect to the game when starting the project\" is off.";
        try
        {
            session.StartWithoutAgent(environment => startGame(assemblyPath, output, environment), reason);
        }
        catch
        {
            Retire(session);
            throw;
        }
        PlayingChanged?.Invoke(this, EventArgs.Empty);
        session.Exited.ContinueWith(_ => PlayingChanged?.Invoke(this, EventArgs.Empty), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return session.Game!;
    }

    /// <summary>
    /// Ends the game that plays, if any: asks it to quit, then ends its process after the grace period.
    /// </summary>
    public Task StopAsync() => Session?.StopAsync() ?? Task.CompletedTask;

    /// <summary>
    /// Ends the game that plays, if any, before the editor exits: asks it to quit and waits a little, then ends its process.
    /// </summary>
    /// <param name="timeout">How long the game has to quit before its process is ended.</param>
    public void EndNow(TimeSpan timeout)
    {
        if (Session is not { Game: { } game } session || session.Exited.IsCompleted)
            return;
        if (session.RequestQuit() && session.Exited.Wait(timeout))
            return;
        (game as IPlayGameProcess)?.Kill();
    }

    /// <summary>
    /// Ends the game that plays and the sessions.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        PlaySession? session;
        lock (@lock)
        {
            if (disposed)
                return;
            disposed = true;
            session = Session;
        }
        if (session is not null)
            await session.DisposeAsync().ConfigureAwait(false);
        Task[] pending;
        lock (@lock)
            pending = [.. endings];
        await Task.WhenAll(pending).ConfigureAwait(false);
    }

    /// <summary>
    /// Ends a session that is replaced: its game, if it still runs, then its connection.
    /// </summary>
    private void Retire(PlaySession session)
    {
        lock (@lock)
            endings.Add(session.DisposeAsync().AsTask());
    }

    private void Watch(Task start)
    {
        start.ContinueWith(x => options.Log?.Error("The play session failed to connect to the game.", x.Exception!.GetBaseException()),
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    /// <summary>
    /// The transport of a session started without the agent: nothing connects to it.
    /// </summary>
    private sealed class NoPlayTransport : IPlayTransport
    {
        public IReadOnlyDictionary<string, string> GameEnvironment { get; } = new Dictionary<string, string>();

        public Task<Stream> AcceptAsync(CancellationToken cancellationToken) => throw new NotSupportedException("A session started without the agent accepts no connection.");

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
