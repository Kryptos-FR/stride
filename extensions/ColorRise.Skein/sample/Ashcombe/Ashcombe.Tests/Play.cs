namespace Ashcombe.Tests;

/// <summary>
/// Pieces of a playthrough that the tests of the later acts start from.
/// </summary>
internal static class Play
{
    /// <summary>
    /// The survey of the village: the bridge and the stones on the green, the well in the cellar under the inn, the
    /// loft of the mill, the bell of the chapel.
    /// </summary>
    public static Task SurveyTheVillage(TestStory story) => ActOneTests.SurveyTheVillage(story);

    /// <summary>
    /// The wheel mended with Pell's gear: the rope of Act III.
    /// </summary>
    public static async Task MendTheWheel(TestStory story)
    {
        if (story.Quest("wheel") is null)
            await story.Talk("Bram");
        await story.Talk("Bram", "I will find you a gear.");
        if (!story.Flag("$pell_met"))
            await story.Talk("Pell");
        await story.Talk("Pell", "Tell him about the salt flats in the south.");
        await story.Talk("Wheel");
        Assert.True(story.Director.Inventory.Has("rope"));
    }

    /// <summary>
    /// Nan's herbs, from the green, the river and the cellar: the remedy of Act III.
    /// </summary>
    public static async Task GatherTheHerbs(TestStory story)
    {
        await story.Use("CottageDoor");
        await story.Talk("Nan", "I will bring them.");
        await story.Use("CottageExit");
        await story.Talk("Nettle");
        await story.Talk("DewCup");
        await story.Use("InnDoor");
        await story.Use("CellarStairs", "Could you show me the cellar? The old well is a landmark.");
        await story.Talk("Hollyroot");
        await story.Walk("CellarUp", "InnExit", "CottageDoor");
        await story.Talk("Nan");
        await story.Use("CottageExit");
        Assert.True(story.Director.Inventory.Has("remedy"));
    }

    /// <summary>
    /// A first act played in full: the survey, the stones (with the given answer to the sergeant), the road (with the
    /// given side), and whichever optional quests are asked for. It ends at the castle.
    /// </summary>
    public static async Task ActOne(TestStory story, string road = "The road should go around the mill.", string stones = "It was Lio, the miller's child.", bool wheel = false, bool herbs = false)
    {
        await story.Start();
        await story.Talk("Maren", "I will watch them.");
        await story.Talk("Corvin");
        story.Reader.Choices.Enqueue(road);
        await story.Talk("Bram");
        if (wheel)
            await MendTheWheel(story);
        if (herbs)
            await GatherTheHerbs(story);
        await SurveyTheVillage(story);
        await story.Until(TimeOfDay.Night);
        await story.Talk("Stones");
        await story.Talk("Maren", stones);
        Assert.Equal("Castle", story.Here);
    }

    /// <summary>
    /// A first act where nothing is done: the second night ends it.
    /// </summary>
    public static async Task ActOneIdle(TestStory story)
    {
        await story.Start();
        while (story.Number("$act") == 0)
        {
            await story.PassTime();
        }

        Assert.Equal("Castle", story.Here);
    }

    /// <summary>
    /// The audience and the three clues to the missing page. The page found ends the act: the cartographer goes to the
    /// mine.
    /// </summary>
    public static async Task FindThePage(TestStory story)
    {
        await story.Talk("Ysolde", "Say nothing more.");
        await story.Talk("Ottilie");
        await story.Talk("Tomb");
        await story.Talk("Desk");
    }

    /// <summary>
    /// The steward's offer, answered.
    /// </summary>
    public static async Task AnswerTheOffer(TestStory story, string answer)
    {
        await story.Talk("Corvin", answer);
    }

    /// <summary>
    /// A second act: the audience, the steward's offer answered, and the page found, which ends the act.
    /// </summary>
    public static async Task ActTwo(TestStory story, string offer = "Refuse.")
    {
        await story.Talk("Ysolde", "Say nothing more.");
        await AnswerTheOffer(story, offer);
        await story.Talk("Ottilie");
        await story.Talk("Tomb");
        await story.Talk("Desk");
        Assert.Equal("Mine", story.Here);
    }

    /// <summary>
    /// A third act down the old shaft (the rope), or the flooded gallery (the remedy), or the long way: the steward
    /// answered when he is there, Jory found, the carvings copied, back up to the camp. It ends at the fair.
    /// </summary>
    /// <param name="corvin">The answer to the steward in the gallery, or null to pass him by.</param>
    public static async Task ActThree(TestStory story, string? corvin = "Leave him for the miners.")
    {
        await story.Use("Lift");
        if (corvin is not null && story.Flag("$corvin_in_mine"))
            await story.Talk("Corvin", corvin);
        if (story.Director.Inventory.Has("rope"))
        {
            await story.Use("OldShaft");
        }
        else if (story.Director.Inventory.Has("remedy"))
        {
            await story.Use("FloodedPassage");
            await story.Use("FloodedOn");
        }
        else
        {
            await story.Use("LongWay");
        }

        await story.Talk("Jory");
        await story.Talk("Carvings");
        await story.Use("DeepLift");
        Assert.Equal(3, story.Number("$act"));
        Assert.Equal("Village", story.Here);
    }
}
