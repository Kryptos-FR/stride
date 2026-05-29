// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Assets.Editor.Services;
using Stride.Core.Presentation.Collections;
using Stride.Core.Presentation.ViewModels;

namespace Stride.Core.Assets.Editor.ViewModels;

/// <summary>
/// View model managing non-blocking notifications for background operations.
/// </summary>
public sealed class NotificationsViewModel : DispatcherViewModel, INotificationService
{
    private readonly ObservableList<WorkProgressViewModel> activeNotifications = [];

    public NotificationsViewModel(IViewModelServiceProvider serviceProvider)
        : base(serviceProvider)
    {
    }

    public IReadOnlyObservableList<WorkProgressViewModel> ActiveNotifications => activeNotifications;

    public void AddNotification(WorkProgressViewModel workProgress)
    {
        Dispatcher.Invoke(() =>
        {
            if (workProgress.WorkDone)
                return;

            activeNotifications.Add(workProgress);
            workProgress.WorkFinished += OnWorkFinished;
        });
    }

    private void OnWorkFinished(object? sender, WorkProgressNotificationEventArgs e)
    {
        // WorkFinished is already raised on the UI thread via RaiseEvent → Dispatcher.Invoke.
        e.WorkProgress.WorkFinished -= OnWorkFinished;
        activeNotifications.Remove(e.WorkProgress);
        e.WorkProgress.NotifyWindowClosed();
    }
}
