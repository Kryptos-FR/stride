// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using Stride.Core.Presentation.ViewModels;
using Stride.Core.Reflection;
using Stride.Editor.PlayMode;
using Stride.Engine;
using Stride.PlayMode.Protocol;
using Stride.PlayMode.Snapshots;

namespace Stride.Assets.Editor.PlayMode;

/// <summary>
/// Opens the picker of a live reference (<see cref="LiveEntityPickerViewModel"/>): the entity, or the component, that a
/// reference of the live grid is set to.
/// </summary>
[Experimental("STREXP012")]
public interface ILiveEntityPickerService
{
    /// <summary>
    /// Shows the picker as a modal dialog.
    /// </summary>
    /// <param name="picker">The picker.</param>
    /// <param name="cancellation">Closes the picker without a pick (the game stopped).</param>
    /// <returns>The pick, or <c>null</c> when the picker was cancelled.</returns>
    Task<LiveEntityPick?> PickAsync(LiveEntityPickerViewModel picker, CancellationToken cancellation);
}

/// <summary>
/// A live entity, or one of its components, picked for a reference.
/// </summary>
/// <param name="EntityHandle">The agent's handle of the entity.</param>
/// <param name="EntityId">The entity's id.</param>
/// <param name="EntityName">The entity's name.</param>
/// <param name="Component">The component picked, or <c>null</c> when an entity is picked.</param>
[Experimental("STREXP012")]
public sealed record LiveEntityPick(int EntityHandle, Guid EntityId, string? EntityName, PlayComponentInfo? Component)
{
    /// <summary>
    /// The reference the edit sends.
    /// </summary>
    public PlayMirrorReference ToReference()
        => Component is { } component
            ? new(PlayMirrorReferenceKind.Component, EntityHandle, EntityId, EntityName, component.Id, component.TypeName)
            : new(PlayMirrorReferenceKind.Entity, EntityHandle, EntityId, EntityName, Guid.Empty, null);
}

/// <summary>
/// The picker of a live reference: the entities of the running game, as the Live tab shows them, under their scenes, narrowed
/// to the ones whose name contains a search text. To pick a component, the entity is picked first, then its component of the
/// type of the reference; the entities with none are listed disabled.
/// </summary>
/// <remarks>
/// The picker follows the Live tab's tree while it is open: an entity that leaves the game leaves the picker.
/// </remarks>
[Experimental("STREXP012")]
public sealed class LiveEntityPickerViewModel : DispatcherViewModel
{
    private readonly LiveTree tree;

    /// <param name="serviceProvider">The services of the session.</param>
    /// <param name="tree">The tree of the Live tab.</param>
    /// <param name="componentType">The type of the component to pick (the type of the reference), or <c>null</c> to pick an
    /// entity.</param>
    /// <param name="initialHandle">The entity selected when the picker opens (the current target of the reference), or 0.</param>
    public LiveEntityPickerViewModel(IViewModelServiceProvider serviceProvider, LiveTree tree, Type? componentType, int initialHandle = 0)
        : base(serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(tree);
        this.tree = tree;
        ComponentType = componentType;
        var name = componentType is null ? null : TypeDescriptorFactory.Default.AttributeRegistry.GetDisplayName(componentType);
        Message = componentType is null
            ? "Select an entity of the running game:"
            : $"Select an entity of the running game, then its {name} component:";
        Rows = BuildRows();
        if (initialHandle != 0)
            SelectedRow = Rows.FirstOrDefault(x => x.Entity?.Handle == initialHandle && x.DisabledReason is null);
        tree.Rows.CollectionChanged += OnTreeChanged;
    }

    /// <summary>
    /// The type of the component to pick, or <c>null</c> when an entity is picked.
    /// </summary>
    public Type? ComponentType { get; }

    /// <summary>
    /// What to pick.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// The rows: the scenes, and their entities under them, indented by depth.
    /// </summary>
    public IReadOnlyList<LivePickerRow> Rows { get; private set => SetValue(ref field, value); }

    /// <summary>
    /// The text that the names of the listed entities contain (case-insensitive); empty lists them all.
    /// </summary>
    public string SearchText
    {
        get;
        set
        {
            if (!SetValue(ref field, value ?? string.Empty))
                return;
            var selected = SelectedRow?.Entity?.Handle;
            Rows = BuildRows();
            SelectedRow = Rows.FirstOrDefault(x => x.Entity is not null && x.Entity.Handle == selected);
        }
    } = string.Empty;

    /// <summary>
    /// The selected row, or <c>null</c>. A scene, or an entity that cannot be picked, is not selected.
    /// </summary>
    public LivePickerRow? SelectedRow
    {
        get;
        set
        {
            if (value is { Entity: null } or { DisabledReason: not null })
                value = null;
            if (!SetValue(ref field, value))
                return;
            Components = value?.Components ?? [];
            // The only component of the type is picked with its entity
            SelectedComponent = Components.Count == 1 ? Components[0] : null;
            OnPropertyChanged(nameof(CanPick));
        }
    }

