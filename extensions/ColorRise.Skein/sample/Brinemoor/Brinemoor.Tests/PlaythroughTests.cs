namespace Brinemoor.Tests;

/// <summary>
/// The story of the sample played from its first event to each of its four endings, without the game: the choices are
/// made by the text of their option, the courier talks and lets time pass through the game's director.
/// </summary>
[Collection(StoryCollection.Name)]
public sealed class PlaythroughTests
{
    [Fact]
    public async Task Odile_keeps_the_light_when_she_is_freed_and_three_hands_ring_for_her()
    {
        var story = TestStory.Brinemoor();
        await story.Start();
        Assert.Equal(3, story.Director.Inventory.Count("letter"));
        Assert.Equal(QuestState.Active, story.Director.StateOf("letters"));

        // Act I: the lamp, the dues, the cat
        await LightTheLamp(story);
        await CutTheDues(story);
        await GetTheCatDown(story);
        Assert.Equal(1, story.Director.Inventory.Count("letter"));

        await story.UntilNextDay();
        Assert.Equal(1, story.Number("$act"));
        Assert.True(story.Flag("$ship_anchored"));
        Assert.False(story.Flag("$wreck_aground"));
        Assert.Equal((2, TimeOfDay.Morning), (story.Director.Clock.Day, story.Director.Clock.Time));

        // Act II: the harbour's side, and the three signs
        await story.Talk("Bell", "Ask about the cargo.", "The harbour should hold it.", "Ask about the keeper.", "Ask about the stranger.", "Leave them to it.");
        Assert.Equal(QuestState.Succeeded, story.Director.StateOf("salvage"));
        await FollowTheTrail(story);

        await story.UntilNextDay();
        Assert.Equal(2, story.Number("$act"));
        Assert.Equal(QuestState.Active, story.Director.StateOf("odile"));

        // Act III: Odile comes back; Maud rings for her on her own, Joss and Pim because the courier says so
        await story.Talk("Cave", "Pull the pin. \"Come back and keep the light.\"");
        Assert.True(story.Flag("$odile_here"));
        Assert.Equal(QuestState.Succeeded, story.Director.StateOf("letters"));
        Assert.Equal(0, story.Director.Inventory.Count("letter"));

        await story.Talk("Maud");
        await story.Talk("Joss", "Ring for Odile.");
        await story.Talk("Pim", "Ring for Odile.");
        await story.Talk("Bell", "Ring the bell.");

        Assert.Equal("keeper", story.Director.Ending);
        Assert.Equal(3, story.Number("$votes_odile"));
        Assert.Equal(2, story.Number("$votes_brandt"));
        Assert.Equal(QuestState.Succeeded, story.Director.StateOf("vote"));
        Assert.Contains("Biscuit moves into the lamp room, where it is warm.", story.TakeLines());
    }

    [Fact]
    public async Task Brandt_keeps_the_light_when_the_courier_only_watches()
    {
        var story = TestStory.Brinemoor();
        await story.Start();

        await story.UntilNextDay();
        await story.UntilNextDay();
        Assert.Equal(QuestState.Failed, story.Director.StateOf("salvage"));
        Assert.Null(story.Director.Ending);

        // The night of the third day rings the bell without the courier
        await story.UntilNextDay();

        Assert.Equal("harbourmaster", story.Director.Ending);
        Assert.Equal(3, story.Number("$votes_brandt"));
        Assert.Equal(3, story.Director.Clock.Day);
        var lines = story.TakeLines();
        Assert.Contains("On the roof of the Anchor, a ginger cat watches the bell and does not come down.", lines);
        Assert.Contains("The cargo sits in the harbour store, and nobody says whose it is.", lines);
    }

    [Fact]
    public async Task Wren_keeps_the_light_when_she_trusts_the_courier_and_three_hands_ring_for_her()
    {
        var story = TestStory.Brinemoor();
        await story.Start();

        // Act I: the dues, and a dark lamp
        await CutTheDues(story);
        await story.UntilNextDay();
        Assert.True(story.Flag("$wreck_aground"));

        // Act II: the truth and a bowl of stew win Wren's trust; the courier speaks for the boats
        await story.Talk("Wren", "Nobody did. I was too late.");
        await story.Talk("Tam", "Give me the bowl. I will take it to her.");
        await story.Talk("Wren");
        Assert.Equal(2, story.Number("$wren_trust"));
        Assert.Equal(QuestState.Succeeded, story.Director.StateOf("meal"));
        await story.Talk("Bell", "Ask about the cargo.", "The boats should have it.", "Leave them to it.");

        await story.UntilNextDay();

        // Act III: she says who she is, and stands
        await story.Talk("Wren", "Stand at the bell. I will speak for you.");
        await story.Talk("Maud", "Ring for Wren.");
        await story.Talk("Joss", "Ring for Wren.");
        await story.Talk("Tam", "Ring for Wren.");
        await story.Talk("Bell", "Ring the bell.");

        Assert.Equal("stranger", story.Director.Ending);
        Assert.Equal(3, story.Number("$votes_wren"));
        Assert.Contains(story.Transcript, t => t == "command: walk Wren Bell");
    }

