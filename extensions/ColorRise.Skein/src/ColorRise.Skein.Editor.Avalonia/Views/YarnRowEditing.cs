using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ColorRise.Skein.Editor.Graph;
using ColorRise.Skein.Editor.ViewModels;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// The editing of the rows of a card in place: a double click on a row opens its box (for a <c>&lt;&lt;set&gt;&gt;</c>,
/// the set builder; for another command or a block, it shows it in the text), a click on the row that adds a line opens its box, and the box writes its text with Enter or
/// when it loses the focus, and closes without writing with Escape.
/// </summary>
/// <remarks>
/// The presses are taken before the graph sees them, so that the second press of a double click on a row does not go
/// to the card under it.
/// </remarks>
public static class YarnRowEditing
{
    /// <summary>
    /// How a press on a control opens its row: on a double click, or on one click.
    /// </summary>
    public static readonly AttachedProperty<YarnRowOpening> OpensProperty =
        AvaloniaProperty.RegisterAttached<Control, YarnRowOpening>("Opens", typeof(YarnRowEditing));

    /// <summary>
    /// Whether a text box is the box of a row.
    /// </summary>
    public static readonly AttachedProperty<bool> IsEditBoxProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("IsEditBox", typeof(YarnRowEditing));

    static YarnRowEditing()
    {
        OpensProperty.Changed.AddClassHandler<Control>((control, e) =>
        {
            control.RemoveHandler(InputElement.PointerPressedEvent, OnRowPressed);
            if (e.NewValue is not YarnRowOpening.None)
            {
                control.AddHandler(InputElement.PointerPressedEvent, OnRowPressed, RoutingStrategies.Tunnel);
            }
        });

        IsEditBoxProperty.Changed.AddClassHandler<TextBox>((box, e) =>
        {
            box.KeyDown -= OnBoxKeyDown;
            box.LostFocus -= OnBoxLostFocus;
            box.PropertyChanged -= OnBoxPropertyChanged;
            if (e.NewValue is true)
            {
                box.KeyDown += OnBoxKeyDown;
                box.LostFocus += OnBoxLostFocus;
                box.PropertyChanged += OnBoxPropertyChanged;
            }
        });
    }

    /// <summary>
    /// Gets how a press on a control opens its row.
    /// </summary>
    public static YarnRowOpening GetOpens(Control control) => control.GetValue(OpensProperty);

    /// <summary>
    /// Sets how a press on a control opens its row.
    /// </summary>
    public static void SetOpens(Control control, YarnRowOpening value) => control.SetValue(OpensProperty, value);

    /// <summary>
    /// Gets whether a text box is the box of a row.
    /// </summary>
    public static bool GetIsEditBox(TextBox box) => box.GetValue(IsEditBoxProperty);

    /// <summary>
    /// Sets whether a text box is the box of a row.
    /// </summary>
    public static void SetIsEditBox(TextBox box, bool value) => box.SetValue(IsEditBoxProperty, value);

    private static YarnScriptEditorViewModel? EditorOf(Visual visual) => visual.FindAncestorOfType<YarnScriptEditorView>()?.DataContext as YarnScriptEditorViewModel;

    private static void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: YarnCardRowViewModel row } control || row.IsEditing || !e.GetCurrentPoint(control).Properties.IsLeftButtonPressed
            || EditorOf(control) is not { } editor)
            return;

        var opening = GetOpens(control);
        if (opening == YarnRowOpening.Click || (opening == YarnRowOpening.DoubleClick && e.ClickCount == 2))
        {
            if (row.IsEditable)
            {
                editor.BeginEdit(row);
            }
            else if (!row.IsAssignment || YarnBuilderPopups.OpenAssignment(control, editor, row) is null)
            {
                editor.ShowInText(row);
            }

            e.Handled = true;
        }
    }

    // The box takes the focus when it opens, with its text selected, but for the speaker the row that adds a line starts with
    private static void OnBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Visual.IsVisibleProperty || sender is not TextBox { IsVisible: true } box)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            box.Focus();
            if (box.DataContext is YarnCardRowViewModel { IsAdd: true })
            {
                box.CaretIndex = box.Text?.Length ?? 0;
            }
            else
            {
                box.SelectAll();
            }
        }, DispatcherPriority.Input);
    }

    private static void OnBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: YarnCardRowViewModel row } box || EditorOf(box) is not { } editor)
            return;

        switch (e.Key)
        {
            case Key.Enter:
                editor.CommitEdit(row);
                e.Handled = true;
                break;
            case Key.Escape:
                editor.CancelEdit(row);
                e.Handled = true;
                break;
        }
    }

    private static void OnBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: YarnCardRowViewModel { IsEditing: true } row } box && EditorOf(box) is { } editor)
        {
            editor.CommitEdit(row);
        }
    }
}

/// <summary>
/// How a press on a row opens it.
/// </summary>
public enum YarnRowOpening
{
    /// <summary>
    /// A press does not open the row.
    /// </summary>
    None,

    /// <summary>
    /// A double click opens the row.
    /// </summary>
    DoubleClick,

    /// <summary>
    /// One click opens the row.
    /// </summary>
    Click,
}
