using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;

namespace ColorRise.Skein.UI;

/// <summary>
/// The look the presenters build when their page lacks the elements they show in: a text box at the bottom of the
/// screen and a column of buttons at its centre. A game that wants its own look gives the presenters a UI page with
/// elements of these names.
/// </summary>
public static class DefaultDialogueLook
{
    /// <summary>
    /// The element shown while a line is, and collapsed when the dialogue is over. Optional in a page.
    /// </summary>
    public const string LineName = "SkeinLine";

    /// <summary>
    /// The text block of the speaker's name. Optional in a page: without it, the name stays in the text.
    /// </summary>
    public const string SpeakerName = "SkeinSpeaker";

    /// <summary>
    /// The text block of the line.
    /// </summary>
    public const string TextName = "SkeinText";

    /// <summary>
    /// The element shown when the line is all there and waits for the player. Optional in a page.
    /// </summary>
    public const string ContinueName = "SkeinContinue";

    /// <summary>
    /// The panel of the options: its buttons, each with a text block inside, take the options in order.
    /// </summary>
    public const string OptionsName = "SkeinOptions";

    private static readonly Color Panel = new(18, 22, 30, 225);
    private static readonly Color Accent = new(232, 178, 92, 255);
    private static readonly Color Text = new(240, 240, 240, 255);

    /// <summary>
    /// The colour of an option's button, and of the one the player is on.
    /// </summary>
    internal static readonly Color Option = new(18, 22, 30, 225);
    internal static readonly Color SelectedOption = new(70, 84, 112, 240);

    /// <summary>
    /// Makes the text box of the lines: the speaker's name, the text, and a mark that shows when the line waits.
    /// </summary>
    public static UIElement CreateLine(SpriteFont font)
    {
        ArgumentNullException.ThrowIfNull(font);

        var content = new StackPanel { Orientation = Orientation.Vertical };
        content.Children.Add(new TextBlock
        {
            Name = SpeakerName,
            Font = font,
            TextSize = 26,
            TextColor = Accent,
            Margin = new Thickness(0, 0, 0, 10),
        });
        content.Children.Add(new TextBlock
        {
            Name = TextName,
            Font = font,
            TextSize = 24,
            TextColor = Text,
            WrapText = true,
        });
        content.Children.Add(new Border
        {
            Name = ContinueName,
            Width = 14,
            Height = 14,
            BackgroundColor = Accent,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0),
            Visibility = Visibility.Hidden,
        });

        return new Border
        {
            Name = LineName,
            Content = content,
            Padding = new Thickness(28, 20, 28, 20),
            BackgroundColor = Panel,
            BorderColor = Accent,
            BorderThickness = new Thickness(0, 2, 0, 0),
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(80, 0, 80, 40),
            MinimumHeight = 170,
            Visibility = Visibility.Collapsed,
        };
    }

    /// <summary>
    /// Makes the panel of the options, empty: the options presenter fills it with <see cref="CreateOption"/>.
    /// </summary>
    public static StackPanel CreateOptions() => new()
    {
        Name = OptionsName,
        Orientation = Orientation.Vertical,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Visibility = Visibility.Collapsed,
    };

    /// <summary>
    /// Makes the button of one option.
    /// </summary>
    public static Button CreateOption(SpriteFont font)
    {
        ArgumentNullException.ThrowIfNull(font);

        return new Button
        {
            Content = new TextBlock
            {
                Font = font,
                TextSize = 24,
                TextColor = Text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            Padding = new Thickness(28, 12, 28, 12),
            Margin = new Thickness(0, 6, 0, 6),
            MinimumWidth = 560,
            BackgroundColor = Option,
        };
    }
}
