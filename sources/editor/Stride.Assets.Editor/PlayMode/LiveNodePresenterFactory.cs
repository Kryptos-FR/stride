// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Assets.Presentation.Quantum.NodePresenters;
using Stride.Core.Assets.Quantum;
using Stride.Core.Diagnostics;
using Stride.Core.Presentation.Quantum;
using Stride.Core.Presentation.Quantum.Presenters;
using Stride.Core.Quantum;
using Stride.Engine;

namespace Stride.Assets.Editor.PlayMode;

/// <summary>
/// The node presenters of a live entity's mirror. They refuse every write from the view (phase 1 of play mode is read-only:
/// nothing the user does reaches the game), and an object outside of the mirror (another entity, one of its components) has no
/// children, as the asset grid does for an object reference.
/// </summary>
internal sealed class LiveNodePresenterFactory(INodeBuilder nodeBuilder, IReadOnlyCollection<INodePresenterCommand> availableCommands, IReadOnlyCollection<INodePresenterUpdater> availableUpdaters, Logger? log)
    : AssetNodePresenterFactory(nodeBuilder, availableCommands, availableUpdaters)
{
    /// <summary>
    /// The number of writes refused since the factory was made (each is a bug of a template or a command that should not be
    /// offered on a live entity).
    /// </summary>
    public int RefusedWrites { get; private set; }

    /// <summary>
    /// Whether the value of <paramref name="presenter"/> is an object outside of the mirror: an entity other than the mirror, or
    /// a component of one.
    /// </summary>
    public static bool IsOutsideOfMirror(INodePresenter presenter)
    {
        var mirror = presenter.Root.Value as Entity;
        return presenter.Value switch
        {
            Entity entity => presenter.Parent is not null && entity != mirror,
            EntityComponent component => component.Entity != mirror,
            _ => false,
        };
    }

    protected override bool ShouldCreateMemberPresenter(INodePresenter parent, IMemberNode member, IPropertyProviderViewModel? propertyProvider)
    {
        return !IsOutsideOfMirror(parent) && base.ShouldCreateMemberPresenter(parent, member, propertyProvider);
    }

    protected override bool ShouldCreateItemPresenter(INodePresenter parent, IObjectNode collectionNode, NodeIndex index, IPropertyProviderViewModel? propertyProvider)
    {
        return !IsOutsideOfMirror(parent) && base.ShouldCreateItemPresenter(parent, collectionNode, index, propertyProvider);
    }

    protected override IInitializingNodePresenter CreateRootPresenter(IPropertyProviderViewModel? propertyProvider, IObjectNode rootNode)
    {
        return new LiveRootNodePresenter(this, propertyProvider, (IAssetObjectNode)rootNode);
    }

    protected override IInitializingNodePresenter CreateMember(IPropertyProviderViewModel? propertyProvider, INodePresenter parentPresenter, IMemberNode member)
    {
        return new LiveMemberNodePresenter(this, propertyProvider, parentPresenter, (IAssetMemberNode)member);
    }

    protected override IInitializingNodePresenter CreateItem(IPropertyProviderViewModel? propertyProvider, INodePresenter containerPresenter, IObjectNode containerNode, NodeIndex index)
    {
        return new LiveItemNodePresenter(this, propertyProvider, containerPresenter, (IAssetObjectNode)containerNode, index);
    }

    private void Refuse(INodePresenter presenter, string what)
    {
        ++RefusedWrites;
        var path = presenter.Parent is { } parent ? $"{parent.Name}.{presenter.Name}" : presenter.Name;
        log?.Warning($"The live entity is read-only: {what} of {path} was refused.");
    }

    private sealed class LiveRootNodePresenter(LiveNodePresenterFactory factory, IPropertyProviderViewModel? propertyProvider, IAssetObjectNode rootNode)
        : AssetRootNodePresenter(factory, propertyProvider, rootNode)
    {
        public override void UpdateValue(object? newValue) => factory.Refuse(this, $"a write ({newValue ?? "null"})");

        public override void AddItem(object value) => factory.Refuse(this, "an item added");

        public override void AddItem(object value, NodeIndex index) => factory.Refuse(this, "an item added");

        public override void RemoveItem(object value, NodeIndex index) => factory.Refuse(this, "an item removed");
    }

    private sealed class LiveMemberNodePresenter(LiveNodePresenterFactory factory, IPropertyProviderViewModel? propertyProvider, INodePresenter parent, IAssetMemberNode member)
        : AssetMemberNodePresenter(factory, propertyProvider, parent, member)
    {
        public override void UpdateValue(object? newValue) => factory.Refuse(this, $"a write ({newValue ?? "null"})");

        public override void AddItem(object value) => factory.Refuse(this, "an item added");

        public override void AddItem(object value, NodeIndex index) => factory.Refuse(this, "an item added");

        public override void RemoveItem(object value, NodeIndex index) => factory.Refuse(this, "an item removed");
    }

    private sealed class LiveItemNodePresenter(LiveNodePresenterFactory factory, IPropertyProviderViewModel? propertyProvider, INodePresenter parent, IAssetObjectNode container, NodeIndex index)
        : AssetItemNodePresenter(factory, propertyProvider, parent, container, index)
    {
        public override void UpdateValue(object? newValue) => factory.Refuse(this, $"a write ({newValue ?? "null"})");

        public override void AddItem(object value) => factory.Refuse(this, "an item added");

        public override void AddItem(object value, NodeIndex index) => factory.Refuse(this, "an item added");

        public override void RemoveItem(object value, NodeIndex index) => factory.Refuse(this, "an item removed");
    }
}
