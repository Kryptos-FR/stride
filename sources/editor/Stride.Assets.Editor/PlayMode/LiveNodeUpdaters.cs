// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Assets.Editor.Quantum.NodePresenters.Updaters;
using Stride.Assets.Editor.Quantum.NodePresenters.Keys;
using Stride.Assets.Models;
using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Quantum.NodePresenters.Commands;
using Stride.Core.Assets.Editor.Quantum.NodePresenters.Keys;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Collections;
using Stride.Core.Diagnostics;
using Stride.Core.Extensions;
using Stride.Core.Presentation.Quantum.Presenters;
using Stride.Core.Quantum;
using Stride.Core.Reflection;
using Stride.Core.Serialization;
using Stride.Core.Yaml;
using Stride.Engine;
using Stride.Rendering;
using Stride.Rendering.Compositing;

namespace Stride.Assets.Editor.PlayMode;

/// <summary>
/// How the grid shows a live entity, which has no asset: the components' headers, the transform first without its children,
/// a placeholder's header named after the type it stands for, the camera slot by name.
/// </summary>
/// <param name="findCameraSlot">The name of a camera slot of the session's graphics compositors, or <c>null</c>.</param>
internal sealed class LiveEntityNodeUpdater(Func<SceneCameraSlotId, string?> findCameraSlot) : NodePresenterUpdaterBase
{
    public override void UpdateNode(INodePresenter node)
    {
        EntityNodeDisplay.UpdateComponent(node);
        EntityNodeDisplay.UpdateComponentCollection(node);

        if (node.Value is IUnloadable placeholder and EntityComponent && node.Parent?.Type == typeof(EntityComponentCollection))
        {
            // The placeholder of a component the editor could not read: its header names the game's type
            var typeName = placeholder.TypeName ?? string.Empty;
            node.DisplayName = typeName[(typeName.LastIndexOf('.') + 1)..];
        }
        else if (node.Value is SceneCameraSlotId slot)
        {
            // The game's compositor has the same slots as the session's: show the slot by name, read-only
            node.Children.ForEach(x => x.IsVisible = false);
            node.AttachedProperties.Set(DisplayData.AttributeDisplayNameKey, slot.IsEmpty ? "(None)" : findCameraSlot(slot) ?? slot.Id.ToString());
        }
    }
}

/// <summary>
/// The last updater of the live grid: says which properties can change while the game runs, and keeps the commands that work
/// on a live entity.
/// </summary>
/// <remarks>
/// <para>
/// A property stays read-only, with the reason in its tooltip (<see cref="LiveEditData.ReadOnlyReasonKey"/>), when it is in a
/// component the editor could not read (a placeholder), is inside a reference to another entity or component, is inside
/// content (which everything that uses it shares), is computed by the editor (a virtual property other than the fields of a
/// vector), or is a member the engine reads once (<see cref="LiveMemberSupport"/>); the inside of such a property too. The
/// components themselves cannot be added, removed, replaced or reordered. With edits off, every property is read-only.
/// </para>
/// <para>
/// The commands kept on a property that can change: copy, select the asset, add and remove items, add a key, a new instance
/// (and the clearing of a reference), the flags of an enum, and the moves of items and renames of keys, which the game does on
/// the item itself (<see cref="LiveMoveItemCommand"/>, <see cref="LiveRenameStringKeyCommand"/>). The pickers of references
/// are the live ones (<see cref="LivePickupAssetCommand"/>, <see cref="LivePickupEntityCommand"/>), and an asset dropped on a
/// reference to content goes through <see cref="LiveContentReferenceViewModel"/>. Paste and replace go: they are for assets.
/// </para>
/// </remarks>
internal sealed class LiveEditNodeUpdater(Func<bool> editsEnabled, Func<LiveEditor?> editor, LiveReferenceContext? references = null) : NodePresenterUpdaterBase
{
    public const string PlaceholderReason = "The editor does not know this component's type: its values cannot change here.";
    public const string InsideReferenceReason = "Inside a reference to an entity or a component: select that entity in the Live tab to change it.";
    public const string SharedContentReason = "Inside content (a model, a texture, a material...), which everything that uses it shares: it cannot change while the game runs. Set the reference to another asset instead.";
    public const string ComputedReason = "Computed by the editor: it is not a value of the game.";
    public const string ComponentsReason = "Components cannot be reordered while the game runs: the engine would remove each and add it back, which restarts scripts and physics bodies.";

    private static readonly HashSet<string> ReadCommands = [CopyPropertyCommand.CommandName, FetchAssetCommand.CommandName];

    private static readonly HashSet<string> EditCommands =
    [
        CopyPropertyCommand.CommandName, FetchAssetCommand.CommandName, AddNewItemCommand.CommandName, RemoveItemCommand.CommandName,
        AddPrimitiveKeyCommand.CommandName, CreateNewInstanceCommand.CommandName, FlagEnumSelectAllCommand.CommandName,
        FlagEnumSelectNoneCommand.CommandName, FlagEnumSelectInvertCommand.CommandName,
    ];

