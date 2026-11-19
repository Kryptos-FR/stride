// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Assets.Editor.Quantum.NodePresenters.Commands;
using Stride.Core.Presentation.Quantum.Presenters;
using Stride.Core.Quantum;
using Stride.Engine;

namespace Stride.Assets.Editor.PlayMode;

/// <summary>
/// Moves an item of a live list: the game moves the item itself, where the asset grid's command removes it and adds a copy,
/// which in a game would make another object.
/// </summary>
internal sealed class LiveMoveItemCommand(Func<LiveEditor?> editor) : MoveItemCommand
{
    protected override void ExecuteSync(INodePresenter nodePresenter, object? parameter, object? preExecuteResult)
    {
        var item = (ItemNodePresenter)nodePresenter;
        var (from, to) = (Tuple<int, int>)parameter!;
        if (item.OwnerCollection is ILiveNodePresenter collection)
            editor()?.Move(collection, new NodeIndex(from), new NodeIndex(to));
    }
}

/// <summary>
/// Gives an item of a live dictionary another key: the game keeps the item, where the asset grid's command removes it and adds
/// a copy.
/// </summary>
internal sealed class LiveRenameStringKeyCommand(Func<LiveEditor?> editor) : RenameStringKeyCommand
{
    protected override void ExecuteSync(INodePresenter nodePresenter, object? parameter, object? preExecuteResult)
    {
        var item = (ItemNodePresenter)nodePresenter;
        if (item.OwnerCollection is not ILiveNodePresenter collection || parameter is null)
            return;
        if (AddPrimitiveKeyCommand.GenerateGenericKey(collection.Value, collection.Descriptor, parameter) is { } key)
            editor()?.Move(collection, item.Index, key);
    }
}

/// <summary>
/// The Add component list of a live entity: the game makes a component of the picked type and adds it to the entity, where the
/// asset grid's command adds it to the asset.
/// </summary>
internal sealed class LiveAddComponentCommand(Func<LiveEditor?> editor) : SyncNodePresenterCommandBase
{
    public override string Name => AddNewItemCommand.CommandName;

    public override bool CanAttach(INodePresenter nodePresenter) => nodePresenter.Type == typeof(EntityComponentCollection);

    protected override void ExecuteSync(INodePresenter nodePresenter, object? parameter, object? preExecuteResult)
    {
        var type = parameter switch
        {
            AbstractNodeType nodeType => nodeType.Type,
            Type value => value,
            _ => null,
        };
        if (type is not null)
            editor()?.AddComponent(nodePresenter, type);
    }
}

/// <summary>
/// The remove button of a component of a live entity: the game removes the component from the entity. The transform has none.
/// </summary>
internal sealed class LiveRemoveComponentCommand(Func<LiveEditor?> editor) : SyncNodePresenterCommandBase
{
    public override string Name => RemoveItemCommand.CommandName;

    public override bool CanAttach(INodePresenter nodePresenter) => nodePresenter.Value is EntityComponent and not TransformComponent;

    protected override void ExecuteSync(INodePresenter nodePresenter, object? parameter, object? preExecuteResult)
        => editor()?.RemoveComponent(nodePresenter);
}
