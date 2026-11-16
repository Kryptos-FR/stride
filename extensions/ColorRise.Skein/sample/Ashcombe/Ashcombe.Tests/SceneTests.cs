using ColorRise.Skein;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Ashcombe.Tests;

/// <summary>
/// The story across scenes, in a game without window: interiors loaded and unloaded under a root scene while a
/// dialogue plays, root scenes swapped, a runner in a scene that goes, and the same actor in two root scenes.
/// </summary>
[Collection(GameCollection.Name)]
public sealed class SceneTests
{
    private const string Village = "Places/Village";
    private const string Inn = "Places/Village/Inn";
    private const string Cellar = "Places/Village/Cellar";
    private const string Castle = "Places/Castle";

    private static readonly Vector3 InnOffset = new(200, 0, 0);
    private static readonly Vector3 CellarOffset = new(0, -50, 0);

    [Fact]
    public void A_dialogue_goes_on_while_an_interior_loads_and_unloads()
    {
        var project = new TestProject("Story", """
            title: Visit
            ---
            Hedda: Come in, I'll show you the cellar.
            <<go InnDoor>>
            Hedda: Here we are, out of the rain. Mind the stairs.
            <<go CellarStairs>>
            Hedda: The old well. Put it on your map.
            <<go CellarUp>>
            <<go InnExit>>
            Hedda: And back out you go.
            ===
            """);
        var presenter = new Recorder();
        using var game = MakeVillage(project, presenter);
        game.Start(Village);

        var visit = Talk(game, "Visit");

        Assert.Equal(["Hedda: Come in, I'll show you the cellar.", "Hedda: Here we are, out of the rain. Mind the stairs.", "Hedda: The old well. Put it on your map.", "Hedda: And back out you go.", "completed"], presenter.Seen);
        Assert.True(visit.IsCompletedSuccessfully, visit.Exception?.ToString());
        Assert.Empty(game.Errors);
        Assert.Equal([$"load {Village}", $"load {Inn}", $"load {Cellar}", $"unload {Cellar}", $"unload {Inn}"], game.Places.History);
        Assert.Equal(Village, game.Story.Whereabouts.Current);
        Assert.Equal(["Cellar", "Inn", "Village"], game.Story.Map.Places);
        Assert.Empty(game.Root.Children);
    }

    [Fact]
    public void A_door_takes_the_cartographer_to_the_arrival_of_the_interior()
    {
        var project = new TestProject("Story", """
            title: Visit
            ---
            <<go InnDoor>>
            Hedda: Sit down.
            ===
            """);
        using var game = MakeVillage(project, new Recorder());
        game.Start(Village);

        Talk(game, "Visit");

        // The arrival of the inn is at (1, 0, 2) in the inn, which is at (200, 0, 0) in the world
        Assert.Equal(new Vector3(201, 0, 2), game.Find("Cartographer").Transform.Position);
        Assert.Equal("Inn", game.Story.Whereabouts.Here);
        var controller = game.Find("Cartographer").Get<PlayerController>()!;
        Assert.Equal(new Vector2(200, 0), controller.Centre);
        Assert.Equal(new Vector2(5, 5), controller.Bounds);
    }

    [Fact]
    public void A_door_of_a_place_that_is_not_loaded_is_an_error_of_the_log_and_the_dialogue_goes_on()
    {
        var project = new TestProject("Story", """
            title: Lost
            ---
            Hedda: Down you go.
            <<go CellarStairs>>
            Hedda: You stay where you are.
            ===
            title: After
            ---
            Hedda: Still here.
            ===
            """);
        var presenter = new Recorder();
        using var game = MakeVillage(project, presenter);
        game.Start(Village);

        var lost = Talk(game, "Lost");

        // Skein says it in the log, as for any command whose target is missing, and goes on
        Assert.True(lost.IsCompletedSuccessfully);
        Assert.Equal(["Hedda: Down you go.", "Hedda: You stay where you are.", "completed"], presenter.Seen);
        Assert.Equal(["ColorRise.Skein.DialogueRunnerComponent: The command <<go CellarStairs>> could not be called: no Doorway named 'CellarStairs' was found for the target."], game.Errors);
        Assert.False(game.System.IsBusy);
        Assert.Equal(Village, game.Story.Whereabouts.Current);

        Talk(game, "After");
        Assert.Contains("Hedda: Still here.", presenter.Seen);
    }

