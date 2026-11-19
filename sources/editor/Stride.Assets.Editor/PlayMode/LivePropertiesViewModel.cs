// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Stride.Assets.Rendering;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Components.Properties;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Assets.Quantum;
using Stride.Core.Extensions;
using Stride.Core.Diagnostics;
using Stride.Core.Presentation.Quantum;
using Stride.Core.Presentation.Quantum.Presenters;
using Stride.Core.Quantum;
using Stride.Editor.PlayMode;
using Stride.Rendering.Compositing;

namespace Stride.Assets.Editor.PlayMode;

/// <summary>
/// The property grid of the live entities of a running game: one or several entities (merged as the grid merges any
/// selection), kept up to date by the snapshots of the play session, whose values the user can change in the running game.
/// </summary>
/// <remarks>
/// <para>
/// The grid is built on the mirrors of the entities with its own node container, with the commands and updaters of the
/// session's grid (the plugins' too) and a few of its own (<see cref="LiveEntityNodeUpdater"/>, <see cref="LiveEditNodeUpdater"/>).
/// </para>
/// <para>
/// A write from the view goes to the game as an edit (see <see cref="LiveEditor"/>): the value shows at once, marked as
/// pending, then the value the game read back replaces it, or the property says the game refused it. The edits change the
/// running game only: the mirror belongs to no asset, so nothing is saved and nothing reaches the undo stack. With
/// <see cref="EditsEnabled"/> off, every property is read-only.
/// </para>
/// <para>
/// Each snapshot is compared with the mirrors the grid shows, and only the changed values are written, in a batch, through the
/// value-only path of the node presenters (<see cref="INodePresenterFactory.BeginBatch"/>); a change of structure rebuilds the
/// grid. The values in <see cref="ProtectedValues"/> (under the pointer, with the focus) are written once released. An entity
/// whose snapshots take longer than <see cref="LiveRefreshThrottle.Budget"/> to apply is refreshed less often (see
/// <see cref="Throttle"/>), and <see cref="LiveStatus"/> says so.
/// </para>
/// <para>
/// Entry points: <see cref="Show(PlaySession, IReadOnlyList{int})"/> follows entities of a play session (the Live tab's
/// selection); <see cref="ShowAsync(IReadOnlyList{LiveEntityMirror})"/> and <see cref="ApplyAsync(LiveSnapshot)"/> drive the
/// grid directly (tests, tools).
/// </para>
/// </remarks>
[Experimental("STREXP012")]
public sealed class LivePropertiesViewModel : SessionObjectPropertiesViewModel
{
    private static readonly Logger Log = GlobalLogger.GetLogger("PlayMode");
    private readonly AssetNodeContainer mirrorNodes = new() { NodeBuilder = { NodeFactory = new AssetNodeFactory() } };
    private readonly LiveNodePresenterFactory presenterFactory;
    private readonly LiveMirrorApplier applier;
    private readonly LiveEditor editor;
    private readonly LiveEditNodeUpdater editUpdater;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly List<Entry> entries = [];
    private PlaySession? playSession;
    private IReadOnlyList<int> handles = [];
    private double rate = 5;
    private LiveSnapshot? pending;
    private int applyScheduled;

