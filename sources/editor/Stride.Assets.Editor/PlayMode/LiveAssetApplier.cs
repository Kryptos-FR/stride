// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Stride.Assets.Entities;
using Stride.Assets.Presentation.ViewModels;
using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Assets.Presentation.Services;
using Stride.Core.Diagnostics;
using Stride.Core.Extensions;
using Stride.Core.Presentation.Services;
using Stride.Core.Quantum;
using Stride.Core.Reflection;
using Stride.Core.Serialization;
using Stride.Core.Yaml;
using Stride.Editor.PlayMode;
using Stride.Engine;
using Stride.PlayMode.Snapshots;

namespace Stride.Assets.Editor.PlayMode;

/// <summary>
/// A step of the path of a member, from its component (or its entity) down: a member, or an item of a collection.
/// </summary>
/// <param name="Member">The member's name, or <c>null</c> for an item.</param>
/// <param name="Index">The item's index or key, for an item.</param>
[Experimental("STREXP012")]
public readonly record struct LiveApplyStep(string? Member, NodeIndex Index)
{
    /// <summary>A member.</summary>
    public static LiveApplyStep Of(string member) => new(member ?? throw new ArgumentNullException(nameof(member)), NodeIndex.Empty);

    /// <summary>An item of a list, or of a dictionary by its key.</summary>
    public static LiveApplyStep At(NodeIndex index) => new(null, index);

    /// <summary>
    /// The path as text, as the edits of the running game write it: <c>Position.X</c>, <c>Values[2]</c>, <c>Table[Fast]</c>.
    /// </summary>
    public static string Format(IEnumerable<LiveApplyStep> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var text = new StringBuilder();
        foreach (var step in path)
        {
            if (step.Member is { } member)
            {
                if (text.Length > 0)
                    text.Append('.');
                text.Append(member);
            }
            else
            {
                text.Append('[').Append(Convert.ToString(step.Index.Value, CultureInfo.InvariantCulture)).Append(']');
            }
        }
        return text.ToString();
    }
}

/// <summary>
/// Who changed a live value that differs from the asset's.
/// </summary>
[Experimental("STREXP012")]
public enum LiveApplyOrigin
{
    /// <summary>The user changed it while the game runs (it is in the session's <see cref="PlayEditRecord"/>).</summary>
    EditedLive,

    /// <summary>The game changed it itself: a script, physics, an animation.</summary>
    ChangedByGame,
}

/// <summary>
/// What applying a difference does to the asset.
/// </summary>
[Experimental("STREXP012")]
public enum LiveApplyKind
{
    /// <summary>The member or item gets the live value (a number, a string, a vector, a reference...).</summary>
    Value,

    /// <summary>The member gets a new object of the live object's type (or none), then the live object's values.</summary>
    Object,

    /// <summary>The list gets the live list's items: the items at the same positions are set, the others added or removed.</summary>
    Collection,

    /// <summary>The dictionary gets the item of the live dictionary's key.</summary>
    ItemAdded,

    /// <summary>The dictionary's item of a key the live dictionary no longer has is removed.</summary>
    ItemRemoved,

    /// <summary>The asset entity gets a new component of the live component's type, with its values.</summary>
    Component,
}

/// <summary>
/// A live entity that values are applied from: its mirror, its row of the Live tab (which knows its counterpart in a scene
/// asset), the tree (for references to other entities), and the edits the user made.
/// </summary>
/// <param name="References">The mirror of the entity, with what its stand-ins stand for.</param>
/// <param name="Item">The entity's row in the Live tab's tree; <c>null</c> when the tree does not have it.</param>
/// <param name="Tree">The Live tab's tree; <c>null</c> when there is none.</param>
/// <param name="Record">The edits of the play session; <c>null</c> when there is none (every value counts as changed by the game).</param>
[Experimental("STREXP012")]
public sealed record LiveApplySource(LiveMirrorReferences References, LiveEntityItem? Item, LiveTree? Tree, PlayEditRecord? Record)
{
    /// <summary>The agent's handle of the entity.</summary>
    public int Handle => References.Mirror.Handle;

    /// <summary>The entity's name.</summary>
    public string Name => References.Mirror.Name ?? string.Empty;
}

/// <summary>
/// A live value that differs from the asset's: what <see cref="LiveAssetApplier"/> lists and applies.
/// </summary>
[Experimental("STREXP012")]
public sealed class LiveApplyChange
{
    internal LiveApplyChange(LiveApplySource source, Guid componentId, string componentName, IReadOnlyList<LiveApplyStep> path, LiveApplyKind kind, object? liveValue, string assetText, string liveText, LiveApplyOrigin origin, string? reason)
    {
        Source = source;
        ComponentId = componentId;
        ComponentName = componentName;
        Path = path;
        PathText = LiveApplyStep.Format(path);
        Kind = kind;
        LiveValue = liveValue;
        AssetText = assetText;
        LiveText = liveText;
        Origin = origin;
        Reason = reason;
    }

    /// <summary>The live entity.</summary>
    public LiveApplySource Source { get; }

    /// <summary>The component's id; <see cref="Guid.Empty"/> for a member of the entity itself.</summary>
    public Guid ComponentId { get; }

    /// <summary>The component's type name, as its header shows it; empty for a member of the entity itself.</summary>
    public string ComponentName { get; }

    /// <summary>The path of the member from its component.</summary>
    public IReadOnlyList<LiveApplyStep> Path { get; }

