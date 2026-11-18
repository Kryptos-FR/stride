// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Assets.Editor.Quantum.NodePresenters.Keys;
using Stride.Assets.Editor.ViewModels;
using Stride.Assets.Entities;
using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Assets.Editor.Quantum.NodePresenters.Commands;
using Stride.Core.Assets.Editor.Quantum.NodePresenters.Keys;
using Stride.Core.Assets.Presentation.Quantum.NodePresenters;
using Stride.Core.Extensions;
using Stride.Core.Reflection;
using Stride.Engine;

namespace Stride.Assets.Editor.Quantum.NodePresenters.Updaters;

internal sealed class EntityHierarchyAssetNodeUpdater : AssetNodePresenterUpdaterBase
{
    protected override void UpdateNode(IAssetNodePresenter node)
    {
        if (node.Asset?.Asset is not EntityHierarchyAssetBase)
            return;

        EntityNodeDisplay.UpdateComponent(node);
        if (node.Type == typeof(EntityComponentCollection))
        {
            var types = typeof(EntityComponent).GetInheritedInstantiableTypes()
                .Where(x => Attribute.GetCustomAttribute(x, typeof(NonInstantiableAttribute)) == null &&
                            (EntityComponentAttributes.Get(x).AllowMultipleComponents
                             || ((EntityComponentCollection)node.Value).All(y => y.GetType() != x)))
                .OrderBy(DisplayAttribute.GetDisplayName)
                .Select(x => new AbstractNodeType(x)).ToArray();
            node.AttachedProperties.Add(EntityHierarchyData.EntityComponentAvailableTypesKey, types);

            //TODO: Choose a better grouping method.
            var typeGroups =
                types.GroupBy(t => ComponentCategoryAttribute.GetCategory(t.Type))
                    .OrderBy(g => g.Key)
                    .Select(g => new AbstractNodeTypeGroup(g.Key, g.ToArray())).ToArray();

            node.AttachedProperties.Add(EntityHierarchyData.EntityComponentAvailableTypeGroupsKey, typeGroups);

            EntityNodeDisplay.UpdateComponentCollection(node);
        }
        if (typeof(EntityComponent).IsAssignableFrom(node.Type)
            || node.Type.IsInterface && node.Type.IsImplementedOnAny<EntityComponent>())
        {
            node.AttachedProperties.Add(ReferenceData.Key, new ComponentReferenceViewModel());
        }
        if (typeof(Entity) == node.Type)
        {
            node.AttachedProperties.Add(ReferenceData.Key, new EntityReferenceViewModel());
        }
    }
}
