namespace Brinemoor.Tests;

/// <summary>
/// The sample's director, journal and save on a small story of ten nodes: the events of the story play when their
/// conditions turn true and nothing else plays.
/// </summary>
[Collection(StoryCollection.Name)]
public sealed class StoryDirectorTests
{
    private const string Stub = """
        title: Story
        ---
        <<enum QuestState>>
            <<case NotStarted>>
            <<case Offered>>
            <<case Active>>
            <<case Succeeded>>
            <<case Failed>>
        <<endenum>>
        <<declare $quest_lamp = QuestState.NotStarted>>
        <<declare $lamp_can_be_lit = has_item("oil") and has_item("key")>>
        ===
        title: Odile
        when: $quest_lamp == QuestState.NotStarted
        ---
        Odile: The lamp is out. Find oil and the key.
        <<set $quest_lamp = QuestState.Active>>
        <<quest_changed lamp>>
        ===
        title: Odile
        when: $quest_lamp == QuestState.Active
        ---
        Odile: Still dark up there.
        ===
        title: OilCan
        ---
        You take the oil.
        <<give oil>>
        You wipe your hands.
        ===
        title: KeyHook
        ---
        <<give key>>
        The key is cold.
        ===
        title: Lamp
        ---
        <<if $lamp_can_be_lit>>
            <<take oil>>
            <<set $quest_lamp = QuestState.Succeeded>>
            <<quest_changed lamp>>
            The lamp burns.
        <<else>>
            The lamp is cold.
        <<endif>>
        ===
        title: Sleep
        ---
        <<end_day>>
        ===
        title: Events
        when: once
        when: $lamp_can_be_lit
        ---
        You have all the lamp needs.
        ===
        title: Events
        when: time_of_day() == "night"
        when: $quest_lamp == QuestState.Active
        ---
        Night falls on a dark lamp.
        <<set $quest_lamp = QuestState.Failed>>
        <<quest_changed lamp>>
        ===
        title: Events
        when: once
        when: $quest_lamp == QuestState.Succeeded
        ---
        <<ending keeper>>
        ===
        """;

    [Fact]
    public async Task An_event_plays_when_its_conditions_turn_true()
    {
        var story = new TestStory(Stub);

        await story.Director.RunEvents();
        Assert.Empty(story.Transcript);

        Assert.True(await story.Director.Talk("OilCan"));
        Assert.Equal(["You take the oil.", "You wipe your hands."], story.TakeLines());

        // The key makes the smart variable true: the event follows the dialogue
        Assert.True(await story.Director.Talk("KeyHook"));
        Assert.Equal(["line: The key is cold.", "complete", "line: You have all the lamp needs.", "complete"], story.Transcript);

        // Once: it does not play again
        story.Transcript.Clear();
        await story.Director.RunEvents();
        Assert.Empty(story.Transcript);
        Assert.Empty(story.Errors);
    }

    [Fact]
    public async Task Nothing_else_plays_while_a_dialogue_plays()
    {
        var story = new TestStory(Stub);
        story.Director.Inventory.Give("oil");
        var during = new List<object>();
        story.Reader.OnLine = async line =>
        {
            if (line.Text.Text != "The key is cold.")
                return;

            // The key is given and the event is true, but the dialogue is not over
            during.Add(story.Director.IsBusy);
            during.Add(story.Session.HasSalientContent(StoryDirector.EventsGroup));
            await story.Director.RunEvents();
            during.Add(await story.Director.Talk("Odile"));
            during.Add(story.Director.CanTalk("Odile"));
            during.Add(story.Transcript.Count);
        };

        await story.Director.Talk("KeyHook");

        Assert.Equal([true, true, false, false, 1], during);
        Assert.Equal(["line: The key is cold.", "complete", "line: You have all the lamp needs.", "complete"], story.Transcript);
        Assert.False(story.Director.IsBusy);
    }

