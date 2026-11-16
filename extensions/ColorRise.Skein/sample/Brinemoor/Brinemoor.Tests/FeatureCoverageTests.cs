using ColorRise.Skein;
using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Assets.Localisation;

namespace Brinemoor.Tests;

/// <summary>
/// The story of the sample uses each feature of Yarn Spinner that Skein supports: one check per feature, on the
/// scripts as the build of the game compiles them.
/// </summary>
[Collection(StoryCollection.Name)]
public sealed class FeatureCoverageTests
{
    private static readonly IReadOnlyList<(string FileName, string Text)> Scripts = StoryFiles.Scripts();
    private static readonly string Source = string.Join("\n", Scripts.Select(s => s.Text));

    private readonly TestStory story = TestStory.Brinemoor(language: "fr");

    private YarnCompilation Compilation => story.Build.Compilation;

    [Fact]
    public void The_story_is_about_sixty_nodes_in_five_scripts_and_compiles_clean()
    {
        Assert.Equal(["Dialogue/Act1.yarn", "Dialogue/Act2.yarn", "Dialogue/Act3.yarn", "Dialogue/Chatter.yarn", "Dialogue/Story.yarn"], Scripts.Select(s => s.FileName));
        Assert.InRange(Compilation.Nodes.Count, 55, 80);
        Assert.Empty(Compilation.Diagnostics.Where(d => d.Severity != YarnSeverity.Info).Select(d => d.ToString()));
        Assert.Empty(story.Build.Warnings);
    }

    [Fact]
    public void Options_have_conditions_and_options_of_their_own()
    {
        Assert.Contains(Compilation.Nodes, n => n.Options.Count > 0);
        Assert.Contains("-> Ask about the cargo. <<if $side == Side.Neither>>", Source);

        // An option inside the lines of another has a group of its own in its node
        Assert.Contains(Compilation.Nodes, n => n.Options.Select(o => o.Group).Distinct().Count() > 1);
    }

    [Fact]
    public void Nodes_jump_and_detour()
    {
        var jumps = Compilation.Nodes.SelectMany(n => n.Jumps).ToList();
        Assert.Contains(jumps, j => j is { IsDetour: false, Target: "ActTwo" });
        Assert.Contains(jumps, j => j is { IsDetour: true, Target: "MeetingCargo" });

        // The town meeting is a hub: each topic is a detour that comes back to it
        var hub = Assert.Single(Compilation.Nodes, n => n.Title == "MeetingTopics");
        Assert.Equal(3, hub.Jumps.Count(j => j.IsDetour));
    }

