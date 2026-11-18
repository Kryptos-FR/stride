// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Threading;
using Stride.Assets.Editor.Avalonia.Templates;
using Stride.Assets.Editor.PlayMode;

namespace Stride.Assets.Editor.Avalonia.Views;

/// <summary>
/// Shows the picker of a live reference in its window, closed without a pick when the game stops.
/// </summary>
internal sealed class AvaloniaLiveEntityPickerService : ILiveEntityPickerService
{
    public async Task<LiveEntityPick?> PickAsync(LiveEntityPickerViewModel picker, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(picker);
        if (cancellation.IsCancellationRequested)
            return null;
        var window = await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var made = new LiveEntityPickerWindow { DataContext = picker };
            // Wider with the list of the components
            if (picker.ComponentType is not null)
                made.Width *= 1.4;
            return made;
        });
        await using var registration = cancellation.Register(() => Dispatcher.UIThread.Post(() => window.Close(false)));
        await TemplateDialogs.ShowModalAsync(window);
        return window.Picked && !cancellation.IsCancellationRequested ? picker.Pick : null;
    }
}
