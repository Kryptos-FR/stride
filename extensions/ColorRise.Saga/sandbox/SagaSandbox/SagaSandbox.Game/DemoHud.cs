using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;

namespace SagaSandbox;

/// <summary>
/// The demo's screen, built in code: the story's state (top left), a log of what happened (top right), name tags over the
/// characters, the prompt of what the player can do, and the dialogue box.
/// </summary>
internal sealed class DemoHud
{
    private static readonly Color Panel = new(0, 0, 0, 170);
    private static readonly Color Dim = new(150, 150, 150, 255);

    private readonly SpriteFont font;
    private readonly TextBlock status;
    private readonly TextBlock log;
    private readonly TextBlock prompt;
    private readonly Border promptBox;
    private readonly Border dialogueBox;
    private readonly TextBlock speaker;
    private readonly TextBlock line;
    private readonly StackPanel choices;
    private readonly TextBlock hint;
    private readonly Canvas tags;
    private readonly Dictionary<Entity, (Border Box, TextBlock Text)> tagBlocks = [];

    public DemoHud(SpriteFont font)
    {
        this.font = font;

        status = Text(18);
        log = Text(16);
        prompt = Text(20);
        speaker = Text(22);
        line = Text(20);
        line.WrapText = true;
        hint = Text(15);
        hint.TextColor = Dim;
        choices = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 10, 0, 0) };
        tags = new Canvas();

        promptBox = Box(prompt);
        promptBox.HorizontalAlignment = HorizontalAlignment.Center;
        promptBox.VerticalAlignment = VerticalAlignment.Bottom;
        promptBox.Margin = new Thickness(0, 0, 0, 40);

        dialogueBox = Box(new StackPanel
        {
            Orientation = Orientation.Vertical,
            Children = { speaker, line, choices, hint },
        });
        dialogueBox.Width = 900;
        dialogueBox.HorizontalAlignment = HorizontalAlignment.Center;
        dialogueBox.VerticalAlignment = VerticalAlignment.Bottom;
        dialogueBox.Margin = new Thickness(0, 0, 0, 44);
        dialogueBox.Visibility = Visibility.Collapsed;

        var statusBox = Box(status);
        statusBox.HorizontalAlignment = HorizontalAlignment.Left;
        statusBox.VerticalAlignment = VerticalAlignment.Top;
        statusBox.Margin = new Thickness(16, 16, 0, 0);

        var logBox = Box(log);
        logBox.Width = 420;
        logBox.HorizontalAlignment = HorizontalAlignment.Right;
        logBox.VerticalAlignment = VerticalAlignment.Top;
        logBox.Margin = new Thickness(0, 16, 16, 0);

        var helpText = Text(15);
        helpText.Text = "WASD or arrows: move   T: talk   E: use   N: next day   Esc: quit";
        var help = Box(helpText);
        help.Padding = new Thickness(10, 4, 10, 4);
        help.HorizontalAlignment = HorizontalAlignment.Left;
        help.VerticalAlignment = VerticalAlignment.Bottom;
        help.Margin = new Thickness(16, 0, 0, 8);

        Page = new UIPage { RootElement = new Grid { Children = { tags, statusBox, logBox, help, promptBox, dialogueBox } } };
    }

    public UIPage Page { get; }

    public void SetStatus(string text) => status.Text = text;

    public void SetLog(IEnumerable<string> lines) => log.Text = string.Join("\n", lines);

    /// <summary>
    /// Shows what the player can do here, or nothing.
    /// </summary>
    public void SetPrompt(string? text)
    {
        prompt.Text = text ?? string.Empty;
        promptBox.Visibility = string.IsNullOrEmpty(text) || dialogueBox.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }

    public void ShowDialogue(string speakerName, Color speakerColor, string text, IReadOnlyList<(string Text, bool IsEnabled)> options)
    {
        speaker.Text = speakerName;
        speaker.TextColor = speakerColor;
        line.Text = text;
        choices.Children.Clear();
        var number = 0;
        foreach (var (choice, isEnabled) in options)
        {
            var block = Text(19);
            block.Text = isEnabled ? $"{++number}. {choice}" : $"    {choice}";
            block.TextColor = isEnabled ? Color.White : Dim;
            choices.Children.Add(block);
        }
        hint.Text = options.Count > 0 ? $"1 to {number}: choose" : "Space: continue";
        dialogueBox.Visibility = Visibility.Visible;
        promptBox.Visibility = Visibility.Collapsed;
    }

    public void HideDialogue() => dialogueBox.Visibility = Visibility.Collapsed;

    /// <summary>
    /// Puts a tag at a point of the screen, (0, 0) at the top left and (1, 1) at the bottom right.
    /// </summary>
    public void SetTag(Entity entity, string text, Color color, Vector2? screen)
    {
        if (!tagBlocks.TryGetValue(entity, out var tag))
        {
            var block = Text(17);
            var box = Box(block);
            box.Padding = new Thickness(8, 3, 8, 3);
            box.SetCanvasPinOrigin(new Vector3(0.5f, 1f, 0f));
            tags.Children.Add(box);
            tag = (box, block);
            tagBlocks.Add(entity, tag);
        }
        tag.Text.Text = text;
        tag.Text.TextColor = color;
        tag.Box.Visibility = screen is null ? Visibility.Collapsed : Visibility.Visible;
        if (screen is { } position)
            tag.Box.SetCanvasRelativePosition(new Vector3(position.X, position.Y, 0f));
    }

    private TextBlock Text(float size) => new() { Font = font, TextSize = size, TextColor = Color.White };

    private static Border Box(UIElement content) => new() { BackgroundColor = Panel, Padding = new Thickness(14, 10, 14, 10), Content = content };
}