    /// <summary>The path as text (<see cref="LiveApplyStep.Format"/>); empty for a whole component.</summary>
    public string PathText { get; }

    /// <summary>The component and the path, as a list shows it: <c>Transform.Position</c>.</summary>
    public string DisplayPath => PathText.Length == 0 ? ComponentName : ComponentName.Length == 0 ? PathText : $"{ComponentName}.{PathText}";

    /// <summary>What applying it does.</summary>
    public LiveApplyKind Kind { get; }

    /// <summary>The asset's value, as text.</summary>
    public string AssetText { get; }

    /// <summary>The live value, as text.</summary>
    public string LiveText { get; }

    /// <summary>Who changed the live value.</summary>
    public LiveApplyOrigin Origin { get; }

    /// <summary>Why it cannot be applied; <c>null</c> when it can.</summary>
    public string? Reason { get; }

    /// <summary>Whether it can be applied.</summary>
    public bool CanApply => Reason is null;

    // The live value when the change was listed: a value, an object, a list or a component
    internal object? LiveValue { get; }

    /// <inheritdoc/>
    public override string ToString() => $"{Source.Name} > {DisplayPath}: {AssetText} -> {LiveText}";
}

/// <summary>
/// Copies values of the running game into the scene assets of the session (Apply to asset): it finds the counterpart of a live
/// entity in its scene asset, lists the values that differ, and sets them through the asset's property graph in one undo step.
/// </summary>
/// <remarks>
/// <para>
/// The counterpart of a live entity is the entity of the same id in the scene asset of its running scene (the Live tab finds
/// it). Its component is matched by id, and a member by its path from the component. The asset's lists are matched with the
/// game's by position, and only while they have as many items and the user added, removed or moved none of them while the game
/// runs: otherwise the whole list is applied. Dictionaries are matched by key.
/// </para>
/// <para>
/// Values are set through the asset's property graph, as the scene editor's property grid sets them: a prefab instance gets an
/// override, the scene editor shows the change, and the change is one undo step of the session. Content goes back by its URL
/// to the session's asset, a reference to an entity or a component to the counterpart in the same scene asset.
/// </para>
/// <para>
/// Used on the UI thread only.
/// </para>
/// </remarks>
/// <param name="session">The session of the scene assets.</param>
[Experimental("STREXP012")]
public sealed class LiveAssetApplier(SessionViewModel session)
{
    private static readonly Logger Log = GlobalLogger.GetLogger("PlayMode");

    private SessionViewModel Session { get; } = session;

    /// <summary>The reason of an entity added from the Live tab.</summary>
    public const string AddedWhilePlayingReason = "The entity was added while the game runs: it has no counterpart in a scene asset.";

    /// <summary>The reason of an entity the game made.</summary>
    public const string MadeByGameReason = "The game made this entity (spawned, or instantiated from a prefab): it has no counterpart in a scene asset.";

    /// <summary>The reason of a value that equals the asset's.</summary>
    public const string SameAsAssetReason = "Same as the asset.";

    /// <summary>The reason of a placeholder component.</summary>
    public const string PlaceholderReason = "The editor does not know this component's type: its values cannot be read.";

    /// <summary>
    /// Finds the counterpart of a live entity: the entity of its id in the scene asset of its running scene.
    /// </summary>
    /// <returns>Why there is none, or <c>null</c> when it was found.</returns>
    public string? FindCounterpart(LiveApplySource source, out SceneViewModel? scene, out EntityDesign? entity)
    {
        ArgumentNullException.ThrowIfNull(source);
        scene = null;
        entity = null;
        var item = source.Item ?? source.Tree?.FindEntity(source.Handle);
        if (item is null)
            return "The entity is no longer in the game's tree.";
        if (item.IsAddedWhilePlaying)
            return AddedWhilePlayingReason;
        if (item.CounterpartScene is not { } running)
            return MadeByGameReason;
        scene = LiveSceneAssets.FindScene(Session, running.Info);
        if (scene is null)
            return $"The scene asset {running.Info.Url} is no longer in the session.";
        if (!scene.Asset.Hierarchy.Parts.TryGetValue(item.Id, out entity))
            return $"The entity was deleted from the scene asset {scene.Url} since the game started.";
        return null;
    }

    /// <summary>
    /// Lists the live values of a member that differ from the asset's: the member itself when it holds a value, or what differs
    /// inside it when it holds an object or a collection.
    /// </summary>
    /// <param name="source">The live entity.</param>
    /// <param name="componentId">The component's id; <see cref="Guid.Empty"/> for a member of the entity itself.</param>
    /// <param name="path">The path of the member from the component.</param>
    /// <param name="reason">Why nothing can be listed (no counterpart, a path the asset does not have), or <c>null</c>.</param>
    public IReadOnlyList<LiveApplyChange> ListChanges(LiveApplySource source, Guid componentId, IReadOnlyList<LiveApplyStep> path, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(path);
        var changes = new List<LiveApplyChange>();
        if ((reason = FindCounterpart(source, out var scene, out var entity)) is not null)
            return changes;
        var context = new Context(this, source, scene!, entity!, componentId, trustIndices: false);
        if ((reason = context.FindRoot(out var root, out var liveRoot, out var componentName)) is not null)
            return changes;
        if (path.Count == 0)
        {
            context.DiffMembers(root!, liveRoot!, [], componentName!, changes);
            return changes;
        }
        if ((reason = context.Walk(root!, liveRoot!, path, out var slot)) is not null)
            return changes;
        context.Diff(slot!, path, componentName!, changes);
        return changes;
    }