    /// <summary>
    /// Initializes a new instance of the <see cref="LivePropertiesViewModel"/> class.
    /// </summary>
    /// <param name="session">The session whose grid's commands and updaters the live grid uses, and whose assets the game's
    /// content maps to.</param>
    public LivePropertiesViewModel(SessionViewModel session)
        : base(session)
    {
        // The same commands and updaters as the session's grid, in the same order (the plugins added theirs to that one)
        var source = (PropertiesViewModel)session.AssetViewProperties;
        foreach (var command in NodePresenterCommands.ToList())
            UnregisterNodePresenterCommand(command);
        foreach (var updater in NodePresenterUpdaters.ToList())
            UnregisterNodePresenterUpdater(updater);
        foreach (var command in source.NodePresenterCommands)
            RegisterNodePresenterCommand(command);
        // Each updater runs behind a guard: one written for assets can fail on an entity that has none
        foreach (var updater in source.NodePresenterUpdaters)
            RegisterNodePresenterUpdater(new GuardedNodeUpdater(updater, Log));
        RegisterNodePresenterUpdater(new GuardedNodeUpdater(new LiveEntityNodeUpdater(FindCameraSlot), Log));
        RegisterNodePresenterUpdater(new GuardedNodeUpdater(new LiveModelMaterialsNodeUpdater(url => session.GetAssetByUrl(url)), Log));
        var references = new LiveReferenceContext(session, () => playSession, () => Tree, () => EditsEnabled ? editor : null, Log);
        editUpdater = new LiveEditNodeUpdater(() => EditsEnabled, () => EditsEnabled ? editor : null, references);
        RegisterNodePresenterUpdater(editUpdater);

        editor = new LiveEditor(() => playSession, FindMirror, url => session.GetAssetByUrl(url)?.Id, Dispatcher, Log);
        editor.Answered += (_, e) => EditAnswered?.Invoke(this, e);
        presenterFactory = new LiveNodePresenterFactory(mirrorNodes.NodeBuilder, ViewModelService.AvailableCommands, ViewModelService.AvailableUpdaters, Log) { Editor = editor };
        ViewModelService.NodePresenterFactory = presenterFactory;
        applier = new LiveMirrorApplier { FindAsset = url => session.GetAssetByUrl(url)?.Id };
    }

    /// <summary>
    /// Raised on the UI thread after each snapshot was applied (in place or by a rebuild).
    /// </summary>
    public event EventHandler<LiveApplyEventArgs>? Applied;

    /// <summary>
    /// Raised on the UI thread when the game answered an edit of the grid.
    /// </summary>
    public event EventHandler<LiveEditEventArgs>? EditAnswered;

    /// <summary>
    /// Whether the grid shows the values of a running game (the header says "Live").
    /// </summary>
    public bool IsLive => true;

    /// <summary>
    /// Whether the user can change the values in the running game (on by default). Off, every property is read-only, as a
    /// guard against changing the game by mistake while reading it. A change builds the grid again.
    /// </summary>
    public bool EditsEnabled
    {
        get;
        set
        {
            if (!SetValue(ref field, value))
                return;
            OnPropertyChanged(nameof(LiveNotice));
            presenterFactory.Editor = value ? editor : null;
            editor.Reset();
            if (entries.Count > 0)
                RebuildAsync(entries.Select(x => x.References.Mirror).ToList()).Forget();
        }
    } = true;

    /// <summary>
    /// What the header says about the edits: that they change the running game only and are not saved, or that they are off.
    /// </summary>
    public string LiveNotice => EditsEnabled
        ? "Edits change the running game only: they are not saved."
        : "Edits are off: the values are read-only.";

    /// <summary>
    /// The tree of the Live tab, which the pickers of entity and component references list; <c>null</c> when there is none
    /// (the references to entities and components cannot be picked then).
    /// </summary>
    public LiveTree? Tree { get; set; }

    /// <summary>
    /// The pickers of live references opened (assets, entities and components).
    /// </summary>
    public int OpenedPickers => editUpdater.OpenedPickers;

    /// <summary>
    /// The edits sent to the game.
    /// </summary>
    public int SentEdits => editor.SentEdits;

    /// <summary>
    /// The edits the game refused.
    /// </summary>
    public int RefusedEdits => editor.RefusedEdits;

    /// <summary>
    /// The values written into a property while its last edit was on its way, replaced by a later value before they could go
    /// (the editor sends at most one edit of a property at a time).
    /// </summary>
    public int CoalescedWrites => editor.CoalescedWrites;

    /// <summary>
    /// The edits on their way to the game.
    /// </summary>
    public int PendingEdits => editor.PendingEdits;

    /// <summary>
    /// Whether the grid shows entities of a game that stopped (<see cref="ShowStopped"/>): it has no values, and the header says
    /// "Stopped" instead of "Live".
    /// </summary>
    public bool IsStopped { get; private set => SetValue(ref field, value); }

    /// <summary>
    /// What the header says under "Live": the game's frame, and which entities are refreshed less often.
    /// </summary>
    public string LiveStatus { get; private set => SetValue(ref field, value); } = string.Empty;

