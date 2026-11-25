using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using ColorRise.Tessera.Editor.TileMapEditor;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Assets.Editor.Editors;

namespace ColorRise.Tessera.Editor.Avalonia.Views;

/// <summary>
/// The view of the map editor: the tool bar, the map on a canvas, the layers, the tile sets with the palette of the selected
/// one, and the status line.
/// </summary>
[AssetEditorView<TileMapEditorViewModel>]
public sealed partial class TileMapEditorView : UserControl, IAssetEditorView
{
    /// <summary>
    /// Makes the view of a map editor.
    /// </summary>
    public TileMapEditorView()
    {
        InitializeComponent();
        // After a click on the tool bar, the canvas has the focus again: its single keys and Space work, and Space does not
        // press the button
        ToolBar.AddHandler(Button.ClickEvent, (_, _) => Canvas.Focus(), RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private TileMapEditorViewModel? Editor => DataContext as TileMapEditorViewModel;

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (Editor is not { } editor)
        {
            return;
        }
        var color = editor.GridColor;
        Canvas.GridColor = Color.FromArgb(color.A, color.R, color.G, color.B);
        // Once the canvas has the editor's scene and view: the map at the last zoom, or all of it the first time
        Dispatcher.UIThread.Post(() =>
        {
            if (Editor != editor)
            {
                return;
            }
            if (editor.OpensAtLastZoom)
            {
                Canvas.Centre();
            }
            else
            {
                Canvas.Fit();
            }
        });
    }

    /// <inheritdoc/>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Canvas.Focus();
    }

    private void OnFlipX(object? sender, RoutedEventArgs e) => Editor?.FlipStampX();

    private void OnFlipY(object? sender, RoutedEventArgs e) => Editor?.FlipStampY();

    private void OnRotate(object? sender, RoutedEventArgs e) => Editor?.RotateStamp(clockwise: true);

    // A double click on a layer's name types it in place
    private void OnLayerNameDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: TileLayerItemViewModel layer })
        {
            layer.IsEditing = true;
            e.Handled = true;
        }
    }
}