    /// <summary>
    /// Lists the live values of a component that differ from the asset's, or of every component of the entity and the entity's
    /// own members: with a component added while the game runs as one difference.
    /// </summary>
    /// <param name="source">The live entity.</param>
    /// <param name="componentId">The component's id; <c>null</c> for the whole entity.</param>
    /// <param name="reason">Why nothing can be listed, or <c>null</c>.</param>
    public IReadOnlyList<LiveApplyChange> ListChanges(LiveApplySource source, Guid? componentId, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(source);
        var changes = new List<LiveApplyChange>();
        if ((reason = FindCounterpart(source, out var scene, out var entity)) is not null)
            return changes;
        var mirror = source.References.Mirror.Entity;
        if (componentId is { } id)
        {
            if (entity!.Entity.Components.All(x => x.Id != id) && mirror.Components.FirstOrDefault(x => x.Id == id) is { } added)
            {
                changes.Add(AddedComponent(source, added));
                return changes;
            }
            return ListChanges(source, id, [], out reason);
        }

        // The entity's own members (its name), then each component in the order of the game
        var own = new Context(this, source, scene!, entity!, Guid.Empty, trustIndices: false);
        own.DiffMember(Session.AssetNodeContainer.GetNode(entity!.Entity)!, mirror, nameof(Entity.Name), [], string.Empty, changes);
        foreach (var component in mirror.Components)
        {
            if (entity!.Entity.Components.All(x => x.Id != component.Id))
            {
                changes.Add(AddedComponent(source, component));
                continue;
            }
            changes.AddRange(ListChanges(source, component.Id, [], out _));
        }
        return changes;
    }

