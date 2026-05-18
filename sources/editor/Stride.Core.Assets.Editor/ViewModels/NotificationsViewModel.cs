// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.ComponentModel;
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
            activeNotifications.Add(workProgress);
            workProgress.PropertyChanged += OnNotificationPropertyChanged;
            if (workProgress.WorkDone)
            {
                ScheduleDismiss(workProgress);
            }
        });
    }

    private void OnNotificationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkProgressViewModel.WorkDone) && sender is WorkProgressViewModel vm && vm.WorkDone)
        {
            ScheduleDismiss(vm);
        }
    }

    private async void ScheduleDismiss(WorkProgressViewModel workProgress)
    {
        await Task.Delay(5000);
        await Dispatcher.InvokeAsync(() =>
        {
            workProgress.PropertyChanged -= OnNotificationPropertyChanged;
            activeNotifications.Remove(workProgress);
        });
    }
}
