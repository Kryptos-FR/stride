// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Stride.Core.Presentation.ViewModels;

namespace Stride.Assets.Editor.PlayMode;

/// <summary>
/// Shows the list of the live values to apply to the assets (<see cref="LiveApplyListViewModel"/>).
/// </summary>
[Experimental("STREXP012")]
public interface ILiveApplyListService
{
    /// <summary>
    /// Shows the list as a modal dialog.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <returns>Whether the user pressed OK: the checked values are to be applied.</returns>
    Task<bool> ShowAsync(LiveApplyListViewModel list);
}

/// <summary>
/// The list of the live values of a component, or of entities, that differ from their asset's, from which the user checks those
/// to apply: the values edited live are checked, those the game changed itself are not.
/// </summary>
/// <remarks>
/// The list keeps the values it was made with: it goes on working after the game stops, and OK applies those values.
/// </remarks>
[Experimental("STREXP012")]
public sealed class LiveApplyListViewModel : DispatcherViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveApplyListViewModel"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services of the session.</param>
    /// <param name="title">What the list applies, for the window's title.</param>
    /// <param name="changes">The values that differ.</param>
    /// <param name="showEntities">Whether each row names its entity (several entities).</param>
    /// <param name="notes">What the list does not show, under it; <c>null</c> for nothing.</param>
    public LiveApplyListViewModel(IViewModelServiceProvider serviceProvider, string title, IReadOnlyList<LiveApplyChange> changes, bool showEntities = false, string? notes = null)
        : base(serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(changes);
        Title = title;
        ShowEntities = showEntities;
        Notes = notes;
        Items = changes.Select(x => new LiveApplyListItem(x, this)).ToList();
    }

    /// <summary>
    /// What the list applies.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// The rows, one per value that differs.
    /// </summary>
    public IReadOnlyList<LiveApplyListItem> Items { get; }

    /// <summary>
    /// Whether each row names its entity.
    /// </summary>
    public bool ShowEntities { get; }

    /// <summary>
    /// What the list does not show, or <c>null</c>.
    /// </summary>
    public string? Notes { get; }

    /// <summary>
    /// Whether no value differs.
    /// </summary>
    public bool IsEmpty => Items.Count == 0;

    /// <summary>
    /// What the list says when no value differs.
    /// </summary>
    public string EmptyMessage => "Every value is the same as the asset's.";

    /// <summary>
    /// The number of rows checked.
    /// </summary>
    public int CheckedCount => Items.Count(x => x.IsChecked);

    /// <summary>
    /// Whether OK applies something.
    /// </summary>
    public bool CanApply => CheckedCount > 0;

    /// <summary>
    /// The values checked, to apply.
    /// </summary>
    public IReadOnlyList<LiveApplyChange> CheckedChanges => Items.Where(x => x.IsChecked).Select(x => x.Change).ToList();

    /// <summary>
    /// Checks every row that can be applied, or unchecks them all when they are all checked.
    /// </summary>
    public void ToggleAll()
    {
        var all = Items.Where(x => x.CanApply).All(x => x.IsChecked);
        foreach (var item in Items.Where(x => x.CanApply))
            item.IsChecked = !all;
    }

    internal void OnCheckedChanged()
    {
        OnPropertyChanged(nameof(CheckedCount), nameof(CanApply));
    }
}

/// <summary>
/// A row of <see cref="LiveApplyListViewModel"/>: a value that differs, and whether to apply it.
/// </summary>
[Experimental("STREXP012")]
public sealed class LiveApplyListItem : ViewModelBase
{
    private readonly LiveApplyListViewModel owner;

    internal LiveApplyListItem(LiveApplyChange change, LiveApplyListViewModel owner)
    {
        Change = change;
        this.owner = owner;
        // Case 38: what the game changed itself (a position a script or physics moved) is rarely wanted
        IsChecked = change.CanApply && change.Origin == LiveApplyOrigin.EditedLive;
    }

    /// <summary>
    /// The value that differs.
    /// </summary>
    public LiveApplyChange Change { get; }

    /// <summary>
    /// The entity's name.
    /// </summary>
    public string Entity => Change.Source.Name;

    /// <summary>
    /// The member's path, from its component.
    /// </summary>
    public string Path => Change.DisplayPath;

    /// <summary>
    /// The asset's value.
    /// </summary>
    public string AssetValue => Change.AssetText;

    /// <summary>
    /// The live value.
    /// </summary>
    public string LiveValue => Change.LiveText;

    /// <summary>
    /// Where the change came from: "Edited live" or "Changed by the game".
    /// </summary>
    public string Origin => Change.Origin == LiveApplyOrigin.EditedLive ? "Edited live" : "Changed by the game";

    /// <summary>
    /// Whether the user changed it.
    /// </summary>
    public bool IsEditedLive => Change.Origin == LiveApplyOrigin.EditedLive;

    /// <summary>
    /// Whether it can be applied; otherwise its row is disabled, with the reason.
    /// </summary>
    public bool CanApply => Change.CanApply;

    /// <summary>
    /// Why it cannot be applied, for the row's tooltip; or what the origin means.
    /// </summary>
    public string ToolTip => Change.Reason ?? (IsEditedLive
        ? "You changed this value while the game runs."
        : "The game changed this value itself (a script, physics, an animation): it is not checked.");

    /// <summary>
    /// Whether the value is applied on OK.
    /// </summary>
    public bool IsChecked
    {
        get;
        set
        {
            if (SetValue(ref field, value && CanApply))
                owner.OnCheckedChanged();
        }
    }
}
