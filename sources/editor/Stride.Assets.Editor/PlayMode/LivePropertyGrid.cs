// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Stride.Core.Assets.Editor.Components.Properties;
using Stride.Core.Assets.Editor.Panels;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Editor.PlayMode;

namespace Stride.Assets.Editor.PlayMode;

/// <summary>
/// Shows the Live tab's selection in the session's property grid: one <see cref="LivePropertiesViewModel"/> per play session,
/// which takes the grid while live entities are selected and gives it back to what it showed before when the selection ends
/// or the game stops.
/// </summary>
/// <remarks>
/// <para>
/// A selection in the asset view or a scene editor takes the grid back as usual (its properties become the session's active
/// ones); coming back to the Live tab shows the live entities again (<see cref="Show"/> with the same selection).
/// </para>
/// <para>
/// The game sends the snapshots of the selected entities only while the live grid is shown: while it is the session's active
/// properties and the property grid panel is on screen. Otherwise the live grid keeps its last values and the game sends
/// nothing (<see cref="LivePropertiesViewModel.Suspend"/>).
/// </para>
/// <para>
/// Once the game stopped, a selection of the rows of its last tree shows a grid with no values that says the game stopped
/// (<see cref="LiveSelection.IsStopped"/>, <see cref="LivePropertiesViewModel.ShowStopped"/>).
/// </para>
/// <para>
/// Used on the UI thread only.
/// </para>
/// </remarks>
[Experimental("STREXP012")]
public sealed class LivePropertyGrid : ILiveSelectionView
{
    private readonly SessionViewModel session;
    private readonly Func<IPanelViewModel?> findGridPanel;
    private IPanelViewModel? gridPanel;
    // The play session of the live grid
    private PlaySession? liveSession;
    // The grid the live entities took the place of, given back when they leave it
    private SessionObjectPropertiesViewModel? previous;
    // The selection the live grid was last asked to show, while it follows it
    private LiveSelection? shown;
    // The last values received of the entities shown, kept after the game stopped for Apply to asset...
    private readonly Dictionary<int, LiveMirrorReferences> kept = [];
    private PlaySession? keptSession;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="LivePropertyGrid"/> class.
    /// </summary>
    /// <param name="session">The session whose property grid shows the live entities.</param>
    /// <param name="findGridPanel">Finds the property grid panel, to know whether it is on screen; <c>null</c> (or a function
    /// that finds none) when there is no such panel: the grid then counts as shown.</param>
    public LivePropertyGrid(SessionViewModel session, Func<IPanelViewModel?>? findGridPanel = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        this.session = session;
        this.findGridPanel = findGridPanel ?? (static () => null);
        session.PropertyChanged += SessionPropertyChanged;
        AssetApplier = new LiveAssetApplier(session);
    }

    /// <summary>
    /// Copies the live values into the scene assets (Apply to asset... of the Live tab).
    /// </summary>
    public LiveAssetApplier AssetApplier { get; }

