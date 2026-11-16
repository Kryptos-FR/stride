namespace Ashcombe.Tests;

/// <summary>
/// Act III, under the hill: the mine is a separate root scene with its levels as child scenes. The lamp's oil is the
/// clock; the way down depends on Act I; the steward is trapped in the gallery; the echo is a project of its own.
/// </summary>
[Collection(StoryCollection.Name)]
public sealed class ActThreeTests
{
    [Fact]
    public async Task The_rope_from_the_wheel_takes_the_cartographer_down_the_old_shaft_to_Jory_and_the_carvings()
    {
        var story = TestStory.Ashcombe();
        await Play.ActOne(story, wheel: true);
        await Play.FindThePage(story);
        Assert.Equal("Places/Mine", story.Director.Whereabouts.Root);
        Assert.Equal(QuestState.Active, story.Quest("jory"));
        Assert.Equal(QuestState.Active, story.Quest("corvin"));
        Assert.Contains("You think of the charter's page. \"As the carvings show.\" The carvings must be down there.", story.TakeLines());

        await story.Use("Lift");
        Assert.Equal("Gallery", story.Here);
        Assert.Equal(2, story.Number("$oil"));
        await story.Use("OldShaft");
        Assert.Equal("Deep", story.Here);
        Assert.Equal(1, story.Number("$way"));
        Assert.Contains("The lantern folk. Not ghosts. A border, written in stone.", story.TakeLines());

        await story.Talk("Jory");
        await story.Talk("Carvings");
        Assert.True(story.Flag("$jory_found"));
        Assert.True(story.Flag("$carvings_copied"));
        Assert.Equal(QuestState.Succeeded, story.Quest("page"));
        Assert.Contains("They match the charter's page. From the bridge stone, north under the hill, to the abbey's well. The true border.", story.TakeLines());

        // Up the lift: the camp, the end of the act, and the journey to the fair
        await story.Use("DeepLift");
        Assert.Equal(3, story.Number("$act"));
        Assert.Equal("Places/Village", story.Director.Whereabouts.Root);
        Assert.Contains("Gudrun: You brought him back. I won't forget it. Nobody under this hill will.", story.TakeLines());
        Assert.True(story.Flag("$fair_up"));
        Assert.Equal(QuestState.Failed, story.Quest("corvin"));
        Assert.True(story.Flag("$corvin_at_fair"));
    }

    [Fact]
    public async Task Nans_remedy_takes_the_cartographer_through_the_flooded_gallery()
    {
        var story = TestStory.Ashcombe();
        await Play.ActOne(story, herbs: true);
        await Play.FindThePage(story);
        await story.Use("Lift");
        Assert.Contains("The old shaft drops straight into the dark. Without a rope, it is a grave.", await Said(story, "OldShaft"));

        await story.Use("FloodedPassage");
        Assert.Equal("Flooded", story.Here);
        Assert.False(story.Director.Inventory.Has("remedy"));
        await story.Use("FloodedOn");

        Assert.Equal("Deep", story.Here);
        Assert.Equal(2, story.Number("$way"));
        Assert.Equal(2, story.Number("$oil"));
        Assert.Equal(["Places/Mine/Deep"], story.Director.Whereabouts.Interiors);
    }

    [Fact]
    public async Task On_the_long_way_the_oil_spent_on_the_steward_is_the_oil_that_would_have_found_Jory()
    {
        var story = TestStory.Ashcombe();
        await Play.ActOne(story);
        await Play.FindThePage(story);
        await story.Use("Lift");
        Assert.Contains("The flooded gallery is black and still. You put a hand in the water and your fingers go numb at once.", await Said(story, "FloodedPassage"));

        await story.Talk("Corvin", "Lift the timber.");
        Assert.Equal(1, story.Number("$corvin_fate"));
        Assert.Equal(1, story.Number("$oil"));
        await story.Use("LongWay");

        // In the dark: Jory lost, back up the lift, the end of the act
        Assert.Equal(0, story.Number("$oil"));
        Assert.Equal(QuestState.Failed, story.Quest("jory"));
        Assert.False(story.Flag("$jory_found"));
        Assert.Equal(QuestState.Failed, story.Quest("carvings"));
        Assert.Equal(3, story.Number("$act"));
        var lines = story.TakeLines();
        Assert.Contains("Your lamp gutters and goes out. The dark is total.", lines);
        Assert.Contains("Gudrun: The oil's out. We dig for him from this side, and we pray.", lines);
    }

