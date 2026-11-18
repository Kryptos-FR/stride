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
    }

    /// <summary>
    /// The live grid of the current play session; <c>null</c> before the first selection and once the game stopped.
    /// </summary>
    public LivePropertiesViewModel? Properties { get; private set; }

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

    private void GridPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IPanelViewModel.IsShown))
            UpdateReceiving();
    }
}
