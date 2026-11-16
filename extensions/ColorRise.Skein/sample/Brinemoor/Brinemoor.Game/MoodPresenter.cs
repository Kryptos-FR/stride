using ColorRise.Skein;
using ColorRise.Skein.UI;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.UI;
using Stride.UI.Controls;

namespace Brinemoor;

/// <summary>
/// Colours the name of who speaks by the mood of the line: a line of the story says its mood with a tag,
/// <c>#mood:angry</c>. It goes after the line presenter in the runner's presenters, whose text box it colours.
/// </summary>
[Display("Mood presenter")]
[ComponentCategory("Brinemoor")]
public sealed class MoodPresenter : DialoguePresenter
{
    private const string MoodTag = "mood:";

    private static readonly Color Calm = new(232, 178, 92, 255);

    private static readonly Dictionary<string, Color> Moods = new()
    {
        ["angry"] = new Color(235, 96, 80, 255),
        ["afraid"] = new Color(170, 150, 235, 255),
        ["glad"] = new Color(130, 210, 120, 255),
        ["sad"] = new Color(120, 170, 230, 255),
    };

    /// <inheritdoc />
    public override Task RunLineAsync(LocalizedLine line, LineCancellation cancellation)
    {
        var mood = line.Metadata.FirstOrDefault(tag => tag.StartsWith(MoodTag, StringComparison.Ordinal))?[MoodTag.Length..];
        if (Entity.Get<UIComponent>()?.Page?.RootElement?.FindName(DefaultDialogueLook.SpeakerName) is TextBlock speaker)
            speaker.TextColor = mood is not null && Moods.TryGetValue(mood, out var color) ? color : Calm;
        return Task.CompletedTask;
    }
}