    /// <summary>
    /// Whether an entity is refreshed less often than the snapshots arrive, because it is slow to show.
    /// </summary>
    public bool IsThrottled { get; private set => SetValue(ref field, value); }

    /// <summary>
    /// The rate limit of the entities.
    /// </summary>
    public LiveRefreshThrottle Throttle { get; init; } = new();

    /// <summary>
    /// The value nodes not to write at the next apply: the view gives those of the property under the pointer and of the one
    /// with the keyboard focus. Called on the UI thread before each apply.
    /// </summary>
    public Func<IReadOnlyCollection<(IGraphNode Node, NodeIndex Index)>>? ProtectedValues { get; set; }

    /// <summary>
    /// Whether the game sends the snapshots of the entities shown: <c>false</c> before <see cref="Show"/>, after
    /// <see cref="Detach"/>, and while <see cref="Suspend"/> holds them back.
    /// </summary>
    public bool IsReceiving { get; private set; }

    /// <summary>
    /// The handles of the entities shown, in order.
    /// </summary>
    public IReadOnlyList<int> Handles => entries.Select(x => x.Handle).ToList();

    /// <summary>
    /// The mirrors the grid shows.
    /// </summary>
    public IReadOnlyList<LiveMirrorReferences> Mirrors => entries.Select(x => x.References).ToList();

    /// <summary>
    /// The number of times the grid was built.
    /// </summary>
    public int BuildCount { get; private set; }

    /// <summary>
    /// The writes the view tried and the grid refused: edits are off, the game is not connected, or a template or a command
    /// wrote to a property that should not be offered.
    /// </summary>
    public int RefusedWrites => presenterFactory.RefusedWrites;

    /// <summary>
    /// Whether a list or a dictionary that gained or lost items is changed in place rather than by a rebuild; see
    /// <see cref="LiveMirrorApplier.InPlaceCollections"/>.
    /// </summary>
    public bool InPlaceCollections { get => applier.InPlaceCollections; set => applier.InPlaceCollections = value; }

