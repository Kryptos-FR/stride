using Avalonia.Controls;
using Avalonia.Input;
using ColorRise.Saga.Editor.ViewModels;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Assets.Editor.Editors;

namespace ColorRise.Saga.Editor.Avalonia.Views;

/// <summary>
/// The view of the dialogue editor: the graph of its lines, choices, branches and ends, a slot per way out. The property grid
/// shows the selected nodes, or the selected choice or branch link.
/// </summary>
[AssetEditorView<DialogueEditorViewModel>]
public sealed partial class DialogueEditorView : UserControl, IAssetEditorView
{
    private SagaGraphBinding<DialogueViewModel>? binding;

    public DialogueEditorView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        binding?.Dispose();
        binding = DataContext is DialogueEditorViewModel editor ? new SagaGraphBinding<DialogueViewModel>(Graph, editor) : null;
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // The editor takes the focus so that Delete works
        if (!IsKeyboardFocusWithin)
            Focus();
    }
}
