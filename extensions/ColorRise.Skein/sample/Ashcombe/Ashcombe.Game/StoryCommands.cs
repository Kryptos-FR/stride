using ColorRise.Skein;

namespace Ashcombe;

/// <summary>
/// The commands and the functions the game gives to the story. Skein finds them by their attributes: the build checks
/// the scripts against them, Game Studio offers them, and the running game calls them. The commands that act on an
/// entity (<c>go</c>, <c>walk</c>) are on their components: <see cref="Doorway"/>, <see cref="Talkable"/>.
/// </summary>
public static class StoryCommands
{
    /// <summary>
    /// The story the commands act on: the one of the running game, which <see cref="StorySystem"/> sets.
    /// </summary>
    public static StoryDirector? Story { get; set; }

    private static StoryDirector Current => Story ?? throw new InvalidOperationException("No story is running: the game's StorySystem sets StoryCommands.Story when it starts.");

    [YarnCommand("give", Description = "Gives a thing to the cartographer: <<give gear>>, <<give herb 3>>.")]
    public static void Give(string item, int count = 1) => Current.Inventory.Give(item, count);

    [YarnCommand("take", Description = "Takes a thing from the cartographer: <<take gear>>.")]
    public static void Take(string item, int count = 1)
    {
        if (!Current.Inventory.Take(item, count))
            throw new YarnCommandException($"take {item} {count}", $"the cartographer carries {Current.Inventory.Count(item)} of it.");
    }

    [YarnCommand("quest_changed", Description = "Writes the state of a quest in the journal, after a <<set $quest_...>>: <<quest_changed survey>>.")]
    public static void QuestChanged(string quest) => Current.QuestChanged(quest);

    [YarnCommand("end_day", Description = "Ends the day: the next morning.")]
    public static void EndDay() => Current.Clock.EndDay();

    [YarnCommand("ending", Description = "Ends the story with one of its endings: <<ending walked>>.")]
    public static void Ending(string name) => Current.End(name);

    [YarnCommand("survey", Description = "Puts a landmark on the cartographer's map: <<survey bell>>.")]
    public static void Survey(string landmark) => Current.Map.Survey(landmark);

    [YarnFunction("has_item", Description = "Whether the cartographer carries a thing: has_item(\"gear\").")]
    public static bool HasItem(string item) => Current.Inventory.Has(item);

    [YarnFunction("item_count", Description = "How many of a thing the cartographer carries: item_count(\"herb\").")]
    public static int ItemCount(string item) => Current.Inventory.Count(item);

    [YarnFunction("time_of_day", Description = "The time of the day: \"morning\", \"afternoon\", \"evening\" or \"night\".")]
    public static string TimeOfDay() => Current.Clock.TimeName;

    [YarnFunction("day", Description = "The day of the story, 1 at its start.")]
    public static int Day() => Current.Clock.Day;

    [YarnFunction("here", Description = "Where the cartographer is: the short name of the place, as \"Village\", \"Inn\", \"Castle\" or \"Deep\".")]
    public static string Here() => Current.Whereabouts.Here;

    [YarnFunction("been_to", Description = "Whether the cartographer has been to a place: been_to(\"Castle\").")]
    public static bool BeenTo(string place) => Current.Map.HasBeenTo(place);

    [YarnFunction("surveyed", Description = "Whether a landmark is on the map: surveyed(\"bell\").")]
    public static bool Surveyed(string landmark) => Current.Map.IsSurveyed(landmark);

    [YarnFunction("survey_count", Description = "How many landmarks are on the map.")]
    public static int SurveyCount() => Current.Map.Landmarks.Count;
}
