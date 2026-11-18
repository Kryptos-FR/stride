// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Assets.Editor.Quantum.NodePresenters.Commands;
using Stride.Core.Presentation.Quantum.Presenters;
using Stride.Core.Quantum;

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
