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

    /// <summary>The game runs without the live connection (the agent did not connect, the connection broke, or the versions
    /// differ): the tab keeps what it had, and says why.</summary>
    NotConnected,

    /// <summary>The game exited: the tab keeps its last tree, greyed.</summary>
    Stopped,
}

/// <summary>
/// The Live tab: the running game's scenes and entities, while a game started with Start project plays, and the live entities
/// selected there (<see cref="Selection"/>), for the property grid.
/// </summary>
/// <remarks>
/// It follows the session of <see cref="PlayModeService"/>: each start replaces the tree. The agent's tree and batches are
/// applied on the UI thread at most 10 times a second. The selected entities are subscribed to at 5 snapshots a second.
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

    /// <summary>
    /// Initializes a new instance of the <see cref="LivePanelViewModel"/> class.
    /// </summary>
    /// <param name="context">The context of the panel.</param>
    /// <param name="playMode">The play sessions the tab follows.</param>
    /// <param name="sceneAssets">The scene assets of the session, to mark the spawned entities and to show an entity in its
    /// scene editor; <c>null</c> for neither.</param>
    public LivePanelViewModel(PanelContext context, PlayModeService playMode, ILiveSceneAssets? sceneAssets)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(playMode);
        this.playMode = playMode;
        this.sceneAssets = sceneAssets;
        dispatcher = ServiceProvider.Get<IDispatcherService>();
        Tree = new LiveTree(sceneAssets is not null ? sceneAssets.GetEntityIds : null);
        SelectedItems.CollectionChanged += SelectedItemsChanged;
        ShowInSceneEditorCommand = new AnonymousTaskCommand<object?>(ServiceProvider, ShowInSceneEditorAsync, x => sceneAssets is not null && Target(x) is { CounterpartScene: not null });
        CopyNameCommand = new AnonymousTaskCommand<object?>(ServiceProvider, x => CopyAsync(x, item => item.Name));
        CopyIdCommand = new AnonymousTaskCommand<object?>(ServiceProvider, x => CopyAsync(x, item => item.Id.ToString()));
        ExpandAllCommand = new AnonymousCommand(ServiceProvider, () => Tree.SetAllExpanded(true));
        CollapseAllCommand = new AnonymousCommand(ServiceProvider, () => Tree.SetAllExpanded(false));

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
    /// The live entities selected, with the session that plays them; <c>null</c> when no entity is selected or no game plays.
    /// </summary>
    public LiveSelection? Selection { get; private set => SetValue(ref field, value); }

    /// <summary>
    /// Where the tab's game is.
    /// </summary>
    public LiveTabState State
    {
        get;
        private set => SetValue(ref field, value, nameof(State), nameof(IsLive), nameof(StateText));
    } = LiveTabState.NotPlaying;

    /// <summary>
    /// Whether the tree follows a game that runs; otherwise it shows the last tree, greyed.
    /// </summary>
    public bool IsLive => State == LiveTabState.Playing;

    /// <summary>
    /// The state as the tab's header shows it.
    /// </summary>
    public string StateText => State switch
    {
        LiveTabState.Connecting => Tr._p("Play", "Connecting"),
        LiveTabState.Playing => Tr._p("Play", "Playing"),
        LiveTabState.NotConnected => Tr._p("Play", "Not connected"),
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
    /// The session the tab follows.
    /// </summary>
    internal PlaySession? Session => session;

    /// <inheritdoc/>
    public override void Destroy()
    {
        playMode.SessionStarted -= SessionStarted;
        Detach();
        SelectedItems.CollectionChanged -= SelectedItemsChanged;
        lifetime.Cancel();
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
        newSession.StateChanged += SessionStateChanged;
        newSession.TreeReceived += SessionTreeReceived;
        newSession.TreeChanged += SessionTreeChanged;
        State = LiveTabState.Connecting;
        Message = null;
        if (replay)
        {
            if (newSession.Tree is { } tree)
                Enqueue(newSession, tree);
            UpdateState(newSession);
        }
    }

    private void Detach()
    {
        if (session is not { } old)
            return;
        old.StateChanged -= SessionStateChanged;
        old.TreeReceived -= SessionTreeReceived;
        old.TreeChanged -= SessionTreeChanged;
        // The selected entities are not sent any more
        if (Selection is not null)
            old.Subscribe([]);
        session = null;
        Selection = null;
    }

    private void SessionStateChanged(object? sender, PlaySessionStateChange change)
    {
        if (sender is PlaySession changed)
            Track(dispatcher.InvokeAsync(() => UpdateState(changed), lifetime.Token));
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
                State = LiveTabState.Playing;
                Message = null;
                break;
            case PlaySessionState.Failed:
            case PlaySessionState.VersionMismatch:
                State = LiveTabState.NotConnected;
                Message = changed.StateMessage;
                Selection = null;
                break;
            case PlaySessionState.GameExited:
                // What arrived before the exit is shown
                ApplyPending();
                ExitCode = changed.ExitCode;
                State = LiveTabState.Stopped;
                Selection = null;
                break;
        }
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
                    break;
                case PlayTreeBatch batch:
                    batches.Add(batch);
                    break;
            }
        }
        if (batches.Count > 0)
            removed.UnionWith(Tree.Apply(batches));
        EntityCount = Tree.EntityCount;

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
    }

    /// <summary>
    /// Subscribes the session to the selected entities, and exposes them.
    /// </summary>
    private void UpdateSelection()
    {
        var handles = SelectedItems.OfType<LiveEntityItem>().Select(x => x.Handle).Distinct().ToList();
        if (session is not { State: PlaySessionState.Connected } playing)
        {
            Selection = null;
            return;
        }
        if (handles.Count == 0 && Selection is null)
            return;
        if (Selection is { } current && current.Session == playing && current.Handles.SequenceEqual(handles))
            return;
        playing.Subscribe(handles, SnapshotRateHz);
        Selection = handles.Count > 0 ? new LiveSelection(playing, handles) : null;
    }

    private LiveEntityItem? Target(object? parameter) => parameter as LiveEntityItem ?? SelectedItems.OfType<LiveEntityItem>().FirstOrDefault();

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
