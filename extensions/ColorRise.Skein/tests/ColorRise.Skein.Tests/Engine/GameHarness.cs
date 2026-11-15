using System.Reflection;
using System.Runtime.CompilerServices;
using ColorRise.Skein.Tests.Session;
using Google.Protobuf;
using Stride.Core.Diagnostics;
using Stride.Engine;
using Stride.Games;
using Stride.Input;

namespace ColorRise.Skein.Tests.Engine;

/// <summary>
/// The tests that make a game run one after the other: a game has global parts (the log, the registered assemblies).
/// </summary>
[CollectionDefinition(Name)]
public sealed class GameCollection
{
    public const string Name = "Game";
}

/// <summary>
/// A game with a scene and no window, whose frames the test steps by hand.
/// </summary>
internal sealed class GameHarness : IDisposable
{
    private static readonly MethodInfo UpdateTime = typeof(GameTime).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly List<ILogMessage> messages = [];

    public GameHarness()
    {
        // As in a game that references Skein: its assembly is loaded, so the game knows its declared game system
        RuntimeHelpers.RunModuleConstructor(typeof(DialogueSystem).Module.ModuleHandle);
        GlobalLogger.GlobalMessageLogged += OnMessage;
        Game = new Game();
        Game.SceneSystem.SceneInstance = new SceneInstance(Game.Services, Scene);

        // A game that runs has an input manager; this one reads a keyboard the test presses
        Input.Initialize(new GameContextHeadless());
        var devices = new InputSourceSimulated();
        Input.Sources.Add(devices);
        Keyboard = devices.AddKeyboard();
        Game.Services.AddService(Input);
    }

    public Game Game { get; }

    public Scene Scene { get; } = new();

    public DialogueSystem Dialogues => Game.Services.GetService<DialogueSystem>()!;

    public InputManager Input { get; } = new();

    public KeyboardSimulated Keyboard { get; }

    /// <summary>
    /// What Skein logged at a level, since the game was made.
    /// </summary>
    public IEnumerable<string> Logged(LogMessageType type) => messages.Where(m => m.Type == type).Select(m => m.Text);

    /// <summary>
    /// Adds an entity to the scene.
    /// </summary>
    public Entity Add(string name, params EntityComponent[] components)
    {
        var entity = new Entity(name);
        foreach (var component in components)
        {
            entity.Add(component);
        }

        Scene.Entities.Add(entity);
        return entity;
    }

    /// <summary>
    /// A runner on a new entity, for the project of a script.
    /// </summary>
    public DialogueRunnerComponent AddRunner(string script, string entity = "Runner", params DialoguePresenter[] presenters)
    {
        var compilation = Scripts.Compile(script);
        var project = new YarnProject { Program = compilation.Program!.ToByteArray() };
        project.Nodes.AddRange(TestDialogue.NodesOf(compilation));
        var runner = new DialogueRunnerComponent { Project = project, LinesOverride = TestDialogue.LinesOf(compilation) };
        runner.Presenters.AddRange(presenters);
        Add(entity, [runner, .. presenters]);
        return runner;
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
    /// Runs a frame in which the player presses a key, and lets the key go.
    /// </summary>
    public void Press(Keys key)
    {
        Keyboard.SimulateDown(key);
        Step();
        Keyboard.SimulateUp(key);
    }

    public void Dispose()
    {
        Game.Dispose();
        Input.Dispose();
        GlobalLogger.GlobalMessageLogged -= OnMessage;
    }

    private void OnMessage(ILogMessage message)
    {
        if (message.Module?.StartsWith("ColorRise.Skein", StringComparison.Ordinal) == true)
            messages.Add(message);
    }
}

/// <summary>
/// A presenter that writes down what it is asked, and waits when the test gives it something to wait for.
/// </summary>
public sealed class RecordingPresenter : DialoguePresenter
{
    public List<string> Seen { get; } = [];

    /// <summary>
    /// Each line waits for the next of these (or for the cancellation of the line); a line with none left goes on.
    /// </summary>
    public Queue<TaskCompletionSource> LineGates { get; } = new();

    /// <summary>
    /// When set, a line fails with it.
    /// </summary>
    public Exception? LineFailure { get; set; }

    /// <summary>
    /// When set, answers the options; else the presenter leaves the choice to another.
    /// </summary>
    public Func<IReadOnlyList<DialogueOption>, CancellationToken, Task<int?>>? Options { get; set; }

    public override Task OnDialogueStartedAsync()
    {
        Seen.Add("started");
        return Task.CompletedTask;
    }

    public override Task OnDialogueCompletedAsync()
    {
        Seen.Add("completed");
        return Task.CompletedTask;
    }

    public override Task OnNodeEnterAsync(string node)
    {
        Seen.Add($"enter: {node}");
        return Task.CompletedTask;
    }

    public override Task OnNodeExitAsync(string node)
    {
        Seen.Add($"exit: {node}");
        return Task.CompletedTask;
    }

    public override async Task RunLineAsync(LocalizedLine line, LineCancellation cancellation)
    {
        Seen.Add($"line: {line.Text.Text}");
        if (LineFailure is not null)
            throw LineFailure;
        if (LineGates.TryDequeue(out var gate))
            await gate.Task.WaitAsync(cancellation.NextLine);
    }

    public override async Task<int?> RunOptionsAsync(IReadOnlyList<DialogueOption> options, CancellationToken cancellation)
    {
        Seen.Add($"options: {string.Join(" | ", options.Select(o => o.Line.Text.Text + (o.IsAvailable ? "" : " [disabled]")))}");
        if (Options is null)
            return null;

        try
        {
            return await Options(options, cancellation);
        }
        catch (OperationCanceledException)
        {
            Seen.Add("options cancelled");
            throw;
        }
    }
}
