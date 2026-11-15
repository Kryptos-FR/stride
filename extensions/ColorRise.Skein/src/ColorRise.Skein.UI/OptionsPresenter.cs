using Stride.Core;
using Stride.Engine;
using Stride.Graphics;
using Stride.Input;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;

namespace ColorRise.Skein.UI;

/// <summary>
/// What an <see cref="OptionsPresenter"/> does with the options whose condition fails.
/// </summary>
public enum UnavailableOptions
{
    /// <summary>
    /// They are not shown.
    /// </summary>
    Hidden,

    /// <summary>
    /// They are shown, and cannot be chosen.
    /// </summary>
    Disabled,
}

/// <summary>
/// Offers the options of a dialogue as a column of buttons: the player clicks one, or moves among them with the
/// keyboard or the gamepad and confirms.
/// </summary>
/// <remarks>
/// It shows in the panel of its page named <see cref="DefaultDialogueLook.OptionsName"/>, whose buttons take the options
/// in order; it builds the default look when the page has no such panel, and adds default buttons when there are more
/// options than buttons.
/// </remarks>
[DataContract("SkeinOptionsPresenter")]
[Display("Dialogue options presenter")]
[ComponentCategory("Dialogue")]
public sealed class OptionsPresenter : DialoguePresenter
{
    private readonly List<ButtonBase> buttons = [];
    private readonly Dictionary<ButtonBase, int> offered = [];
    private Panel? panel;
    private SpriteFont? font;
    private int? clicked;
    private bool bound;
    private bool reportedTooFew;

    /// <summary>
    /// The UI the options show in; null for the UI of this entity, made when it has none.
    /// </summary>
    /// <userdoc>The UI component whose page shows the options. Leave empty to use (or create) the UI of this entity. The page's panel named SkeinOptions is used, with its buttons; without one, a default column of buttons is added.</userdoc>
    [DataMember(10)]
    public UIComponent? Page { get; set; }

    /// <summary>
    /// The font of the default look.
    /// </summary>
    /// <userdoc>The font of the default buttons. Not needed when the page has its own SkeinOptions with enough buttons.</userdoc>
    [DataMember(20)]
    public SpriteFont? Font { get; set; }

    /// <summary>
    /// Whether the options whose condition fails are hidden, or shown and not to choose.
    /// </summary>
    /// <userdoc>What to do with the options whose condition fails: hide them, or show them disabled.</userdoc>
    [DataMember(30)]
    public UnavailableOptions Unavailable { get; set; } = UnavailableOptions.Hidden;

    /// <summary>
    /// The key that goes to the previous option.
    /// </summary>
    /// <userdoc>The key that moves to the previous option. The gamepad's pad up does the same.</userdoc>
    [DataMember(40)]
    public Keys PreviousKey { get; set; } = Keys.Up;

    /// <summary>
    /// The key that goes to the next option.
    /// </summary>
    /// <userdoc>The key that moves to the next option. The gamepad's pad down does the same.</userdoc>
    [DataMember(50)]
    public Keys NextKey { get; set; } = Keys.Down;

    /// <summary>
    /// The key that chooses the option the player is on.
    /// </summary>
    /// <userdoc>The key that chooses the highlighted option.</userdoc>
    [DataMember(60)]
    public Keys ChooseKey { get; set; } = Keys.Enter;

    /// <summary>
    /// The gamepad button that chooses the option the player is on.
    /// </summary>
    /// <userdoc>The gamepad button that chooses the highlighted option.</userdoc>
    [DataMember(70)]
    public GamePadButton ChooseButton { get; set; } = GamePadButton.A;

