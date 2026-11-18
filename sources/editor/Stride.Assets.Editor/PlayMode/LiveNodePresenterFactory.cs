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
/// The node presenters of a live entity's mirror. A write from the view goes to the game as an edit (<see cref="Editor"/>)
/// instead of into the mirror; without an editor (edits off, the game stopped) it is refused. An object outside of the mirror
/// (another entity, one of its components) has no children, as the asset grid does for an object reference.
/// </summary>
internal sealed class LiveNodePresenterFactory(INodeBuilder nodeBuilder, IReadOnlyCollection<INodePresenterCommand> availableCommands, IReadOnlyCollection<INodePresenterUpdater> availableUpdaters, Logger? log)
    : AssetNodePresenterFactory(nodeBuilder, availableCommands, availableUpdaters)
{
    /// <summary>
    /// The number of writes refused since the factory was made: the game was not connected, or a template or a command wrote
    /// to a property that should not be offered.
    /// </summary>
    public int RefusedWrites { get; private set; }

    /// <summary>
    /// Sends the writes to the game; <c>null</c> refuses them.
    /// </summary>
    public LiveEditor? Editor { get; set; }

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

    private void Write(ILiveNodePresenter presenter, object? value)
    {
        if (Editor?.Set(presenter, value) != true)
            Refuse(presenter, $"a write ({value ?? "null"})");
    }

    private void Add(ILiveNodePresenter presenter, object value)
    {
        if (Editor?.Add(presenter, NewItem(value)) != true)
            Refuse(presenter, "an item added");
    }

    private void Insert(ILiveNodePresenter presenter, object value, NodeIndex index)
    {
        if (Editor?.Insert(presenter, NewItem(value), index) != true)
            Refuse(presenter, "an item added");
    }

    // A new item of a collection of entities or components is empty, and is then picked from the Live tab: an entity or a
    // component made in the editor is in no running scene of the game, which refuses it
    private static object? NewItem(object value) => value is Entity or EntityComponent ? null : value;

    private void Remove(ILiveNodePresenter presenter, NodeIndex index)
    {
        if (Editor?.Remove(presenter, index) != true)
            Refuse(presenter, "an item removed");
    }

    private void Refuse(INodePresenter presenter, string what)
    {
        ++RefusedWrites;
        var path = presenter.Parent is { } parent ? $"{parent.Name}.{presenter.Name}" : presenter.Name;
        log?.Warning($"The live entity does not take edits now: {what} of {path} was refused.");
    }

    private sealed class LiveRootNodePresenter(LiveNodePresenterFactory factory, IPropertyProviderViewModel? propertyProvider, IAssetObjectNode rootNode)
        : AssetRootNodePresenter(factory, propertyProvider, rootNode), ILiveNodePresenter
    {
        public bool IsDisposed { get; private set; }

        public override void UpdateValue(object? newValue) => factory.Refuse(this, "a write of the entity");

        public override void AddItem(object value) => factory.Refuse(this, "an item added");

        public override void AddItem(object value, NodeIndex index) => factory.Refuse(this, "an item added");

        public override void RemoveItem(object value, NodeIndex index) => factory.Refuse(this, "an item removed");

        public void WriteLocal(object? value) => base.UpdateValue(value);

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    // The grid's copy runs the game's own setters: when one refuses a value, the copy keeps the value it had (written again,
    // which ends the change the view saw begin) and the game's answer says why
    private static void WriteCopy(INodePresenter presenter, object? value, Action<object?> update)
    {
        var current = presenter.Value;
        try
        {
            update(value);
        }
        catch (NodePresenterException)
        {
            update(current);
        }
    }

    private sealed class LiveMemberNodePresenter(LiveNodePresenterFactory factory, IPropertyProviderViewModel? propertyProvider, INodePresenter parent, IAssetMemberNode member)
        : AssetMemberNodePresenter(factory, propertyProvider, parent, member), ILiveNodePresenter
    {
        public bool IsDisposed { get; private set; }

        public override void UpdateValue(object? newValue) => factory.Write(this, newValue);

        public override void AddItem(object value) => factory.Add(this, value);

        public override void AddItem(object value, NodeIndex index) => factory.Insert(this, value, index);

        public override void RemoveItem(object value, NodeIndex index) => factory.Remove(this, index);

        public void WriteLocal(object? value) => WriteCopy(this, value, x => base.UpdateValue(x));

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class LiveItemNodePresenter(LiveNodePresenterFactory factory, IPropertyProviderViewModel? propertyProvider, INodePresenter parent, IAssetObjectNode container, NodeIndex index)
        : AssetItemNodePresenter(factory, propertyProvider, parent, container, index), ILiveNodePresenter
    {
        public bool IsDisposed { get; private set; }

        public override void UpdateValue(object? newValue) => factory.Write(this, newValue);

        public override void AddItem(object value) => factory.Add(this, value);

        public override void AddItem(object value, NodeIndex index) => factory.Insert(this, value, index);

        public override void RemoveItem(object value, NodeIndex index) => factory.Remove(this, index);

        public void WriteLocal(object? value) => WriteCopy(this, value, x => base.UpdateValue(x));

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
