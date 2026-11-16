using ColorRise.Skein;

namespace Brinemoor;

/// <summary>
/// The commands and the functions the game gives to the story. Skein finds them by their attributes: the build checks
/// the scripts against them, Game Studio offers them, and the running game calls them.
/// </summary>
public static class StoryCommands
{
    /// <summary>
    /// The story the commands act on: the one of the running game.
    /// </summary>
    public static StoryDirector? Story { get; set; }

    private static StoryDirector Current => Story ?? throw new InvalidOperationException("No story is running: the game sets StoryCommands.Story when it starts.");

    [YarnCommand("give", Description = "Gives a thing to the courier: <<give oil>>, <<give letter 3>>.")]
    public static void Give(string item, int count = 1) => Current.Inventory.Give(item, count);

    [YarnCommand("take", Description = "Takes a thing from the courier: <<take oil>>.")]
    public static void Take(string item, int count = 1)
    {
        if (!Current.Inventory.Take(item, count))
            throw new YarnCommandException($"take {item} {count}", $"the courier carries {Current.Inventory.Count(item)} of it.");
    }

    [YarnCommand("quest_changed", Description = "Writes the state of a quest in the journal, after a <<set $quest_...>>: <<quest_changed lamp>>.")]
    public static void QuestChanged(string quest) => Current.QuestChanged(quest);

    [YarnCommand("end_day", Description = "Ends the day: the next morning.")]
    public static void EndDay() => Current.Clock.EndDay();

    [YarnCommand("ending", Description = "Ends the story with one of its endings: <<ending keeper>>.")]
    public static void Ending(string name) => Current.End(name);

    [YarnFunction("has_item", Description = "Whether the courier carries a thing: has_item(\"oil\").")]
    public static bool HasItem(string item) => Current.Inventory.Has(item);

    [YarnFunction("item_count", Description = "How many of a thing the courier carries: item_count(\"letter\").")]
    public static int ItemCount(string item) => Current.Inventory.Count(item);

    [YarnFunction("time_of_day", Description = "The time of the day: \"morning\", \"afternoon\", \"evening\" or \"night\".")]
    public static string TimeOfDay() => Current.Clock.TimeName;

    [YarnFunction("day", Description = "The day of the story, 1 at its start.")]
    public static int Day() => Current.Clock.Day;
}
