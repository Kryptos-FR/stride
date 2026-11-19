// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

namespace Stride.Editor.PlayMode;

/// <summary>
/// What shows the values of the live entities selected in the Live tab: the property grid, in the editor.
/// </summary>
/// <remarks>
/// The Live tab hands it each change of its selection, on the UI thread, and disposes it when the tab closes. It asks the game
/// for the entities' snapshots (<see cref="PlaySession.Subscribe"/>) while it shows them, and only then.
/// </remarks>
[Experimental("STREXP012")]
public interface ILiveSelectionView : IDisposable
{
    /// <summary>
    /// Shows the selected live entities, or gives the place back to what was shown before them when there are none (nothing
    /// selected, or the game stopped). Called again with the same selection when the user comes back to the Live tab.
    /// </summary>
    /// <param name="selection">The selection, or <c>null</c>.</param>
    void Show(LiveSelection? selection);

    /// <summary>
    /// Whether the user can change the running game from this view (its lock is open). The Live tab's commands that change the
    /// game's structure follow it. <c>true</c> by default.
    /// </summary>
    bool EditsEnabled => true;

    /// <summary>
    /// Raised when <see cref="EditsEnabled"/> changes.
    /// </summary>
    event EventHandler? EditsEnabledChanged
    {
        add { }
        remove { }
    }

    /// <summary>
    /// Why the live values of the selected entities cannot be copied into the scene assets (Apply to asset... of the Live tab),
    /// or <c>null</c> when they can.
    /// </summary>
    /// <param name="selection">The selection.</param>
    string? GetApplyToAssetDisabledReason(LiveSelection selection) => "Apply to asset is not available here.";

    /// <summary>
    /// Lists the live values of the selected entities that differ from their scene assets, and copies those the user checks.
    /// Works on the last values received once the game stopped.
    /// </summary>
    /// <param name="selection">The selection.</param>
    /// <returns>The number of values copied.</returns>
    Task<int> ApplyToAssetAsync(LiveSelection selection) => Task.FromResult(0);
}
