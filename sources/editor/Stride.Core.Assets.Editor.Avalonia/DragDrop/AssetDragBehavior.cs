// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using Stride.Core.Assets.Presentation.ViewModels;

namespace Stride.Core.Assets.Editor.Avalonia.DragDrop;

/// <summary>
/// Behavior that initiates a drag operation when the user drags an asset item beyond a minimum distance.
/// Attach to the root element of an asset list item's <c>DataTemplate</c>.
/// The <c>DataContext</c> must be an <see cref="AssetViewModel"/>.
/// </summary>
public sealed class AssetDragBehavior : StyledElementBehavior<Control>
{
    private const double DragThreshold = 4.0;
    private Point? dragStartPoint;
    private PointerPressedEventArgs? pressedArgs;

    protected override void OnAttached()
    {
        AssociatedObject!.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, handledEventsToo: false);
        AssociatedObject!.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, handledEventsToo: false);
    }

    protected override void OnDetaching()
    {
        AssociatedObject!.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        AssociatedObject!.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(AssociatedObject).Properties.IsLeftButtonPressed)
        {
            dragStartPoint = e.GetPosition(AssociatedObject);
            pressedArgs = e;
        }
        else
        {
            dragStartPoint = null;
            pressedArgs = null;
        }
    }

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (dragStartPoint is not { } start || pressedArgs is null) return;
        if (AssociatedObject?.DataContext is not AssetViewModel asset) return;

        var current = e.GetPosition(AssociatedObject);
        if (Math.Abs(current.X - start.X) < DragThreshold &&
            Math.Abs(current.Y - start.Y) < DragThreshold)
            return;

        var savedArgs = pressedArgs;
        dragStartPoint = null;
        pressedArgs = null;

        var item = new DataTransferItem();
        item.Set(AssetDragDropHelper.AssetViewModelFormat, asset);
        var data = new DataTransfer();
        data.Add(item);

        await global::Avalonia.Input.DragDrop.DoDragDropAsync(savedArgs, data, DragDropEffects.Link);
    }
}
