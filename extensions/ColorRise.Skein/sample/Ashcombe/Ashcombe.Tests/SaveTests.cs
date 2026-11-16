using ColorRise.Skein;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Ashcombe.Tests;

/// <summary>
/// A save made in one place and loaded in another: the root scene, the interiors, where everyone stood, and the
/// variables of both Yarn projects of the game.
/// </summary>
[Collection(GameCollection.Name)]
public sealed class SaveTests
{
    private const string Village = "Places/Village";
    private const string Inn = "Places/Village/Inn";
    private const string Cellar = "Places/Village/Cellar";
    private const string Castle = "Places/Castle";

    private const string Story = """
        title: Start
        ---
        <<declare $lamp_lent = false>>
        <<declare $echo_answered = false>>
        Hedda: Welcome.
        ===
        title: Cellar
        ---
        <<go InnDoor>>
        <<walk Hedda Bar>>
        <<go CellarStairs>>
        <<set $lamp_lent to true>>
        <<survey well>>
        ===
        title: Road
        ---
        <<go CastleRoad>>
        ===
        title: Home
        ---
        <<go VillageRoad>>
        ===
        """;

    // A second project of the game, as the echo of the mine: it declares a variable of its own and one of the story's
    private const string Echo = """
        title: Echo
        ---
        <<declare $echo_answered = false>>
        <<declare $echo_riddles = 0>>
        Echo: What walks on stone and never tires?
        <<set $echo_riddles to $echo_riddles + 1>>
        <<set $echo_answered to true>>
        ===
        """;

    [Fact]
    public void A_save_made_in_the_cellar_loads_from_the_castle_with_the_inn_and_the_cellar_and_everyone_in_place()
    {
        var project = new TestProject("Story", Story);
        using var game = MakeGame(project);
        game.Start(Village);
        Play(game, "Cellar");
        var hedda = game.Find("Hedda").Transform.Position;
        var cartographer = game.Find("Cartographer").Transform.Position;
        var save = SaveSlot.FromJson(SaveSlot.ToJson(game.System.Capture()));

        Play(game, "Road");
        Assert.Equal(Castle, game.Story.Whereabouts.Root);
        game.Story.Map.Replace([], []);

        game.System.Restore(save);
        game.StepUntil(() => game.System.Stage?.Entity.Scene == game.Root && !game.System.IsBusy);

        Assert.Equal(Village, game.Story.Whereabouts.Root);
        Assert.Equal([Inn, Cellar], game.Story.Whereabouts.Interiors);
        Assert.Equal("Cellar", game.Story.Whereabouts.Here);
        Assert.Equal(cartographer, game.Find("Cartographer").Transform.Position);
        Assert.Equal(hedda, game.Find("Hedda").Transform.Position);
        Assert.True(Flag(game, "$lamp_lent"));
        Assert.True(game.Story.Map.IsSurveyed("well"));
        Assert.Equal(new Vector2(200, 0), game.Find("Cartographer").Get<PlayerController>()!.Centre);
        Assert.Empty(game.Errors);
    }

    [Fact]
    public void A_save_made_in_the_castle_loads_from_the_cellar()
    {
        var project = new TestProject("Story", Story);
        using var game = MakeGame(project);
        game.Start(Village);
        Play(game, "Road");
        var save = game.System.Capture();
        Play(game, "Home");
        Play(game, "Cellar");

        game.System.Restore(save);
        game.StepUntil(() => game.Story.Whereabouts.Root == Castle && game.System.Stage?.Entity.Scene == game.Root && !game.System.IsBusy);

        Assert.Empty(game.Story.Whereabouts.Interiors);
        Assert.Equal(new Vector3(0, 0, 9), game.Find("Cartographer").Transform.Position);

        // The cartographer is put where the save says before the scene's scripts start: the camera follows from where
        // the scene has it, not from where the cartographer was put
        game.Step();
        Assert.Equal(Make.CameraOffset, game.Find("Camera").Transform.Position - game.Find("Cartographer").Transform.Position);
        Assert.DoesNotContain(Inn, game.Places.Loaded);
        Assert.DoesNotContain(Cellar, game.Places.Loaded);
        Assert.DoesNotContain(Village, game.Places.Loaded);
        Assert.Empty(game.Errors);
    }

