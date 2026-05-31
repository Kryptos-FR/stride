// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Specialized;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Presentation.ViewModels;

namespace Stride.GameStudio.Avalonia.ViewModels;

/// <summary>
/// View model that drives the asset preview panel: it observes the session's asset selection,
/// asks the <see cref="IAssetPreviewService"/> to build a preview for the selected asset, and
/// exposes the resulting preview view through <see cref="PreviewObject"/>.
/// </summary>
internal sealed class PreviewViewModel : DispatcherViewModel, IDisposable
{
    private readonly SessionViewModel session;
    private IAssetPreviewService? previewService;
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewViewModel"/> class.
    /// </summary>
    /// <param name="session">The session whose asset selection drives the preview.</param>
    public PreviewViewModel(SessionViewModel session)
        : base(session.ServiceProvider)
    {
        this.session = session;
        session.AssetCollection.SelectedAssets.CollectionChanged += SelectedAssetsCollectionChanged;
    }

    /// <summary>
    /// Gets the view produced by the preview service for the current selection, or <c>null</c> when
    /// there is no preview to display.
    /// </summary>
    public object? PreviewObject { get; private set { SetValue(ref field, value); } }

    /// <summary>
    /// Gets or sets a value indicating whether the preview panel is currently visible. The view binds
    /// this to the preview tab's selected state so the preview game only renders while it is on screen.
    /// </summary>
    public bool IsPreviewVisible
    {
        get;
        set
        {
            if (SetValue(ref field, value))
            {
                if (value)
                    PreviewService?.OnShowPreview();
                else
                    PreviewService?.OnHidePreview();
            }
        }
    } = true;

    private IAssetPreviewService? PreviewService
    {
        get
        {
            if (previewService != null)
                return previewService;

            previewService = ServiceProvider.TryGet<IAssetPreviewService>();
            if (previewService == null)
                return null;

            previewService.PreviewAssetUpdated += PreviewAssetUpdated;
            return previewService;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;

        if (previewService != null)
        {
            previewService.PreviewAssetUpdated -= PreviewAssetUpdated;
            previewService.Dispose();
        }

        session.AssetCollection.SelectedAssets.CollectionChanged -= SelectedAssetsCollectionChanged;
    }

    private void PreviewAssetUpdated(object? sender, EventArgs e)
    {
        // PreviewAssetUpdated is raised on the preview game's script thread. Marshal back to the UI
        // thread without blocking the caller, otherwise the game thread and UI thread can deadlock.
        Dispatcher.InvokeAsync(() => PreviewObject = previewService?.GetCurrentPreviewView());
    }

    private void SelectedAssetsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (isDisposed || PreviewService == null)
            return;

        if (session.AssetCollection.SelectedAssets.Count == 1)
        {
            previewService!.SetAssetToPreview(session.AssetCollection.SelectedAssets[0]);
        }
        else
        {
            previewService!.SetAssetToPreview(null);
        }
    }
}
