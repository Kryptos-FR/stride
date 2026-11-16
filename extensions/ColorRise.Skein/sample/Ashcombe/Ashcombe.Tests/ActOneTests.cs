namespace Ashcombe.Tests;

/// <summary>
/// Act I, the survey of the village: the five landmarks (three of them in child scenes), the moving stones, the
/// quarrel over the road, and the three optional quests; the act ends with the survey done, or on the second night.
/// </summary>
[Collection(StoryCollection.Name)]
public sealed class ActOneTests
{
    [Fact]
    public async Task The_survey_and_the_stones_done_on_the_first_day_send_the_cartographer_to_the_castle()
    {
        var story = TestStory.Ashcombe();
        await story.Start();
        Assert.Contains("\"Survey the village before the spring fair. Five landmarks, set down true. The hearing will want them.\"", story.TakeLines());
        Assert.Equal(QuestState.Active, story.Quest("survey"));

        await story.Talk("Maren", "I will watch them.");
        Assert.Equal(QuestState.Active, story.Quest("stones"));

        await SurveyTheVillage(story);
        Assert.Equal(QuestState.Succeeded, story.Quest("survey"));
        Assert.Equal(5, story.Director.Map.Landmarks.Count);
        Assert.Equal(["Cellar", "Chapel", "Inn", "Mill", "Village"], story.Director.Map.Places.Order());

        await story.Until(TimeOfDay.Night);
        await story.Talk("Stones");
        Assert.Contains("Lio: There. Where grandmother said. The old line runs under the hill, not along the river.", story.TakeLines());

        // Naming Lio closes the stones; with the survey done, the Countess sends for the cartographer at once
        await story.Talk("Maren", "It was Lio, the miller's child.");
        Assert.Equal(1, story.Number("$culprit"));
        Assert.Equal(QuestState.Succeeded, story.Quest("stones"));
        Assert.Equal(1, story.Number("$act"));
        Assert.Equal("Places/Castle", story.Director.Whereabouts.Root);
        Assert.Equal("Castle", story.Here);
        Assert.Equal(2, story.Director.Clock.Day);
        Assert.True(story.Flag("$pell_at_castle"));
        Assert.False(story.Flag("$pell_in_village"));
    }

    [Fact]
    public async Task The_second_night_ends_the_act_with_what_is_done_and_the_peddler_blamed()
    {
        var story = TestStory.Ashcombe();
        await story.Start();
        await story.Talk("BridgeMark");
        await story.Talk("Stones");
        Assert.Equal(2, story.Director.Map.Landmarks.Count);

        await story.Until(TimeOfDay.Night);
        Assert.Equal(0, story.Number("$act"));
        while (story.Number("$act") == 0)
        {
            await story.PassTime();
        }

        // The summons brought the next morning
        Assert.Equal((3, TimeOfDay.Morning), (story.Director.Clock.Day, story.Director.Clock.Time));
        Assert.Equal(1, story.Number("$act"));
        Assert.Equal(QuestState.Failed, story.Quest("survey"));
        Assert.Equal(QuestState.Failed, story.Quest("stones"));
        Assert.Equal(2, story.Number("$culprit"));
        Assert.Equal("Castle", story.Here);
        var lines = story.TakeLines();
        Assert.Contains("You look at the map. 2 landmarks out of five. It will have to do.", lines);
        Assert.Contains("Maren: The stones moved again last night. I have my man. The peddler. Strangers always.", lines);
    }

    [Theory]
    [InlineData("Corvin is right. A straight road is a good road.", 1)]
    [InlineData("The road should go around the mill.", 2)]
    [InlineData("I draw what is there. Roads are for the hearing.", 3)]
    public async Task The_steward_and_the_miller_quarrel_once_both_have_spoken_and_the_cartographer_takes_a_side(string answer, int road)
    {
        var story = TestStory.Ashcombe();
        await story.Start();
        await story.Talk("Corvin");
        Assert.Equal(0, story.Number("$road"));

        // The quarrel is an event: it plays after the dialogue that made both meetings true
        story.Reader.Choices.Enqueue(answer);
        await story.Talk("Bram");
        Assert.Equal(road, story.Number("$road"));
        Assert.Contains("Bram: Tell him, cartographer. Tell him the old road goes around the mill and always has.", story.TakeLines());

        await story.Talk("Corvin");
        Assert.Single(story.TakeLines());
    }

    [Fact]
    public async Task The_wheel_mended_with_the_peddlers_gear_gives_the_rope()
    {
        var story = TestStory.Ashcombe();
        await story.Start();
        await story.Talk("Bram");
        Assert.Equal(QuestState.Offered, story.Quest("wheel"));
        await story.Talk("Bram", "I will find you a gear.");
        await story.Talk("Pell");
        await story.Talk("Pell", "Tell him about the salt flats in the south.");
        Assert.True(story.Director.Inventory.Has("gear"));
        Assert.Equal(1, story.Number("$pell_trust"));

        await story.Talk("Wheel");

        Assert.Equal(QuestState.Succeeded, story.Quest("wheel"));
        Assert.True(story.Director.Inventory.Has("rope"));
        Assert.False(story.Director.Inventory.Has("gear"));
        Assert.Contains("The wheel turns, and the mill hums behind it.", await Said(story, "Wheel"));
    }

