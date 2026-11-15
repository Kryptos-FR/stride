using Stride.Core;
using Stride.Engine;
using Stride.Graphics;
using Stride.UI;
using Stride.UI.Controls;
using Yarn.Markup;

namespace ColorRise.Skein.UI;

/// <summary>
/// Shows the lines of a dialogue in a text box: the speaker's name, the text typed little by little, and a mark when
/// the line waits for the player. A <see cref="LineAdvancer"/> lets the player hurry the text and go on.
/// </summary>
/// <remarks>
/// It shows in the elements of its page named as <see cref="DefaultDialogueLook"/> says, and builds the default look
/// when the page has no text block named <see cref="DefaultDialogueLook.TextName"/>.
/// </remarks>
[DataContract("SkeinLinePresenter")]
[Display("Dialogue line presenter")]
[ComponentCategory("Dialogue")]
public sealed class LinePresenter : DialoguePresenter
{
    private UIElement? box;
    private TextBlock? speaker;
    private TextBlock? text;
    private UIElement? waiting;
    private bool bound;

    /// <summary>
    /// The typewriter passed a marker of the line other than a pause: a game's own inline events
    /// (<c>[shake/]</c>, <c>[sound name=bell/]</c>).
    /// </summary>
    public event Action<MarkupAttribute>? MarkerReached;

    /// <summary>
    /// The UI the lines show in; null for the UI of this entity, made when it has none.
    /// </summary>
    /// <userdoc>The UI component whose page shows the lines. Leave empty to use (or create) the UI of this entity. The page's elements named SkeinText, SkeinSpeaker, SkeinContinue and SkeinLine are used; without a SkeinText, a default text box is added.</userdoc>
    [DataMember(10)]
    public UIComponent? Page { get; set; }

    /// <summary>
    /// The font of the default look.
    /// </summary>
    /// <userdoc>The font of the default text box. Not needed when the page has its own SkeinText.</userdoc>
    [DataMember(20)]
    public SpriteFont? Font { get; set; }

    /// <summary>
    /// How the text comes: letter by letter, word by word, or all at once.
    /// </summary>
    /// <userdoc>How the text of a line appears: letter by letter, word by word, or all at once.</userdoc>
    [DataMember(30)]
    public TypewriterMode Typewriter { get; set; } = TypewriterMode.Letter;

    /// <summary>
    /// Letters, or words, per second.
    /// </summary>
    /// <userdoc>The speed of the typewriter, in letters (or words) per second.</userdoc>
    [DataMember(40)]
    public float Speed { get; set; } = 40;

    /// <summary>
    /// Whether a line goes on by itself after <see cref="HoldTime"/>, without waiting for the player.
    /// </summary>
    /// <userdoc>Go to the next line by itself once the text is shown, after the hold time. Off, the line waits for the player (a line advancer).</userdoc>
    [DataMember(50)]
    public bool AutoAdvance { get; set; }

    /// <summary>
    /// With <see cref="AutoAdvance"/>, the seconds a line stays once it is all shown.
    /// </summary>
    /// <userdoc>With Auto advance, how many seconds a line stays once its text is all shown.</userdoc>
    [DataMember(60)]
    public float HoldTime { get; set; } = 1.5f;

    /// <inheritdoc />
    public override Task OnDialogueCompletedAsync()
    {
        if (box is not null)
            box.Visibility = Visibility.Collapsed;
        if (text is not null)
            text.Text = "";
        if (waiting is not null)
            waiting.Visibility = Visibility.Hidden;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task RunLineAsync(LocalizedLine line, LineCancellation cancellation)
    {
        // With nothing to show in, the line is not held
        if (!Bind() || text is null)
            return;

        var typewriter = new Typewriter(speaker is null ? line.Text : line.TextWithoutCharacterName, Typewriter, Speed);
        typewriter.MarkerReached += marker =>
        {
            if (marker.Name != LineParser.CharacterAttribute)
                MarkerReached?.Invoke(marker);
        };

        if (speaker is not null)
        {
            speaker.Text = line.CharacterName ?? "";
            speaker.Visibility = line.CharacterName is null ? Visibility.Collapsed : Visibility.Visible;
        }

        if (waiting is not null)
            waiting.Visibility = Visibility.Hidden;
        if (box is not null)
            box.Visibility = Visibility.Visible;

        text.Text = typewriter.Advance(TimeSpan.Zero);
        var hurried = false;
        while (!typewriter.IsComplete)
        {
            await Script.NextFrame();
            if (cancellation.NextLine.IsCancellationRequested)
                return;

            if (cancellation.HurryUp.IsCancellationRequested)
            {
                typewriter.Complete();
                hurried = true;
            }
            else
            {
                typewriter.Advance(Game.UpdateTime.Elapsed);
            }

            text.Text = typewriter.VisibleText;
        }

        // All shown: the line waits for the player, or for its time. A hurry-up that comes now asks to go on.
        if (waiting is not null && !AutoAdvance)
            waiting.Visibility = Visibility.Visible;

        var held = TimeSpan.Zero;
        while (!cancellation.NextLine.IsCancellationRequested)
        {
            if (!hurried && cancellation.HurryUp.IsCancellationRequested)
                break;
            if (AutoAdvance && held.TotalSeconds >= HoldTime)
                break;

            await Script.NextFrame();
            held += Game.UpdateTime.Elapsed;
        }

        if (waiting is not null)
            waiting.Visibility = Visibility.Hidden;
    }

    /// <summary>
    /// Finds the elements of the page, or builds the default look; once.
    /// </summary>
    /// <returns>Whether there is a text block to show lines in.</returns>
    private bool Bind()
    {
        if (bound)
            return text is not null;

        bound = true;
        var ui = DialoguePage.Of(Entity, Page);
        text = DialoguePage.Find<TextBlock>(ui, DefaultDialogueLook.TextName);
        if (text is null)
        {
            var font = Font ?? DialoguePage.FontOf(ui);
            if (font is null)
            {
                Log.Error($"The line presenter of '{Entity.Name}' has nothing to show lines in: give it a font, or a UI page with a text block named '{DefaultDialogueLook.TextName}'.");
                return false;
            }

            DialoguePage.Add(ui, DefaultDialogueLook.CreateLine(font));
            text = DialoguePage.Find<TextBlock>(ui, DefaultDialogueLook.TextName);
        }

        speaker = DialoguePage.Find<TextBlock>(ui, DefaultDialogueLook.SpeakerName);
        waiting = DialoguePage.Find<UIElement>(ui, DefaultDialogueLook.ContinueName);
        box = DialoguePage.Find<UIElement>(ui, DefaultDialogueLook.LineName);
        return text is not null;
    }
}
