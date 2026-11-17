using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using ColorRise.Skein.Editor.Conditions;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// The condition builder, shown in a popover over a card or the property grid.
/// </summary>
public sealed partial class YarnConditionBuilderView : UserControl
{
    /// <summary>
    /// Makes the view; its data context is a <see cref="YarnConditionBuilderViewModel"/>.
    /// </summary>
    public YarnConditionBuilderView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble, handledEventsToo: false);
    }

    /// <summary>
    /// Raised when the user is done: true to write what the builder made, false to leave the text as it is.
    /// </summary>
    public event Action<bool>? Done;

    private YarnConditionBuilderViewModel? Builder => DataContext as YarnConditionBuilderViewModel;

    private void OnAddTerm(object? sender, RoutedEventArgs e) => Builder?.AddTerm();

    private void OnRemoveTerm(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: YarnConditionTermViewModel term })
        {
            Builder?.RemoveTerm(term);
        }
    }

    // Text that is not rows stays text: the box the click unticked shows it again
    private void OnTextClick(object? sender, RoutedEventArgs e)
    {
        if (Builder is { } builder && TextCheck.IsChecked != builder.IsText)
        {
            TextCheck.SetCurrentValue(ToggleButton.IsCheckedProperty, builder.IsText);
        }
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Done?.Invoke(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Done?.Invoke(false);

    // A box whose list is open takes Enter and Escape first
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                Done?.Invoke(true);
                break;
            case Key.Escape:
                e.Handled = true;
                Done?.Invoke(false);
                break;
        }
    }
}