    [Fact]
    public void The_chatter_of_an_interior_stops_in_the_middle_of_a_line_when_the_cartographer_leaves()
    {
        var project = new TestProject("Story", """
            title: Leave
            ---
            <<go InnExit>>
            ===
            title: Enter
            ---
            <<go InnDoor>>
            ===
            title: Gossip
            ---
            Wim: Did you hear about the stones?
            Saul: Lio again, I'd bet.
            ===
            """);
        var chatterPresenter = new Recorder();
        var gate = new TaskCompletionSource();
        chatterPresenter.Gates.Enqueue(gate);
        using var game = MakeVillage(project, new Recorder(), chatter: (project, chatterPresenter));
        game.Start(Village);
        Talk(game, "Enter");

        var chatter = game.Find("Regulars").Get<DialogueRunnerComponent>()!;
        game.StepUntil(() => chatterPresenter.Seen.Count > 0);
        Assert.True(chatter.IsRunning);

        Talk(game, "Leave");

        Assert.Equal(["Wim: Did you hear about the stones?"], chatterPresenter.Cancelled);
        Assert.False(chatter.IsRunning);
        Assert.DoesNotContain("Saul: Lio again, I'd bet.", chatterPresenter.Seen);
        Assert.Empty(game.Errors);
        Assert.DoesNotContain(Inn, game.Places.Loaded);
    }

    [Fact]
    public void A_journey_asked_in_a_dialogue_waits_for_its_end_and_the_story_goes_on_in_the_next_scene()
    {
        var project = new TestProject("Story", """
            title: Summons
            tracking: always
            ---
            <<declare $summoned = false>>
            Maren: The Countess wants you at the castle.
            <<set $summoned to true>>
            <<go CastleRoad>>
            Maren: I'll walk you to the bridge.
            ===
            title: Greeting
            when: $summoned
            when: visited("Summons")
            ---
            Corvin: You came up from the village. The Countess is waiting.
            ===
            """);
        var villagePresenter = new Recorder();
        var castlePresenter = new Recorder();
        using var game = MakeVillage(project, villagePresenter);
        game.Places.Add(Castle, () => Make.Root(project, castlePresenter, new Entity("Gate", new Vector3(0, 0, 9))));
        game.Start(Village);

        Talk(game, "Summons");

        // The dialogue ended in the village, then the castle came
        Assert.Equal(["Maren: The Countess wants you at the castle.", "Maren: I'll walk you to the bridge.", "completed"], villagePresenter.Seen);
        game.StepUntil(() => game.Story.Whereabouts.Root == Castle && !game.System.IsBusy);
        Assert.Equal([$"load {Village}", $"load {Castle}", $"unload {Village}"], game.Places.History);
        Assert.Equal(new Vector3(0, 0, 9), game.Find("Cartographer").Transform.Position);

        // Same variables, same memory of what was seen
        Assert.True(game.Story.Variables.TryGetValue<bool>("$summoned", out var summoned) && summoned);
        Assert.True(Talk(game, "Greeting").Result, $"{game.System.Stage?.Entity.Scene?.Name} {string.Join(",", game.Dialogues.Variables.StoredNames)} {string.Join(" | ", game.Warnings)}");
        Assert.Equal(["Corvin: You came up from the village. The Countess is waiting.", "completed"], castlePresenter.Seen);
        Assert.Empty(game.Errors);
    }

    [Fact]
    public void The_same_actor_in_two_root_scenes_is_the_one_of_the_scene_that_is_up()
    {
        var project = new TestProject("Story", """
            title: Move
            ---
            <<walk Pell Stall>>
            Pell: Here I am.
            ===
            title: Travel
            ---
            <<go CastleRoad>>
            ===
            """);
        using var game = MakeVillage(project, new Recorder(), pell: new Vector3(-4, 0, 0));
        game.Places.Add(Castle, () => Make.Root(project, new Recorder(), Make.Person("Pell", "Pell", new Vector3(6, 0, 0)), new Entity("Stall", new Vector3(6, 0, 8)), new Entity("Gate")));
        game.Start(Village);

        Talk(game, "Travel");
        game.StepUntil(() => game.Story.Whereabouts.Root == Castle && !game.System.IsBusy);
        var pell = game.Find("Pell");

        Talk(game, "Move");

        // The castle's Pell walked to the castle's stall
        Assert.Equal(6, pell.Transform.Position.X, 3);
        Assert.InRange(pell.Transform.Position.Z, 6.5f, 8f);
        Assert.Empty(game.Errors);
    }