    [Fact]
    public void Variables_are_declared_with_a_description_and_some_are_enums_or_smart()
    {
        var variables = Compilation.Variables.Where(v => !v.IsInternal).ToList();
        Assert.All(variables, v => Assert.False(v.IsImplicit, $"{v.Name} is not declared."));
        Assert.All(variables, v => Assert.False(string.IsNullOrWhiteSpace(v.Description), $"{v.Name} has no description."));

        Assert.Equal("Act", Assert.Single(variables, v => v.Name == "$act").Type);
        Assert.Equal("QuestState", Assert.Single(variables, v => v.Name == "$quest_lamp").Type);
        Assert.Equal(["$is_night", "$lamp_can_be_lit", "$trail_complete"], variables.Where(v => v.IsSmart).Select(v => v.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Lines_use_conditions_assignments_and_values()
    {
        Assert.Contains("<<if ", Source);
        Assert.Contains("<<elseif ", Source);
        Assert.Contains("<<else>>", Source);
        Assert.Contains("<<set $dues_cut = true>>", Source);
        Assert.Contains("The dues are {$dues} shillings a boat now.", Source);
    }

    [Fact]
    public void Characters_and_events_are_node_groups()
    {
        string[] groups = ["Events", "Brandt", "Maud", "Joss", "Pim", "Tam", "Wren", "Cat", "Cave", "Bell", "LighthouseDoor", "OilCan", "PierChatter"];
        foreach (var group in groups)
        {
            var nodes = Compilation.Nodes.Where(n => n.Title == group).ToList();
            Assert.True(nodes.Count > 1, $"{group} has {nodes.Count} node.");
            Assert.All(nodes, n => Assert.Equal(group, n.NodeGroup));
            Assert.All(nodes, n => Assert.Contains(n.Headers, h => h.Key == "when"));
        }

        var events = Compilation.Nodes.Where(n => n.Title == "Events").ToList();
        Assert.Contains(events, n => n.Headers.Any(h => h is { Key: "when", Value: "once" }));
        Assert.Contains(events, n => n.Headers.Count(h => h.Key == "when") == 3);
        Assert.Contains(Compilation.Nodes, n => n.Headers.Any(h => h is { Key: "when", Value: "always" }));

        // A condition of a group calls a function of the game
        Assert.Contains(Compilation.Nodes, n => n.Headers.Any(h => h.Key == "when" && h.Value.Contains("has_item(\"fish\")", StringComparison.Ordinal)));
    }

    [Fact]
    public void Line_groups_pick_one_line()
    {
        Assert.Contains("=> Maud: The fish don't wait, courier.", Source);
        Assert.Contains("=> Biscuit moves into the lamp room, where it is warm. <<if $cat_down and $votes_odile >= 3>>", Source);
    }

    [Fact]
    public void Some_content_plays_once_and_some_knows_it_was_visited()
    {
        Assert.Contains("<<once>>", Source);
        Assert.Contains("<<endonce>>", Source);
        Assert.Contains("visited(\"TownMeeting\")", Source);
        Assert.Contains("visited_count(\"InnMenu\")", Source);
    }

    [Fact]
    public void The_dues_use_the_standard_functions()
    {
        Assert.Contains("round(11 - dice(6) / 2)", Source);
        Assert.Contains("format(\"{0:0.0}\", $dues / 2)", Source);
    }

    [Fact]
    public void The_story_calls_the_functions_and_the_commands_of_the_game()
    {
        // Functions of the game, in a condition, in a line and in a smart variable
        Assert.Contains("has_item(\"key\")", Source);
        Assert.Contains("item_count(\"letter\")", Source);
        Assert.Contains("time_of_day()", Source);
        Assert.Equal(["day", "has_item", "item_count", "time_of_day"], story.Actions.Describe().Where(a => a.Kind == YarnActionKind.Function).Select(a => a.Name).Order(StringComparer.Ordinal));

        // Commands of the game, a command on an entity that the dialogue waits for, and Skein's own wait
        string[] commands = ["<<give letter 3>>", "<<take oil>>", "<<quest_changed lamp>>", "<<end_day>>", "<<ending keeper>>", "<<walk Pim Cat>>", "<<wait 1>>"];
        Assert.All(commands, c => Assert.Contains(c, Source));
    }

    [Fact]
    public void Lines_use_markup()
    {
        Assert.Contains("[select value={time_of_day()} morning=", Source);
        Assert.Contains("[plural value={item_count(\"letter\")} one=", Source);
        Assert.Contains("[ordinal value={$clues_found} one=", Source);
        Assert.Contains("[pause=700 /]", Source);

        // A line with a speaker
        Assert.Contains(Compilation.Nodes, n => n.Speakers.Contains("Odile"));
    }

    [Fact]
    public void Every_line_has_an_id_and_some_have_a_mood()
    {
        // A shadow line has the id of its source instead
        Assert.DoesNotContain(Compilation.Lines, l => l.Value.IsImplicitId && l.Value.ShadowOf is null);
        Assert.Contains(Compilation.Lines.Values, l => l.Metadata.Contains("mood:angry"));
        Assert.Contains(Compilation.Lines.Values, l => l.Metadata.Contains("mood:glad"));
        Assert.Contains(Compilation.Lines.Values, l => l.Metadata.Contains("mood:afraid"));
        Assert.Contains(Compilation.Lines.Values, l => l.Metadata.Contains("mood:sad"));
    }

    [Fact]
    public void A_farewell_is_a_shadow_line()
    {
        var shadow = Assert.Single(Compilation.Lines.Values, l => l.ShadowOf is not null);
        Assert.Equal("line:mind_the_tide", shadow.ShadowOf);
        Assert.Equal("Brandt: Mind the tide.", Compilation.Lines["line:mind_the_tide"].Text);
    }

    [Fact]
    public async Task Options_with_nothing_left_are_passed_over()
    {
        var english = TestStory.Brinemoor();
        await english.Start();

        // The dues, then the catch: after them Joss has nothing left to be asked
        await english.Talk("Joss");
        await english.Talk("Joss", "How is the catch?");
        english.Transcript.Clear();
        await english.Talk("Joss");

        Assert.Equal(
            [
                "line: Joss: What can I do for the post?",
                "option: Have you a fish to spare? It is for a cat. [unavailable]",
                "option: How is the catch? [unavailable]",
                "line: Joss: Back to the nets, then.",
                "complete",
            ],
            english.Transcript);
    }

    [Fact]
    public void Nodes_say_their_act_and_the_editor_has_clusters_colours_and_notes()
    {
        string[] acts = ["All", "Arrival", "Wreck", "HighTide"];
        Assert.All(Compilation.Nodes, n => Assert.Contains(Assert.Single(n.Headers, h => h.Key == "act").Value, acts));
        Assert.Contains(Compilation.Nodes, n => n.Headers.Any(h => h.Key == "scenario"));
        Assert.Contains(Compilation.Nodes, n => n.Headers.Any(h => h.Key == "quest"));
        Assert.Contains(Compilation.Nodes, n => n.Headers.Any(h => h is { Key: "cluster", Value: "Town meeting" }));
        Assert.Contains(Compilation.Nodes, n => n.Headers.Any(h => h is { Key: "color", Value: "yellow" }));
        Assert.Equal(3, Compilation.Nodes.Count(n => n.Headers.Any(h => h is { Key: "style", Value: "note" })));
    }

    [Fact]
    public void The_game_has_words_for_every_quest_item_act_and_ending()
    {
        var ids = Compilation.Lines.Keys.ToHashSet();
        foreach (var quest in ArgumentsOf("quest_changed"))
        {
            Assert.Contains(Compilation.Variables, v => v.Name == StoryDirector.QuestVariablePrefix + quest);
            Assert.Contains("line:quest_" + quest, ids);
        }

        Assert.Equal(8, ArgumentsOf("quest_changed").Count);
        Assert.All(ArgumentsOf("give"), item => Assert.Contains("line:item_" + item, ids));
        Assert.Equal(["dark", "harbourmaster", "keeper", "stranger"], ArgumentsOf("ending").Order(StringComparer.Ordinal));
        Assert.All(ArgumentsOf("ending"), ending => Assert.Contains("line:ending_" + ending, ids));
        Assert.All(new[] { 0, 1, 2 }, act => Assert.Contains("line:act_" + act, ids));
        Assert.All(Enum.GetValues<TimeOfDay>(), time => Assert.Contains("line:time_" + time.ToString().ToLowerInvariant(), ids));
    }

    [Fact]
    public void Every_line_is_translated_in_French()
    {
        var english = YarnStringsCsv.RowsOf(Compilation, "en");
        var french = StoryFiles.Strings("fr").ToDictionary(r => r.Id);
        Assert.Equal(english.Select(r => r.Id).Order(StringComparer.Ordinal), french.Keys.Order(StringComparer.Ordinal));

        var status = YarnStringsStatus.Of(english, [.. french.Values]);
        Assert.Equal((english.Count, 0, 0, 0), (status.Translated, status.ToUpdate, status.Missing, status.Dead));

        foreach (var row in english)
        {
            var translation = french[row.Id].Text;
            Assert.False(string.IsNullOrWhiteSpace(translation), row.Id);

            // The values and the markup of a line are in its translation too
            Assert.Equal(Count(row.Text, "{0}"), Count(translation, "{0}"));
            Assert.Equal(Count(row.Text, "["), Count(translation, "["));
            Assert.Equal(Count(row.Text, "[pause="), Count(translation, "[pause="));
            Assert.Equal(SpeakerOf(row.Text), SpeakerOf(translation));
        }

        // A French line reads through the markup, with its plural
        var line = story.Lines.GetLine("line:094217f", ["2"]);
        Assert.Equal("Brandt: Pour le maître du port : 2 mains.", line.Text.Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("fr")]
    public void Only_the_people_of_the_island_speak(string? language)
    {
        // A colon in a line that nobody says is written "\:", or what is before it would be the name of a speaker
        string?[] cast = [null, "Brandt", "Maud", "Joss", "Pim", "Tam", "Wren", "Odile"];
        var lines = TestStory.Brinemoor(language: language).Lines;

        foreach (var (id, info) in Compilation.Lines)
        {
            var line = lines.GetLine(id, [.. Enumerable.Repeat("2", Count(info.Text, "{"))]);
            Assert.Equal(language ?? "en", line.Language);
            Assert.Contains(line.CharacterName, cast);
            Assert.DoesNotContain('\\', line.TextWithoutCharacterName.Text);
        }
    }

    // The first argument of each use of a command in the scripts
    private static List<string> ArgumentsOf(string command)
    {
        var arguments = new List<string>();
        var start = $"<<{command} ";
        for (var at = Source.IndexOf(start, StringComparison.Ordinal); at >= 0; at = Source.IndexOf(start, at + 1, StringComparison.Ordinal))
        {
            var from = at + start.Length;
            var to = Source.IndexOfAny([' ', '>'], from);
            arguments.Add(Source[from..to]);
        }

        return [.. arguments.Distinct()];
    }

    private static int Count(string text, string part)
    {
        var count = 0;
        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    // The name before the first colon of a line that a character says
    private static string SpeakerOf(string text)
    {
        string[] speakers = ["Brandt", "Maud", "Joss", "Pim", "Tam", "Wren", "Odile"];
        return speakers.FirstOrDefault(s => text.StartsWith(s + ": ", StringComparison.Ordinal)) ?? "";
    }
}
