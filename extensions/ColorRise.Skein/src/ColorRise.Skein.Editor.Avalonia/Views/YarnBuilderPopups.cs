using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ColorRise.Skein.Editor.Conditions;
using ColorRise.Skein.Editor.Graph;
using ColorRise.Skein.Editor.ViewModels;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// Opens the condition and set builders in a popover: over the condition of a row of a card (a click on it, or on the
/// "+ if" of a row without one), over a <c>&lt;&lt;set&gt;&gt;</c> row, or beside a <c>when:</c> header of the property
/// grid. OK, Enter or a click outside writes what the builder made; Cancel or Escape does not.
/// </summary>
public static class YarnBuilderPopups
{
    /// <summary>
    /// Whether a click on a control of a row opens the builder of the row's condition.
    /// </summary>
    public static readonly AttachedProperty<bool> OpensConditionProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("OpensCondition", typeof(YarnBuilderPopups));

    /// <summary>
    /// The builder open over a control, while it is open.
    /// </summary>
    public static readonly AttachedProperty<Control?> OpenBuilderProperty =
        AvaloniaProperty.RegisterAttached<Control, Control?>("OpenBuilder", typeof(YarnBuilderPopups));

    static YarnBuilderPopups()
    {
        OpensConditionProperty.Changed.AddClassHandler<Control>((control, e) =>
        {
            control.RemoveHandler(InputElement.PointerPressedEvent, OnConditionPressed);
            if (e.NewValue is true)
            {
                control.AddHandler(InputElement.PointerPressedEvent, OnConditionPressed, RoutingStrategies.Tunnel);
            }
        });
    }

    /// <summary>
    /// Gets whether a click on a control opens the builder of its row's condition.
    /// </summary>
    public static bool GetOpensCondition(Control control) => control.GetValue(OpensConditionProperty);

    /// <summary>
    /// Sets whether a click on a control opens the builder of its row's condition.
    /// </summary>
    public static void SetOpensCondition(Control control, bool value) => control.SetValue(OpensConditionProperty, value);

    /// <summary>
    /// Gets the view of the builder open over a control, or null.
    /// </summary>
    public static Control? GetOpenBuilder(Control control) => control.GetValue(OpenBuilderProperty);

    /// <summary>
    /// Opens the builder of the condition of a line, an option or a line group item of a card.
    /// </summary>
    /// <returns>The builder, or null for a row without a condition to edit.</returns>
    public static YarnConditionBuilderViewModel? OpenCondition(Control anchor, YarnScriptEditorViewModel editor, YarnCardRowViewModel row)
    {
        if (editor.EditCondition(row) is not { } builder)
            return null;

        var view = new YarnConditionBuilderView { DataContext = builder };
        Show(anchor, view, done => view.Done += done, () => editor.CommitCondition(row, builder));
        return builder;
    }

    /// <summary>
    /// Opens the builder of a <c>&lt;&lt;set&gt;&gt;</c> row of a card.
    /// </summary>
    /// <returns>The builder, or null for a row that is not a set the builder reads.</returns>
    public static YarnAssignmentBuilderViewModel? OpenAssignment(Control anchor, YarnScriptEditorViewModel editor, YarnCardRowViewModel row)
    {
        if (editor.EditAssignment(row) is not { } builder)
            return null;

        var view = new YarnAssignmentBuilderView { DataContext = builder };
        Show(anchor, view, done => view.Done += done, () => editor.CommitAssignment(row, builder));
        return builder;
    }

    /// <summary>
    /// Opens the builder of a <c>when:</c> header.
    /// </summary>
    /// <param name="anchor">What the popover opens under.</param>
    /// <param name="knowledge">What the project offers.</param>
    /// <param name="header">The value of the header.</param>
    /// <param name="write">Writes the new value of the header; empty for none.</param>
    public static YarnConditionBuilderViewModel OpenHeader(Control anchor, YarnConditionKnowledge knowledge, string? header, Action<string> write)
    {
        var builder = YarnConditionBuilderViewModel.ForHeader(knowledge, header);
        var view = new YarnConditionBuilderView { DataContext = builder };
        Show(anchor, view, done => view.Done += done, () =>
        {
            if (builder.IsChanged)
            {
                write(builder.HeaderText ?? "");
            }
        });
        return builder;
    }

    private static void Show(Control anchor, Control view, Action<Action<bool>> subscribe, Action commit)
    {
        // One builder at a time over a control: the one open stays
        if (GetOpenBuilder(anchor) is not null)
            return;

        var flyout = new Flyout { Content = view, Placement = PlacementMode.BottomEdgeAlignedLeft };
        var finished = false;
        void Finish(bool write)
        {
            if (finished)
                return;

            finished = true;
            anchor.SetValue(OpenBuilderProperty, null);
            flyout.Hide();
            if (write)
            {
                commit();
            }
        }

        subscribe(Finish);

        // A click outside writes, as a box of a card does when it loses the focus
        flyout.Closed += (_, _) => Finish(true);
        anchor.SetValue(OpenBuilderProperty, view);
        flyout.ShowAt(anchor);

        // The keys go to the builder: Enter writes, Escape does not
        view.Focusable = true;
        Dispatcher.UIThread.Post(() => view.Focus(), DispatcherPriority.Input);
    }

    private static void OnConditionPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: YarnCardRowViewModel row } control || !e.GetCurrentPoint(control).Properties.IsLeftButtonPressed
            || control.FindAncestorOfType<YarnScriptEditorView>()?.DataContext is not YarnScriptEditorViewModel editor)
            return;

        OpenCondition(control, editor, row);
        e.Handled = true;
    }
}
