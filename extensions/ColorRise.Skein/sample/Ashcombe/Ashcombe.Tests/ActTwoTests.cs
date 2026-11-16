namespace Ashcombe.Tests;

/// <summary>
/// Act II, the castle: a separate root scene. The audience, the three clues to the missing page, the steward's offer,
/// the cook's feast that sends the cartographer back to the village and back, the portraits and the peddler again.
/// </summary>
[Collection(StoryCollection.Name)]
public sealed class ActTwoTests
{
    [Fact]
    public async Task The_castle_greets_the_cartographer_by_the_side_taken_on_the_road()
    {
        var story = TestStory.Ashcombe();
        await Play.ActOne(story, road: "Corvin is right. A straight road is a good road.");

        Assert.Contains("Corvin: Our cartographer! The one with a sensible eye. The Countess is waiting in the great hall.", story.TakeLines());
        Assert.Equal(QuestState.Active, story.Quest("audience"));
        Assert.True(story.Director.Map.HasBeenTo("Castle"));
    }

    [Fact]
    public async Task The_missing_page_found_in_the_stewards_desk_ends_the_act_with_news_of_the_collapse()
    {
        var story = TestStory.Ashcombe();
        await Play.ActOne(story);
        await story.Talk("Ysolde", "Tell her about the stones.");
        Assert.Contains("Ysolde: Under the hill. That is what my grandfather said, too, when nobody listened.", story.TakeLines());
        Assert.Equal(QuestState.Succeeded, story.Quest("audience"));

        // The desk says nothing useful before the other two clues
        Assert.Contains("The steward's desk, neat as a new grave. You don't know what to look for yet.", await Said(story, "Desk"));
        await story.Talk("Ottilie");
        await story.Talk("Tomb");
        await story.Talk("Desk");

        Assert.True(story.Quest("page") == QuestState.Succeeded, string.Join("\n", story.Transcript));
        Assert.Equal(2, story.Number("$act"));
        Assert.Equal("Places/Mine", story.Director.Whereabouts.Root);
        Assert.Contains("Boy: The old gallery fell in! Foreman Gudrun says Jory is under the hill, and the lamps won't reach him.", story.TakeLines());
    }

    [Theory]
    [InlineData("Take the crowns. Draw his border.", 1, "false_map")]
    [InlineData("Refuse.", 2, null)]
    [InlineData("Refuse, and keep the letter.", 3, "letter")]
    public async Task The_stewards_offer_is_taken_refused_or_refused_with_the_letter_kept(string answer, int offer, string? item)
    {
        var story = TestStory.Ashcombe();
        await Play.ActOne(story);
        Assert.Equal(["Corvin: The Countess first, cartographer. Then come and see me in my study."], await Said(story, "Corvin"));
        await story.Talk("Ysolde");

        await Play.AnswerTheOffer(story, answer);

        Assert.Equal(offer, story.Number("$offer"));
        Assert.Equal(QuestState.Succeeded, story.Quest("offer"));
        Assert.Equal(offer == 1 ? 40 : 0, story.Director.Inventory.Count("crowns"));
        if (item is not null)
            Assert.True(story.Director.Inventory.Has(item));
    }

    [Fact]
    public async Task The_feast_sends_the_cartographer_to_the_village_and_back_for_ale_and_flour()
    {
        var story = TestStory.Ashcombe();
        await Play.ActOne(story, wheel: true);
        await story.Use("VillageRoad");
        Assert.Contains("The road goes down to the village. The Countess expects you here today.", story.TakeLines());
        Assert.Equal("Castle", story.Here);

        await story.Talk("Agathe", "I will go down to the village for you.");
        await story.Use("VillageRoad");

        // The road is a journey: the village's scene, and what changed there in a day
        Assert.Equal("Places/Village", story.Director.Whereabouts.Root);
        var lines = story.TakeLines();
        Assert.Contains("Pell's cart is gone from the green. He went up the hill to sell to the castle.", lines);
        Assert.Contains("The boundary stones stand where the sergeant left them. Nobody moved them last night.", lines);

        await story.Talk("Bram");
        await story.Use("InnDoor");
        await story.Talk("Hedda");
        await story.Walk("InnExit", "CastleRoad");
        Assert.Equal("Castle", story.Here);
        await story.Talk("Agathe");

        Assert.Equal(QuestState.Succeeded, story.Quest("feast"));
        Assert.True(story.Flag("$feast_served"));
        Assert.Equal(1, story.Number("$act"));
    }

    [Fact]
    public async Task Without_the_mended_wheel_the_miller_has_no_flour_for_the_feast()
    {
        var story = TestStory.Ashcombe();
        await Play.ActOneIdle(story);
        await story.Talk("Agathe", "I will go down to the village for you.");
        await story.Use("VillageRoad");

        Assert.Equal(["Bram: Flour? With the wheel stuck? I have none to give, not even to a Countess."], await Said(story, "Bram"));
        Assert.False(story.Director.Inventory.Has("flour"));
    }

    [Fact]
    public async Task The_portraits_are_counted_once_each_and_the_hound_counts_the_visits()
    {
        var story = TestStory.Ashcombe();
        await Play.ActOneIdle(story);
        await story.Talk("PortraitHound");
        Assert.Equal(QuestState.Active, story.Quest("portraits"));
        await story.Talk("PortraitHound");
        Assert.Equal(["You have looked at this dog 3 times now."], await Said(story, "PortraitHound"));
        Assert.Equal(1, story.Number("$portraits_seen"));

        await story.Talk("PortraitFounder");
        await story.Talk("PortraitTwins");
        await story.Talk("PortraitSaint");

        Assert.Equal(4, story.Number("$portraits_seen"));
        Assert.Equal(QuestState.Succeeded, story.Quest("portraits"));
    }

    [Fact]
    public async Task The_peddler_blamed_in_the_village_sells_at_the_castle_only_after_an_apology()
    {
        var story = TestStory.Ashcombe();
        await Play.ActOne(story, stones: "I saw nobody.");
        Assert.Equal(-2, story.Number("$pell_trust"));

        await story.Talk("Pell", "I am sorry. I should have spoken.", "Trade him a story for the ribbon.");

        Assert.Equal(QuestState.Succeeded, story.Quest("wares"));
        Assert.True(story.Director.Inventory.Has("ribbon"));
        Assert.Equal(-1, story.Number("$pell_trust"));
    }

    [Fact]
    public async Task The_evening_ends_the_act_with_the_audience_never_given()
    {
        var story = TestStory.Ashcombe();
        await Play.ActOneIdle(story);
        await story.Talk("Agathe", "I will go down to the village for you.");

        await story.Until(TimeOfDay.Evening);

        Assert.Equal(2, story.Number("$act"));
        Assert.Equal(QuestState.Failed, story.Quest("audience"));
        Assert.Equal(QuestState.Failed, story.Quest("page"));
        Assert.Equal(QuestState.Failed, story.Quest("feast"));
        Assert.Equal("Mine", story.Here);
    }

    private static async Task<List<string>> Said(TestStory story, string node)
    {
        story.TakeLines();
        await story.Talk(node);
        return story.TakeLines();
    }
}
