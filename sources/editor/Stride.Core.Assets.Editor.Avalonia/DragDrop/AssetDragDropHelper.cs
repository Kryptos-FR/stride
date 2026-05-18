// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Input;
using Stride.Core.Assets.Presentation.ViewModels;

namespace Stride.Core.Assets.Editor.Avalonia.DragDrop;

/// <summary>
/// Helpers for dragging <see cref="AssetViewModel"/> instances between controls.
/// </summary>
public static class AssetDragDropHelper
{
    public static readonly DataFormat<AssetViewModel> AssetViewModelFormat =
        DataFormat.CreateInProcessFormat<AssetViewModel>("Stride.AssetViewModel");

    public static bool TryGetAssetViewModel(IDataTransfer data, out AssetViewModel? asset)
    {
        asset = data.TryGetValue(AssetViewModelFormat);
        return asset is not null;
    }

    public static bool IsCompatible(AssetViewModel asset, Type acceptedType)
    {
        return acceptedType.IsAssignableFrom(asset.AssetType);
    }
}