    /// <summary>
    /// The components of the selected entity that a reference of <see cref="ComponentType"/> can hold.
    /// </summary>
    public IReadOnlyList<LivePickerComponent> Components { get; private set => SetValue(ref field, value); } = [];

    /// <summary>
    /// The selected component, or <c>null</c>.
    /// </summary>
    public LivePickerComponent? SelectedComponent
    {
        get;
        set
        {
            if (SetValue(ref field, value))
                OnPropertyChanged(nameof(CanPick));
        }
    }

    /// <summary>
    /// Whether the selection can be picked: an entity, and its component when a component is picked.
    /// </summary>
    public bool CanPick => SelectedRow?.Entity is not null && (ComponentType is null || SelectedComponent is not null);

    /// <summary>
    /// The pick of the selection, or <c>null</c> when it cannot be picked.
    /// </summary>
    public LiveEntityPick? Pick
        => CanPick && SelectedRow?.Entity is { } entity
            ? new LiveEntityPick(entity.Handle, entity.Id, entity.Name, ComponentType is null ? null : SelectedComponent?.Info)
            : null;

    /// <inheritdoc/>
    public override void Destroy()
    {
        tree.Rows.CollectionChanged -= OnTreeChanged;
        base.Destroy();
    }

    private void OnTreeChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // An entity came or left (or a row of the tab was expanded): the rows follow, the selection stays if its entity does
        var selected = SelectedRow?.Entity?.Handle;
        var component = SelectedComponent?.Info.Id;
        Rows = BuildRows();
        SelectedRow = Rows.FirstOrDefault(x => x.Entity is not null && x.Entity.Handle == selected && x.DisabledReason is null);
        if (component is { } id && Components.FirstOrDefault(x => x.Info.Id == id) is { } kept)
            SelectedComponent = kept;
    }

    private List<LivePickerRow> BuildRows()
    {
        var rows = new List<LivePickerRow>();
        foreach (var root in tree.Roots)
            AddRows(root, 0, rows);
        return rows;
    }

    // Adds the rows of an item and its children; returns whether it added an entity that matches the search (a scene, or an
    // entity, with none under it is left out)
    private bool AddRows(LiveTreeItem item, int depth, List<LivePickerRow> rows)
    {
        var at = rows.Count;
        var entity = item as LiveEntityItem;
        var isMatch = entity is not null && (SearchText.Length == 0 || entity.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        var childMatched = false;
        foreach (var child in item.Children)
            childMatched |= AddRows(child, depth + 1, rows);
        if (entity is null)
        {
            if (childMatched)
                rows.Insert(at, new LivePickerRow(item.Name, depth, null, [], null));
            return childMatched;
        }
        if (isMatch)
        {
            var components = ComponentsOf(entity);
            var reason = ComponentType is not null && components.Count == 0
                ? $"{entity.Name} has no {TypeDescriptorFactory.Default.AttributeRegistry.GetDisplayName(ComponentType)} component."
                : null;
            rows.Insert(at, new LivePickerRow(entity.Name, depth, entity, components, reason));
        }
        else if (childMatched)
        {
            // Over children that match: listed for the hierarchy, not pickable
            rows.Insert(at, new LivePickerRow(entity.Name, depth, entity, [], "Its name does not contain the search text."));
        }
        return isMatch || childMatched;
    }

    private List<LivePickerComponent> ComponentsOf(LiveEntityItem entity)
    {
        if (ComponentType is null)
            return [];
        var components = new List<LivePickerComponent>();
        foreach (var component in entity.Components)
        {
            if (AssemblyRegistry.GetType(component.TypeName, throwOnError: false) is { } type && ComponentType.IsAssignableFrom(type))
                components.Add(new LivePickerComponent(component, TypeDescriptorFactory.Default.AttributeRegistry.GetDisplayName(type)));
        }
        return components;
    }
}

/// <summary>
/// A row of the picker of a live reference.
/// </summary>
/// <param name="Name">The name of the scene or the entity.</param>
/// <param name="Depth">Its depth in the tree.</param>
/// <param name="Entity">The entity, or <c>null</c> for a scene.</param>
/// <param name="Components">The components of the entity that the reference can hold, when a component is picked.</param>
/// <param name="DisabledReason">Why the entity cannot be picked, or <c>null</c>.</param>
[Experimental("STREXP012")]
public sealed record LivePickerRow(string Name, int Depth, LiveEntityItem? Entity, IReadOnlyList<LivePickerComponent> Components, string? DisabledReason)
{
    /// <summary>
    /// Whether the row can be selected.
    /// </summary>
    public bool IsPickable => Entity is not null && DisabledReason is null;

    /// <summary>
    /// Whether the row is a scene.
    /// </summary>
    public bool IsScene => Entity is null;
}

/// <summary>
/// A component that the picker of a live reference offers.
/// </summary>
/// <param name="Info">The component's id and type.</param>
/// <param name="DisplayName">The name of its type.</param>
[Experimental("STREXP012")]
public sealed record LivePickerComponent(PlayComponentInfo Info, string DisplayName);