    /// <summary>
    /// Shows the entities of a play session, from its next snapshot on, and asks the game for their snapshots.
    /// </summary>
    /// <param name="session">The play session.</param>
    /// <param name="entityHandles">The handles of the entities (the Live tab's selection); empty clears the grid.</param>
    /// <param name="rateHz">The snapshots a second to ask the game for.</param>
    /// <remarks>
    /// The grid takes the place of the session's other grids. Snapshots that arrive while another grid is shown (the user
    /// selected an asset) are dropped; calling this again shows the entities again.
    /// </remarks>
    public void Show(PlaySession session, IReadOnlyList<int> entityHandles, double rateHz = 5)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(entityHandles);
        Dispatcher.EnsureAccess();
        Detach();
        IsStopped = false;
        playSession = session;
        handles = entityHandles.ToList();
        rate = rateHz;
        Throttle.Reset();
        entries.Clear();
        session.SnapshotReceived += OnSnapshotReceived;
        session.Subscribe(handles, rateHz);
        IsReceiving = true;
        ShowWaitingAsync(session).Forget();
    }

    /// <summary>
    /// Shows entities of a game that stopped (it exited, or its connection was lost), selected in the last tree of the Live
    /// tab: there are no values to show, so the grid says that the game stopped, under the names of the entities.
    /// </summary>
    /// <param name="session">The play session of the game.</param>
    /// <param name="entityHandles">The handles of the entities.</param>
    /// <param name="names">The names of the entities, in the order of the handles; <c>null</c> to read them in the session's
    /// last tree.</param>
    public void ShowStopped(PlaySession session, IReadOnlyList<int> entityHandles, IReadOnlyList<string>? names = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(entityHandles);
        Dispatcher.EnsureAccess();
        Detach();
        handles = entityHandles.ToList();
        Throttle.Reset();
        entries.Clear();
        IsThrottled = false;
        IsStopped = true;
        LiveStatus = session.State == PlaySessionState.GameExited ? "The game has stopped." : "The game is no longer connected.";
        ShowStoppedAsync(session, names).Forget();
    }

    /// <summary>
    /// Asks the game to stop sending the snapshots while nothing shows them (the grid is hidden, or shows something else); the
    /// grid keeps the last values. <see cref="Resume"/> asks for them again.
    /// </summary>
    public void Suspend()
    {
        if (playSession is not { } session || !IsReceiving)
            return;
        IsReceiving = false;
        session.Subscribe([], rate);
        Interlocked.Exchange(ref pending, null);
    }

    /// <summary>
    /// Asks the game for the snapshots again after <see cref="Suspend"/>: the next one is applied in place.
    /// </summary>
    public void Resume()
    {
        if (playSession is not { } session || IsReceiving)
            return;
        IsReceiving = true;
        session.Subscribe(handles, rate);
    }

    private async Task ShowWaitingAsync(PlaySession session)
    {
        await GenerateSelectionPropertiesAsync([]);
        // Until the first snapshot, the grid says what it waits for
        if (handles.Count > 0 && entries.Count == 0 && playSession == session)
        {
            SetHeader(null);
            FallbackMessage = "Waiting for the game's values...";
        }
    }

    private async Task ShowStoppedAsync(PlaySession session, IReadOnlyList<string>? names)
    {
        var shownHandles = handles;
        await GenerateSelectionPropertiesAsync([]);
        // Shown live again meanwhile, or other entities
        if (!IsStopped || playSession is not null || handles != shownHandles)
            return;
        var count = shownHandles.Count;
        TypeDescription = count > 1 ? "Live entities" : "Live entity";
        Name = count switch
        {
            0 => string.Empty,
            1 => names is [var name] ? name : session.Tree?.Entities.FirstOrDefault(x => x.Handle == shownHandles[0])?.Name ?? string.Empty,
            _ => $"{count} entities selected",
        };
        FallbackMessage = session.State == PlaySessionState.GameExited
            ? (count > 1 ? "The game has stopped: the values of these entities are gone. Start the project again to see them live." : "The game has stopped: the values of this entity are gone. Start the project again to see them live.")
            : (count > 1 ? "The game is no longer connected: the values of these entities are not sent any more." : "The game is no longer connected: the values of this entity are not sent any more.");
    }

    /// <summary>
    /// Stops following the play session: the game stops sending snapshots, the grid keeps the last values.
    /// </summary>
    public void Detach()
    {
        if (playSession is { } session)
        {
            session.SnapshotReceived -= OnSnapshotReceived;
            if (IsReceiving)
                session.Subscribe([], rate);
            IsReceiving = false;
            playSession = null;
        }
        Interlocked.Exchange(ref pending, null);
        editor.Reset();
    }

    /// <summary>
    /// Shows the given mirrors (a build of the grid).
    /// </summary>
    public async Task<LiveApplyEventArgs> ShowAsync(IReadOnlyList<LiveEntityMirror> mirrors)
    {
        ArgumentNullException.ThrowIfNull(mirrors);
        Dispatcher.EnsureAccess();
        var stopwatch = Stopwatch.StartNew();
        await RebuildAsync(mirrors);
        var args = new LiveApplyEventArgs(null, true, stopwatch.Elapsed, []);
        Applied?.Invoke(this, args);
        return args;
    }

    /// <summary>
    /// Applies a snapshot: writes the changed values in place, or rebuilds the grid when the structure changed or an entity left
    /// the game. The entities the rate limit holds back keep their values. Without <see cref="Show"/>, the first snapshot
    /// gives the entities to show: all of its entities.
    /// </summary>
    public async Task<LiveApplyEventArgs> ApplyAsync(LiveSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Dispatcher.EnsureAccess();
        var stopwatch = Stopwatch.StartNew();
        // The entities shown, else those asked for, else (no play session: tests, tools) every entity of the snapshot
        var wanted = entries.Count > 0 ? entries.Select(x => x.Handle).ToList()
            : playSession is not null || handles.Count > 0 ? handles
            : snapshot.Entities.Select(x => x.Handle).ToList();
        var mirrors = wanted.Select(h => snapshot.Entities.FirstOrDefault(x => x.Handle == h)).OfType<LiveEntityMirror>().ToList();
        if (entries.Count == 0 || mirrors.Count != entries.Count)
        {
            // First snapshot, or an entity left the game
            await RebuildAsync(mirrors);
            if (mirrors.Count == 0 && wanted.Count > 0)
                FallbackMessage = wanted.Count == 1 ? "The entity is no longer in the game." : "The entities are no longer in the game.";
            return Done(snapshot, true, stopwatch, []);
        }

        var now = clock.Elapsed;
        var results = new List<(int Handle, LiveApplyResult Result, TimeSpan Elapsed)>();
        var protectedValues = ProtectedValues?.Invoke();
        // Also the values whose edit is on its way, or whose read-back is newer than the snapshot
        var frame = snapshot.Frame;
        applier.IsProtected = protectedValues is { Count: > 0 }
            ? (node, index) => protectedValues.Contains((node, index)) || editor.IsProtected(node, index, frame)
            : (node, index) => editor.IsProtected(node, index, frame);
        string? structuralChange = null;
        using (presenterFactory.BeginBatch())
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!Throttle.ShouldApply(entry.Handle, now))
                    continue;
                var entityWatch = Stopwatch.StartNew();
                var result = applier.Apply(entry.Provider.GetRootNode(), entry.References, mirrors[i]);
                if (result.StructureChanged)
                {
                    structuralChange = $"{mirrors[i].Name}: {result.StructuralChange}";
                    break;
                }
                results.Add((entry.Handle, result, entityWatch.Elapsed));
            }
        }
        if (structuralChange is not null)
        {
            Log.Verbose($"Live grid rebuilt: {structuralChange}");
            await RebuildAsync(mirrors);
            return Done(snapshot, true, stopwatch, results, structuralChange);
        }
        // The cost of an entity includes its share of the end of the batch (the changes of structure finalized at its end)
        var throttleChanged = false;
        foreach (var (handle, _, elapsed) in results)
            throttleChanged |= Throttle.Record(handle, elapsed, now);
        if (throttleChanged || IsThrottled != Throttle.IsThrottled)
            IsThrottled = Throttle.IsThrottled;
        return Done(snapshot, false, stopwatch, results);
    }

    private LiveApplyEventArgs Done(LiveSnapshot snapshot, bool rebuilt, Stopwatch stopwatch, IReadOnlyList<(int Handle, LiveApplyResult Result, TimeSpan Elapsed)> results, string? structuralChange = null)
    {
        UpdateStatus(snapshot);
        var args = new LiveApplyEventArgs(snapshot, rebuilt, stopwatch.Elapsed, results, structuralChange);
        Applied?.Invoke(this, args);
        return args;
    }

    private async Task RebuildAsync(IReadOnlyList<LiveEntityMirror> mirrors)
    {
        // The edits on their way belong to the presenters of the old grid: their answers change nothing in the new one
        editor.Reset();
        entries.Clear();
        Throttle.Keep(mirrors.Select(x => x.Handle).ToList());
        foreach (var mirror in mirrors)
        {
            applier.ResolveContent(mirror);
            entries.Add(new Entry(mirror.Handle, new LiveEntityPropertyProvider(mirror, mirrorNodes), new LiveMirrorReferences(mirror)));
        }
        SetHeader(mirrors);
        await GenerateSelectionPropertiesAsync(entries.Select(x => x.Provider).ToList());
        if (entries.Count > 0)
            ++BuildCount;
    }

    private void SetHeader(IReadOnlyList<LiveEntityMirror>? mirrors)
    {
        var count = mirrors?.Count ?? handles.Count;
        TypeDescription = count > 1 ? "Live entities" : "Live entity";
        Name = count switch
        {
            0 => string.Empty,
            1 => mirrors?[0].Name ?? playSession?.Tree?.Entities.FirstOrDefault(x => x.Handle == handles[0])?.Name ?? string.Empty,
            _ => $"{count} entities selected",
        };
    }

    private void UpdateStatus(LiveSnapshot snapshot)
    {
        var status = string.Create(CultureInfo.InvariantCulture, $"Frame {snapshot.Frame}");
        if (Throttle.IsThrottled)
        {
            var slow = entries.Where(x => Throttle.GetInterval(x.Handle) > Throttle.BaseInterval).ToList();
            var interval = Throttle.LongestInterval.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture);
            status += slow.Count == entries.Count && entries.Count == 1
                ? $" · refreshed every {interval} s, the entity is slow to show"
                : $" · {slow.Count} of {entries.Count} entities refreshed every {interval} s or less, slow to show";
        }
        LiveStatus = status;
    }

    private void OnSnapshotReceived(object? sender, LiveSnapshot snapshot)
    {
        // On the session's reading task: keep the latest, and apply it on the UI thread when the last apply is done
        if (sender != playSession || !IsReceiving)
            return;
        Interlocked.Exchange(ref pending, snapshot);
        if (Interlocked.CompareExchange(ref applyScheduled, 1, 0) == 0)
            Dispatcher.InvokeTask(ApplyPendingAsync).Forget();
    }

    private async Task ApplyPendingAsync()
    {
        try
        {
            while (Interlocked.Exchange(ref pending, null) is { } snapshot)
            {
                // Another grid is shown (the user selected something else): the live grid waits until it is shown again
                if (playSession is null || entries.Count > 0 && Session.ActiveProperties != this)
                    continue;
                try
                {
                    await ApplyAsync(snapshot);
                }
                catch (Exception e)
                {
                    Log.Error("The live grid could not apply a snapshot of the game.", e);
                }
            }
        }
        finally
        {
            Volatile.Write(ref applyScheduled, 0);
            // A snapshot that arrived after the loop ended and before the flag was cleared
            if (Volatile.Read(ref pending) is not null && Interlocked.CompareExchange(ref applyScheduled, 1, 0) == 0)
                Dispatcher.InvokeTask(ApplyPendingAsync).Forget();
        }
    }

    private LiveMirrorReferences? FindMirror(Stride.Engine.Entity entity)
        => entries.FirstOrDefault(x => x.References.Mirror.Entity == entity)?.References;

    private string? FindCameraSlot(SceneCameraSlotId slot)
    {
        foreach (var compositor in Session.AllAssets.Where(x => x.AssetType == typeof(GraphicsCompositorAsset)))
        {
            if (((GraphicsCompositorAsset)compositor.Asset).Cameras.FirstOrDefault(x => x.Id == slot.Id) is { } found)
                return $"{compositor.Url} > {found.Name}";
        }
        return null;
    }

    private sealed record Entry(int Handle, LiveEntityPropertyProvider Provider, LiveMirrorReferences References);

    /// <summary>
    /// A live entity's mirror as a property provider: not an asset, every member shown.
    /// </summary>
    private sealed class LiveEntityPropertyProvider(LiveEntityMirror mirror, NodeContainer nodes) : IPropertyProviderViewModel
    {
        public LiveEntityMirror Mirror { get; } = mirror;

        public bool CanProvidePropertiesViewModel => true;

        public IObjectNode GetRootNode() => nodes.GetOrCreateNode(Mirror.Entity)!;

        public bool ShouldConstructMember(IMemberNode member) => true;

        public bool ShouldConstructItem(IObjectNode collection, NodeIndex index) => true;
    }
}

/// <summary>
/// What a snapshot did to the live grid.
/// </summary>
/// <param name="Snapshot">The snapshot, or <c>null</c> for a build from mirrors.</param>
/// <param name="Rebuilt">Whether the grid was built again.</param>
/// <param name="Elapsed">The time on the UI thread, the build included.</param>
/// <param name="Entities">For each entity applied in place: its handle, what was written, and the time it took. The entities the
/// rate limit held back are not listed.</param>
/// <param name="StructuralChange">The change of structure that made the grid build again, if any.</param>
[Experimental("STREXP012")]
public sealed record LiveApplyEventArgs(LiveSnapshot? Snapshot, bool Rebuilt, TimeSpan Elapsed, IReadOnlyList<(int Handle, LiveApplyResult Result, TimeSpan Elapsed)> Entities, string? StructuralChange = null);