    /// <summary>
    /// Sets the asset values of the changes that can be applied, in one undo step named <paramref name="actionName"/>.
    /// </summary>
    /// <returns>The number of changes applied.</returns>
    public int Apply(IReadOnlyCollection<LiveApplyChange> changes, string actionName)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(actionName);
        var applicable = changes.Where(x => x.CanApply).ToList();
        if (applicable.Count == 0)
            return 0;
        var undoRedo = Session.ServiceProvider.Get<IUndoRedoService>();
        var applied = 0;
        using (var transaction = undoRedo.CreateTransaction())
        {
            foreach (var change in applicable)
            {
                try
                {
                    if (ApplyChange(change))
                        ++applied;
                }
                catch (Exception e)
                {
                    Log.Error($"Apply to asset could not set {change.Source.Name} > {change.DisplayPath}.", e);
                }
            }
            undoRedo.SetName(transaction, actionName);
        }
        return applied;
    }

    /// <summary>
    /// The text of a value, as a list of differences shows it.
    /// </summary>
    public static string Describe(object? value)
    {
        switch (value)
        {
            case null:
                return "(none)";
            case string text:
                return $"\"{text}\"";
            case Entity entity:
                return entity.Name ?? entity.Id.ToString();
            case EntityComponent component:
                return $"{DisplayName(component.GetType())} of {component.Entity?.Name ?? "?"}";
            case UrlReferenceBase url:
                return url.Url ?? ContentReferenceHelper.EmptyReference;
        }
        if (AttachedReferenceManager.GetAttachedReference(value) is { } content)
            return string.IsNullOrEmpty(content.Url) ? ContentReferenceHelper.CreatedAtRunTime : content.Url;
        if (!value.GetType().IsValueType && AssetRegistry.IsExactContentType(value.GetType()))
            return ContentReferenceHelper.CreatedAtRunTime;
        if (value is ICollection collection && value is not string)
            return collection.Count == 1 ? "1 item" : $"{collection.Count} items";
        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        var type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || type.IsValueType)
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        return DisplayName(type);
    }

    /// <summary>
    /// The name of a type as the property grid shows it (its display attribute, else its name).
    /// </summary>
    internal static string DisplayName(Type type) => TypeDescriptorFactory.Default.AttributeRegistry.GetAttribute<DisplayAttribute>(type)?.Name ?? type.Name;

    private LiveApplyChange AddedComponent(LiveApplySource source, EntityComponent component)
    {
        var name = DisplayName(component.GetType());
        var origin = source.Record?.IsComponentAdded(source.Handle, component.Id) == true ? LiveApplyOrigin.EditedLive : LiveApplyOrigin.ChangedByGame;
        string? reason = null;
        if (component is IUnloadable)
            reason = PlaceholderReason;
        else if (!CanCreate(component.GetType(), out var why))
            reason = why;
        else if (FindCounterpart(source, out _, out var entity) is null && !EntityComponentAttributes.Get(component.GetType()).AllowMultipleComponents
            && entity!.Entity.Components.Any(x => x.GetType() == component.GetType()))
            reason = $"The asset entity already has a {name}, which cannot be there twice.";
        return new LiveApplyChange(source, component.Id, name, [], LiveApplyKind.Component, component, "(not in the asset)", "added while playing", origin, reason);
    }

    private bool ApplyChange(LiveApplyChange change)
    {
        var source = change.Source;
        if (FindCounterpart(source, out var scene, out var entity) is { } missing)
        {
            Log.Warning($"Apply to asset skipped {source.Name} > {change.DisplayPath}: {missing}");
            return false;
        }
        if (change.Kind == LiveApplyKind.Component)
            return AddComponent(change, scene!, entity!);

        var context = new Context(this, source, scene!, entity!, change.ComponentId, trustIndices: change.Kind == LiveApplyKind.Collection);
        if (context.FindRoot(out var root, out var liveRoot, out _) is { } noRoot)
        {
            Log.Warning($"Apply to asset skipped {source.Name} > {change.DisplayPath}: {noRoot}");
            return false;
        }
        if (context.Walk(root!, liveRoot!, change.Path, out var slot) is { } noPath)
        {
            Log.Warning($"Apply to asset skipped {source.Name} > {change.DisplayPath}: {noPath}");
            return false;
        }
        return context.Apply(slot!, change.Path, change.Kind, change.LiveValue);
    }

    private bool AddComponent(LiveApplyChange change, SceneViewModel scene, EntityDesign entity)
    {
        var live = (EntityComponent)change.LiveValue!;
        if (entity.Entity.Components.Any(x => x.Id == live.Id))
            return false;
        var component = (EntityComponent)ObjectFactoryRegistry.NewInstance(live.GetType());
        component.Id = live.Id;
        var components = Session.AssetNodeContainer.GetNode(entity.Entity)![nameof(Entity.Components)].Target!;
        components.Add(component);
        // Then the live values, as for a component of the asset
        var context = new Context(this, change.Source, scene, entity, live.Id, trustIndices: true);
        if (context.FindRoot(out var root, out var liveRoot, out var name) is null)
        {
            var changes = new List<LiveApplyChange>();
            context.DiffMembers(root!, liveRoot!, [], name!, changes);
            foreach (var inner in changes.Where(x => x.CanApply))
                ApplyChange(inner);
        }
        return true;
    }

    private static bool CanCreate(Type type, out string? reason)
    {
        if (type.IsAbstract || type.IsInterface)
            reason = $"{DisplayName(type)} is abstract: the editor cannot make one.";
        else if (!type.IsValueType && type.GetConstructor(Type.EmptyTypes) is null && !ObjectFactoryRegistry.CanCreateInstance(type))
            reason = $"The editor cannot make a {DisplayName(type)}: it has no constructor without parameters.";
        else
            reason = null;
        return reason is null;
    }

    /// <summary>
    /// Where a value of the asset is: a member of an object, or an item of a collection, with the live value at the same place.
    /// </summary>
    private sealed class Slot
    {
        public required IGraphNode Owner { get; init; }

        public NodeIndex Index { get; init; }

        public required Type Type { get; init; }

        public object? Live { get; init; }

        public bool LiveHas { get; init; } = true;

        public bool AssetHas { get; init; } = true;

        // The live object that holds the member, for the members the game reads once
        public object? LiveOwner { get; init; }

        public object? Asset => !AssetHas ? null : Owner is IMemberNode member ? member.Retrieve() : ((IObjectNode)Owner).Retrieve(Index);

        public IObjectNode? Target => Owner switch
        {
            IMemberNode member => member.Target,
            IObjectNode { ItemReferences: not null } collection when AssetHas => collection.IndexedTarget(Index),
            _ => null,
        };

        public void Update(object? value)
        {
            if (Owner is IMemberNode member)
                member.Update(value);
            else
                ((IObjectNode)Owner).Update(value, Index);
        }
    }

    /// <summary>
    /// The comparison of a live entity with its counterpart, for one component (or the entity's own members).
    /// </summary>
    private sealed class Context(LiveAssetApplier owner, LiveApplySource source, SceneViewModel scene, EntityDesign entity, Guid componentId, bool trustIndices)
    {
        private readonly HashSet<object> visited = new(ReferenceEqualityComparer.Instance);

        public string? FindRoot(out IObjectNode? root, out object? liveRoot, out string? componentName)
        {
            root = null;
            liveRoot = null;
            componentName = null;
            var mirror = source.References.Mirror.Entity;
            if (componentId == Guid.Empty)
            {
                root = owner.Session.AssetNodeContainer.GetNode(entity.Entity);
                liveRoot = mirror;
                componentName = string.Empty;
                return root is null ? "The asset entity has no property graph." : null;
            }
            if (mirror.Components.FirstOrDefault(x => x.Id == componentId) is not { } live)
                return "The component is no longer in the game.";
            if (live is IUnloadable)
                return PlaceholderReason;
            componentName = DisplayName(live.GetType());
            if (entity.Entity.Components.FirstOrDefault(x => x.Id == componentId) is not { } asset)
                return source.Record?.IsComponentAdded(source.Handle, componentId) == true
                    ? "The component was added while the game runs: apply the whole component to add it to the asset."
                    : "The asset entity does not have this component.";
            if (asset.GetType() != live.GetType())
                return $"The component is a {componentName} in the game and a {DisplayName(asset.GetType())} in the asset.";
            root = owner.Session.AssetNodeContainer.GetNode(asset);
            liveRoot = live;
            return root is null ? "The asset's component has no property graph." : null;
        }

        /// <summary>
        /// Finds the asset's place of a path, and the live value there.
        /// </summary>
        public string? Walk(IObjectNode root, object liveRoot, IReadOnlyList<LiveApplyStep> path, out Slot? slot)
        {
            slot = null;
            var node = root;
            var live = liveRoot;
            for (var i = 0; i < path.Count; i++)
            {
                var step = path[i];
                if (slot is not null)
                {
                    var text = LiveApplyStep.Format(path.Take(i));
                    if (slot.Target is not { } target || slot.Live is not { } next)
                        return $"{text} differs in the game and in the asset: apply {text} itself.";
                    if (!IsCollection(target) && target.Retrieve()?.GetType() != next.GetType())
                        return $"{text} is a {DisplayName(next.GetType())} in the game and a {DescribeType(target.Retrieve())} in the asset: apply {text} itself.";
                    node = target;
                    live = next;
                }
                if (step.Member is { } name)
                {
                    if (node.TryGetChild(name) is not { } member)
                        return $"The asset has no {name} here.";
                    slot = new Slot { Owner = member, Type = member.Type, Live = SafeGet(member.MemberDescriptor, live), LiveOwner = live };
                    continue;
                }

                if (!IsCollection(node) || TypeDescriptorFactory.Default.Find(live.GetType()) is not CollectionBaseDescriptor liveDescriptor)
                    return $"{LiveApplyStep.Format(path.Take(i))} is not a list or a dictionary.";
                var collectionPath = LiveApplyStep.Format(path.Take(i));
                if (liveDescriptor is not DictionaryDescriptor && Uncertain(node, live, liveDescriptor, collectionPath) is { } uncertain)
                    return uncertain;
                var liveHas = liveDescriptor.EnumerateKeys(live).Cast<object?>().Any(x => Equals(x, step.Index.Value));
                var assetHas = node.Indices!.Contains(step.Index);
                if (!liveHas && !assetHas)
                    return $"{LiveApplyStep.Format(path.Take(i + 1))} is neither in the game nor in the asset.";
                slot = new Slot
                {
                    Owner = node,
                    Index = step.Index,
                    Type = ItemType(node),
                    Live = liveHas ? liveDescriptor.GetValue(live, step.Index.Value!) : null,
                    LiveHas = liveHas,
                    AssetHas = assetHas,
                };
            }
            return slot is null ? "The path is empty." : null;
        }

        /// <summary>
        /// Lists what differs at a place: the value, or what differs inside the object or the collection there.
        /// </summary>
        public void Diff(Slot slot, IReadOnlyList<LiveApplyStep> path, string componentName, List<LiveApplyChange> changes)
        {
            var asset = slot.Asset;
            var live = slot.Live;

            // An item of a dictionary that only one side has
            if (!slot.AssetHas)
            {
                changes.Add(Change(path, componentName, LiveApplyKind.ItemAdded, live, "(no item)", Describe(live), LiveValueReason(slot.Type, live)));
                return;
            }
            if (!slot.LiveHas)
            {
                changes.Add(Change(path, componentName, LiveApplyKind.ItemRemoved, null, Describe(asset), "(no item)", null));
                return;
            }

            // The members the game reads once, or that a rule makes read-only, are what the asset says
            if (slot.Owner is IMemberNode member && slot.LiveOwner is { } liveOwner
                && LiveMemberSupport.GetReadOnlyReason(liveOwner.GetType(), member.MemberDescriptor.Name, liveOwner) is { } readOnly)
            {
                if (!LeafEquals(asset, live))
                    changes.Add(Change(path, componentName, LiveApplyKind.Value, live, Describe(asset), Describe(live), readOnly));
                return;
            }

            if (IsEntityReference(slot.Type, asset, live))
            {
                var reason = MapReference(live, out var mapped);
                if (reason is null ? !ReferenceEquals(mapped, asset) : !SameReference(asset, live))
                    changes.Add(Change(path, componentName, LiveApplyKind.Value, live, Describe(asset), Describe(live), reason));
                return;
            }
            if (IsContent(slot.Type, asset, live))
            {
                if (!SameContent(asset, live))
                    changes.Add(Change(path, componentName, LiveApplyKind.Value, live, Describe(asset), Describe(live), MapContent(slot.Type, live, out _)));
                return;
            }

            var target = slot.Target;
            if (target is null)
            {
                // A value (a number, a vector, a string...) or an object the asset does not have
                if (live is not null && asset is null && !IsLeafType(slot.Type, live))
                    changes.Add(Change(path, componentName, LiveApplyKind.Object, live, Describe(asset), Describe(live), CanCreate(live.GetType(), out var why) ? null : why));
                else if (!LeafEquals(asset, live))
                    changes.Add(Change(path, componentName, LiveApplyKind.Value, live, Describe(asset), Describe(live), null));
                return;
            }
            if (live is null)
            {
                if (asset is not null)
                    changes.Add(Change(path, componentName, LiveApplyKind.Object, null, Describe(asset), Describe(null), null));
                return;
            }
            if (IsCollection(target))
            {
                DiffCollection(target, live, path, componentName, changes);
                return;
            }
            if (asset is null || asset.GetType() != live.GetType())
            {
                changes.Add(Change(path, componentName, LiveApplyKind.Object, live, Describe(asset), Describe(live), CanCreate(live.GetType(), out var why) ? null : why));
                return;
            }
            DiffMembers(target, live, path, componentName, changes);
        }

        public void DiffMembers(IObjectNode node, object live, IReadOnlyList<LiveApplyStep> path, string componentName, List<LiveApplyChange> changes)
        {
            // An object referenced twice is compared once
            if (!live.GetType().IsValueType && !visited.Add(live))
                return;
            foreach (var member in node.Members)
                DiffMember(node, live, member.Name, path, componentName, changes);
        }

        public void DiffMember(IObjectNode node, object live, string name, IReadOnlyList<LiveApplyStep> path, string componentName, List<LiveApplyChange> changes)
        {
            if (node.TryGetChild(name) is not { } member)
                return;
            // The transform's parent and children are the tree's, which this job does not apply
            if (live is TransformComponent && name is nameof(TransformComponent.Children))
                return;
            var slot = new Slot { Owner = member, Type = member.Type, Live = SafeGet(member.MemberDescriptor, live), LiveOwner = live };
            Diff(slot, [.. path, LiveApplyStep.Of(name)], componentName, changes);
        }

        private void DiffCollection(IObjectNode node, object live, IReadOnlyList<LiveApplyStep> path, string componentName, List<LiveApplyChange> changes)
        {
            if (TypeDescriptorFactory.Default.Find(live.GetType()) is not CollectionBaseDescriptor liveDescriptor)
                return;
            var liveKeys = liveDescriptor.EnumerateKeys(live).Cast<object?>().Select(x => new NodeIndex(x)).ToList();
            var assetKeys = node.Indices!.ToList();
            if (liveDescriptor is DictionaryDescriptor)
            {
                foreach (var key in liveKeys.Union(assetKeys))
                {
                    var liveHas = liveKeys.Contains(key);
                    var slot = new Slot
                    {
                        Owner = node,
                        Index = key,
                        Type = ItemType(node),
                        Live = liveHas ? liveDescriptor.GetValue(live, key.Value!) : null,
                        LiveHas = liveHas,
                        AssetHas = assetKeys.Contains(key),
                    };
                    Diff(slot, [.. path, LiveApplyStep.At(key)], componentName, changes);
                }
                return;
            }

            if (liveKeys.Count != assetKeys.Count || Uncertain(node, live, liveDescriptor, LiveApplyStep.Format(path)) is not null)
            {
                var reason = node.Descriptor is ArrayDescriptor ? "An array cannot change its size: the game's and the asset's have different sizes." : null;
                if (reason is null && liveKeys.Count == assetKeys.Count && ListEquals(node, live, liveDescriptor))
                    return;
                changes.Add(Change(path, componentName, LiveApplyKind.Collection, live, Describe(node.Retrieve()), Describe(live), reason));
                return;
            }
            foreach (var key in liveKeys)
            {
                var slot = new Slot { Owner = node, Index = key, Type = ItemType(node), Live = liveDescriptor.GetValue(live, key.Value!) };
                Diff(slot, [.. path, LiveApplyStep.At(key)], componentName, changes);
            }
        }

        /// <summary>
        /// Sets the asset's value at a place to the live value: a value, a new object with the live object's values, a whole
        /// list, a dictionary's item added or removed.
        /// </summary>
        public bool Apply(Slot slot, IReadOnlyList<LiveApplyStep> path, LiveApplyKind kind, object? live)
        {
            switch (kind)
            {
                case LiveApplyKind.ItemRemoved:
                    if (!slot.AssetHas)
                        return false;
                    ((IObjectNode)slot.Owner).Remove(slot.Asset, slot.Index);
                    return true;
                case LiveApplyKind.ItemAdded:
                    if (slot.AssetHas)
                        return SetValue(slot, path, live);
                    var made = ToAssetValue(slot.Type, live, out var refused);
                    if (refused is not null)
                        throw new InvalidOperationException(refused);
                    ((IObjectNode)slot.Owner).Add(made, slot.Index);
                    return ApplyInside(path, live);
                case LiveApplyKind.Collection:
                    return ApplyList(slot, path, live);
                default:
                    return SetValue(slot, path, live);
            }
        }

        private bool SetValue(Slot slot, IReadOnlyList<LiveApplyStep> path, object? live)
        {
            var value = ToAssetValue(slot.Type, live, out var refused);
            if (refused is not null)
                throw new InvalidOperationException(refused);
            slot.Update(value);
            return ApplyInside(path, live);
        }

        /// <summary>
        /// After a new object was set, gives it the live object's values: what differs inside it, applied one by one.
        /// </summary>
        private bool ApplyInside(IReadOnlyList<LiveApplyStep> path, object? live)
        {
            if (live is null || IsLeafType(live.GetType(), live))
                return true;
            if (FindRoot(out var root, out var liveRoot, out var componentName) is not null || Walk(root!, liveRoot!, path, out var slot) is not null || slot!.Target is null)
                return true;
            var inner = new List<LiveApplyChange>();
            visited.Clear();
            Diff(slot, path, componentName!, inner);
            foreach (var change in inner.Where(x => x.CanApply))
            {
                if (Walk(root!, liveRoot!, change.Path, out var innerSlot) is null)
                    Apply(innerSlot!, change.Path, change.Kind, change.LiveValue);
            }
            return true;
        }

        private bool ApplyList(Slot slot, IReadOnlyList<LiveApplyStep> path, object? live)
        {
            if (slot.Target is not { } node || live is null || TypeDescriptorFactory.Default.Find(live.GetType()) is not CollectionBaseDescriptor liveDescriptor)
                return SetValue(slot, path, live);
            var liveItems = liveDescriptor.EnumerateKeys(live).Select(x => liveDescriptor.GetValue(live, x)).ToList();
            var assetCount = node.Indices!.Count();
            // The items past the live list's end go, from the last
            for (var i = assetCount - 1; i >= liveItems.Count; --i)
                node.Remove(node.Retrieve(new NodeIndex(i)), new NodeIndex(i));
            for (var i = 0; i < liveItems.Count; ++i)
            {
                var itemPath = (IReadOnlyList<LiveApplyStep>)[.. path, LiveApplyStep.At(new NodeIndex(i))];
                if (i >= assetCount)
                {
                    var made = ToAssetValue(ItemType(node), liveItems[i], out var refused);
                    if (refused is not null)
                        throw new InvalidOperationException(refused);
                    node.Add(made);
                    ApplyInside(itemPath, liveItems[i]);
                    continue;
                }
                var itemSlot = new Slot { Owner = node, Index = new NodeIndex(i), Type = ItemType(node), Live = liveItems[i] };
                var inner = new List<LiveApplyChange>();
                Diff(itemSlot, itemPath, string.Empty, inner);
                foreach (var change in inner.Where(x => x.CanApply))
                {
                    if (FindRoot(out var root, out var liveRoot, out _) is null && Walk(root!, liveRoot!, change.Path, out var innerSlot) is null)
                        Apply(innerSlot!, change.Path, change.Kind, change.LiveValue);
                }
            }
            return true;
        }

        /// <summary>
        /// The asset's value for a live value: the value itself, the asset's reference for content and for entities, or a new
        /// object of the live object's type (its values are set after it).
        /// </summary>
        private object? ToAssetValue(Type type, object? live, out string? reason)
        {
            reason = null;
            if (live is null)
                return null;
            if (IsEntityReference(type, null, live))
            {
                reason = MapReference(live, out var mapped);
                return mapped;
            }
            if (IsContent(type, null, live))
            {
                reason = MapContent(type, live, out var reference);
                return reference;
            }
            if (IsLeafType(live.GetType(), live))
                return live;
            if (!CanCreate(live.GetType(), out reason))
                return null;
            return live.GetType().IsValueType ? Activator.CreateInstance(live.GetType()) : ObjectFactoryRegistry.NewInstance(live.GetType());
        }

        /// <summary>
        /// The asset entity or component that a live reference stands for: the counterpart, in the same scene asset, of the live
        /// entity it references.
        /// </summary>
        private string? MapReference(object? live, out object? mapped)
        {
            mapped = null;
            if (live is null)
                return null;
            var mirror = source.References.Mirror;
            int handle;
            Guid component;
            string? name;
            if (live == mirror.Entity)
            {
                (handle, component, name) = (mirror.Handle, Guid.Empty, mirror.Name);
            }
            else if (live is EntityComponent own && own.Entity == mirror.Entity)
            {
                (handle, component, name) = (mirror.Handle, own.Id, mirror.Name);
            }
            else if (source.References.Find(live) is { } reference)
            {
                (handle, component, name) = (reference.EntityHandle, reference.Kind == PlayMirrorReferenceKind.Component ? reference.ComponentId : Guid.Empty, reference.EntityName);
            }
            else
            {
                return "The editor does not know which entity of the game this reference holds.";
            }

            name ??= "The entity";
            EntityDesign? design;
            if (handle == mirror.Handle)
            {
                design = entity;
            }
            else
            {
                if (source.Tree?.FindEntity(handle) is not { } item)
                    return $"{name} is no longer in the game.";
                if (item.IsAddedWhilePlaying)
                    return $"{name} was added while the game runs: it has no counterpart in a scene asset.";
                if (item.CounterpartScene is not { } running)
                    return $"{name} was made by the game: it has no counterpart in a scene asset.";
                var thisItem = source.Item ?? source.Tree.FindEntity(mirror.Handle);
                if (running != thisItem?.CounterpartScene)
                    return $"{name} is in another scene ({running.Name}): a reference cannot go from one scene asset to another.";
                if (!scene.Asset.Hierarchy.Parts.TryGetValue(item.Id, out design))
                    return $"{name} was deleted from the scene asset since the game started.";
            }
            if (component == Guid.Empty)
            {
                mapped = design.Entity;
                return null;
            }
            mapped = design.Entity.Components.FirstOrDefault(x => x.Id == component);
            return mapped is null ? $"The component of {name} was added while the game runs: it is not in the asset." : null;
        }

        /// <summary>
        /// The session's reference to the asset of live content, by its URL.
        /// </summary>
        private string? MapContent(Type type, object? live, out object? reference)
        {
            reference = null;
            if (live is null)
                return null;
            var url = live is UrlReferenceBase urlReference ? urlReference.Url : AttachedReferenceManager.GetAttachedReference(live)?.Url;
            if (string.IsNullOrEmpty(url))
                return "The game made this content at run time: it has no asset.";
            if (owner.Session.GetAssetByUrl(url) is not { } asset)
                return $"{url} has no asset in the session any more.";
            reference = ContentReferenceHelper.CreateReference(asset, type);
            return reference is null ? $"{url} is not a {DisplayName(type)}." : null;
        }

        private bool SameReference(object? asset, object? live)
        {
            if (asset is null || live is null)
                return asset is null && live is null;
            var mirror = source.References.Mirror;
            var (liveEntity, liveComponent) = live switch
            {
                _ when live == mirror.Entity => (mirror.Id, Guid.Empty),
                EntityComponent own when own.Entity == mirror.Entity => (mirror.Id, own.Id),
                _ when source.References.Find(live) is { } reference => (reference.EntityId, reference.Kind == PlayMirrorReferenceKind.Component ? reference.ComponentId : Guid.Empty),
                _ => (Guid.NewGuid(), Guid.Empty),
            };
            return asset switch
            {
                Entity entity => entity.Id == liveEntity && liveComponent == Guid.Empty,
                EntityComponent component => component.Entity?.Id == liveEntity && component.Id == liveComponent,
                _ => false,
            };
        }

        private static bool SameContent(object? asset, object? live)
        {
            if (asset is null || live is null)
                return asset is null && live is null;
            var assetUrl = asset is UrlReferenceBase assetUrlReference ? assetUrlReference.Url : AttachedReferenceManager.GetAttachedReference(asset)?.Url;
            var liveUrl = live is UrlReferenceBase liveUrlReference ? liveUrlReference.Url : AttachedReferenceManager.GetAttachedReference(live)?.Url;
            return !string.IsNullOrEmpty(liveUrl) && string.Equals(assetUrl, liveUrl, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Why the positions of a list cannot be matched with the asset's: a different count, or items the user added, removed
        /// or moved while the game runs; <c>null</c> when they can, or when the whole list is applied.
        /// </summary>
        private string? Uncertain(IObjectNode node, object live, CollectionBaseDescriptor liveDescriptor, string collectionPath)
        {
            if (trustIndices)
                return null;
            var name = collectionPath.Length > 0 ? collectionPath : "The list";
            var liveCount = liveDescriptor.EnumerateKeys(live).Count();
            if (liveCount != node.Indices!.Count())
                return $"{name} has {liveCount} items in the game and {node.Indices!.Count()} in the asset: its items cannot be matched one by one. Apply {name} itself to copy the whole list.";
            if (source.Record?.HasCollectionChange(source.Handle, componentId, collectionPath) == true)
                return $"Items of {name} were added, removed or moved while the game runs: its items cannot be matched one by one. Apply {name} itself to copy the whole list.";
            return null;
        }

        private static bool ListEquals(IObjectNode node, object live, CollectionBaseDescriptor liveDescriptor)
        {
            var liveItems = liveDescriptor.EnumerateKeys(live).Select(x => liveDescriptor.GetValue(live, x)).ToList();
            var assetItems = node.Indices!.Select(node.Retrieve).ToList();
            return liveItems.Count == assetItems.Count && Enumerable.Zip(liveItems, assetItems).All(x => LeafEquals(x.Second, x.First) && (x.First is null || IsLeafType(x.First.GetType(), x.First)));
        }

        private string? LiveValueReason(Type type, object? live)
        {
            if (IsEntityReference(type, null, live))
                return MapReference(live, out _);
            if (IsContent(type, null, live))
                return MapContent(type, live, out _);
            return live is null || IsLeafType(live.GetType(), live) || CanCreate(live.GetType(), out var reason) ? null : reason;
        }

        private LiveApplyChange Change(IReadOnlyList<LiveApplyStep> path, string componentName, LiveApplyKind kind, object? live, string assetText, string liveText, string? reason)
        {
            var origin = source.Record?.IsEdited(source.Handle, componentId, LiveApplyStep.Format(path)) == true ? LiveApplyOrigin.EditedLive : LiveApplyOrigin.ChangedByGame;
            return new LiveApplyChange(source, componentId, componentName, path, kind, live, assetText, liveText, origin, reason);
        }

        private static bool IsCollection(IObjectNode node) => node.IsEnumerable && node.Descriptor is CollectionBaseDescriptor;

        private static Type ItemType(IObjectNode node) => node.Descriptor switch
        {
            DictionaryDescriptor dictionary => dictionary.ValueType,
            CollectionDescriptor collection => collection.ElementType,
            ArrayDescriptor array => array.ElementType,
            _ => typeof(object),
        };

        private static bool IsEntityReference(Type type, object? asset, object? live)
            => typeof(Entity).IsAssignableFrom(type) || typeof(EntityComponent).IsAssignableFrom(type)
               || type.IsInterface && type.IsImplementedOnAny<EntityComponent>()
               || asset is Entity or EntityComponent || live is Entity or EntityComponent;

        private static bool IsContent(Type type, object? asset, object? live)
            => AssetRegistry.CanBeAssignedToContentTypes(type, checkIsUrlType: true)
               || asset is UrlReferenceBase || live is UrlReferenceBase
               || AttachedReferenceManager.GetAttachedReference(asset) is not null || AttachedReferenceManager.GetAttachedReference(live) is not null;

        // A value the property graph holds as a whole: a primitive, an enum, a string, a struct of the math types
        private static bool IsLeafType(Type type, object? value)
            => type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || value is string
               || type.IsValueType && TypeDescriptorFactory.Default.Find(type) is PrimitiveDescriptor or NullableDescriptor
               || type.IsValueType && type.Namespace == "Stride.Core.Mathematics";

        private static bool LeafEquals(object? asset, object? live) => Equals(asset, live);

        private static string DescribeType(object? value) => value is null ? "(none)" : DisplayName(value.GetType());

        private static object? SafeGet(IMemberDescriptor member, object? instance)
        {
            if (instance is null)
                return null;
            try
            {
                return member.Get(instance);
            }
            catch (Exception)
            {
                // A getter that needs the game: nothing to compare
                return null;
            }
        }
    }
}