    [Fact]
    public void A_save_keeps_the_variables_of_a_project_that_no_scene_up_uses_when_the_stage_declares_it()
    {
        var project = new TestProject("Story", Story);
        var echo = new TestProject("Echo", Echo);
        using var game = MakeGame(project, echo);
        game.Start(Village);
        game.Dialogues.Variables.SetValue("$echo_riddles", 2f);
        var save = game.System.Capture();
        game.Dialogues.Variables.SetValue("$echo_riddles", 0f);

        var dropped = game.System.Restore(save);

        Assert.Empty(dropped);
        Assert.True(game.Dialogues.Variables.TryGetValue<float>("$echo_riddles", out var riddles));
        Assert.Equal(2f, riddles);
    }

    [Fact]
    public void A_save_keeps_the_variables_of_a_project_no_scene_has_declared_yet()
    {
        // Without the stage's list of projects, the echo's project is known only once a scene that uses it has started:
        // the variables keep its values from a save until then, unchecked
        var project = new TestProject("Story", Story);
        var echo = new TestProject("Echo", Echo);
        SaveData save;
        using (var before = MakeGame(project, echo))
        {
            before.Start(Village);
            before.Dialogues.Variables.SetValue("$echo_riddles", 2f);
            save = SaveSlot.FromJson(SaveSlot.ToJson(before.System.Capture()));
        }

        using var game = MakeGame(project);
        game.Start(Village);
        var dropped = game.System.Restore(save);

        Assert.Empty(dropped);
        Assert.Equal(["$echo_riddles"], game.Dialogues.Variables.UndeclaredNames);

        game.Dialogues.Variables.Declare(echo.Project);

        Assert.Empty(game.Dialogues.Variables.UndeclaredNames);
        Assert.True(game.Dialogues.Variables.TryGetValue<float>("$echo_riddles", out var riddles));
        Assert.Equal(2f, riddles);
    }

    [Fact]
    public void The_echo_speaks_its_own_project_and_the_story_reads_what_it_set()
    {
        var project = new TestProject("Story", Story + """
            title: Gudrun
            when: $echo_answered
            ---
            Gudrun: You answered the echo? Nobody answers the echo.
            ===
            """);
        var echo = new TestProject("Echo", Echo);
        var echoPresenter = new Recorder();
        var runner = new DialogueRunnerComponent { Project = echo.Project, LinesOverride = echo.Lines };
        runner.Presenters.Add(echoPresenter);
        using var game = MakeGame(project, echo, new Entity("Echo") { runner, echoPresenter, new Talkable { Node = "Echo", Runner = runner } });
        game.Start(Village);
        Assert.False(game.Story.CanTalk("Gudrun"));

        var echoStage = new Stage.RunnerStage(runner);
        var talk = game.Story.Talk("Echo", echoStage);
        game.StepUntil(() => talk.IsCompleted && !game.System.IsBusy);

        Assert.Equal(["Echo: What walks on stone and never tires?", "completed"], echoPresenter.Seen);
        Assert.True(game.Story.CanTalk("Gudrun"));
        Assert.Empty(game.Errors);
    }

    private static bool Flag(SceneHarness game, string name) => game.Story.Variables.TryGetValue<bool>(name, out var value) && value;

    private static SceneHarness MakeGame(TestProject project, TestProject? echo = null, Entity? extra = null)
    {
        var game = new SceneHarness();
        game.Places.Add(Village, () =>
        {
            var scene = Make.Root(project, new Recorder(),
            [
                Make.Door("InnDoor", Inn),
                Make.Door("CastleRoad", Castle, journey: true, spawn: "Gate"),
                .. extra is null ? [] : new[] { extra },
            ]);
            if (echo is not null)
                scene.Entities.First(e => e.Name == "Stage").Get<Stage>()!.Projects.Add(echo.Project);
            return scene;
        });
        game.Places.Add(Inn, () => Make.Interior(new Vector3(200, 0, 0),
            Make.Person("Hedda", "Hedda", new Vector3(-2, 0, 0)),
            new Entity("Bar", new Vector3(3, 0, -3)),
            Make.Door("InnExit", Village),
            Make.Door("CellarStairs", Cellar)));
        game.Places.Add(Cellar, () => Make.Interior(new Vector3(0, -50, 0), Make.Door("CellarUp", Inn)));
        game.Places.Add(Castle, () => Make.Root(project, new Recorder(), new Entity("Gate", new Vector3(0, 0, 9)), Make.Door("VillageRoad", Village, journey: true)));
        return game;
    }

    private static void Play(SceneHarness game, string node)
    {
        var talk = game.Story.Talk(node);
        game.StepUntil(() => talk.IsCompleted && !game.System.IsBusy);
        Assert.True(talk.IsCompletedSuccessfully, talk.Exception?.ToString());
        Assert.True(talk.Result, $"'{node}' had nothing to say.");
    }
}
