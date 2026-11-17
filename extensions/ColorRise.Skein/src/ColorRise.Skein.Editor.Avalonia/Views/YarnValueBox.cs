using Avalonia.Controls;
using Avalonia.Input;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// A box of the condition and set builders: what is typed is the value, and a list of what the project offers opens
/// when the box gets the focus, filtered as the user types.
/// </summary>
public sealed class YarnValueBox : AutoCompleteBox
{
    /// <summary>
    /// Makes an empty box.
    /// </summary>
    public YarnValueBox()
    {
        // Everything until the user types: the value the box shows would leave only itself
        ItemFilter = (search, item) => item is string text && (string.IsNullOrEmpty(search) || search == FocusText || text.Contains(search, StringComparison.OrdinalIgnoreCase));
        MinimumPrefixLength = 0;
        MaxDropDownHeight = 260;
        FilterMode = AutoCompleteFilterMode.Custom;
    }

    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(AutoCompleteBox);

    // The text when the box got the focus
    private string? FocusText { get; set; }

    /// <inheritdoc/>
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        FocusText = Text;
        if (!IsDropDownOpen && ItemsSource is not null)
        {
            PopulateComplete();
            IsDropDownOpen = true;
        }
    }
}
