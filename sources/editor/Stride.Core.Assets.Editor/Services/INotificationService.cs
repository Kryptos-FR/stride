// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Presentation.Collections;

namespace Stride.Core.Assets.Editor.Services;

/// <summary>
/// Service for managing non-blocking notifications for background operations.
/// </summary>
public interface INotificationService
{
    IReadOnlyObservableList<WorkProgressViewModel> ActiveNotifications { get; }

    void AddNotification(WorkProgressViewModel workProgress);
}