    /// <summary>
    /// The live grid of the current play session; <c>null</c> before the first selection and once the game stopped.
    /// </summary>
    public LivePropertiesViewModel? Properties
    {
        get;
        private set
        {
            if (field == value)
                return;
            var wasEnabled = EditsEnabled;
            if (field is not null)
            {
                field.PropertyChanged -= PropertiesPropertyChanged;
                field.Applied -= PropertiesApplied;
            }
            field = value;
            if (value is not null)
            {
                value.PropertyChanged += PropertiesPropertyChanged;
                value.Applied += PropertiesApplied;
            }
            if (wasEnabled != EditsEnabled)
                EditsEnabledChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc/>
    /// <remarks>The lock of the live grid of the current play session: on until the user turns it off there.</remarks>
    public bool EditsEnabled => Properties?.EditsEnabled ?? true;

    /// <inheritdoc/>
    public event EventHandler? EditsEnabledChanged;

    /// <summary>
    /// The live entities selected, or <c>null</c>.
    /// </summary>
    public LiveSelection? Selection { get; private set; }

    /// <inheritdoc/>
    public void Show(LiveSelection? selection)
    {
        if (disposed)
            return;
        Selection = selection;
        if (selection is null)
        {
            GiveBack();
            shown = null;
            // The game stopped (or lost its connection): its grid goes; a new selection in the same game uses it again
            if (liveSession is not { State: PlaySessionState.Connected })
                DestroyProperties();
            else
                Properties?.Detach();
            return;
        }

        if (Properties is null || liveSession != selection.Session)
        {
            DestroyProperties();
            Properties = new LivePropertiesViewModel(session);
            liveSession = selection.Session;
        }
        var properties = Properties;
        properties.Tree = selection.Tree ?? properties.Tree;
        if (session.ActiveProperties != properties)
            previous = session.ActiveProperties;
        if (selection.IsStopped)
        {
            // The rows of a game that stopped: no values, the grid says why
            shown = selection;
            properties.ShowStopped(selection.Session, selection.Handles, selection.Names);
        }
        else if (shown is null || shown.Session != selection.Session || !shown.Handles.SequenceEqual(selection.Handles))
        {
            shown = selection;
            properties.Show(selection.Session, selection.Handles, LivePanelViewModel.SnapshotRateHz);
        }
        session.ActiveProperties = properties;
        UpdateReceiving();
    }

    /// <inheritdoc/>
    /// <remarks>The entities need values received while the game ran: those of the last entities the grid showed.</remarks>
    public string? GetApplyToAssetDisabledReason(LiveSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var reasons = new List<string>();
        var applicable = 0;
        foreach (var source in ApplySources(selection, reasons))
        {
            if (AssetApplier.FindCounterpart(source, out _, out _) is { } reason)
                reasons.Add(selection.Handles.Count > 1 ? $"{source.Name}: {reason}" : reason);
            else
                ++applicable;
        }
        return applicable > 0 ? null : string.Join(Environment.NewLine, reasons.Distinct());
    }

    /// <inheritdoc/>
    public async Task<int> ApplyToAssetAsync(LiveSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var changes = new List<LiveApplyChange>();
        var entities = 0;
        foreach (var source in ApplySources(selection, []))
        {
            var found = AssetApplier.ListChanges(source, null, out var reason);
            if (reason is not null)
                continue;
            ++entities;
            changes.AddRange(found);
        }
        if (entities == 0)
            return 0;
        var title = entities == 1 ? changes.FirstOrDefault()?.Source.Name ?? string.Empty : $"{entities} entities";
        return await LivePropertiesViewModel.ShowApplyListAsync(session, title, changes, entities > 1, AssetApplier);
    }

    /// <summary>
    /// The selected entities whose values the grid received, with the reasons of those it did not.
    /// </summary>
    private List<LiveApplySource> ApplySources(LiveSelection selection, List<string> reasons)
    {
        var sources = new List<LiveApplySource>();
        for (var i = 0; i < selection.Handles.Count; i++)
        {
            var handle = selection.Handles[i];
            if (keptSession == selection.Session && kept.TryGetValue(handle, out var references))
            {
                sources.Add(new LiveApplySource(references, selection.Tree?.FindEntity(handle), selection.Tree, selection.Session.EditRecord));
                continue;
            }
            var reason = selection.IsStopped
                ? "The game stopped before its values of this entity arrived: select it while the game runs to apply them after it stops."
                : "The game's values of this entity have not arrived yet.";
            var name = selection.Names is { } names && i < names.Count ? names[i] : selection.Tree?.FindEntity(handle)?.Name;
            reasons.Add(selection.Handles.Count > 1 && name is not null ? $"{name}: {reason}" : reason);
        }
        return sources;
    }

    /// <summary>
    /// Gives the grid back and ends the live grid.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        session.PropertyChanged -= SessionPropertyChanged;
        if (gridPanel is INotifyPropertyChanged notifying)
            notifying.PropertyChanged -= GridPanelPropertyChanged;
        gridPanel = null;
        GiveBack();
        DestroyProperties();
    }

    /// <summary>
    /// The grid goes back to what it showed before the live entities, if they still have it.
    /// </summary>
    private void GiveBack()
    {
        if (Properties is { } live && session.ActiveProperties == live)
            session.ActiveProperties = previous ?? session.AssetViewProperties;
        previous = null;
    }

    private void DestroyProperties()
    {
        if (Properties is not { } live)
            return;
        live.Detach();
        live.ViewModel?.Destroy();
        Properties = null;
        liveSession = null;
        shown = null;
    }

    /// <summary>
    /// The game sends the snapshots while the live grid is shown, and only then.
    /// </summary>
    private void UpdateReceiving()
    {
        if (Properties is not { } live || shown is null)
            return;
        if (Selection is not null && session.ActiveProperties == live && IsGridShown())
            live.Resume();
        else
            live.Suspend();
    }

    private bool IsGridShown()
    {
        if (gridPanel is null && findGridPanel() is { } panel)
        {
            gridPanel = panel;
            if (panel is INotifyPropertyChanged notifying)
                notifying.PropertyChanged += GridPanelPropertyChanged;
        }
        return gridPanel?.IsShown ?? true;
    }

    private void SessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SessionViewModel.ActiveProperties))
            UpdateReceiving();
    }

    private void PropertiesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LivePropertiesViewModel.EditsEnabled))
            EditsEnabledChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PropertiesApplied(object? sender, LiveApplyEventArgs e)
    {
        if (sender is not LivePropertiesViewModel properties || liveSession is null)
            return;
        if (keptSession != liveSession)
        {
            kept.Clear();
            keptSession = liveSession;
        }
        foreach (var references in properties.Mirrors)
            kept[references.Mirror.Handle] = references;
    }

    private void GridPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IPanelViewModel.IsShown))
            UpdateReceiving();
    }
}
