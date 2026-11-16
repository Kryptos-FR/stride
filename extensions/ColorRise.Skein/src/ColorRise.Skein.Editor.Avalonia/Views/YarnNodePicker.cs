using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// A box that picks the title of a Yarn node among those of a project: the list opens with every node when the box
/// gets the focus, and filters as the user types. The text becomes the <see cref="Node"/> when a node is picked in the
/// list, on Enter (the first node shown when none is highlighted) or when the focus leaves, not on every key; Escape
/// shows the node again.
/// </summary>
public sealed class YarnNodePicker : AutoCompleteBox
{
    /// <summary>
    /// The title of the picked node: what a property binds to.
    /// </summary>
    public static readonly StyledProperty<string?> NodeProperty = AvaloniaProperty.Register<YarnNodePicker, string?>(nameof(Node), defaultBindingMode: BindingMode.TwoWay);

    public YarnNodePicker()
    {
        // Every node until the user types: the title the box shows would leave only itself
        ItemFilter = (search, item) => !HasChanges || (item as string)?.Contains(search ?? "", StringComparison.OrdinalIgnoreCase) == true;
        MinimumPrefixLength = 0;
        MaxDropDownHeight = 300;
        DropDownClosed += OnDropDownClosed;
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// The title of the picked node.
    /// </summary>
    public string? Node
    {
        get => GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }

    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(AutoCompleteBox);

    // Whether the box shows something else than the node: what the user typed or picked, not sent yet
    private bool HasChanges => (Text ?? "") != (Node ?? "");

    /// <summary>
    /// Makes the text of the box the node.
    /// </summary>
    public void Commit()
    {
        if (HasChanges)
        {
            SetCurrentValue(NodeProperty, Text ?? "");
        }
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == NodeProperty)
        {
            SetCurrentValue(TextProperty, Node ?? "");
        }
    }

    /// <inheritdoc/>
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (!IsDropDownOpen && ItemsSource is not null)
        {
            // The list still has what was last typed: it is filtered again before it opens
            PopulateComplete();
            IsDropDownOpen = true;
        }
    }

    /// <inheritdoc/>
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);

        // The list is part of the box: the focus has left when it is in another control
        if (!IsKeyboardFocusWithin)
        {
            Commit();
        }
    }

    private void OnDropDownClosed(object? sender, EventArgs e)
    {
        // A node picked in the list: the box shows its title once the list has closed. The list also closes when the
        // text is cleared, or shows no node
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsDropDownOpen && SelectedItem is string title && title == Text)
            {
                Commit();
            }
        });
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                if (IsDropDownOpen && SelectedItem is null && FirstShown() is { } title)
                {
                    Text = title;
                    CaretIndex = title.Length;
                }

                Commit();
                break;

            case Key.Escape when HasChanges:
                // The box would put the typed text back when its list closes: the list closes first
                IsDropDownOpen = false;
                SetCurrentValue(TextProperty, Node ?? "");
                e.Handled = true;
                break;
        }
    }

    // The node the open list shows first for the text: the one of that title, or the first whose title contains it
    private string? FirstShown()
    {
        if (ItemsSource is not IEnumerable<string> titles || string.IsNullOrEmpty(Text))
            return null;

        var text = Text;
        return titles.FirstOrDefault(t => string.Equals(t, text, StringComparison.OrdinalIgnoreCase))
            ?? titles.FirstOrDefault(t => t.Contains(text, StringComparison.OrdinalIgnoreCase));
    }
}