    [Fact]
    public async Task Nans_herbs_from_the_green_the_river_and_the_cellar_make_the_remedy()
    {
        var story = TestStory.Ashcombe();
        await story.Start();
        await story.Use("CottageDoor");
        await story.Talk("Nan", "I will bring them.");
        await story.Use("CottageExit");
        await story.Talk("Nettle");
        await story.Talk("DewCup");
        await story.Use("InnDoor");
        await story.Use("CellarStairs", "Could you show me the cellar? The old well is a landmark.");
        Assert.Equal("Cellar", story.Here);
        await story.Talk("Hollyroot");
        await story.Walk("CellarUp", "InnExit", "CottageDoor");

        await story.Talk("Nan");

        Assert.Equal(QuestState.Succeeded, story.Quest("herbs"));
        Assert.True(story.Director.Inventory.Has("remedy"));
        Assert.False(story.Director.Inventory.Has("nettle"));
    }

    [Fact]
    public async Task The_dew_cup_closes_after_the_morning_of_the_second_day()
    {
        var story = TestStory.Ashcombe();
        await story.Start();
        await story.Use("CottageDoor");
        await story.Talk("Nan", "I will bring them.");
        await story.Use("CottageExit");
        await story.PassTime();
        Assert.Contains("The dew-cup has closed. They only open in the morning.", await Said(story, "DewCup"));

        await story.Until(TimeOfDay.Morning);
        Assert.Equal(2, story.Director.Clock.Day);
        Assert.Equal(QuestState.Active, story.Quest("herbs"));
        await story.PassTime();

        Assert.Equal(QuestState.Failed, story.Quest("herbs"));
        Assert.Contains("The dew-cups by the river have closed for the year. Nan will have to make do.", story.TakeLines());
    }

    [Fact]
    public async Task Heddas_recipe_is_found_in_the_cellar_she_shows_the_way_to_and_the_dialogue_goes_on_down_there()
    {
        var story = TestStory.Ashcombe();
        await story.Start();
        await story.Use("InnDoor");
        await story.Talk("Hedda", "I will look for it.");
        Assert.Equal(QuestState.Active, story.Quest("recipe"));
        story.TakeLines();

        await story.Use("CellarStairs", "Could you show me the cellar? The old well is a landmark.");

        // The lines after the door are said in the cellar: the inn's scene, with Hedda, stays loaded above it
        Assert.Equal(["Places/Village/Inn", "Places/Village/Cellar"], story.Director.Whereabouts.Interiors);
        Assert.Equal(["command: walk Hedda CellarStairs", "command: go CellarStairs"], story.Transcript.Where(t => t.StartsWith("command: ", StringComparison.Ordinal)));
        Assert.Equal("Hedda: And don't touch the wine.", story.TakeLines()[^1]);

        await story.Talk("RecipeBox");
        await story.Walk("CellarUp");
        await story.Talk("Hedda");
        Assert.Equal(QuestState.Succeeded, story.Quest("recipe"));

        // Hedda lets the cartographer down from then on
        await story.Use("CellarStairs");
        Assert.Equal("Cellar", story.Here);
    }

    [Fact]
    public async Task The_castle_road_and_the_mine_path_are_closed_during_the_survey()
    {
        var story = TestStory.Ashcombe();
        await story.Start();

        await story.Use("CastleRoad");
        await story.Use("MineRoad");

        Assert.Equal("Village", story.Here);
        var lines = story.TakeLines();
        Assert.Contains("The road climbs to the castle. The Countess expects you when the survey is done, not before.", lines);
        Assert.Contains("Miner: The mine is no place for visitors. Turn back.", lines);
    }

    /// <summary>
    /// Puts the five landmarks on the map: the bridge and the stones on the green, the well in the cellar under the inn,
    /// the loft of the mill, the bell of the chapel.
    /// </summary>
    internal static async Task SurveyTheVillage(TestStory story)
    {
        await story.Talk("BridgeMark");
        await story.Talk("Stones");
        await story.Use("InnDoor");
        if (story.Flag("$cellar_allowed"))
            await story.Use("CellarStairs");
        else
            await story.Use("CellarStairs", "Could you show me the cellar? The old well is a landmark.");
        await story.Talk("Well");
        await story.Walk("CellarUp", "InnExit", "MillDoor");
        await story.Talk("Loft");
        await story.Walk("MillExit", "ChapelDoor");
        await story.Talk("Bell");
        await story.Use("ChapelExit");
    }

    private static async Task<List<string>> Said(TestStory story, string node)
    {
        story.TakeLines();
        await story.Talk(node);
        return story.TakeLines();
    }
}
