using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using ColorRise.Saga.Editor.ViewModels;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Assets.Editor.Editors;

namespace ColorRise.Saga.Editor.Avalonia.Views;

/// <summary>
/// The view of the story editor: the breadcrumb of the open level above the graph of its cards. The property grid shows the
/// selected cards or links.
/// </summary>
[AssetEditorView<StoryEditorViewModel>]
public sealed partial class StoryEditorView : UserControl, IAssetEditorView
{
    private SagaGraphBinding<StoryViewModel>? binding;
    private StoryEditorViewModel? editor;

    public StoryEditorView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        binding?.Dispose();
        binding = null;
        if (editor is not null)
            editor.PropertyChanged -= OnEditorPropertyChanged;
        editor = DataContext as StoryEditorViewModel;
        if (editor is not null)
        {
            binding = new SagaGraphBinding<StoryViewModel>(Graph, editor, editor.OpenCommand.Execute);
            editor.PropertyChanged += OnEditorPropertyChanged;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // The editor takes the focus so that Delete works
        if (!IsKeyboardFocusWithin)
            Focus();
    }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Another level shows other cards
        if (e.PropertyName == nameof(StoryEditorViewModel.CurrentLevel))
            binding?.FitToView();
    }
}
