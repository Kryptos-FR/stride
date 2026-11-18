// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Assets.Editor.Quantum.NodePresenters.Updaters;
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
/// The last updater of the live grid: every node is read-only, and only the commands that read a value stay (copy, and select
/// the referenced asset in the asset view), so that the buttons that would change the entity (add, remove, replace, pick,
/// clear...) are not shown.
/// </summary>
internal sealed class LiveReadOnlyNodeUpdater : NodePresenterUpdaterBase
{
    private static readonly HashSet<string> KeptCommands = [CopyPropertyCommand.CommandName, FetchAssetCommand.CommandName];

    public override void UpdateNode(INodePresenter node)
    {
        node.IsReadOnly = true;
        node.Commands.RemoveAll(x => !KeptCommands.Contains(x.Name));
    }
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