    [Fact]
    public async Task Nobody_keeps_the_light_when_no_one_has_three_hands()
    {
        var story = TestStory.Brinemoor();
        await story.Start();

        // Act I: the lamp and the cat
        await LightTheLamp(story);
        await GetTheCatDown(story);
        await story.UntilNextDay();

        // Act II: no side; the stew
        await story.Talk("Bell", "Ask about the cargo.", "I carry letters. I take no side.", "Leave them to it.");
        Assert.Equal(QuestState.Active, story.Director.StateOf("salvage"));
        await story.Talk("Wren", "I lit it myself. The keeper is missing.");
        await story.Talk("Tam", "Give me the bowl. I will take it to her.");
        await story.Talk("Wren");
        await story.UntilNextDay();
        Assert.Equal(QuestState.Failed, story.Director.StateOf("salvage"));
        Assert.Equal(QuestState.NotStarted, story.Director.StateOf("odile"));

        // Act III: Wren is told not to stand, and the two who listen are told to ring for nobody
        await story.Talk("Wren", "The island should choose one of its own.");
        Assert.False(story.Flag("$wren_stands"));
        await story.Talk("Tam", "Ring for nobody.");
        await story.Talk("Pim", "Ring for nobody.");
        await story.Talk("Bell", "Not yet.");
        Assert.Null(story.Director.Ending);
        await story.Talk("Bell", "Ring the bell.");

        Assert.Equal("dark", story.Director.Ending);
        Assert.Equal(1, story.Number("$votes_brandt"));
        Assert.Equal(QuestState.Failed, story.Director.StateOf("vote"));
    }

    [Fact]
    public async Task The_night_falls_on_a_dark_lamp_and_the_ship_strikes_the_rocks()
    {
        var story = TestStory.Brinemoor();
        await story.Start();
        await story.Talk("LighthouseDoor");
        Assert.Equal(QuestState.Active, story.Director.StateOf("lamp"));
        await story.Talk("Brandt", "I will light the lamp. Give me the key.");
        await story.Talk("LighthouseDoor");
        Assert.Contains("The key fits. But the lamp is dry: it needs oil.", story.TakeLines());

        await story.UntilNextDay();

        Assert.Equal(QuestState.Failed, story.Director.StateOf("lamp"));
        Assert.True(story.Flag("$wreck_aground"));
        Assert.False(story.Flag("$lamp_lit"));
        Assert.Equal(1, story.Number("$act"));
        var night = story.TakeLines();
        Assert.Contains("Night falls on a dark lamp.", night);
        Assert.Contains(night, l => l.StartsWith("Morning. The ship lies broken on the north rocks", StringComparison.Ordinal));

        // The stranger was on the ship
        await story.Talk("Wren", "Ask the harbourmaster.");
        Assert.Contains("Wren: You are the post. I was on that ship when she struck. I would like to know why.", story.TakeLines());
        Assert.Equal(0, story.Number("$wren_trust"));
    }

    [Fact]
    public async Task The_story_plays_in_French()
    {
        var story = TestStory.Brinemoor(language: "fr");
        await story.Start();
        var opening = story.TakeLines();
        Assert.Equal(3, opening.Count);
        Assert.DoesNotContain(opening, l => l.Contains("causeway", StringComparison.Ordinal));

        await LightTheLamp(story);
        await CutTheDues(story);
        await GetTheCatDown(story);
        await story.UntilNextDay();
        await story.Talk("Bell", "Parler de la cargaison.", "Elle revient aux bateaux.", "Les laisser discuter.");
        await FollowTheTrail(story);
        await story.UntilNextDay();
        await story.UntilNextDay();

        Assert.NotNull(story.Director.Ending);
        Assert.Empty(story.Errors);
    }

    private static async Task LightTheLamp(TestStory story)
    {
        await story.Talk("LighthouseDoor");
        Assert.Equal(QuestState.Active, story.Director.StateOf("lamp"));

        // Brandt and Maud each have a letter to take and something for the lamp
        await story.Talk("Brandt");
        await story.Talk("Brandt");
        await story.Talk("Maud");
        await story.Talk("Maud");
        await story.Talk("OilCan");
        Assert.True(story.Director.Inventory.Has("key"));
        Assert.True(story.Director.Inventory.Has("oil"));
        Assert.True(story.Flag("$letter_brandt"));
        Assert.True(story.Flag("$letter_maud"));

        await story.Talk("LighthouseDoor");
        Assert.True(story.Flag("$lamp_lit"));
        Assert.False(story.Director.Inventory.Has("oil"));
        Assert.Equal(QuestState.Succeeded, story.Director.StateOf("lamp"));
    }

    private static async Task CutTheDues(TestStory story)
    {
        await story.Talk("Joss");
        Assert.True(story.Flag("$dues_asked"));

        // Brandt may have a letter to take or a key to give first
        while (!story.Flag("$dues_settled"))
        {
            await story.Talk("Brandt");
        }

        Assert.True(story.Flag("$dues_cut"));
        Assert.InRange(story.Number("$dues"), 8, 11);
    }

    private static async Task GetTheCatDown(TestStory story)
    {
        await story.Talk("Cat");
        await story.Talk("Pim");
        Assert.Equal(QuestState.Active, story.Director.StateOf("cat"));

        // Joss may have the dues to complain about first
        for (var i = 0; i < 2 && !story.Director.Inventory.Has("fish"); i++)
        {
            await story.Talk("Joss");
        }

        Assert.True(story.Director.Inventory.Has("fish"));
        await story.Talk("Cat");
        Assert.True(story.Flag("$cat_down"));
        Assert.Equal(QuestState.Succeeded, story.Director.StateOf("cat"));
        Assert.False(story.Director.CanTalk("Cat"));
    }

    private static async Task FollowTheTrail(TestStory story)
    {
        await story.Talk("Boot");
        await story.Talk("LighthouseDoor");
        Assert.Equal(QuestState.Active, story.Director.StateOf("trail"));
        Assert.True(story.Director.Inventory.Has("pages"));
        await story.Talk("Lantern");

        // The third sign makes the event that ends the trail
        Assert.Equal(3, story.Number("$clues_found"));
        Assert.Equal(QuestState.Succeeded, story.Director.StateOf("trail"));
    }
}
