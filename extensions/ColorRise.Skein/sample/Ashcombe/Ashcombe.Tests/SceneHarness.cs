using System.Reflection;
using System.Runtime.CompilerServices;
using ColorRise.Skein;
using ColorRise.Skein.Assets;
using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Assets.Localisation;
using Stride.Core.Diagnostics;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Input;

namespace Ashcombe.Tests;

/// <summary>
/// The tests that make a game run one after the other: a game has global parts (the log, the registered assemblies,
/// the story of <see cref="StoryCommands"/>).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GameCollection
{
    public const string Name = "Game";
}

/// <summary>
/// A game with no window and places made in code, whose frames the test steps by hand: the game's
/// <see cref="StorySystem"/>, the stage of each root scene, the places loaded and unloaded by URL.
/// </summary>
internal sealed class SceneHarness : IDisposable
{
    private static readonly MethodInfo UpdateTime = typeof(GameTime).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Stride's GameTime has no Update method any more.");

    private readonly List<ILogMessage> messages = [];

    public SceneHarness()
    {
        // As in the game: Skein's assembly and the game's are loaded, so the game makes their declared game systems
        RuntimeHelpers.RunModuleConstructor(typeof(DialogueSystem).Module.ModuleHandle);
        RuntimeHelpers.RunModuleConstructor(typeof(StorySystem).Module.ModuleHandle);
        GlobalLogger.GlobalMessageLogged += OnMessage;
        Game = new Game();
        System.Loader = Places;

        Input.Initialize(new GameContextHeadless());
        var devices = new InputSourceSimulated();
        Input.Sources.Add(devices);
        Keyboard = devices.AddKeyboard();
        Game.Services.AddService(Input);
    }

    public Game Game { get; }

    public StorySystem System => Game.Services.GetService<StorySystem>()!;

    public DialogueSystem Dialogues => Game.Services.GetService<DialogueSystem>()!;

    public StoryDirector Story => System.Story;

    public TestPlaces Places { get; } = new();

    public InputManager Input { get; } = new();

    public KeyboardSimulated Keyboard { get; }

    public Scene Root => Game.SceneSystem.SceneInstance.RootScene;

    public IEnumerable<string> Errors => messages.Where(m => m.Type >= LogMessageType.Error).Select(m => $"{m.Module}: {m.Text}");

    public IEnumerable<string> Warnings => messages.Where(m => m.Type == LogMessageType.Warning).Select(m => $"{m.Module}: {m.Text}");

    /// <summary>
    /// Starts the game in a root place.
    /// </summary>
    public void Start(string url)
    {
        Game.SceneSystem.SceneInstance = new SceneInstance(Game.Services, Places.Load(url));
        Step();
    }

    /// <summary>
    /// Runs frames of the game's scripts.
    /// </summary>
    public void Step(int frames = 1, double seconds = 1 / 60.0)
    {
        for (var i = 0; i < frames; i++)
        {
            var elapsed = TimeSpan.FromSeconds(seconds);
            UpdateTime.Invoke(Game.UpdateTime, [Game.UpdateTime.Total + elapsed, elapsed, true]);
            Input.Update(Game.UpdateTime);
            Game.Script.Update(Game.UpdateTime);
        }
    }

    /// <summary>
    /// Runs frames until a condition holds, at most a few seconds of the game.
    /// </summary>
    public void StepUntil(Func<bool> condition, int frames = 600)
    {
        for (var i = 0; i < frames && !condition(); i++)
        {
            Step();
        }

        Assert.True(condition(), $"Still not so after {frames} frames. Errors: {string.Join(" | ", Errors)}");
    }

    /// <summary>
    /// Runs a frame in which the player presses a key, and lets the key go.
    /// </summary>
    public void Press(Keys key)
    {
        Keyboard.SimulateDown(key);
        Step();
        Keyboard.SimulateUp(key);
        Step();
    }

    /// <summary>
    /// The entity of a name in the scenes loaded now.
    /// </summary>
    public Entity Find(string name)
    {
        var found = Scenes(Root).SelectMany(s => s.Entities).SelectMany(Self).FirstOrDefault(e => e.Name == name);
        Assert.True(found is not null, $"No entity '{name}' is loaded.");
        return found!;
    }

    public void Dispose()
    {
        Game.Dispose();
        Input.Dispose();
        GlobalLogger.GlobalMessageLogged -= OnMessage;
        StoryCommands.Story = null;
    }

    private static IEnumerable<Scene> Scenes(Scene scene) => scene.Children.SelectMany(Scenes).Prepend(scene);

    private static IEnumerable<Entity> Self(Entity entity) => entity.Transform.Children.Select(t => t.Entity).SelectMany(Self).Prepend(entity);

    private void OnMessage(ILogMessage message) => messages.Add(message);
}

/// <summary>
/// The places of a test: each URL makes its scene again on each load, as the content of a game does after an unload.
/// </summary>
internal sealed class TestPlaces : IPlaceLoader
{
    private readonly Dictionary<string, Func<Scene>> makers = [];
    private readonly Dictionary<Scene, string> loaded = [];

    /// <summary>
    /// The URLs loaded and unloaded, in order: <c>load Places/Village</c>, <c>unload Places/Village/Inn</c>.
    /// </summary>
    public List<string> History { get; } = [];

