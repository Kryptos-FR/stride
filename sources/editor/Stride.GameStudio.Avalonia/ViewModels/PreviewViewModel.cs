// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Specialized;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Presentation.ViewModels;

namespace Stride.GameStudio.Avalonia.ViewModels;

internal sealed class PreviewViewModel : DispatcherViewModel, IDisposable
{
    private readonly SessionViewModel session;
    private IAssetPreviewService? previewService;
    private bool isDisposed;

    public PreviewViewModel(SessionViewModel session)
        : base(session.ServiceProvider)
    {
        this.session = session;
        session.AssetCollection.SelectedAssets.CollectionChanged += SelectedAssetsCollectionChanged;
    }

    public object? PreviewObject { get; private set { SetValue(ref field, value); } }

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

    public void OnShowPreview()
    {
        PreviewService?.OnShowPreview();
    }

    public void OnHidePreview()
    {
        PreviewService?.OnHidePreview();
    }

    private void PreviewAssetUpdated(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() => PreviewObject = previewService?.GetCurrentPreviewView());
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
