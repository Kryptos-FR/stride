// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Stride.PlayMode.Protocol;

namespace Stride.Editor.PlayMode;

/// <summary>
/// A row of the Live tab's tree: a running scene or one of its entities.
/// </summary>
[Experimental("STREXP012")]
public abstract class LiveTreeItem : INotifyPropertyChanged
{
    private readonly LiveTree owner;

    private protected LiveTreeItem(LiveTree owner, int handle, Guid id, string? name)
    {
        this.owner = owner;
        Handle = handle;
        Id = id;
        Name = name ?? string.Empty;
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// The agent's handle of this scene or entity, unique for the life of the game.
    /// </summary>
    public int Handle { get; }

    /// <summary>
    /// The id of the scene, or of the entity in its scene or prefab asset.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// The name.
    /// </summary>
    public string Name { get; internal set => SetValue(ref field, value); }

    /// <summary>
    /// The scene or entity it is under; <c>null</c> for the root scene.
    /// </summary>
    public LiveTreeItem? Parent { get; internal set; }

    /// <summary>
    /// The child scenes and entities, in the game's order.
    /// </summary>
    public IReadOnlyList<LiveTreeItem> Children => ChildList;

    /// <summary>
    /// Whether it has children, for the row's expander.
    /// </summary>
    public bool HasChildren { get; private set => SetValue(ref field, value); }

    /// <summary>
    /// How deep the row is in the tree: 0 for the root scene.
    /// </summary>
    public int Depth { get; internal set => SetValue(ref field, value); }

    /// <summary>
    /// Whether its children are shown. While the tab filters, the rows that lead to a match are shown expanded whatever this says.
    /// </summary>
    public bool IsExpanded
    {
        get;
        set
        {
            if (SetValue(ref field, value))
                owner.OnExpandedChanged();
        }
    }

    /// <summary>
    /// Whether the row matches the search of the tab.
    /// </summary>
    public bool IsMatch { get; internal set => SetValue(ref field, value); }

    internal List<LiveTreeItem> ChildList { get; } = [];

    internal void AddChild(LiveTreeItem child)
    {
        child.Parent = this;
        ChildList.Add(child);
        HasChildren = true;
    }

    internal void RemoveChild(LiveTreeItem child)
    {
        if (ChildList.Remove(child))
            child.Parent = null;
        HasChildren = ChildList.Count > 0;
    }

    /// <summary>
    /// Sets a property's storage and raises <see cref="PropertyChanged"/> when the value changes.
    /// </summary>
    /// <returns>Whether the value changed.</returns>
    private protected bool SetValue<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private protected void OnPropertyChanged(string? propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// A running scene in the Live tab: the root scene or a child scene.
/// </summary>
[Experimental("STREXP012")]
public sealed class LiveSceneItem : LiveTreeItem
{
    internal LiveSceneItem(LiveTree owner, PlaySceneInfo info)
        : base(owner, info.Handle, info.Id, DisplayName(info))
    {
        Info = info;
    }

    /// <summary>
    /// The scene as the game sent it: its URL finds its asset.
    /// </summary>
    public PlaySceneInfo Info { get; }

    /// <summary>
    /// The name of the scene's asset, as the user knows the scene (a loaded scene keeps the default name of a scene), else
    /// the name the game gave it.
    /// </summary>
    private static string? DisplayName(PlaySceneInfo info)
        => string.IsNullOrEmpty(info.Url) ? info.Name : info.Url[(info.Url.LastIndexOf('/') + 1)..];
}

/// <summary>
/// A running entity in the Live tab.
/// </summary>
[Experimental("STREXP012")]
public sealed class LiveEntityItem : LiveTreeItem
{
    internal LiveEntityItem(LiveTree owner, PlayEntityInfo info)
        : base(owner, info.Handle, info.Id, info.Name)
    {
        IsEnabled = info.Enabled;
        ComponentTypeNames = [.. info.Components.Select(x => x.TypeName)];
        SceneHandle = info.SceneHandle;
    }

    /// <summary>
    /// Whether all its components that can be disabled are enabled; a disabled entity's row is greyed.
    /// </summary>
    public bool IsEnabled { get; internal set => SetValue(ref field, value); }

    /// <summary>
    /// The type names of its components, in order (the full name, a comma and the assembly's simple name): the row's icon is
    /// its main component's.
    /// </summary>
    public IReadOnlyList<string> ComponentTypeNames { get; internal set => SetValue(ref field, value); }

    /// <summary>
    /// The handle of the scene it is in.
    /// </summary>
    public int SceneHandle { get; internal set; }

    /// <summary>
    /// The scene whose asset holds this entity, when it has a counterpart there; <c>null</c> otherwise.
    /// </summary>
    public LiveSceneItem? CounterpartScene
    {
        get;
        internal set
        {
            if (SetValue(ref field, value))
                OnPropertyChanged(nameof(IsSpawned));
        }
    }

    /// <summary>
    /// Whether the entity has no counterpart in the scene assets of the running scenes: the game created it (spawned it,
    /// instantiated a prefab, cloned an entity). The instances of a prefab keep the prefab's ids, so several live entities can
    /// share an id; only the first of them that has the id of an entity of a scene asset is its counterpart, and the others
    /// are marked spawned.
    /// </summary>
    public bool IsSpawned => CounterpartScene is null;
}