    [Fact]
    public async Task The_long_way_without_the_steward_still_has_light_for_Jory()
    {
        var story = TestStory.Ashcombe();
        await Play.ActOne(story);
        await Play.FindThePage(story);
        await story.Use("Lift");
        await story.Talk("Corvin", "Leave him for the miners.");
        Assert.Equal(["Corvin says nothing now. He is saving his breath for the miners."], await Said(story, "Corvin"));
        await story.Use("LongWay");

        Assert.Equal("Deep", story.Here);
        Assert.Equal(3, story.Number("$way"));
        Assert.Equal(1, story.Number("$oil"));
        await story.Talk("Jory");
        Assert.True(story.Flag("$jory_found"));
    }

    [Theory]
    [InlineData("Lift the timber.", 1, false, true)]
    [InlineData("Lift the timber, and take the notebook while he is dazed.", 3, true, true)]
    [InlineData("Leave him for the miners.", 2, false, false)]
    public async Task The_steward_in_the_gallery_is_helped_robbed_or_left(string answer, int fate, bool notebook, bool atFair)
    {
        var story = TestStory.Ashcombe();
        await Play.ActOne(story, wheel: true);
        await Play.FindThePage(story);
        await story.Use("Lift");

        await story.Talk("Corvin", answer);

        Assert.Equal(fate, story.Number("$corvin_fate"));
        Assert.Equal(QuestState.Succeeded, story.Quest("corvin"));
        Assert.Equal(notebook, story.Director.Inventory.Has("notebook"));

        await story.Use("OldShaft");
        await story.Talk("Jory");
        await story.Talk("Carvings");
        await story.Use("DeepLift");
        Assert.Equal(atFair, story.Flag("$corvin_at_fair"));
    }

    [Fact]
    public async Task The_steward_paid_for_his_border_stays_out_of_the_mine()
    {
        var story = TestStory.Ashcombe();
        await Play.ActOne(story);
        await story.Talk("Ysolde", "Say nothing more.");
        await Play.AnswerTheOffer(story, "Take the crowns. Draw his border.");
        await story.Until(TimeOfDay.Evening);

        Assert.Equal("Mine", story.Here);
        Assert.False(story.Flag("$corvin_in_mine"));
        Assert.Equal(QuestState.NotStarted, story.Quest("corvin"));
    }

    [Fact]
    public async Task The_tally_counts_the_miners_still_under_the_hill_and_the_lift_needs_oil()
    {
        var story = TestStory.Ashcombe();
        await Play.ActOneIdle(story);
        await story.Until(TimeOfDay.Evening);
        Assert.Equal("Mine", story.Here);
        await story.Talk("Gudrun");
        await story.Talk("TallyBoard");

        var lines = story.TakeLines();
        Assert.Contains("You count the tokens on the pegs. 11 miners came up.", lines);
        Assert.Contains("One peg is empty, the 12th. Jory's.", lines);
        Assert.Equal(QuestState.Succeeded, story.Quest("tally"));
    }

    [Fact]
    public async Task The_echo_speaks_its_own_project_and_the_story_sees_it_answered()
    {
        var story = TestStory.Ashcombe();
        await Play.ActOne(story, wheel: true);
        await Play.FindThePage(story);
        await story.Talk("Gudrun");
        Assert.Equal(QuestState.Offered, story.Quest("echo"));
        await story.Use("Lift");
        await story.Use("OldShaft");

        await story.TalkToEcho("A river.", "Footsteps.", "An echo.");

        Assert.True(story.Flag("$echo_answered"));
        Assert.Equal(1, story.Number("$echo_riddles"));
        Assert.Equal(QuestState.Succeeded, story.Quest("echo"));
        var lines = story.TakeLines();
        Assert.Contains("Echo: Right. Right. Right...", lines);
        Assert.Contains("Somewhere far below, the echo has fallen silent. The miners will talk about it for a year.", lines);
    }

    [Fact]
    public async Task A_wrong_answer_brings_the_echo_back_with_its_next_riddle()
    {
        var story = TestStory.Ashcombe();
        await Play.ActOne(story, wheel: true);
        await Play.FindThePage(story);
        await story.Use("Lift");
        await story.Use("OldShaft");

        await story.TalkToEcho("A mine.", "Coins.", "A bell.");
        Assert.False(story.Flag("$echo_answered"));
        await story.TalkToEcho("A river.", "Footsteps.", "An echo.");

        Assert.True(story.Flag("$echo_answered"));
        Assert.Contains("Echo: Back again. The 2nd riddle, then.", story.TakeLines());
    }

    private static async Task<List<string>> Said(TestStory story, string node)
    {
        story.TakeLines();
        await story.Talk(node);
        return story.TakeLines();
    }
}
