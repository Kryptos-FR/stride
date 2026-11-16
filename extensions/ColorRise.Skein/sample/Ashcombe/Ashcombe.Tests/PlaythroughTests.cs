namespace Ashcombe.Tests;

/// <summary>
/// The story played from its first event to each of its six endings, through the four acts and their scenes, without
/// the game: the choices are made by the text of their option, the cartographer talks, uses doors and lets time pass
/// through the game's director. And the optional quests: all done once, all skipped once.
/// </summary>
[Collection(StoryCollection.Name)]
public sealed class PlaythroughTests
{
    [Fact]
    public async Task The_true_border_and_the_road_around_the_mill_when_the_steward_is_left_under_the_hill()
    {
        var story = TestStory.Ashcombe();
        await Play.ActOne(story, road: "The road should go around the mill.", wheel: true);
        await Play.ActTwo(story, offer: "Refuse.");
        await Play.ActThree(story, corvin: "Leave him for the miners.");
        Assert.False(story.Flag("$corvin_at_fair"));

        await story.Talk("Bram", "I will speak for the mill.");
        story.TakeLines();
        await story.Talk("Hearing", "Speak for the mill. The road should go around it.", "Draw the carved border, from the stones under the hill.");

        Assert.Equal("true_around", story.Director.Ending);
        Assert.Equal(QuestState.Succeeded, story.Quest("border"));
        Assert.Equal(QuestState.Succeeded, story.Quest("mill"));
        var lines = story.TakeLines();
        Assert.DoesNotContain("Corvin: The castle has a map of its own. An old one, from the archive. Please. Look at the line.", lines);
        Assert.Contains("Anselm: The charter's page says the same. The abbey cannot argue with its own founder's hand.", lines);
        Assert.Contains(lines, l => l.StartsWith("Jory ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_true_border_and_the_road_through_the_village_when_the_steward_was_helped_and_nobody_spoke_for_the_mill()
    {
        var story = TestStory.Ashcombe();
        await Play.ActOne(story, road: "Corvin is right. A straight road is a good road.", wheel: true);
        await Play.ActTwo(story, offer: "Refuse.");
        await Play.ActThree(story, corvin: "Lift the timber.");
        Assert.True(story.Flag("$corvin_at_fair"));
        await story.Talk("Bram", "The map speaks for itself.");

        story.TakeLines();
        await story.Talk("Hearing", "Draw the carved border, from the stones under the hill.");

        Assert.Equal("true_through", story.Director.Ending);
        Assert.Contains("Corvin: You pulled me out of the dark. I won't call you a liar in front of the valley. The castle withdraws its map.", story.TakeLines());
    }

    [Fact]
    public async Task Speaking_for_the_mill_turns_the_stewards_road_around_it()
    {
        var story = TestStory.Ashcombe();
        await Play.ActOne(story, road: "Corvin is right. A straight road is a good road.", wheel: true);
        await Play.ActTwo(story, offer: "Refuse.");
        await Play.ActThree(story, corvin: "Leave him for the miners.");
        await story.Talk("Bram", "I will speak for the mill.");

        await story.Talk("Hearing", "Speak for the mill. The road should go around it.", "Draw the carved border, from the stones under the hill.");

        Assert.Equal("true_around", story.Director.Ending);
    }

    [Fact]
    public async Task The_stewards_border_when_the_cartographer_took_his_crowns()
    {
        var story = TestStory.Ashcombe();
        await Play.ActOne(story, wheel: true);
        await Play.ActTwo(story, offer: "Take the crowns. Draw his border.");
        await Play.ActThree(story, corvin: null);

        await story.Talk("Hearing", "Draw the steward's border.");

        Assert.Equal("forged", story.Director.Ending);
        Assert.Equal(2, story.Number("$border"));
        Assert.Contains("You leave the valley with forty crowns and a map you will never show anyone.", story.TakeLines());
    }

    [Fact]
    public async Task The_stewards_border_when_he_presents_it_and_nothing_proves_it_forged()
    {
        var story = TestStory.Ashcombe();
        await Play.ActOne(story, wheel: true);
        await Play.ActTwo(story, offer: "Refuse.");

        // Past the steward in the gallery without a word: he comes up on his own, with his map
        await Play.ActThree(story, corvin: null);
        Assert.Equal(0, story.Number("$corvin_fate"));

        await story.Talk("Hearing", "Draw the carved border, from the stones under the hill.");

        Assert.Equal("forged", story.Director.Ending);
        Assert.Contains("Anselm: The abbey would rather have the steward's meadows than a ghost's carvings.", story.TakeLines());
    }

    [Theory]
    [InlineData("Refuse, and keep the letter.", "Lift the timber.", "You lay the steward's letter on the table. \"Draw it so, and it is worth forty crowns to you.\" In his own hand.")]
    [InlineData("Refuse.", "Lift the timber, and take the notebook while he is dazed.", "You lay the steward's notebook on the table, open. The same border, drawn again and again, older-looking each time.")]
    public async Task The_steward_exposed_by_his_letter_or_his_notebook(string offer, string corvin, string proof)
    {
        var story = TestStory.Ashcombe();
        await Play.ActOne(story, wheel: true);
        await Play.ActTwo(story, offer: offer);
        await Play.ActThree(story, corvin: corvin);

        await story.Talk("Hearing", "Draw the carved border, from the stones under the hill.");

        Assert.Equal("exposed", story.Director.Ending);
        Assert.Contains(proof, story.TakeLines());
    }

    [Fact]
    public async Task The_walked_border_with_Lios_map_beside_it()
    {
        var story = TestStory.Ashcombe();
        await Play.ActOne(story, stones: "I saw nobody.", herbs: true);
        await Play.ActTwo(story);
        await Play.ActThree(story);
        Assert.Equal(2, story.Number("$way"));
        await story.Use("MillDoor");
        await story.Talk("Lio");
        await story.Use("MillExit");

        story.TakeLines();
        await story.Talk("Hearing", "Draw the path the villagers walk, from stone to stone.");

        Assert.Equal("walked", story.Director.Ending);
        var lines = story.TakeLines();
        Assert.Contains("You lay Lio's map beside it. The two lines agree almost everywhere.", lines);
        Assert.Contains("Lio's first map hangs behind Hedda's bar, framed, with the old line in red.", lines);
    }

    [Fact]
    public async Task The_hearing_fails_with_a_map_full_of_holes_and_every_optional_quest_skipped()
    {
        var story = TestStory.Ashcombe();
        await Play.ActOneIdle(story);
        await story.Until(TimeOfDay.Evening);
        Assert.Equal("Mine", story.Here);

        // No rope, no remedy: the long way, with no light left for Jory nor the carvings after the steward is helped
        await story.Use("Lift");
        await story.Talk("Corvin", "Lift the timber.");
        await story.Use("LongWay");
        Assert.Equal(3, story.Number("$act"));
        Assert.Equal("Village", story.Here);

        story.TakeLines();
        await story.Talk("Hearing");

        Assert.Equal("failed", story.Director.Ending);
        var lines = story.TakeLines();
        Assert.Contains("Anselm: A map with holes is no map. The abbey will not sign this.", lines);
        Assert.Contains(lines, l => l.Contains("Jory", StringComparison.Ordinal) && !l.StartsWith("Jory ", StringComparison.Ordinal));
        Assert.Equal(QuestState.Failed, story.Quest("border"));
        Assert.Equal(QuestState.Failed, story.Quest("jory"));
        foreach (var quest in new[] { "wheel", "herbs", "recipe", "feast", "portraits", "wares", "tally", "echo", "lio_map", "last_sale" })
        {
            Assert.True(story.Quest(quest) is QuestState.NotStarted or QuestState.Offered or QuestState.Failed, $"{quest} is {story.Quest(quest)}");
        }

    }

    [Fact]
    public async Task Every_quest_done_once_on_the_way_to_the_true_border()
    {
        var story = TestStory.Ashcombe();
        await EveryQuest(story);

        Assert.Equal("true_around", story.Director.Ending);
        foreach (var quest in new[] { "survey", "stones", "wheel", "herbs", "recipe", "audience", "page", "offer", "feast", "portraits", "wares", "jory", "carvings", "corvin", "tally", "echo", "border", "mill", "lio_map", "last_sale" })
        {
            Assert.True(story.Quest(quest) == QuestState.Succeeded, $"{quest} is {story.Quest(quest)}");
        }

        Assert.Empty(story.Errors);
    }

    [Fact]
    public async Task Every_quest_done_once_in_French()
    {
        var story = TestStory.Ashcombe(language: "fr");
        await EveryQuest(story);

        Assert.Equal("true_around", story.Director.Ending);
        Assert.Equal(QuestState.Succeeded, story.Quest("echo"));
        Assert.Empty(story.Errors);
        var lines = story.Reader.Said;
        Assert.Contains("Vous comptez les jetons sur les chevilles. 11 mineurs sont remontés.", lines);
        Assert.Contains("Une cheville est vide, la 12e. Celle de Jory.", lines);
        Assert.Contains("Écho: Juste. Juste. Juste...", lines);
        Assert.Contains("La vraie frontière entre dans la charte, et la nouvelle route contourne le moulin, par le long chemin, l'ancien chemin.", lines);
        Assert.Contains("Le gâteau de foire d'Hedda, de la recette de sa mère, a disparu avant midi.", lines);

        // Not one line of the story nor of the echo was said in English
        var english = story.EnglishTexts();
        Assert.DoesNotContain(lines, english.Contains);
    }

    [Fact]
    public async Task The_hearing_fails_in_French_with_the_number_of_landmarks()
    {
        var story = TestStory.Ashcombe(language: "fr");
        await Play.ActOneIdle(story);
        await story.Until(TimeOfDay.Evening);
        await story.Use("Lift");
        await story.Talk("Corvin", "Lift the timber.");
        await story.Use("LongWay");

        story.TakeLines();
        await story.Talk("Hearing");

        Assert.Equal("failed", story.Director.Ending);
        var lines = story.TakeLines();
        Assert.Contains("Vous déroulez votre carte. 0 repère, et du parchemin vierge là où devrait être le reste.", lines);
        Assert.Contains("Anselm: Une carte trouée n'est pas une carte. L'abbaye ne signera pas ceci.", lines);
    }

    /// <summary>
    /// Every quest of the story done once, on the way to the true border with the road around the mill.
    /// </summary>
    private static async Task EveryQuest(TestStory story)
    {

        // Act I: the wheel, the herbs, the recipe
        await story.Start();
        await story.Talk("Maren", "I will watch them.");
        await story.Talk("Corvin");
        story.Reader.Choices.Enqueue("The road should go around the mill.");
        await story.Talk("Bram");
        await Play.MendTheWheel(story);
        await Play.GatherTheHerbs(story);
        await story.Use("InnDoor");
        await story.Talk("Hedda", "I will look for it.");
        await story.Use("CellarStairs");
        await story.Talk("RecipeBox");
        await story.Talk("Well");
        await story.Use("CellarUp");
        await story.Talk("Hedda");
        await story.Use("InnExit");
        await Play.SurveyTheVillage(story);
        await story.Until(TimeOfDay.Night);
        await story.Talk("Stones");
        await story.Talk("Maren", "I saw nobody.");
        Assert.Equal("Castle", story.Here);

        // Act II: the portraits, the wares, the feast, then the page
        await story.Talk("Ysolde", "Say nothing more.");
        await story.Talk("Corvin", "Refuse, and keep the letter.");
        foreach (var portrait in new[] { "PortraitFounder", "PortraitTwins", "PortraitSaint", "PortraitHound" })
        {
            await story.Talk(portrait);
        }

        await story.Talk("Pell", "I am sorry. I should have spoken.", "Trade him a story for the ribbon.");
        await story.Talk("Agathe", "I will go down to the village for you.");
        await story.Use("VillageRoad");
        await story.Talk("Bram");
        await story.Walk("InnDoor");
        await story.Talk("Hedda");
        await story.Walk("InnExit", "CastleRoad");
        await story.Talk("Agathe");
        await story.Talk("Ottilie");
        await story.Talk("Tomb");
        await story.Talk("Desk");
        Assert.Equal("Mine", story.Here);

        // Act III: the tally and the echo, the rope, Jory and the carvings
        await story.Talk("Gudrun");
        await story.Talk("TallyBoard");
        await story.Use("Lift");
        await story.Talk("Corvin", "Leave him for the miners.");
        await story.Use("OldShaft");
        await story.TalkToEcho("A river.", "Footsteps.", "An echo.");
        await story.Talk("Jory");
        await story.Talk("Carvings");
        await story.Use("DeepLift");
        Assert.Equal("Village", story.Here);

        // Act IV: Lio's map, Pell's last sale, the mill, and the hearing
        await story.Use("MillDoor");
        await story.Talk("Lio");
        await story.Use("MillExit");
        await story.Talk("Pell");
        await story.Talk("Bram", "I will speak for the mill.");

        // The steward was left under the hill: he does not present his map, and the letter is not needed
        await story.Talk("Hearing", "Speak for the mill. The road should go around it.", "Draw the carved border, from the stones under the hill.");
    }
}