    [Fact]
    public void Someone_who_walked_is_still_there_when_the_place_loads_again()
    {
        var project = new TestProject("Story", """
            title: Fetch
            ---
            <<walk Hedda Bar>>
            Hedda: I'll stay by the bar.
            <<go InnExit>>
            ===
            title: Back
            ---
            <<go InnDoor>>
            ===
            """);
        using var game = MakeVillage(project, new Recorder());
        game.Start(Village);
        Talk(game, "Back");

        Talk(game, "Fetch");
        Talk(game, "Back");

        // By the bar, not at the door where the scene of the inn puts her
        var hedda = game.Find("Hedda");
        Assert.InRange(Vector3.Distance(hedda.Transform.Position, new Vector3(3, 0, -3)), 0f, 1.5f);
        Assert.Equal([$"load {Village}", $"load {Inn}", $"unload {Inn}", $"load {Inn}"], game.Places.History);
    }

    [Fact]
    public void A_place_of_the_story_is_up_while_its_variable_is_true()
    {
        var project = new TestProject("Story", """
            title: Open
            ---
            <<declare $fair_up = false>>
            <<set $fair_up to true>>
            ===
            title: Close
            ---
            <<set $fair_up to false>>
            ===
            """);
        const string fair = "Places/Village/Fair";
        using var game = MakeVillage(project, new Recorder(), new Entity("FairGround") { new StoryPlace { Place = new(fair), Variable = "$fair_up" } });
        game.Places.Add(fair, () => Make.Interior(new Vector3(0, 0, 30), Make.Person("Anselm", "Anselm")));
        game.Start(Village);
        Assert.DoesNotContain(fair, game.Places.Loaded);

        Talk(game, "Open");
        game.Step();
        Assert.Contains(fair, game.Places.Loaded);
        Assert.Equal("Places/Village", game.Story.Whereabouts.Current);
        game.Find("Anselm");

        Talk(game, "Close");
        game.Step();
        Assert.DoesNotContain(fair, game.Places.Loaded);
    }

    [Fact]
    public void Spike_an_entity_moved_to_another_root_scene_has_its_scripts_cancelled_and_started_again()
    {
        using var game = new SceneHarness();
        var counter = new Counter();
        var mover = new Entity("Mover") { counter };
        var first = new Scene();
        first.Entities.Add(mover);
        var second = new Scene();
        game.Places.Add(Village, () => first);
        game.Start(Village);
        Assert.Equal((1, 0), (counter.Starts, counter.Cancels));

        // Moved out of the scene that is up, then that scene is put up: Stride cancels, then starts it again
        mover.Scene = second;
        game.Game.SceneSystem.SceneInstance.RootScene = second;
        game.Step();

        Assert.Equal((2, 1), (counter.Starts, counter.Cancels));
    }

    /// <summary>
    /// The village of a test: its stage, a door to the inn, a road to the castle, Pell; the inn with Hedda, its bar, a
    /// door out and stairs down; the cellar with stairs up.
    /// </summary>
    private static SceneHarness MakeVillage(TestProject project, Recorder presenter, Entity? extra = null, (TestProject Project, Recorder Presenter)? chatter = null, Vector3 pell = default)
    {
        var game = new SceneHarness();
        game.Places.Add(Village, () => Make.Root(project, presenter,
        [
            Make.Door("InnDoor", Inn),
            Make.Door("CastleRoad", Castle, journey: true, spawn: "Gate"),
            Make.Person("Pell", "Pell", pell),
            .. extra is null ? [] : new[] { extra },
        ]));
        game.Places.Add(Inn, () =>
        {
            var entities = new List<Entity>
            {
                Make.Person("Hedda", "Hedda", new Vector3(-2, 0, 0)),
                new("Bar", new Vector3(3, 0, -3)),
                Make.Door("InnExit", Village, spawn: "Arrival"),
                Make.Door("CellarStairs", Cellar),
            };
            if (chatter is { } talk)
            {
                var runner = new DialogueRunnerComponent { Project = talk.Project.Project, LinesOverride = talk.Project.Lines };
                runner.Presenters.Add(talk.Presenter);
                entities.Add(new Entity("Regulars") { runner, talk.Presenter, new Chatter { Runner = runner, Node = "Gossip", Distance = 50 } });
            }

            return Make.Interior(InnOffset, [.. entities]);
        });
        game.Places.Add(Cellar, () => Make.Interior(CellarOffset, Make.Door("CellarUp", Inn, spawn: "Arrival")));
        return game;
    }

    /// <summary>
    /// Plays a node as the cartographer would, and runs the game until the story and the places are still.
    /// </summary>
    private static Task<bool> Talk(SceneHarness game, string node)
    {
        var talk = game.Story.Talk(node);
        game.StepUntil(() => talk.IsCompleted && !game.System.IsBusy);
        return talk;
    }

    private sealed class Counter : StartupScript
    {
        public int Starts { get; private set; }

        public int Cancels { get; private set; }

        public override void Start() => Starts++;

        public override void Cancel() => Cancels++;
    }
}