    [Fact]
    public async Task A_fact_of_the_world_plays_its_event()
    {
        var story = new TestStory(Stub);
        await story.Director.Talk("Odile");
        story.Transcript.Clear();

        story.Director.Clock.Advance();
        await story.Director.RunEvents();
        Assert.Empty(story.Transcript);

        story.Director.Clock.Advance();
        story.Director.Clock.Advance();
        await story.Director.RunEvents();

        Assert.Equal(["Night falls on a dark lamp."], story.TakeLines());
        Assert.Equal(QuestState.Failed, story.Director.StateOf("lamp"));
        Assert.Equal([new JournalEntry(1, "lamp", QuestState.Active), new JournalEntry(1, "lamp", QuestState.Failed)], story.Director.Journal.Entries);
        Assert.Empty(story.Director.Journal.Open);
    }

    [Fact]
    public async Task The_functions_of_the_game_answer_before_any_dialogue()
    {
        var story = new TestStory(Stub);
        Assert.False(story.Session.HasSalientContent(StoryDirector.EventsGroup));

        story.Director.Inventory.Give("oil");
        story.Director.Inventory.Give("key");

        Assert.True(story.Session.HasSalientContent(StoryDirector.EventsGroup));
        await story.Director.RunEvents();
        Assert.Equal(["You have all the lamp needs."], story.TakeLines());
        Assert.Empty(story.Errors);
    }

    [Fact]
    public async Task Quest_changed_writes_the_journal()
    {
        var story = new TestStory(Stub);
        var told = new List<JournalEntry>();
        story.Director.Journal.Added += told.Add;
        Assert.Equal(QuestState.NotStarted, story.Director.StateOf("lamp"));

        await story.Director.Talk("Sleep");
        await story.Director.Talk("Odile");

        var entry = Assert.Single(story.Director.Journal.Entries);
        Assert.Equal(new JournalEntry(2, "lamp", QuestState.Active), entry);
        Assert.Equal([entry], told);
        Assert.Equal([entry], story.Director.Journal.Open);

        // A quest the story does not have is an error of the script, not of the game
        story.Director.QuestChanged("cat");
        Assert.Single(story.Director.Journal.Entries);
        Assert.Contains(story.Errors, e => e.Contains("$quest_cat"));
        Assert.Null(story.Director.StateOf("cat"));
    }

    [Fact]
    public async Task An_ending_ends_the_story()
    {
        var story = new TestStory(Stub);
        var endings = new List<string>();
        story.Director.Ended += endings.Add;
        story.Director.Inventory.Give("oil");
        story.Director.Inventory.Give("key");

        await story.Director.Talk("Lamp");

        Assert.Equal(["keeper"], endings);
        Assert.Equal("keeper", story.Director.Ending);
        Assert.False(story.Director.Inventory.Has("oil"));
        Assert.Equal(QuestState.Succeeded, story.Director.StateOf("lamp"));

        // Nothing plays after the end
        story.Transcript.Clear();
        Assert.False(story.Director.CanTalk("Odile"));
        Assert.False(await story.Director.Talk("Lamp"));
        await story.Director.RunEvents();
        Assert.Empty(story.Transcript);
    }

    [Fact]
    public async Task Talk_plays_only_what_has_something_to_say()
    {
        var story = new TestStory(Stub);
        story.Director.Inventory.Give("oil");
        story.Director.Inventory.Give("key");
        await story.Director.Talk("Lamp");
        var after = new TestStory(Stub);
        await after.Director.Talk("Odile");
        await after.Director.Talk("Lamp");

        // No node of that name; then a node group whose nodes all have a condition that fails
        Assert.False(after.Director.CanTalk("Nobody"));
        Assert.False(await after.Director.Talk("Nobody"));
        after.Variables.SetValue("$quest_lamp", (float)QuestState.Failed);
        Assert.False(after.Director.CanTalk("Odile"));
        Assert.False(await after.Director.Talk("Odile"));
        Assert.Empty(after.Errors);
    }

