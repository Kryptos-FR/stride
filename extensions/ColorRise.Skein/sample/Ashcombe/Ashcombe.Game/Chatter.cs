using ColorRise.Skein;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;

namespace Ashcombe;

/// <summary>
/// People who talk among themselves: while the cartographer is near, a dialogue runner of their own plays their node
/// group again and again, whatever the cartographer is doing, a talk with someone else included. The two runners share
/// the variables of the story, so the chatter follows the acts. When their scene is unloaded in the middle of a line,
/// the chatter stops with it.
/// </summary>
[Display("Chatter")]
[ComponentCategory("Ashcombe")]
public sealed class Chatter : AsyncScript
{
    /// <summary>
    /// The runner of the chatter: not the one of the story.
    /// </summary>
    /// <userdoc>The dialogue runner that plays the chatter. It is another runner than the story's, with a caption presenter.</userdoc>
    [DataMember(10)]
    public DialogueRunnerComponent? Runner { get; set; }

    /// <summary>
    /// The node group of the chatter.
    /// </summary>
    /// <userdoc>The node group of the chatter.</userdoc>
    [DataMember(20)]
    public string Node { get; set; } = "";

    /// <summary>
    /// Who must be near to hear it; null for the cartographer of the scene that is up.
    /// </summary>
    /// <userdoc>The entity that must be near for the chatter to play. Empty: the cartographer, whatever scene is up.</userdoc>
    [DataMember(30)]
    public Entity? Listener { get; set; }

    /// <summary>
    /// How near, in metres.
    /// </summary>
    /// <userdoc>How near the listener must be, in metres.</userdoc>
    [DataMember(40)]
    public float Distance { get; set; } = 9f;

    /// <summary>
    /// The seconds of silence between two chatters.
    /// </summary>
    /// <userdoc>The seconds of silence between two chatters.</userdoc>
    [DataMember(50)]
    public float Pause { get; set; } = 10f;

    /// <inheritdoc />
    public override async Task Execute()
    {
        var silence = 2.0;
        while (!CancellationToken.IsCancellationRequested)
        {
            await Script.NextFrame();
            var listener = Listener ?? Services.GetService<StorySystem>()?.Stage?.Player;
            if (Runner is null || listener is null || Runner.IsRunning)
                continue;

            silence -= Game.UpdateTime.Elapsed.TotalSeconds;
            var near = Vector3.Distance(StorySystem.WorldPosition(listener), StorySystem.WorldPosition(Entity)) <= Distance;
            if (silence > 0 || !near || !Runner.NodeExists(Node) || !Runner.HasSalientContent(Node))
                continue;

            await Runner.StartDialogue(Node);
            silence = Pause;
        }
    }
}

/// <summary>
/// Shows the lines of a dialogue as a caption at the top of the screen, each for the time it takes to read it. It is a
/// presenter of the game's own: a few lines over <see cref="DialoguePresenter"/>.
/// </summary>
[Display("Caption presenter")]
[ComponentCategory("Ashcombe")]
public sealed class CaptionPresenter : DialoguePresenter
{
    private TextBlock? caption;
    private Border? box;

    /// <summary>
    /// The font of the caption.
    /// </summary>
    /// <userdoc>The font of the caption.</userdoc>
    [DataMember(10)]
    public SpriteFont? Font { get; set; }

    /// <summary>
    /// The seconds a line stays, whatever its length.
    /// </summary>
    /// <userdoc>The seconds a line stays, whatever its length.</userdoc>
    [DataMember(20)]
    public float HoldTime { get; set; } = 1.6f;

    /// <summary>
    /// The seconds a line stays more for each of its letters.
    /// </summary>
    /// <userdoc>The seconds a line stays more, for each of its letters.</userdoc>
    [DataMember(30)]
    public float TimePerLetter { get; set; } = 0.045f;

    /// <inheritdoc />
    public override Task OnDialogueCompletedAsync()
    {
        if (box is not null)
            box.Visibility = Visibility.Collapsed;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task RunLineAsync(LocalizedLine line, LineCancellation cancellation)
    {
        if (Font is null)
            return;

        if (caption is null || box is null)
        {
            caption = new TextBlock { Font = Font, TextSize = 18, TextColor = new Color(225, 228, 235, 255) };
            box = new Border
            {
                Content = caption,
                BackgroundColor = new Color(18, 22, 30, 170),
                Padding = new Thickness(14, 8, 14, 8),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 76, 0, 0),
            };
            Entity.Add(new UIComponent { Page = new UIPage { RootElement = new Grid { Children = { box } } }, Resolution = new Vector3(1280, 720, 1000) });
        }

        caption.Text = line.Text.Text;
        box.Visibility = Visibility.Visible;
        var left = HoldTime + TimePerLetter * line.Text.Text.Length;
        while (left > 0 && !cancellation.NextLine.IsCancellationRequested)
        {
            await Script.NextFrame();
            left -= (float)Game.UpdateTime.Elapsed.TotalSeconds;
        }
    }
}
