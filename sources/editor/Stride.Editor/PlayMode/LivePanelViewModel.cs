// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Stride.Core.Assets.Editor.Panels;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Extensions;
using Stride.Core.Presentation.Commands;
using Stride.Core.Presentation.Services;
using Stride.Core.Translation;
using Stride.PlayMode.Protocol;

namespace Stride.Editor.PlayMode;

/// <summary>
/// Where the Live tab's game is.
/// </summary>
[Experimental("STREXP012")]
public enum LiveTabState
{
    /// <summary>No game was started from the editor in this session.</summary>
    NotPlaying,

    /// <summary>The game is starting; its agent has not connected yet.</summary>
    Connecting,

    /// <summary>The game runs and its tree follows it.</summary>
    Playing,

    /// <summary>The game is paused (Pause, Step): it goes on drawing, its tree and the selected entities follow it.</summary>
    Paused,

    /// <summary>The game runs without the live connection (the agent did not connect, the connection broke, the versions
    /// differ, or the game was started without it): the tab keeps what it had, and says why.</summary>
    NotConnected,

    /// <summary>The game is connected but has sent nothing for a few seconds: it may be busy, stuck, or stopped at a breakpoint
    /// of a debugger.</summary>
    NotResponding,

    /// <summary>The game exited: the tab keeps its last tree, greyed.</summary>
    Stopped,
}

/// <summary>
/// The Live tab: the running game's scenes and entities, while a game started with Start project plays, and the live entities
/// selected there (<see cref="Selection"/>), for the property grid.
/// </summary>
/// <remarks>
/// It follows the session of <see cref="PlayModeService"/>: each start replaces the tree. The agent's tree and batches are
/// applied on the UI thread at most 10 times a second. The selection goes to an <see cref="ILiveSelectionView"/> (the property
/// grid), which asks the game for the entities' snapshots while it shows them.
/// </remarks>
[Experimental("STREXP012")]
public sealed class LivePanelViewModel : PanelViewModel
{
    /// <summary>
    /// How often the selected entities are sent, in snapshots per second.
    /// </summary>
    public const double SnapshotRateHz = 5;

    private readonly PlayModeService playMode;
    private readonly ILiveSceneAssets? sceneAssets;
    private readonly ILiveSelectionView? selectionView;
    private readonly IDispatcherService dispatcher;
    private readonly ConcurrentQueue<(PlaySession Session, PlayMessage Message)> pending = new();
    private readonly Lock @lock = new();
    // The work this view model started on other threads and on the dispatcher, awaited by WhenIdleAsync
    private readonly HashSet<Task> running = [];
    private readonly CancellationTokenSource lifetime = new();
    private PlaySession? session;
    private bool applyScheduled;
    private long lastApply;
    private bool updatingSelection;
    // The silence of the game the user chose to wait through
    private bool waiting;
    // The reason of the pause the tab shows, and the exception it showed last (dismissed or not)
    private PlayPauseReason shownReason;
    // Whether the first tree said which scene the game started on
    private bool sceneChecked;
    private PlayScriptException? shownException;
    private readonly bool ownsControls;
    private readonly ILivePrefabPicker? prefabPicker;
    // The entities a command added, to select once a tree batch brings them
    private List<int>? pendingSelection;

    /// <summary>
    /// Initializes a new instance of the <see cref="LivePanelViewModel"/> class.
    /// </summary>
    /// <param name="context">The context of the panel.</param>
    /// <param name="playMode">The play sessions the tab follows.</param>
    /// <param name="sceneAssets">The scene assets of the session, to mark the spawned entities and to show an entity in its
    /// scene editor; <c>null</c> for neither.</param>
    /// <param name="selectionView">Shows the values of the selected entities (the property grid); the tab disposes it.
    /// <c>null</c> for none: the selection is then only exposed.</param>
    /// <param name="reloadCodeCommand">Reloads the game's code in the editor, offered when the game runs another build of it;
    /// <c>null</c> for none.</param>
    /// <param name="controls">Pause, Step and the time scale of the toolbar, shared with the main window's commands; a new one
    /// the tab destroys otherwise.</param>
    /// <param name="prefabPicker">Picks the prefab to spawn in the running game; <c>null</c> to offer no spawn.</param>
    public LivePanelViewModel(PanelContext context, PlayModeService playMode, ILiveSceneAssets? sceneAssets, ILiveSelectionView? selectionView = null, ICommandBase? reloadCodeCommand = null, PlayControlsViewModel? controls = null, ILivePrefabPicker? prefabPicker = null)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(playMode);
        this.playMode = playMode;
        ownsControls = controls is null;
        Controls = controls ?? new PlayControlsViewModel(ServiceProvider, playMode);
        this.sceneAssets = sceneAssets;
        this.selectionView = selectionView;
        ReloadCodeCommand = reloadCodeCommand;
        dispatcher = ServiceProvider.Get<IDispatcherService>();
        Tree = new LiveTree(sceneAssets is not null ? sceneAssets.GetEntityIds : null);
        SelectedItems.CollectionChanged += SelectedItemsChanged;
        ShowInSceneEditorCommand = new AnonymousTaskCommand<object?>(ServiceProvider, ShowInSceneEditorAsync, x => sceneAssets is not null && Target(x) is { CounterpartScene: not null });
        CopyNameCommand = new AnonymousTaskCommand<object?>(ServiceProvider, x => CopyAsync(x, item => item.Name));
        CopyIdCommand = new AnonymousTaskCommand<object?>(ServiceProvider, x => CopyAsync(x, item => item.Id.ToString()));
        ExpandAllCommand = new AnonymousCommand(ServiceProvider, () => Tree.SetAllExpanded(true));
        CollapseAllCommand = new AnonymousCommand(ServiceProvider, () => Tree.SetAllExpanded(false));
        WaitCommand = new AnonymousCommand(ServiceProvider, Wait);
        EndGameCommand = new AnonymousCommand(ServiceProvider, EndGame);
        SelectExceptionEntityCommand = new AnonymousCommand(ServiceProvider, SelectExceptionEntity) { IsEnabled = false };
        DismissExceptionCommand = new AnonymousCommand(ServiceProvider, () => ScriptException = null);
        StopGameCommand = new AnonymousTaskCommand(ServiceProvider, () => session?.StopAsync() ?? Task.CompletedTask);
        ShowSelectionCommand = new AnonymousCommand(ServiceProvider, () => selectionView?.Show(Selection));
        this.prefabPicker = prefabPicker;
        CreateEntityCommand = new AnonymousTaskCommand<object?>(ServiceProvider, CreateEntityAsync) { IsEnabled = false };
        DeleteEntityCommand = new AnonymousTaskCommand<object?>(ServiceProvider, DeleteEntitiesAsync) { IsEnabled = false };
        DuplicateEntityCommand = new AnonymousTaskCommand<object?>(ServiceProvider, DuplicateEntitiesAsync) { IsEnabled = false };
        SpawnPrefabCommand = new AnonymousTaskCommand<object?>(ServiceProvider, SpawnPrefabAsync) { IsEnabled = false };
        ApplyToAssetCommand = new AnonymousTaskCommand(ServiceProvider, ApplyToAssetAsync) { IsEnabled = false };
        if (selectionView is not null)
            selectionView.EditsEnabledChanged += SelectionViewEditsEnabledChanged;