    [Fact]
    public async Task Events_that_never_turn_false_stop_with_an_error()
    {
        var story = new TestStory("""
            title: Events
            when: always
            ---
            Again.
            ===
            """);

        await story.Director.RunEvents();

        Assert.Equal(20, story.TakeLines().Count);
        Assert.Contains(story.Errors, e => e.Contains("'Events'") && e.Contains("when: once"));
        Assert.False(story.Director.IsBusy);
    }

    [Fact]
    public async Task A_command_that_cannot_be_done_is_an_error_and_the_dialogue_goes_on()
    {
        var story = new TestStory("""
            title: Start
            ---
            <<take oil>>
            Nothing to take.
            ===
            """);

        Assert.True(await story.Director.Talk("Start"));

        Assert.Equal(["Nothing to take."], story.TakeLines());
        Assert.Contains(story.Errors, e => e.Contains("take oil") && e.Contains("carries 0"));
    }

    [Fact]
    public async Task Save_then_load_restores_the_variables_the_inventory_the_day_and_the_journal()
    {
        var story = new TestStory(Stub);
        await story.Director.Talk("Odile");
        await story.Director.Talk("OilCan");
        story.Director.Clock.Advance();
        var json = SaveSlot.ToJson(SaveSlot.Capture(story.Director));

        // The story goes on to its end
        await story.Director.Talk("KeyHook");
        await story.Director.Talk("Sleep");
        await story.Director.Talk("Lamp");
        Assert.Equal("keeper", story.Director.Ending);

        var dropped = SaveSlot.Restore(story.Director, SaveSlot.FromJson(json));

        Assert.Empty(dropped);
        Assert.Null(story.Director.Ending);
        Assert.Equal(QuestState.Active, story.Director.StateOf("lamp"));
        Assert.Equal(new Dictionary<string, int> { ["oil"] = 1 }, story.Director.Inventory.Items);
        Assert.Equal((1, TimeOfDay.Afternoon), (story.Director.Clock.Day, story.Director.Clock.Time));
        Assert.Equal([new JournalEntry(1, "lamp", QuestState.Active)], story.Director.Journal.Entries);

        // What happened once after the save happens again: the save holds what the story has seen
        story.Transcript.Clear();
        await story.Director.Talk("KeyHook");
        Assert.Equal(["The key is cold.", "You have all the lamp needs."], story.TakeLines());

        // A save made after it holds that it happened
        var later = SaveSlot.Capture(story.Director);
        story.Director.Inventory.Take("key");
        SaveSlot.Restore(story.Director, later);
        await story.Director.RunEvents();
        Assert.Empty(story.Transcript);
        Assert.True(story.Director.Inventory.Has("key"));
    }

    [Fact]
    public async Task The_save_is_a_file_and_is_not_made_during_a_dialogue()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"brinemoor-{Guid.NewGuid():N}");
        var path = Path.Combine(folder, "save.json");
        try
        {
            var story = new TestStory(Stub);
            Assert.Null(SaveSlot.Read(path));

            await story.Director.Talk("OilCan");
            var data = SaveSlot.Capture(story.Director);
            data.Places["Player"] = [1.5f, -2f];
            SaveSlot.Write(data, path);
            story.Director.Inventory.Take("oil");
            var read = SaveSlot.Read(path);
            Assert.NotNull(read);
            SaveSlot.Restore(story.Director, read);
            Assert.True(story.Director.Inventory.Has("oil"));
            Assert.Equal([1.5f, -2f], read.Places["Player"]);

            story.Reader.OnLine = _ =>
            {
                Assert.Throws<InvalidOperationException>(() => SaveSlot.Capture(story.Director));
                Assert.Throws<InvalidOperationException>(() => SaveSlot.Restore(story.Director, new SaveData()));
                return default;
            };
            await story.Director.Talk("OilCan");

            File.WriteAllText(path, "not a save");
            Assert.Throws<FormatException>(() => SaveSlot.Read(path));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }
}