    private readonly LiveMoveItemCommand move = new(editor);
    private readonly LiveAddComponentCommand addComponent = new(editor);
    private readonly LiveRemoveComponentCommand removeComponent = new(editor);
    private readonly LiveRenameStringKeyCommand rename = new(editor);
    private readonly LivePickupAssetCommand? pickAsset = references is null ? null : new(references);
    private readonly LivePickupEntityCommand? pickEntity = references is null ? null : new(references, pickComponent: false);
    private readonly LivePickupEntityCommand? pickComponent = references is null ? null : new(references, pickComponent: true);

    /// <summary>
    /// The pickers of live references opened (assets, entities and components).
    /// </summary>
    public int OpenedPickers => (pickAsset?.Opened ?? 0) + (pickEntity?.Opened ?? 0) + (pickComponent?.Opened ?? 0);

    public override void UpdateNode(INodePresenter node)
    {
        // The components of the entity: added from the list of the types the game can make, removed by their button
        if (editsEnabled() && node.Parent is { Parent: null } && node.Type == typeof(EntityComponentCollection))
        {
            UpdateComponents(node);
            return;
        }
        if (editsEnabled() && IsComponentItem(node) && GetReadOnlyReason(node) is null)
        {
            node.IsReadOnly = true;
            node.Commands.RemoveAll(x => !ReadCommands.Contains(x.Name));
            if (removeComponent.CanAttach(node))
                node.Commands.Add(removeComponent);
            return;
        }
        if (!editsEnabled() || node.Parent is null || IsComponentItem(node) || GetReadOnlyReason(node) is not null)
        {
            node.IsReadOnly = true;
            node.Commands.RemoveAll(x => !ReadCommands.Contains(x.Name));
            if (editsEnabled() && GetReadOnlyReason(node) is { } reason)
            {
                node.AttachedProperties.Set(LiveEditData.ReadOnlyReasonKey, reason);
                node.AttachedProperties.Set(LiveEditData.TipKey, reason);
            }
            return;
        }

        var canMove = node.Commands.Any(x => x.Name == MoveItemCommand.CommandName);
        var canRename = node.Commands.Any(x => x.Name == RenameStringKeyCommand.CommandName);
        node.Commands.RemoveAll(x => !EditCommands.Contains(x.Name));
        if (canMove)
            node.Commands.Add(move);
        if (canRename)
            node.Commands.Add(rename);
        if (references is null || node.Descriptor is CollectionBaseDescriptor)
            return;
        // The references: set by the live pickers, and content by an asset dropped too
        if (pickAsset!.CanAttach(node))
        {
            node.Commands.Add(pickAsset);
            node.AttachedProperties.Set(ReferenceData.Key, new LiveContentReferenceViewModel(references));
        }
        else if (pickComponent!.CanAttach(node))
        {
            node.Commands.Add(pickComponent);
            node.AttachedProperties.Remove(ReferenceData.Key);
        }
        else if (pickEntity!.CanAttach(node))
        {
            node.Commands.Add(pickEntity);
            node.AttachedProperties.Remove(ReferenceData.Key);
        }
    }

    /// <summary>
    /// The types of component the entity can take, for the Add component list, as the asset grid lists them
    /// (<see cref="EntityHierarchyAssetNodeUpdater"/>): the game makes the component by its type name. Reordering stays off.
    /// </summary>
    private void UpdateComponents(INodePresenter node)
    {
        node.IsReadOnly = true;
        node.Commands.RemoveAll(x => !ReadCommands.Contains(x.Name));
        node.Commands.Add(addComponent);
        node.AttachedProperties.Set(LiveEditData.TipKey, ComponentsReason);
        var components = (EntityComponentCollection)node.Value;
        var types = typeof(EntityComponent).GetInheritedInstantiableTypes()
            .Where(x => Attribute.GetCustomAttribute(x, typeof(NonInstantiableAttribute)) == null
                        && !typeof(TransformComponent).IsAssignableFrom(x)
                        && (EntityComponentAttributes.Get(x).AllowMultipleComponents || components.All(y => y.GetType() != x)))
            .OrderBy(DisplayAttribute.GetDisplayName)
            .Select(x => new AbstractNodeType(x)).ToArray();
        node.AttachedProperties.Set(EntityHierarchyData.EntityComponentAvailableTypesKey, types);
        var groups = types.GroupBy(x => ComponentCategoryAttribute.GetCategory(x.Type))
            .OrderBy(x => x.Key)
            .Select(x => new AbstractNodeTypeGroup(x.Key, x.ToArray())).ToArray();
        node.AttachedProperties.Set(EntityHierarchyData.EntityComponentAvailableTypeGroupsKey, groups);
    }

