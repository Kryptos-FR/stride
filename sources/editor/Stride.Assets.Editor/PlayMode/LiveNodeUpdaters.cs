// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Assets.Editor.Quantum.NodePresenters.Updaters;
using Stride.Assets.Editor.Quantum.NodePresenters.Keys;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Quantum.NodePresenters.Commands;
using Stride.Core.Assets.Editor.Quantum.NodePresenters.Keys;
using Stride.Core.Diagnostics;
using Stride.Core.Extensions;
using Stride.Core.Presentation.Quantum.Presenters;
using Stride.Core.Yaml;
using Stride.Engine;
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
/// component the editor could not read (a placeholder), is a reference to an entity, a component or content, is computed by
/// the editor (a virtual property other than the fields of a vector), or is a member the engine reads once
/// (<see cref="LiveMemberSupport"/>); the inside of such a property too. The components themselves cannot be added, removed,
/// replaced or reordered. With edits off, every property is read-only.
/// </para>
/// <para>
/// The commands kept on a property that can change: copy, select the asset, add and remove items, add a key, a new instance,
/// the flags of an enum, and the moves of items and renames of keys, which the game does on the item itself
/// (<see cref="LiveMoveItemCommand"/>, <see cref="LiveRenameStringKeyCommand"/>). Paste, replace, pick and the reference
/// commands go: they are for assets and references.
/// </para>
/// </remarks>
internal sealed class LiveEditNodeUpdater(Func<bool> editsEnabled, Func<LiveEditor?> editor) : NodePresenterUpdaterBase
{
    public const string PlaceholderReason = "The editor does not know this component's type: its values cannot change here.";
    public const string ReferenceReason = "A reference to an entity or a component: references cannot change while the game runs yet.";
    public const string ContentReason = "A reference to content (a model, a texture, a material...): it cannot change while the game runs yet.";
    public const string ComputedReason = "Computed by the editor: it is not a value of the game.";
    public const string ComponentsReason = "Components cannot be added, removed or reordered while the game runs yet.";

    private static readonly HashSet<string> ReadCommands = [CopyPropertyCommand.CommandName, FetchAssetCommand.CommandName];

    private static readonly HashSet<string> EditCommands =
    [
        CopyPropertyCommand.CommandName, FetchAssetCommand.CommandName, AddNewItemCommand.CommandName, RemoveItemCommand.CommandName,
        AddPrimitiveKeyCommand.CommandName, CreateNewInstanceCommand.CommandName, FlagEnumSelectAllCommand.CommandName,
        FlagEnumSelectNoneCommand.CommandName, FlagEnumSelectInvertCommand.CommandName,
    ];

    private readonly LiveMoveItemCommand move = new(editor);
    private readonly LiveRenameStringKeyCommand rename = new(editor);

    public override void UpdateNode(INodePresenter node)
    {
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
            if (!IsComponentItem(current) && (LiveNodePresenterFactory.IsOutsideOfMirror(current) || IsReferenceType(current.Type)))
                return ReferenceReason;
            if (AssetRegistry.CanBeAssignedToContentTypes(current.Type, checkIsUrlType: true))
                return ContentReason;
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

    private static bool IsReferenceType(Type type)
        => typeof(Entity).IsAssignableFrom(type) || typeof(EntityComponent).IsAssignableFrom(type);

    // A component of the entity: its members change, not the component itself
    private static bool IsComponentItem(INodePresenter node)
        => node is ItemNodePresenter && node.Parent is { Type: var type, Parent.Parent: null } && type == typeof(EntityComponentCollection);
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
