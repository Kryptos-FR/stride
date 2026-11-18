// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Assets.Editor.Quantum.NodePresenters.Commands;
using Stride.Core.Extensions;
using Stride.Core.Presentation.Quantum.Presenters;
using Stride.Core.Reflection;
using Stride.Engine;

namespace Stride.Assets.Editor.Quantum.NodePresenters.Updaters;

/// <summary>
/// How the property grid shows the components of an entity, in a scene or prefab asset and in a live entity of a running game.
/// </summary>
internal static class EntityNodeDisplay
{
    /// <summary>
    /// When <paramref name="node"/> is a component of an entity: its header shows the component's display name, it combines with
    /// the same component of the other selected entities, and the transform comes first, cannot be removed and hides its
    /// children (the scene graph shows them).
    /// </summary>
    public static void UpdateComponent(INodePresenter node)
    {
        if (node.Value is not EntityComponent || node.Parent?.Type != typeof(EntityComponentCollection))
            return;

        // Apply the display name of the component
        var displayAttribute = TypeDescriptorFactory.Default.AttributeRegistry.GetAttribute<DisplayAttribute>(node.Value.GetType());
        if (!string.IsNullOrEmpty(displayAttribute?.Name))
            node.DisplayName = displayAttribute.Name;

        node.CombineKey = node.Value.GetType().Name;
        if (node.Value is TransformComponent)
        {
            // Always put the transformation component in first.
            node.Order = -1;
            node.Commands.RemoveAll(x => x.Name == RemoveItemCommand.CommandName);

            // Remove the Children property of the transformation component (it should be accessible via the scene graph)
            node[nameof(TransformComponent.Children)].IsVisible = false;
        }
    }

    /// <summary>
    /// When <paramref name="node"/> is the component collection of an entity: it cannot be replaced, and each component combines
    /// with the component of the same type and rank of the other selected entities.
    /// </summary>
    public static void UpdateComponentCollection(INodePresenter node)
    {
        if (node.Type != typeof(EntityComponentCollection))
            return;

        // Cannot replace entity component collection.
        var replaceCommandIndex = node.Commands.IndexOf(x => x.Name == ReplacePropertyCommand.CommandName);
        if (replaceCommandIndex >= 0)
            node.Commands.RemoveAt(replaceCommandIndex);

        // Combine components by type, but also append a index in case multiple components of the same types exist in the same collection.
        var componentCount = new Dictionary<Type, int>();
        foreach (var componentNode in node.Children)
        {
            if (componentNode.Value is not { } component)
                continue;
            var type = component.GetType();
            componentCount.TryGetValue(type, out var count);
            componentNode.CombineKey = $"{type.Name}@{count}";
            componentCount[type] = ++count;
        }
    }
}