    /// <summary>
    /// Why a property of the live grid cannot change while the game runs, or <c>null</c> when it can.
    /// </summary>
    public static string? GetReadOnlyReason(INodePresenter node)
    {
        if (node.Type == typeof(EntityComponentCollection))
            return ComponentsReason;
        for (var current = node; current.Parent is not null; current = current.Parent)
        {
            if (current.Value is IUnloadable)
                return PlaceholderReason;
            // A reference itself can change; what it holds cannot from here
            if (current != node && !IsComponentItem(current))
            {
                if (LiveNodePresenterFactory.IsOutsideOfMirror(current) || IsEntityReferenceType(current.Type))
                    return InsideReferenceReason;
                if (AssetRegistry.CanBeAssignedToContentTypes(current.Type, checkIsUrlType: true))
                    return SharedContentReason;
            }
            switch (current)
            {
                case MemberNodePresenter member when member.MemberDescriptor.MemberInfo is { DeclaringType: { } type } info:
                    if (LiveMemberSupport.GetReadOnlyReason(type, info.Name, current.Parent.Value) is { } reason)
                        return reason;
                    break;
                case ILiveNodePresenter:
                    break;
                // A field of a vector writes the vector (MathematicsNodeUpdater); any other virtual property writes where the
                // editor chose, not into the game
                case { Parent: ILiveNodePresenter { Type.Namespace: "Stride.Core.Mathematics" } }:
                    break;
                default:
                    return ComputedReason;
            }
        }
        return null;
    }

    private static bool IsEntityReferenceType(Type type)
        => typeof(Entity).IsAssignableFrom(type) || typeof(EntityComponent).IsAssignableFrom(type);

    // A component of the entity: its members change, not the component itself
    private static bool IsComponentItem(INodePresenter node)
        => node is ItemNodePresenter && node.Parent is { Type: var type, Parent.Parent: null } && type == typeof(EntityComponentCollection);
}

/// <summary>
/// One row per material slot of the model of a live <see cref="ModelComponent"/>, named after the model's material, as the
/// asset grid shows them (<see cref="ModelComponentNodeUpdater"/>, which works on assets only). A slot the scene does not
/// override shows the model's material; picking a material sets the slot's override in the game, or adds it, and Clear removes
/// it, so the model's own material shows again.
/// </summary>
/// <param name="findAsset">Finds an asset of the session by its URL.</param>
internal sealed class LiveModelMaterialsNodeUpdater(Func<string, AssetViewModel?> findAsset) : NodePresenterUpdaterBase
{
    public override void UpdateNode(INodePresenter node)
    {
        if (node is not ILiveNodePresenter)
            return;
        // The slots follow the model
        if (node.Value is ModelComponent && node.Parent?.Value is EntityComponentCollection)
        {
            if (Child(node, nameof(ModelComponent.Materials)) is { } materialsNode && Child(node, nameof(ModelComponent.Model)) is { } modelNode)
                materialsNode.AddDependency(modelNode, false);
            return;
        }
        if (node.Value is not IndexingDictionary<Material> materials || node.Parent?.Value is not ModelComponent component
            || node.Factory is not LiveNodePresenterFactory factory || node.GetNodeAccessor().Node is not IMemberNode { Target: { } collectionNode })
            return;
        if (ModelAsset(component) is not { } model)
            return;

        foreach (var child in node.Children.ToList())
            child.IsVisible = false;
        var slots = model.Materials.Count;
        for (var i = 0; i < slots; i++)
        {
            var slot = i;
            var row = factory.CreateItemSlot((ILiveNodePresenter)node, collectionNode, $"MaterialSlot{slot}", typeof(Material), slot, new NodeIndex(slot),
                () => materials.SafeGet(slot) ?? ModelMaterial(component, slot),
                () => materials.ContainsKey(slot));
            row.DisplayName = model.Materials[slot]?.Name ?? $"(Material {slot + 1})";
        }
    }

    // The model's own material of a slot, which shows when the scene does not override it
    private Material? ModelMaterial(ModelComponent component, int slot)
        => ModelAsset(component) is { } model && slot < model.Materials.Count
            ? model.Materials[slot]?.MaterialInstance?.Material
            : null;

    // The game knows its content by URL
    private IModelAsset? ModelAsset(ModelComponent component)
        => component.Model is { } reference && AttachedReferenceManager.GetUrl(reference) is { } url
            ? findAsset(url)?.Asset as IModelAsset
            : null;

    private static INodePresenter? Child(INodePresenter node, string name) => node.Children.FirstOrDefault(x => x.Name == name);
}

/// <summary>
/// Runs an updater of the session's grid on a live entity, and logs what it throws instead of failing the build of the grid:
/// an updater written for assets can fail on an object that has none.
/// </summary>
internal sealed class GuardedNodeUpdater(INodePresenterUpdater inner, Logger? log) : INodePresenterUpdater
{
    private bool reported;

    public INodePresenterUpdater Inner => inner;

    public void UpdateNode(INodePresenter node)
    {
        try
        {
            inner.UpdateNode(node);
        }
        catch (Exception e)
        {
            Report(node, e);
        }
    }

    public void FinalizeTree(INodePresenter root)
    {
        try
        {
            inner.FinalizeTree(root);
        }
        catch (Exception e)
        {
            Report(root, e);
        }
    }

    private void Report(INodePresenter node, Exception e)
    {
        // Once per updater and grid: an updater that fails on a node fails on every snapshot
        if (reported)
            return;
        reported = true;
        log?.Warning($"{inner.GetType().Name} failed on the live entity's {node.Type.Name} {node.Name}; the grid shows the entity without it.", e);
    }
}
