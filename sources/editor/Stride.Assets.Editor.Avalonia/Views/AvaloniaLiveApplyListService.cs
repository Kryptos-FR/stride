// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Threading;
using Stride.Assets.Editor.Avalonia.Templates;
using Stride.Assets.Editor.PlayMode;

namespace Stride.Assets.Editor.Avalonia.Views;

/// <summary>
/// Shows the list of live values to apply to the assets in its window.
/// </summary>
internal sealed class AvaloniaLiveApplyListService : ILiveApplyListService
{
    public async Task<bool> ShowAsync(LiveApplyListViewModel list)
    {
        ArgumentNullException.ThrowIfNull(list);
        var window = await Dispatcher.UIThread.InvokeAsync(() => new LiveApplyListWindow { DataContext = list });
        await TemplateDialogs.ShowModalAsync(window);
        return window.Applied;
    }
}