        playMode.SessionStarted += SessionStarted;
        if (playMode.Session is { } current)
            Attach(current, replay: true);
    }

    /// <summary>
    /// The shortest time between two updates of the tree.
    /// </summary>
    internal static TimeSpan MinimumApplyInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// The scenes and entities of the running game, and the rows to show.
    /// </summary>
    public LiveTree Tree { get; }

    /// <summary>
    /// The selected rows (scenes and entities); several can be selected.
    /// </summary>
    public ObservableCollection<object> SelectedItems { get; } = [];

    /// <summary>
    /// Pause, Step, the time scale and the frame rate of the toolbar.
    /// </summary>
    public PlayControlsViewModel Controls { get; }

    /// <summary>
    /// The live entities selected, with the session that plays them; <c>null</c> when no entity is selected or no game plays.
    /// </summary>
    public LiveSelection? Selection { get; private set => SetValue(ref field, value, () => selectionView?.Show(value)); }

    /// <summary>
    /// Where the tab's game is.
    /// </summary>
    public LiveTabState State
    {
        get;
        private set => SetValue(ref field, value, nameof(State), nameof(IsLive), nameof(IsPlaying), nameof(IsPaused), nameof(IsNotResponding), nameof(StateText));
    } = LiveTabState.NotPlaying;

    /// <summary>
    /// Whether the game plays and answers.
    /// </summary>
    public bool IsPlaying => State == LiveTabState.Playing;

    /// <summary>
    /// Whether the game is paused.
    /// </summary>
    public bool IsPaused => State == LiveTabState.Paused;

    /// <summary>
    /// Whether the connected game has sent nothing for a few seconds.
    /// </summary>
    public bool IsNotResponding => State == LiveTabState.NotResponding;

    /// <summary>
    /// Whether the tree follows a game that runs; otherwise it shows the last tree, greyed.
    /// </summary>
    public bool IsLive => State is LiveTabState.Playing or LiveTabState.Paused or LiveTabState.NotResponding;

    /// <summary>
    /// The state as the tab's header shows it.
    /// </summary>
    public string StateText => State switch
    {
        LiveTabState.Connecting => Tr._p("Play", "Connecting"),
        LiveTabState.Playing => Tr._p("Play", "Playing"),
        LiveTabState.Paused => Tr._p("Play", "Paused"),
        LiveTabState.NotConnected => Tr._p("Play", "Not connected"),
        LiveTabState.NotResponding => Tr._p("Play", "Not responding"),
        LiveTabState.Stopped => ExitCode is { } code ? string.Format(Tr._p("Play", "Stopped (exit code {0})"), code) : Tr._p("Play", "Stopped"),
        _ => Tr._p("Play", "Not playing"),
    };

    /// <summary>
    /// Why the game runs without the live connection; or how to start one when no game plays. <c>null</c> otherwise.
    /// </summary>
    public string? Message { get; private set => SetValue(ref field, value); } = Tr._p("Play", "Start the project (F5) to see the running game's scenes and entities here.");

    /// <summary>
    /// The exit code of the game, once it exited.
    /// </summary>
    public int? ExitCode { get; private set => SetValue(ref field, value, nameof(ExitCode), nameof(StateText)); }

    /// <summary>
    /// Why the game is paused, when the editor did not pause it: "Paused: F7 in the game window", "Paused: started paused",
    /// "Paused: script exception in PlayerController"; <c>null</c> otherwise.
    /// </summary>
    public string? PauseText { get; private set => SetValue(ref field, value); }

    /// <summary>
    /// Which scene the game was started on when it is not its default scene (Play this scene): "Playing Level2 instead of
    /// MainScene", or why the game started on its default scene instead; <c>null</c> otherwise.
    /// </summary>
    public string? SceneText { get; private set => SetValue(ref field, value); }

    /// <summary>
    /// The script exception that paused the game, while it is paused for it: the notice, with
    /// <see cref="SelectExceptionEntityCommand"/>. <c>null</c> otherwise.
    /// </summary>
    public PlayScriptException? ScriptException
    {
        get;
        private set => SetValue(ref field, value, () =>
        {
            ExceptionNotice = value is null ? null : PlayPauseText.DescribeException(value);
            SelectExceptionEntityCommand.IsEnabled = value is { EntityHandle: > 0 };
        });
    }

    /// <summary>
    /// The notice of <see cref="ScriptException"/>: which script threw what; <c>null</c> when there is none.
    /// </summary>
    public string? ExceptionNotice { get; private set => SetValue(ref field, value); }

    /// <summary>
    /// Selects the entity of the script that threw (<see cref="ScriptException"/>), so that the property grid shows it.
    /// </summary>
    public ICommandBase SelectExceptionEntityCommand { get; }

    /// <summary>
    /// Hides the notice of the script exception.
    /// </summary>
    public ICommandBase DismissExceptionCommand { get; }

    /// <summary>
    /// Ends the game: asks it to quit, then ends its process (Stop game).
    /// </summary>
    public ICommandBase StopGameCommand { get; }

    /// <summary>
    /// Whether the notice "the game is not responding" is shown, with <see cref="WaitCommand"/> and <see cref="EndGameCommand"/>.
    /// </summary>
    public bool ShowsNotResponding { get; private set => SetValue(ref field, value); }

    /// <summary>
    /// The game's assemblies the editor has another build of, when there are some: the notice that some values show as
    /// placeholders, with <see cref="ReloadCodeCommand"/>. <c>null</c> otherwise.
    /// </summary>
    public string? StaleNotice { get; private set => SetValue(ref field, value); }

    /// <summary>
    /// What the tab says over the last tree of a game that stopped: its rows no longer follow the game, and their entities have
    /// no values any more. <c>null</c> while the game plays, or when no tree was received.
    /// </summary>
    public string? StoppedNotice { get; private set => SetValue(ref field, value); }

    /// <summary>
    /// Reloads the game's code in the editor (the editor's own action); <c>null</c> when the editor offers none.
    /// </summary>
    public ICommandBase? ReloadCodeCommand { get; }

    /// <summary>
    /// Hides the notice that the game is not responding, until the game answers and stops answering again.
    /// </summary>
    public ICommandBase WaitCommand { get; }

    /// <summary>
    /// Ends the game's process at once (the game is not responding, so it cannot be asked to quit).
    /// </summary>
    public ICommandBase EndGameCommand { get; }

    /// <summary>
    /// Shows the selected entities again in the property grid, after something else took it (the user came back to the tab).
    /// </summary>
    public ICommandBase ShowSelectionCommand { get; }

    /// <summary>
    /// The number of live entities.
    /// </summary>
    public int EntityCount { get; private set => SetValue(ref field, value); }

    /// <summary>
    /// The search box: the entities whose name contains it, and the rows that lead to them.
    /// </summary>
    public string SearchText
    {
        get;
        set => SetValue(ref field, value ?? string.Empty, () => Tree.Filter = value ?? string.Empty);
    } = string.Empty;

    /// <summary>
    /// Opens the scene editor of the entity's scene and selects the entity there, for an entity that has a counterpart in a scene
    /// asset. The parameter is the row; the first selected row otherwise.
    /// </summary>
    public ICommandBase ShowInSceneEditorCommand { get; }

    /// <summary>
    /// Copies the name of the row (the parameter) to the clipboard.
    /// </summary>
    public ICommandBase CopyNameCommand { get; }

    /// <summary>
    /// Copies the id of the row (the parameter) to the clipboard.
    /// </summary>
    public ICommandBase CopyIdCommand { get; }

    /// <summary>
    /// Expands every row.
    /// </summary>
    public ICommandBase ExpandAllCommand { get; }

    /// <summary>
    /// Collapses every row but the scenes.
    /// </summary>
    public ICommandBase CollapseAllCommand { get; }

    /// <summary>
    /// Creates an empty entity in the running game: under the entity of the row (the parameter, or the first selected entity),
    /// else at the root of the selected scene, else at the root of the root scene. The tree selects it.
    /// </summary>
    public ICommandBase CreateEntityCommand { get; }

    /// <summary>
    /// Removes the selected entities (or the entity of the parameter), with their children, from the running game. There is no
    /// undo: the scene assets keep their entities, and the game gets them back at its next start.
    /// </summary>
    public ICommandBase DeleteEntityCommand { get; }

    /// <summary>
    /// Clones the selected entities (or the entity of the parameter) with their children in the running game, each copy right
    /// after its original. The tree selects the copies.
    /// </summary>
    public ICommandBase DuplicateEntityCommand { get; }

    /// <summary>
    /// Picks a prefab of the game's build and spawns it in the running game, where <see cref="CreateEntityCommand"/> creates an
    /// entity. The tree selects its roots.
    /// </summary>
    public ICommandBase SpawnPrefabCommand { get; }

    /// <summary>
    /// Whether the tab offers to spawn prefabs (a picker was given).
    /// </summary>
    public bool CanSpawnPrefabs => prefabPicker is not null;

    /// <summary>
    /// Whether the user can change the structure of the running game now: the game is connected and the edits are on.
    /// </summary>
    public bool CanChangeStructure { get; private set => SetValue(ref field, value); }

    /// <summary>
    /// Why the structure of the running game cannot be changed now, for the tooltips; <c>null</c> when it can.
    /// </summary>
    public string? StructureDisabledReason { get; private set => SetValue(ref field, value); } = Tr._p("Play", "No game is connected.");

    /// <summary>
    /// Lists the live values of the selected entities that differ from their scene assets, to copy those the user checks into
    /// the assets; works on the last values received once the game stopped. <see cref="UpdateApplyToAsset"/> enables it.
    /// </summary>
    public ICommandBase ApplyToAssetCommand { get; }

    /// <summary>
    /// Why <see cref="ApplyToAssetCommand"/> is disabled, or <c>null</c>.
    /// </summary>
    public string? ApplyToAssetDisabledReason { get; private set => SetValue(ref field, value); }

    /// <summary>
    /// The session the tab follows.
    /// </summary>
    internal PlaySession? Session => session;

    /// <summary>
    /// How many times the trees and batches received were applied (the tests check the rate).
    /// </summary>
    internal int ApplyCount { get; private set; }

    /// <inheritdoc/>
    public override void Destroy()
    {
        playMode.SessionStarted -= SessionStarted;
        if (selectionView is not null)
            selectionView.EditsEnabledChanged -= SelectionViewEditsEnabledChanged;
        Detach();
        SelectedItems.CollectionChanged -= SelectedItemsChanged;
        lifetime.Cancel();
        selectionView?.Dispose();
        if (ownsControls)
            Controls.Destroy();
        base.Destroy();
    }

    /// <summary>
    /// Waits until the updates scheduled so far are applied (the tests wait on it before they end).
    /// </summary>
    internal async Task WhenIdleAsync()
    {
        while (true)
        {
            Task[] tasks;
            lock (@lock)
                tasks = [.. running];
            if (tasks.Length == 0)
                return;
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    private void SessionStarted(object? sender, PlaySession started) => Track(dispatcher.InvokeAsync(() => Attach(started, replay: false), lifetime.Token));

    /// <summary>
    /// Follows a new session: the tree starts empty.
    /// </summary>
    /// <param name="newSession">The session.</param>
    /// <param name="replay">Whether the session may have started already (the tab opens while a game plays): its last tree and
    /// state are read.</param>
    private void Attach(PlaySession newSession, bool replay)
    {
        if (IsDestroyed || newSession == session)
            return;
        Detach();
        session = newSession;
        pending.Clear();
        SelectedItems.Clear();
        Tree.Clear();
        EntityCount = 0;
        ExitCode = null;
        StaleNotice = null;
        StoppedNotice = null;
        ShowsNotResponding = false;
        PauseText = null;
        ScriptException = null;
        SceneText = newSession.Scene?.Description;
        sceneChecked = newSession.Scene is null;
        waiting = false;
        pendingSelection = null;
        Tree.IsAddedWhilePlaying = newSession.IsAddedWhilePlaying;
        newSession.StateChanged += SessionStateChanged;
        newSession.TreeReceived += SessionTreeReceived;
        newSession.TreeChanged += SessionTreeChanged;
        newSession.RespondingChanged += SessionRespondingChanged;
        newSession.HeartbeatReceived += SessionHeartbeatReceived;
        newSession.GameStateChanged += SessionGameStateChanged;
        newSession.ScriptExceptionReceived += SessionScriptExceptionReceived;
        State = LiveTabState.Connecting;
        Message = null;
        if (replay && newSession.Tree is { } tree)
            Enqueue(newSession, tree);
        // The state may have changed before this ran on the UI thread (a game started without the agent is not connected at once)
        UpdateState(newSession);
    }

    private void Detach()
    {
        if (session is not { } old)
            return;
        old.StateChanged -= SessionStateChanged;
        old.TreeReceived -= SessionTreeReceived;
        old.TreeChanged -= SessionTreeChanged;
        old.RespondingChanged -= SessionRespondingChanged;
        old.HeartbeatReceived -= SessionHeartbeatReceived;
        old.GameStateChanged -= SessionGameStateChanged;
        old.ScriptExceptionReceived -= SessionScriptExceptionReceived;
        session = null;
        // The view of the selection gives its place back, and stops the snapshots
        Selection = null;
    }

    private void SessionStateChanged(object? sender, PlaySessionStateChange change)
    {
        if (sender is PlaySession changed)
            Track(dispatcher.InvokeAsync(() => UpdateState(changed), lifetime.Token));
    }

    private void SessionRespondingChanged(object? sender, bool responding)
    {
        if (sender is PlaySession changed)
            Track(dispatcher.InvokeAsync(() => UpdateState(changed), lifetime.Token));
    }

    private void SessionGameStateChanged(object? sender, PlayGameState state)
    {
        // Paused or playing: the header's badge, and why
        if (sender is PlaySession changed && (state.Paused != (State == LiveTabState.Paused) || state.PauseReason != shownReason))
            Track(dispatcher.InvokeAsync(() => UpdateState(changed), lifetime.Token));
    }

    private void SessionScriptExceptionReceived(object? sender, PlayScriptException exception)
    {
        if (sender is PlaySession changed)
            Track(dispatcher.InvokeAsync(() => UpdateState(changed), lifetime.Token));
    }

    private void SessionHeartbeatReceived(object? sender, PlayHeartbeat heartbeat)
    {
        // The game reports the assemblies it loads after the hello: a script assembly loaded late can be stale too
        if (sender is PlaySession changed && StaleNotice is null && changed.Assemblies.StaleAssemblies.Count > 0)
            Track(dispatcher.InvokeAsync(() => UpdateStaleNotice(changed), lifetime.Token));
    }

    private void SessionTreeReceived(object? sender, PlayTree tree)
    {
        if (sender is PlaySession received)
            Enqueue(received, tree);
    }

    private void SessionTreeChanged(object? sender, PlayTreeBatch batch)
    {
        if (sender is PlaySession received)
            Enqueue(received, batch);
    }

    private void UpdateState(PlaySession changed)
    {
        if (changed != session)
            return;
        switch (changed.State)
        {
            case PlaySessionState.Starting:
                State = LiveTabState.Connecting;
                Message = null;
                break;
            case PlaySessionState.Connected:
                if (changed.IsResponding)
                    waiting = false;
                State = !changed.IsResponding ? LiveTabState.NotResponding : changed.IsPaused ? LiveTabState.Paused : LiveTabState.Playing;
                ShowsNotResponding = !changed.IsResponding && !waiting;
                Message = null;
                UpdateStaleNotice(changed);
                UpdatePause(changed);
                break;
            case PlaySessionState.Failed:
            case PlaySessionState.VersionMismatch:
            case PlaySessionState.WithoutAgent:
                State = LiveTabState.NotConnected;
                Message = changed.StateMessage;
                ShowsNotResponding = false;
                ClearSelectedItems();
                Selection = null;
                break;
            case PlaySessionState.GameExited:
                // What arrived before the exit is shown
                ApplyPending();
                ExitCode = changed.ExitCode;
                State = LiveTabState.Stopped;
                ShowsNotResponding = false;
                PauseText = null;
                shownReason = PlayPauseReason.None;
                // The grid gives its place back, and the rows are no longer selected: a row selected now says that the game
                // stopped (UpdateSelection)
                ClearSelectedItems();
                Selection = null;
                StoppedNotice = Tree.EntityCount > 0 || Tree.Rows.Count > 0 ? Tr._p("Play", "The game has stopped. This is its last tree: the entities have no values any more, and the property grid says so. Start the project again to see them live.") : null;
                pendingSelection = null;
                break;
        }
        UpdateStructureCommands();
    }

    /// <summary>
    /// Says why the game is paused, and shows the exception that paused it until the game is resumed.
    /// </summary>
    private void UpdatePause(PlaySession changed)
    {
        var reason = changed.PauseReason;
        shownReason = reason;
        PauseText = reason is PlayPauseReason.None or PlayPauseReason.Editor ? null : PlayPauseText.Describe(reason, changed.LastScriptException, changed.LaunchOptions.Hotkey);
        if (reason == PlayPauseReason.ScriptException)
        {
            if (changed.LastScriptException is { } exception && exception != shownException)
            {
                shownException = exception;
                ScriptException = exception;
            }
        }
        else if (!changed.IsPaused)
        {
            // Resumed: the Output pane keeps the exception
            ScriptException = null;
        }
    }

    private void SelectExceptionEntity()
    {
        if (ScriptException is not { EntityHandle: > 0 } exception)
            return;
        // Applied first: the entity may have come in the last batch
        ApplyPending();
        if (Tree.FindEntity(exception.EntityHandle) is not { } entity)
            return;
        for (var parent = entity.Parent; parent is not null; parent = parent.Parent)
            parent.IsExpanded = true;
        SearchText = string.Empty;
        SelectedItems.Clear();
        SelectedItems.Add(entity);
    }

    private void UpdateStaleNotice(PlaySession changed)
    {
        if (changed != session)
            return;
        var stale = changed.Assemblies.StaleAssemblies;
        StaleNotice = stale.Count == 0 ? null
            : string.Format(Tr._p("Play", "Script types changed since the editor loaded them ({0}): some values show as placeholders. Reload the project to see them."), string.Join(", ", stale));
    }

    private void Wait()
    {
        waiting = true;
        ShowsNotResponding = false;
    }

    private void EndGame()
    {
        ShowsNotResponding = false;
        (session?.Game as IPlayGameProcess)?.Kill();
    }

    private void Enqueue(PlaySession from, PlayMessage message)
    {
        pending.Enqueue((from, message));
        TimeSpan wait;
        lock (@lock)
        {
            if (applyScheduled || lifetime.IsCancellationRequested)
                return;
            applyScheduled = true;
            wait = MinimumApplyInterval - Stopwatch.GetElapsedTime(lastApply);
        }
        Track(ApplyLaterAsync(wait));
    }

    private async Task ApplyLaterAsync(TimeSpan wait)
    {
        try
        {
            // Never on the session's thread: it goes on reading
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, lifetime.Token).ConfigureAwait(false);
            else
                await Task.Yield();
            lock (@lock)
            {
                applyScheduled = false;
                lastApply = Stopwatch.GetTimestamp();
            }
            await dispatcher.InvokeAsync(ApplyPending, lifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The tab was closed
        }
    }

    /// <summary>
    /// Applies the trees and batches received since the last time, on the UI thread.
    /// </summary>
    /// <summary>
    /// Says whether the game started on the scene it was asked to start on, from its first tree: a scene that is not in the
    /// game's built data leaves it on its default scene.
    /// </summary>
    private void CheckScene(PlaySession from, PlayTree tree)
    {
        if (sceneChecked || from.Scene is not { } scene)
            return;
        sceneChecked = true;
        var root = tree.Scenes.FirstOrDefault(x => x.ParentHandle == 0);
        if (root?.Url is { } url && !scene.IsScene(url))
            SceneText = scene.NotInBuildDescription;
    }

    private void ApplyPending()
    {
        if (IsDestroyed)
            return;
        var removed = new HashSet<int>();
        var batches = new List<PlayTreeBatch>();
        while (pending.TryDequeue(out var item))
        {
            if (item.Session != session)
                continue;
            switch (item.Message)
            {
                case PlayTree tree:
                    // A new tree replaces the batches before it
                    batches.Clear();
                    removed.UnionWith(Tree.Reset(tree));
                    CheckScene(item.Session, tree);
                    break;
                case PlayTreeBatch batch:
                    batches.Add(batch);
                    break;
            }
        }
        if (batches.Count > 0)
            ApplyKeepingSelection(batches, removed);
        ++ApplyCount;
        EntityCount = Tree.EntityCount;

        SelectAdded();

        // A selected entity that is gone leaves the selection
        if (removed.Count > 0)
        {
            var gone = SelectedItems.OfType<LiveEntityItem>().Where(x => removed.Contains(x.Handle) && Tree.FindEntity(x.Handle) != x).ToList();
            if (gone.Count > 0)
            {
                updatingSelection = true;
                try
                {
                    foreach (var item in gone)
                        SelectedItems.Remove(item);
                }
                finally
                {
                    updatingSelection = false;
                }
                UpdateSelection();
            }
        }
    }

    private void SelectedItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!updatingSelection)
            UpdateSelection();
        UpdateStructureCommands();
    }

    /// <summary>
    /// Exposes the selected entities, and hands them to the view of the selection.
    /// </summary>
    /// <remarks>
    /// Once the game stopped, the rows of its last tree can still be selected: the selection then has the names of the
    /// entities, and the property grid says that the game stopped instead of showing values (<see cref="LiveSelection.IsStopped"/>).
    /// </remarks>
    private void UpdateSelection()
    {
        var entities = SelectedItems.OfType<LiveEntityItem>().DistinctBy(x => x.Handle).ToList();
        if (session is not { State: not PlaySessionState.Starting } followed)
        {
            Selection = null;
            return;
        }
        if (entities.Count == 0 && Selection is null)
            return;
        var handles = entities.Select(x => x.Handle).ToList();
        if (Selection is { } current && current.Session == followed && current.Handles.SequenceEqual(handles))
            return;
        Selection = handles.Count > 0 ? new LiveSelection(followed, handles) { Names = entities.Select(x => x.Name).ToList(), Tree = Tree } : null;
    }

    private void ClearSelectedItems()
    {
        if (SelectedItems.Count == 0)
            return;
        updatingSelection = true;
        try
        {
            SelectedItems.Clear();
        }
        finally
        {
            updatingSelection = false;
        }
    }

    private LiveEntityItem? Target(object? parameter) => parameter as LiveEntityItem ?? SelectedItems.OfType<LiveEntityItem>().FirstOrDefault();

    /// <summary>
    /// Whether the dragged entities can go under the target row (an entity, or a scene for its root): not under one of them or
    /// their descendants, and only while the structure can change.
    /// </summary>
    /// <param name="items">The dragged entities.</param>
    /// <param name="target">The row they are dropped on.</param>
    public bool CanMove(IReadOnlyCollection<LiveEntityItem> items, LiveTreeItem? target)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (!CanChangeStructure || target is null || items.Count == 0)
            return false;
        var roots = TopLevel(items);
        // Under itself or one of its descendants: refused, as the scene editor refuses it
        if (roots.Any(x => x.IsSelfOrAncestorOf(target)))
            return false;
        // Somewhere new for at least one of them
        return roots.Any(x => x.Parent != target);
    }

    /// <summary>
    /// Moves the dragged entities under the target row: under an entity, or to the root of a scene. As in the scene editor, they
    /// keep their local transform, relative to their new parent, unless <paramref name="keepWorldTransform"/> (Alt) keeps their
    /// place in the world. Each goes last among its new siblings.
    /// </summary>
    /// <param name="items">The dragged entities.</param>
    /// <param name="target">The row they are dropped on.</param>
    /// <param name="keepWorldTransform">Whether they keep their place in the world.</param>
    /// <returns>The game's answers, one per entity moved.</returns>
    public async Task<IReadOnlyList<PlayStructureResult>> MoveAsync(IReadOnlyCollection<LiveEntityItem> items, LiveTreeItem target, bool keepWorldTransform)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(target);
        if (!CanMove(items, target) || session is not { } current)
            return [];
        var (parentHandle, sceneHandle) = target is LiveEntityItem entity ? (entity.Handle, 0) : (0, target.Handle);
        // Sent in the order of the tree, so that they keep it among their new siblings
        var commands = Tree.InTreeOrder(TopLevel(items).Where(x => x.Parent != target))
            .Select(x => current.ChangeStructureAsync(new PlayStructureEdit(current.NextEditId(), PlayStructureOperation.MoveEntity, EntityHandle: x.Handle, SceneHandle: sceneHandle, ParentHandle: parentHandle, KeepWorldTransform: keepWorldTransform)))
            .ToList();
        // The target opens, to show them there
        target.IsExpanded = true;
        return await Task.WhenAll(commands);
    }

    /// <summary>
    /// Applies the tree batches, and keeps the selected rows selected: a row that moves in the list (an entity moved to
    /// another parent, as a drop does, or a sibling before it) leaves the view's selection, as the list removes it and adds it
    /// back. The rows of the entities that are gone are not selected again.
    /// </summary>
    private void ApplyKeepingSelection(List<PlayTreeBatch> batches, HashSet<int> removed)
    {
        var selected = SelectedItems.ToList();
        updatingSelection = true;
        try
        {
            removed.UnionWith(Tree.Apply(batches));
            var index = 0;
            foreach (var item in selected)
            {
                var stays = item is LiveEntityItem entity ? Tree.FindEntity(entity.Handle) == entity : item is not LiveTreeItem || Tree.Rows.Contains(item);
                if (stays && !SelectedItems.Contains(item))
                    SelectedItems.Insert(Math.Min(index, SelectedItems.Count), item);
                if (stays)
                    index++;
            }
        }
        finally
        {
            updatingSelection = false;
        }
        // Only a row that left the tree changes the selection (the gone entities are handled after)
        if (!SelectedItems.SequenceEqual(selected))
            UpdateSelection();
    }

    private void SelectionViewEditsEnabledChanged(object? sender, EventArgs e) => Track(dispatcher.InvokeAsync(UpdateStructureCommands, lifetime.Token));

    /// <summary>
    /// Enables the commands that change the game's structure: the game is connected, the property grid's lock is open, and the
    /// selection fits each command.
    /// </summary>
    private void UpdateStructureCommands()
    {
        string? reason = null;
        if (session is not { State: PlaySessionState.Connected } || !IsLive)
            reason = State == LiveTabState.Stopped ? Tr._p("Play", "The game has stopped.") : Tr._p("Play", "No game is connected.");
        else if (selectionView is { EditsEnabled: false })
            reason = Tr._p("Play", "Live edits are off: turn them on in the property grid.");
        StructureDisabledReason = reason;
        CanChangeStructure = reason is null;
        var hasEntity = SelectedItems.OfType<LiveEntityItem>().Any();
        CreateEntityCommand.IsEnabled = CanChangeStructure;
        SpawnPrefabCommand.IsEnabled = CanChangeStructure && prefabPicker is not null;
        DeleteEntityCommand.IsEnabled = CanChangeStructure && hasEntity;
        DuplicateEntityCommand.IsEnabled = CanChangeStructure && hasEntity;
    }

    /// <summary>
    /// Enables <see cref="ApplyToAssetCommand"/> for the selection, with the reason when it is not: called when the context menu
    /// of the tab opens, as the answer depends on the values received and on the scene assets.
    /// </summary>
    public void UpdateApplyToAsset()
    {
        string? reason;
        if (Selection is not { } selection)
            reason = Tr._p("Play", "Select the entities to apply.");
        else if (selectionView is null)
            reason = Tr._p("Play", "Apply to asset is not available here.");
        else
            reason = selectionView.GetApplyToAssetDisabledReason(selection);
        ApplyToAssetDisabledReason = reason;
        ApplyToAssetCommand.IsEnabled = reason is null;
    }

    private async Task ApplyToAssetAsync()
    {
        if (Selection is { } selection && selectionView is not null)
            await selectionView.ApplyToAssetAsync(selection);
    }

    /// <summary>
    /// The entities of the parameter (a row) or of the selection, without those under another of them.
    /// </summary>
    private List<LiveEntityItem> TargetEntities(object? parameter)
        => parameter is LiveEntityItem item && !SelectedItems.Contains(item) ? [item] : TopLevel(SelectedItems.OfType<LiveEntityItem>().ToList());

    private static List<LiveEntityItem> TopLevel(IReadOnlyCollection<LiveEntityItem> items)
        => [.. items.Distinct().Where(x => !items.Any(other => other != x && other.IsSelfOrAncestorOf(x)))];

    /// <summary>
    /// Where a new entity goes: under the entity of the parameter or of the selection, else at the root of the selected scene,
    /// else at the root of the root scene.
    /// </summary>
    private (int ParentHandle, int SceneHandle) NewEntityPlace(object? parameter)
    {
        var row = parameter as LiveTreeItem ?? SelectedItems.OfType<LiveTreeItem>().FirstOrDefault();
        return row switch
        {
            LiveEntityItem entity => (entity.Handle, 0),
            LiveSceneItem scene => (0, scene.Handle),
            _ => (0, 0),
        };
    }

    private async Task CreateEntityAsync(object? parameter)
    {
        if (!CanChangeStructure || session is not { } current)
            return;
        var (parentHandle, sceneHandle) = NewEntityPlace(parameter);
        var result = await current.ChangeStructureAsync(new PlayStructureEdit(current.NextEditId(), PlayStructureOperation.CreateEntity, SceneHandle: sceneHandle, ParentHandle: parentHandle));
        await SelectWhenAddedAsync(current, result, roots: 1);
    }

    private async Task DeleteEntitiesAsync(object? parameter)
    {
        if (!CanChangeStructure || session is not { } current)
            return;
        var entities = TargetEntities(parameter);
        if (entities.Count == 0)
            return;
        // No confirmation: the game gets its entities back at its next start
        await Task.WhenAll(entities.Select(x => current.ChangeStructureAsync(new PlayStructureEdit(current.NextEditId(), PlayStructureOperation.DeleteEntity, EntityHandle: x.Handle))));
    }

    private async Task DuplicateEntitiesAsync(object? parameter)
    {
        if (!CanChangeStructure || session is not { } current)
            return;
        var entities = TargetEntities(parameter);
        if (entities.Count == 0)
            return;
        var results = await Task.WhenAll(entities.Select(x => current.ChangeStructureAsync(new PlayStructureEdit(current.NextEditId(), PlayStructureOperation.DuplicateEntity, EntityHandle: x.Handle))));
        // The copies are selected, as the scene editor selects them: the first entity of each answer is a copy's root
        await SelectWhenAddedAsync(current, results.Where(x => x.Status == PlayEditStatus.Applied && x.Entities.Count > 0).Select(x => x.Entities[0]).ToList());
    }

    private async Task SpawnPrefabAsync(object? parameter)
    {
        if (!CanChangeStructure || session is not { } current || prefabPicker is null)
            return;
        // Taken before the picker opens: the selection may change while it is open
        var (parentHandle, sceneHandle) = NewEntityPlace(parameter);
        using var stopped = new CancellationTokenSource();
        void OnStateChanged(object? sender, PlaySessionStateChange change)
        {
            if (current.State != PlaySessionState.Connected)
                stopped.Cancel();
        }
        current.StateChanged += OnStateChanged;
        string? url;
        try
        {
            url = await prefabPicker.PickPrefabAsync(current, stopped.Token);
        }
        catch (OperationCanceledException)
        {
            url = null;
        }
        finally
        {
            current.StateChanged -= OnStateChanged;
        }
        if (url is null || current != session || current.State != PlaySessionState.Connected)
            return;
        var result = await current.ChangeStructureAsync(new PlayStructureEdit(current.NextEditId(), PlayStructureOperation.SpawnPrefab, SceneHandle: sceneHandle, ParentHandle: parentHandle, Url: url));
        await SelectWhenAddedAsync(current, result, roots: -1);
    }

    /// <summary>
    /// Selects the entities a command added: the first <paramref name="roots"/> handles of its answer (all its roots when
    /// negative: those whose parent is not among them, once the tree has them).
    /// </summary>
    private Task SelectWhenAddedAsync(PlaySession from, PlayStructureResult result, int roots)
    {
        if (result.Status != PlayEditStatus.Applied || result.Entities.Count == 0)
            return Task.CompletedTask;
        return SelectWhenAddedAsync(from, roots >= 0 ? result.Entities.Take(roots).ToList() : [.. result.Entities]);
    }

    private Task SelectWhenAddedAsync(PlaySession from, List<int> handles)
    {
        if (handles.Count == 0)
            return Task.CompletedTask;
        // The answer comes before the tree batch that adds the entities, at the end of the same frame
        var task = dispatcher.InvokeAsync(() =>
        {
            if (from != session)
                return;
            pendingSelection = handles;
            SelectAdded();
        }, lifetime.Token);
        Track(task);
        return task;
    }

    /// <summary>
    /// Selects the entities a command added, once the tree has them: the rows that lead to them are expanded.
    /// </summary>
    private void SelectAdded()
    {
        if (pendingSelection is not { } handles)
            return;
        var entities = handles.Select(Tree.FindEntity).OfType<LiveEntityItem>().ToList();
        if (entities.Count == 0)
            return;
        pendingSelection = null;
        // Only the roots of what was added: a prefab's children come with their root
        entities = TopLevel(entities);
        foreach (var entity in entities)
        {
            for (var parent = entity.Parent; parent is not null; parent = parent.Parent)
                parent.IsExpanded = true;
        }
        if (!string.IsNullOrEmpty(SearchText) && entities.Any(x => !x.IsMatch))
            SearchText = string.Empty;
        SelectedItems.Clear();
        foreach (var entity in entities)
            SelectedItems.Add(entity);
    }

    private async Task ShowInSceneEditorAsync(object? parameter)
    {
        if (sceneAssets is not null && Target(parameter) is { CounterpartScene: { } scene } entity)
            await sceneAssets.ShowInSceneEditorAsync(scene.Info, entity.Id);
    }

    private async Task CopyAsync(object? parameter, Func<LiveTreeItem, string> text)
    {
        var item = parameter as LiveTreeItem ?? SelectedItems.OfType<LiveTreeItem>().FirstOrDefault();
        if (item is not null && ServiceProvider.TryGet<IClipboardService>() is { } clipboard)
            await clipboard.SetTextAsync(text(item));
    }

    private void Track(Task task)
    {
        if (task.IsCompleted)
            return;
        lock (@lock)
            running.Add(task);
        task.ContinueWith(x =>
        {
            lock (@lock)
                running.Remove(x);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Forget();
    }
}