    /// <summary>
    /// The scenes loaded now.
    /// </summary>
    public IReadOnlyCollection<string> Loaded => loaded.Values;

    public void Add(string url, Func<Scene> make) => makers.Add(url, make);

    public Scene Load(string url)
    {
        Assert.True(makers.TryGetValue(url, out var make), $"The test has no place '{url}'.");
        var scene = make!();
        scene.Name = url;
        loaded.Add(scene, url);
        History.Add($"load {url}");
        return scene;
    }

    public void Unload(Scene scene)
    {
        Assert.True(loaded.Remove(scene, out var url), $"The scene '{scene.Name}' was not loaded, or was unloaded twice.");
        History.Add($"unload {url}");
    }

    public string? UrlOf(Scene scene) => loaded.GetValueOrDefault(scene);
}

/// <summary>
/// The Yarn project of a test, built from scripts as the game's build builds them, against the game's functions.
/// </summary>
internal sealed class TestProject
{
    public TestProject(string name, params string[] scripts)
    {
        var actions = new YarnActionRegistry();
        actions.RegisterType(typeof(StoryCommands));
        var functions = actions.Describe().Where(a => a.Kind == YarnActionKind.Function).Select(YarnFunctionDeclaration.From).ToList();

        Build = YarnProjectBuilder.Build(new YarnProjectAsset(), name, [.. scripts.Select((s, i) => ($"{name}{i}.yarn", s))], functions);
        Assert.Empty(Build.Compilation.Diagnostics.Where(d => d.Severity == YarnSeverity.Error).Select(d => d.ToString()));
        Project = Build.Project!;
        var strings = Build.BaseStrings!;
        Lines = new TextLineProvider(language => language == strings.Language ? strings : null, Project.BaseLanguage, GlobalLogger.GetLogger("Test"))
        {
            LineMetadata = Project.LineMetadata,
            ShadowLines = Project.ShadowLines,
        };
    }

    public YarnProjectBuild Build { get; }

    public YarnProject Project { get; }

    public TextLineProvider Lines { get; }
}

/// <summary>
/// Makes the entities of the places of a test.
/// </summary>
internal static class Make
{
    /// <summary>
    /// A root scene with its stage: a runner of the project with a recording presenter, and the cartographer.
    /// </summary>
    public static Scene Root(TestProject project, Recorder presenter, params Entity[] entities)
    {
        var runner = new DialogueRunnerComponent { Project = project.Project, LinesOverride = project.Lines };
        runner.Presenters.Add(presenter);
        var player = new Entity("Cartographer") { new PlayerController() };
        var stage = new Entity("Stage") { runner, presenter };
        stage.Add(new Stage { Runner = runner, Player = player });

        var scene = new Scene();
        scene.Entities.Add(stage);
        scene.Entities.Add(player);
        scene.Entities.Add(new Entity("Area") { new PlaceArea { HalfSize = new Vector2(20, 20) } });
        scene.Entities.Add(new Entity("Arrival"));
        foreach (var entity in entities)
        {
            scene.Entities.Add(entity);
        }

        return scene;
    }

    /// <summary>
    /// A place loaded under another: its area and its arrival, at an offset.
    /// </summary>
    public static Scene Interior(Vector3 offset, params Entity[] entities)
    {
        var scene = new Scene { Offset = offset };
        scene.Entities.Add(new Entity("Area") { new PlaceArea { HalfSize = new Vector2(5, 5) } });
        scene.Entities.Add(new Entity("Arrival", new Vector3(1, 0, 2)));
        foreach (var entity in entities)
        {
            scene.Entities.Add(entity);
        }

        return scene;
    }

    public static Entity Door(string name, string target, bool journey = false, string spawn = "")
        => new(name) { new Doorway { Target = new(target), IsJourney = journey, Spawn = spawn } };

    public static Entity Person(string name, string node, Vector3 position = default)
        => new(name, position) { new Talkable { Node = node }, new DialogueActorComponent { ActorName = name } };
}

/// <summary>
/// A presenter that writes down what it is shown, picks the first option it can, and waits on a line when the test
/// gives it something to wait for.
/// </summary>
public sealed class Recorder : DialoguePresenter
{
    public List<string> Seen { get; } = [];

    /// <summary>
    /// Each line waits for the next of these, or for its cancellation; a line with none left goes on.
    /// </summary>
    public Queue<TaskCompletionSource> Gates { get; } = new();

    /// <summary>
    /// The lines whose wait was cancelled.
    /// </summary>
    public List<string> Cancelled { get; } = [];

    public override Task OnDialogueCompletedAsync()
    {
        Seen.Add("completed");
        return Task.CompletedTask;
    }

    public override async Task RunLineAsync(LocalizedLine line, LineCancellation cancellation)
    {
        Seen.Add(line.Text.Text);
        if (!Gates.TryDequeue(out var gate))
            return;

        try
        {
            await gate.Task.WaitAsync(cancellation.NextLine);
        }
        catch (OperationCanceledException)
        {
            Cancelled.Add(line.Text.Text);
            throw;
        }
    }

    public override Task<int?> RunOptionsAsync(IReadOnlyList<DialogueOption> options, CancellationToken cancellation)
        => Task.FromResult(options.FirstOrDefault(o => o.IsAvailable)?.Index);
}
