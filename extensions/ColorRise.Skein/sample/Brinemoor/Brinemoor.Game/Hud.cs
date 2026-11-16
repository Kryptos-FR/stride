using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;

namespace Brinemoor;

/// <summary>
/// The game's own screen, built in code: the day and what the courier carries (top left), the journal (top right),
/// what just happened (top centre), names over the people, what the courier can do here, and the ending. The dialogue
/// box is not here: Skein's presenters show it.
/// </summary>
internal sealed class Hud
{
    private static readonly Color Panel = new(18, 22, 30, 240);
    private static readonly Color Accent = new(232, 178, 92, 255);
    private static readonly Color Bright = new(240, 240, 240, 255);
    private static readonly Color Dim = new(165, 170, 180, 255);

    private readonly SpriteFont font;
    private readonly TextBlock status;
    private readonly TextBlock journalTitle;
    private readonly TextBlock journal;
    private readonly Border journalBox;
    private readonly TextBlock toast;
    private readonly Border toastBox;
    private readonly TextBlock prompt;
    private readonly Border promptBox;
    private readonly TextBlock help;
    private readonly Border helpBox;
    private readonly TextBlock endingTitle;
    private readonly TextBlock endingText;
    private readonly TextBlock endingHelp;
    private readonly Border endingBox;
    private readonly Canvas tags = new();
    private readonly Dictionary<Entity, (Border Box, TextBlock Text)> tagOf = [];

    public Hud(SpriteFont font)
    {
        this.font = font;

        status = Text(18, Bright);
        var statusBox = Box(status);
        statusBox.HorizontalAlignment = HorizontalAlignment.Left;
        statusBox.VerticalAlignment = VerticalAlignment.Top;
        statusBox.Margin = new Thickness(16, 16, 0, 0);

        journalTitle = Text(18, Accent);
        journal = Text(17, Bright);
        journalBox = Box(new StackPanel { Orientation = Orientation.Vertical, Children = { journalTitle, journal } });
        journalBox.HorizontalAlignment = HorizontalAlignment.Right;
        journalBox.VerticalAlignment = VerticalAlignment.Top;
        journalBox.Margin = new Thickness(0, 16, 16, 0);

        toast = Text(20, Accent);
        toastBox = Box(toast);
        toastBox.HorizontalAlignment = HorizontalAlignment.Center;
        toastBox.VerticalAlignment = VerticalAlignment.Top;
        toastBox.Margin = new Thickness(0, 16, 0, 0);
        toastBox.Visibility = Visibility.Collapsed;

        prompt = Text(20, Bright);
        promptBox = Box(prompt);
        promptBox.HorizontalAlignment = HorizontalAlignment.Center;
        promptBox.VerticalAlignment = VerticalAlignment.Bottom;
        promptBox.Margin = new Thickness(0, 0, 0, 56);

        help = Text(14, Dim);
        helpBox = Box(help);
        helpBox.Padding = new Thickness(10, 4, 10, 4);
        helpBox.HorizontalAlignment = HorizontalAlignment.Center;
        helpBox.VerticalAlignment = VerticalAlignment.Bottom;
        helpBox.Margin = new Thickness(0, 0, 0, 10);

        endingTitle = Text(40, Accent);
        endingTitle.HorizontalAlignment = HorizontalAlignment.Center;
        endingText = Text(24, Bright);
        endingText.HorizontalAlignment = HorizontalAlignment.Center;
        endingText.Margin = new Thickness(0, 14, 0, 18);
        endingHelp = Text(16, Dim);
        endingHelp.HorizontalAlignment = HorizontalAlignment.Center;
        endingBox = Box(new StackPanel { Orientation = Orientation.Vertical, Children = { endingTitle, endingText, endingHelp } });
        endingBox.Padding = new Thickness(60, 36, 60, 36);
        endingBox.HorizontalAlignment = HorizontalAlignment.Center;
        endingBox.VerticalAlignment = VerticalAlignment.Center;
        endingBox.Visibility = Visibility.Collapsed;

        Page = new UIPage { RootElement = new Grid { Children = { tags, statusBox, journalBox, toastBox, helpBox, promptBox, endingBox } } };
    }

    public UIPage Page { get; }

    public void SetStatus(string text) => status.Text = text;

    public void SetHelp(string text) => help.Text = text;

    /// <summary>
    /// Shows the quests to do under a title, or nothing when there is none.
    /// </summary>
    public void SetJournal(string title, IReadOnlyList<string> quests)
    {
        journalTitle.Text = title;
        journal.Text = string.Join("\n", quests);
        journalBox.Visibility = quests.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Shows what just happened, or nothing.
    /// </summary>
    public void SetToast(string? text)
    {
        toast.Text = text ?? "";
        toastBox.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Shows what the courier can do here and the keys of the game, or neither while a dialogue plays.
    /// </summary>
    public void SetPrompt(string? text, bool showHelp)
    {
        prompt.Text = text ?? "";
        promptBox.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        helpBox.Visibility = showHelp ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Shows the ending of the story over the game, or nothing while the story goes on.
    /// </summary>
    public void SetEnding(string? title, string text, string keys)
    {
        endingTitle.Text = title ?? "";
        endingText.Text = text;
        endingHelp.Text = keys;
        endingBox.Visibility = title is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Puts a name over someone, at a point of the screen from (0, 0) at the top left to (1, 1) at the bottom right; no
    /// point hides it.
    /// </summary>
    public void SetTag(Entity entity, string text, bool bright, Vector2? screen)
    {
        if (!tagOf.TryGetValue(entity, out var tag))
        {
            var block = Text(16, Bright);
            var box = Box(block);
            box.Padding = new Thickness(8, 3, 8, 3);
            box.SetCanvasPinOrigin(new Vector3(0.5f, 1f, 0f));
            tags.Children.Add(box);
            tag = (box, block);
            tagOf.Add(entity, tag);
        }

        tag.Text.Text = text;
        tag.Text.TextColor = bright ? Bright : Dim;
        tag.Box.Visibility = screen is null ? Visibility.Collapsed : Visibility.Visible;
        if (screen is { } point)
            tag.Box.SetCanvasRelativePosition(new Vector3(point.X, point.Y, 0f));
    }

    private TextBlock Text(float size, Color color) => new() { Font = font, TextSize = size, TextColor = color };

    private static Border Box(UIElement content) => new() { BackgroundColor = Panel, Padding = new Thickness(14, 10, 14, 10), Content = content };
}