    /// <inheritdoc />
    public override Task OnDialogueCompletedAsync()
    {
        Hide();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task<int?> RunOptionsAsync(IReadOnlyList<DialogueOption> options, CancellationToken cancellation)
    {
        if (!Bind() || panel is null)
            return null;

        var shown = options.Where(o => o.IsAvailable || Unavailable == UnavailableOptions.Disabled).ToList();
        if (!shown.Any(o => o.IsAvailable))
            return null;

        shown = Fit(shown);
        offered.Clear();
        clicked = null;
        for (var i = 0; i < buttons.Count; i++)
        {
            var button = buttons[i];
            if (i >= shown.Count)
            {
                button.Visibility = Visibility.Collapsed;
                continue;
            }

            var option = shown[i];
            if (button.FindVisualChildOfType<TextBlock>() is { } label)
                label.Text = option.Line.TextWithoutCharacterName.Text;
            button.IsEnabled = option.IsAvailable;
            button.Visibility = Visibility.Visible;
            if (option.IsAvailable)
                offered[button] = option.Index;
        }

        var selected = shown.FindIndex(o => o.IsAvailable);
        Highlight(shown, selected);
        panel.Visibility = Visibility.Visible;
        try
        {
            while (true)
            {
                // The press that ended the line before is not a choice: input counts from the next frame
                await Script.NextFrame();
                cancellation.ThrowIfCancellationRequested();
                if (clicked is { } byPointer)
                    return byPointer;

                var move = Pressed(NextKey, GamePadButton.PadDown) ? 1 : Pressed(PreviousKey, GamePadButton.PadUp) ? -1 : 0;
                if (move != 0)
                {
                    do
                    {
                        selected = (selected + move + shown.Count) % shown.Count;
                    }
                    while (!shown[selected].IsAvailable);

                    Highlight(shown, selected);
                }

                if (Pressed(ChooseKey, ChooseButton))
                    return shown[selected].Index;
            }
        }
        finally
        {
            offered.Clear();
            Hide();
        }
    }

    private bool Pressed(Keys key, GamePadButton button)
        => Input.IsKeyPressed(key) || Input.DefaultGamePad?.IsButtonPressed(button) == true;

    private void Hide()
    {
        if (panel is not null)
            panel.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Shows which option the player is on, and which cannot be chosen.
    /// </summary>
    private void Highlight(List<DialogueOption> shown, int selected)
    {
        for (var i = 0; i < shown.Count && i < buttons.Count; i++)
        {
            var button = buttons[i];
            button.Opacity = !shown[i].IsAvailable ? 0.4f : i == selected ? 1f : 0.8f;

            // A button of the default look has no images: its colour says where the player is
            if (button is Button { NotPressedImage: null })
                button.BackgroundColor = i == selected ? DefaultDialogueLook.SelectedOption : DefaultDialogueLook.Option;
        }
    }

    /// <summary>
    /// Makes sure there is a button per option: adds default buttons when a font is known, else keeps the options
    /// that fit and says so once.
    /// </summary>
    private List<DialogueOption> Fit(List<DialogueOption> shown)
    {
        while (buttons.Count < shown.Count && font is not null)
        {
            var button = DefaultDialogueLook.CreateOption(font);
            panel!.Children.Add(button);
            Add(button);
        }

        if (buttons.Count >= shown.Count)
            return shown;

        if (!reportedTooFew)
        {
            reportedTooFew = true;
            Log.Error($"The options presenter of '{Entity.Name}' has {buttons.Count} buttons for {shown.Count} options: the others are not offered. Add buttons to the panel '{DefaultDialogueLook.OptionsName}', or give the presenter a font to make them.");
        }

        return shown[..buttons.Count];
    }

    private void Add(ButtonBase button)
    {
        buttons.Add(button);
        button.Click += (_, _) =>
        {
            if (offered.TryGetValue(button, out var index))
                clicked = index;
        };
    }

    /// <summary>
    /// Finds the panel of the page and its buttons, or builds the default look; once.
    /// </summary>
    /// <returns>Whether there is a panel to show options in.</returns>
    private bool Bind()
    {
        if (bound)
            return panel is not null;

        bound = true;
        var ui = DialoguePage.Of(Entity, Page);
        font = Font ?? DialoguePage.FontOf(ui);
        panel = DialoguePage.Find<Panel>(ui, DefaultDialogueLook.OptionsName);
        if (panel is null)
        {
            if (font is null)
            {
                Log.Error($"The options presenter of '{Entity.Name}' has nothing to show options in: give it a font, or a UI page with a panel named '{DefaultDialogueLook.OptionsName}' that holds buttons.");
                return false;
            }

            panel = DefaultDialogueLook.CreateOptions();
            DialoguePage.Add(ui, panel);
        }

        foreach (var button in panel.Children.OfType<ButtonBase>())
        {
            Add(button);
        }

        panel.Visibility = Visibility.Collapsed;
        return true;
    }
}
