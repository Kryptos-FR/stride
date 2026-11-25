using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ColorRise.Tessera.Editor.TileSetEditor;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Assets.Editor.Editors;
using Stride.Core.IO;

namespace ColorRise.Tessera.Editor.Avalonia.Views;

/// <summary>
/// The view of the tile set editor: the sources on the left, the selected one on a canvas, the tool bar, and in Animation
/// mode the bar of frames under the canvas.
/// </summary>
[AssetEditorView<TileSetEditorViewModel>]
public sealed partial class TileSetEditorView : UserControl, IAssetEditorView
{
    // The editor whose changes of source the view follows
    private TileSetEditorViewModel? editor;

    /// <summary>
    /// Makes the view of a tile set editor.
    /// </summary>
    public TileSetEditorView()
    {
        InitializeComponent();
        FileDropBehavior.SetCommand(CanvasArea, new ProposeFirstFile(this));
        // A button of the bar of frames gives the keys back to the canvas
        AnimationBar.AddHandler(Button.ClickEvent, (_, _) => Canvas.Focus(), RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        editor?.PropertyChanged -= EditorPropertyChanged;
        editor = DataContext as TileSetEditorViewModel;
        editor?.PropertyChanged += EditorPropertyChanged;
    }

    /// <inheritdoc/>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Canvas.Focus();
    }

    // Another source shows whole
    private void EditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TileSetEditorViewModel.SelectedSource))
        {
            Canvas.Fit();
        }
    }

    // The first of the files dropped on the canvas is proposed as an image source
    private sealed class ProposeFirstFile(TileSetEditorView view) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => view.editor is not null && parameter is IReadOnlyList<UFile> { Count: > 0 };

        public void Execute(object? parameter)
        {
            if (view.editor is { } editor && parameter is IReadOnlyList<UFile> { Count: > 0 } files)
            {
                editor.ProposeImage(files[0]);
            }
        }
    }
}
