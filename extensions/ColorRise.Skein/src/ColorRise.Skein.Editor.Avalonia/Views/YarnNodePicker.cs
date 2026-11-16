using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// A box that picks the title of a Yarn node among those of a project: the list opens when the box gets the focus and
/// filters as the user types. The text goes to its binding when a node is picked in the list, on Enter (the first node
/// shown when none is highlighted) or when the focus leaves, not on every key; Escape shows the bound value again.
/// </summary>
/// <remarks>
/// The binding of <see cref="AutoCompleteBox.Text"/> uses <c>UpdateSourceTrigger=Explicit</c>.
/// </remarks>
public sealed class YarnNodePicker : AutoCompleteBox
{
    private bool hasChanges;

    public YarnNodePicker()
    {
        FilterMode = AutoCompleteFilterMode.Contains;
        MinimumPrefixLength = 0;
        MaxDropDownHeight = 300;
        TextChanged += OnTextChanged;
        DropDownClosed += OnDropDownClosed;
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(AutoCompleteBox);

    /// <summary>
    /// Sends the text to the binding when the user changed it.
    /// </summary>
    public void Commit()
    {
        if (!hasChanges)
            return;

        hasChanges = false;
        BindingOperations.GetBindingExpressionBase(this, TextProperty)?.UpdateSource();
    }

    /// <inheritdoc/>
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (!IsDropDownOpen && ItemsSource is not null)
        {
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

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        // The text also changes when the binding shows its value: only what the user does counts
        if (IsKeyboardFocusWithin || IsDropDownOpen)
        {
            hasChanges = true;
        }
    }

    private void OnDropDownClosed(object? sender, EventArgs e)
    {
        // A node picked in the list: the box shows its title. The list also closes when the text is cleared
        if (SelectedItem is string title && title == Text)
        {
            Commit();
        }
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

            case Key.Escape when hasChanges:
                // The box would put the typed text back when its list closes: the list closes first
                hasChanges = false;
                IsDropDownOpen = false;
                BindingOperations.GetBindingExpressionBase(this, TextProperty)?.UpdateTarget();
                hasChanges = false;
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
