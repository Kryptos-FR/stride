using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// The builder of a <c>&lt;&lt;set&gt;&gt;</c> command, shown in a popover over a card.
/// </summary>
public sealed partial class YarnAssignmentBuilderView : UserControl
{
    /// <summary>
    /// Makes the view; its data context is a <see cref="Conditions.YarnAssignmentBuilderViewModel"/>.
    /// </summary>
    public YarnAssignmentBuilderView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble, handledEventsToo: false);
    }

    /// <summary>
    /// Raised when the user is done: true to write what the builder made, false to leave the text as it is.
    /// </summary>
    public event Action<bool>? Done;

    private void OnOk(object? sender, RoutedEventArgs e) => Done?.Invoke(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Done?.Invoke(false);

    // A box whose list is open takes Enter and Escape first
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Escape)
        {
            e.Handled = true;
            Done?.Invoke(e.Key == Key.Enter);
        }
    }
}
