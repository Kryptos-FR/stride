namespace Brinemoor.Tests;

/// <summary>
/// The sample's inventory and clock, and the commands and functions that give them to the story.
/// </summary>
[Collection(StoryCollection.Name)]
public sealed class WorldTests
{
    [Fact]
    public void The_inventory_counts_things()
    {
        var inventory = new Inventory();
        var changes = 0;
        inventory.Changed += () => changes++;

        inventory.Give("letter", 3);
        inventory.Give("oil");
        Assert.True(inventory.Take("letter"));

        Assert.Equal(2, inventory.Count("letter"));
        Assert.True(inventory.Has("oil"));
        Assert.False(inventory.Has("key"));
        Assert.Equal(3, changes);

        // Taking more than there is takes nothing
        Assert.False(inventory.Take("letter", 3));
        Assert.Equal(2, inventory.Count("letter"));
        Assert.Equal(3, changes);

        Assert.True(inventory.Take("letter", 2));
        Assert.Equal(["oil"], inventory.Items.Keys);

        inventory.Replace(new Dictionary<string, int> { ["key"] = 1, ["dust"] = 0 });
        Assert.Equal(["key"], inventory.Items.Keys);
    }

    [Fact]
    public void The_clock_goes_through_the_day()
    {
        var clock = new DayClock();
        var seen = new List<string>();
        clock.Changed += () => seen.Add($"{clock.Day} {clock.TimeName}");

        for (var i = 0; i < 5; i++)
        {
            clock.Advance();
        }

        clock.EndDay();

        Assert.Equal(["1 afternoon", "1 evening", "1 night", "2 morning", "2 afternoon", "3 morning"], seen);
    }

    [Fact]
    public async Task The_story_reads_and_changes_the_world()
    {
        var story = new TestStory("""
            title: Start
            ---
            <<give letter 3>>
            <<take letter>>
            <<give oil>>
            You carry {item_count("letter")} letters on the {time_of_day()} of day {day()}.
            <<end_day>>
            <<if has_item("oil") and not has_item("key")>>
                Day {day()}: oil, and no key.
            <<endif>>
            ===
            """);
        story.Director.Clock.Advance();

        await story.Director.Talk("Start");

        Assert.Equal(["You carry 2 letters on the afternoon of day 1.", "Day 2: oil, and no key."], story.TakeLines());
        Assert.Empty(story.Errors);
    }
}
