// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Stride.GameStudio.Avalonia.ViewModels;

namespace Stride.GameStudio.Avalonia.Views;

public partial class AssetPreviewView : UserControl
{
    private PreviewViewModel? previewViewModel;
    private ContentPresenter? previewContent;
    private TextBlock? placeholderText;

    public AssetPreviewView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        previewContent = this.FindControl<ContentPresenter>("PreviewContent");
        placeholderText = this.FindControl<TextBlock>("PlaceholderText");

        if (DataContext is MainViewModel mainVm)
        {
            mainVm.PropertyChanged += OnMainViewModelPropertyChanged;
            TryAttachToSession(mainVm);
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mainVm)
        {
            mainVm.PropertyChanged -= OnMainViewModelPropertyChanged;
        }

        DetachPreviewViewModel();
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Session) && sender is MainViewModel mainVm)
        {
            DetachPreviewViewModel();
            TryAttachToSession(mainVm);
        }
    }

    private void TryAttachToSession(MainViewModel mainVm)
    {
        if (mainVm.Session == null)
            return;

        previewViewModel = new PreviewViewModel(mainVm.Session);
        previewViewModel.PropertyChanged += OnPreviewViewModelPropertyChanged;
        previewViewModel.OnShowPreview();
    }

    private void DetachPreviewViewModel()
    {
        if (previewViewModel == null)
            return;

        previewViewModel.PropertyChanged -= OnPreviewViewModelPropertyChanged;
        previewViewModel.OnHidePreview();
        previewViewModel.Dispose();
        previewViewModel = null;

        UpdateContent(null);
    }

    private void OnPreviewViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PreviewViewModel.PreviewObject))
        {
            UpdateContent(previewViewModel?.PreviewObject);
        }
    }

    private void UpdateContent(object? content)
    {
        if (previewContent != null)
            previewContent.Content = content;

        if (placeholderText != null)
            placeholderText.IsVisible = content == null;
    }
}
