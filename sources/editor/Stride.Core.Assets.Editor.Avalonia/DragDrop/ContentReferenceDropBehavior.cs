// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using Stride.Core.Presentation.Quantum.ViewModels;

namespace Stride.Core.Assets.Editor.Avalonia.DragDrop;

/// <summary>
/// Drop behavior for content-reference property nodes.
/// Accepts an <see cref="Stride.Core.Assets.Presentation.ViewModels.AssetViewModel"/> drag and
/// sets the node value to the dropped asset reference.
/// </summary>
public sealed class ContentReferenceDropBehavior : StyledElementBehavior<Control>
{
    public static readonly StyledProperty<Type?> AcceptedTypeProperty =
        AvaloniaProperty.Register<ContentReferenceDropBehavior, Type?>(nameof(AcceptedType));

    public Type? AcceptedType
    {
        get => GetValue(AcceptedTypeProperty);
        set => SetValue(AcceptedTypeProperty, value);
    }

    protected override void OnAttached()
    {
        global::Avalonia.Input.DragDrop.SetAllowDrop(AssociatedObject!, true);
        AssociatedObject!.AddHandler(global::Avalonia.Input.DragDrop.DragOverEvent, OnDragOver);
        AssociatedObject!.AddHandler(global::Avalonia.Input.DragDrop.DropEvent, OnDrop);
    }

    protected override void OnDetaching()
    {
        AssociatedObject!.RemoveHandler(global::Avalonia.Input.DragDrop.DragOverEvent, OnDragOver);
        AssociatedObject!.RemoveHandler(global::Avalonia.Input.DragDrop.DropEvent, OnDrop);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (AcceptedType is null ||
            !AssetDragDropHelper.TryGetAssetViewModel(e.DataTransfer, out var asset) ||
            !AssetDragDropHelper.IsCompatible(asset!, AcceptedType))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }
        e.DragEffects = DragDropEffects.Link;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (AcceptedType is null ||
            !AssetDragDropHelper.TryGetAssetViewModel(e.DataTransfer, out var asset) ||
            !AssetDragDropHelper.IsCompatible(asset!, AcceptedType))
            return;

        if (AssociatedObject?.DataContext is not NodeViewModel node)
            return;

        // Create a content reference object from the dropped asset.
        // The AttachedReferenceManager creates the proper IReference<T> that the node expects.
        var reference = Stride.Core.Serialization.AttachedReferenceManager.CreateProxyObject(AcceptedType, asset!.Id, asset.Url);
        node.NodeValue = reference;
        e.Handled = true;
    }
}
