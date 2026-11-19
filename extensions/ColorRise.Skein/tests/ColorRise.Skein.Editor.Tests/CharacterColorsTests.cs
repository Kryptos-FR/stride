using ColorRise.Skein.Assets;
using ColorRise.Skein.Editor.Graph;
using ColorRise.Skein.Editor.Player;
using ColorRise.Skein.Editor.Services;
using Stride.Core.Mathematics;

namespace ColorRise.Skein.Editor.Tests;

/// <summary>
/// Each character has a colour in Game Studio: one made from its name, or the one its project chose.
/// </summary>
public sealed class CharacterColorsTests
{
    private const string Act1 = "title: Start\n---\nOdile: Hello.\nFisher: Hi.\nodile: Again.\nThe sea.\n===\n";

    [Fact]
    public void A_name_makes_the_same_colour_at_each_run_whatever_its_case()
    {
        var colors = new YarnCharacterColors(new Dictionary<string, Color3> { ["Fisher"] = new Color3(1, 0, 0) });
        var other = new YarnCharacterColors(new Dictionary<string, Color3>());

        Assert.Equal(colors.Of("Odile"), other.Of("Odile"));
        Assert.Equal(colors.Of("Odile"), colors.Of(" ODILE "));
        Assert.NotEqual(colors.Of("Odile"), colors.Of("Kye"));
        Assert.Equal(new Color3(1, 0, 0), colors.Of("fisher"));
        Assert.False(colors.SameAs(other));
        Assert.True(other.SameAs(YarnCharacterColors.Default));

        // A project's first colour of a name in any case wins; a colour without a name is none
        var project = new YarnProjectAsset { CharacterColors = { ["Odile"] = new Color3(0, 0, 1), ["ODILE"] = new Color3(1, 1, 1), [" "] = new Color3(1, 0, 0) } };
        Assert.Equal(new Color3(0, 0, 1), YarnCharacterColors.From(project).Of("odile"));
        Assert.Equal(colors.Of("Kye"), YarnCharacterColors.From(project).Of("Kye"));

        // A colour that does not read on a background is made lighter or darker
        var navy = new Color3(0, 0, 0.3f);
        Assert.True(Luminance(YarnCharacterColors.Readable(navy, onDark: true)) >= 0.49f);
        Assert.Equal(navy, YarnCharacterColors.Readable(navy, onDark: false));
        Assert.True(Luminance(YarnCharacterColors.Readable(new Color3(1, 1, 0.6f), onDark: false)) <= 0.41f);
        Assert.InRange(Luminance(YarnCharacterColors.ReadableOnBoth(new Color3(1, 1, 1))), 0.3f, 0.51f);
        Assert.InRange(Luminance(YarnCharacterColors.ReadableOnBoth(navy)), 0.29f, 0.5f);
    }

    [Fact]
    public async Task The_speakers_take_their_colour_on_the_cards_and_in_the_player_and_a_chosen_colour_needs_no_new_build()
    {
        await using var session = await SkeinSession.OpenAsync(SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1));
        var editor = session.Edit("Dialogue/Act1");
        await session.Analysis.WhenIdle();

        var rows = editor.Cards.OfType<YarnNodeCardViewModel>().Single().Rows.Where(r => !r.IsAdd).ToList();
        var made = YarnCharacterColors.Default;
        Assert.Equal([made.Of("Odile"), made.Of("Fisher"), made.Of("Odile"), null], rows.Select(r => r.SpeakerColor));

        var project = session.ProjectAt("Dialogue/Story");
        await session.Player.Start(project, "Start");
        var item = PlayerTranscriptItem.Of(session.Player.Steps[2], 2, session.Player.Characters);
        Assert.Equal(("Odile", made.Of("Odile")), (item!.Speaker, item.SpeakerColor));

        // A colour the project chooses shows at once; the scripts are not compiled again and the run is not stale
        var build = session.Analysis.AnalysisOf(project)!.Build;
        var changes = 0;
        session.Player.Changed += () => changes++;
        project.Asset.CharacterColors.Add("odile", new Color3(1, 0, 0));
        project.Asset.CharacterColors.Add("Odile", new Color3(0, 1, 0));
        project.Asset.CharacterColors.Add("", new Color3(0, 0, 1));
        session.Analysis.Invalidate();
        await session.Analysis.WhenIdle();

        Assert.Same(build, session.Analysis.AnalysisOf(project)!.Build);
        Assert.Equal([new Color3(1, 0, 0), made.Of("Fisher"), new Color3(1, 0, 0), null], rows.Select(r => r.SpeakerColor));
        Assert.Equal(new Color3(1, 0, 0), editor.Characters.Of("Odile"));
        Assert.Equal(new Color3(1, 0, 0), session.Player.Characters.Of("Odile"));
        Assert.False(session.Player.IsStale);
        Assert.Equal(1, changes);
    }

    private static float Luminance(Color3 color) => (0.2126f * color.R) + (0.7152f * color.G) + (0.0722f * color.B);
}
