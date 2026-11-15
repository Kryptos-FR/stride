using System.Runtime.ExceptionServices;
using Stride.Core;
using Stride.Engine;

namespace ColorRise.Skein;

/// <summary>
/// Runs the dialogues of a Yarn project in a game: it steps the story and asks its presenters to show each line and
/// each choice. Several runners can run at once, each its own dialogue.
/// </summary>
/// <remarks>
/// A dialogue runs as a micro-thread of the game's scripts, on the game's thread. A failure of a presenter or of a
/// command ends the dialogue and is an error of the game like the failure of any script: the task of
/// <see cref="StartDialogue"/> fails with it, and the script system reports it.
/// </remarks>
[DataContract("DialogueRunnerComponent")]
[Display("Dialogue runner")]
[ComponentCategory("Dialogue")]
public sealed class DialogueRunnerComponent : StartupScript
{
    private readonly HashSet<string> reportedCommands = [];
    private DialogueSession? session;
    private (YarnProject Project, VariableStorage Variables, ILineProvider Lines) sessionOf;
    private Run? run;
    private bool reportedNoAnswer;

    /// <summary>
    /// A dialogue starts.
    /// </summary>
    public event Action? DialogueStarted;

    /// <summary>
    /// The dialogue is over: it reached its end, or it was stopped. The runner is free to start another.
    /// </summary>
    public event Action? DialogueCompleted;

    /// <summary>
    /// The dialogue enters a node; the name is the node's in the program.
    /// </summary>
    public event Action<string>? NodeStarted;

    /// <summary>
    /// The dialogue leaves a node.
    /// </summary>
    public event Action<string>? NodeCompleted;

    /// <summary>
    /// The script ran a command no handler of the game has; the text is the command's, the script's values in it. With
    /// no listener, the runner logs a warning, once per command.
    /// </summary>
    public event Action<string>? UnhandledCommand;

    /// <summary>
    /// The project whose nodes the runner runs.
    /// </summary>
    /// <userdoc>The Yarn project whose nodes this runner runs.</userdoc>
    [DataMember(10)]
    public YarnProject? Project { get; set; }

    /// <summary>
    /// The node, or the node group, <see cref="AutoStart"/> starts.
    /// </summary>
    /// <userdoc>The node (or node group) to run when Auto start is on.</userdoc>
    [DataMember(20)]
    [YarnNode(nameof(Project))]
    public string StartNode { get; set; } = "";

    /// <summary>
    /// Whether the runner starts <see cref="StartNode"/> when its scene starts.
    /// </summary>
    /// <userdoc>Starts the start node as soon as the scene starts. Off, a script starts the dialogue with StartDialogue.</userdoc>
    [DataMember(30)]
    public bool AutoStart { get; set; }

    /// <summary>
    /// Who shows the dialogue. A line goes to every presenter at once, and the dialogue goes on when all are done; the
    /// first presenter that gives a choice answers the options.
    /// </summary>
    /// <userdoc>The presenters that show this runner's lines and options.</userdoc>
    [DataMember(40)]
    public List<DialoguePresenter> Presenters { get; } = [];

    /// <summary>
    /// How the runner picks among the nodes of a node group and the lines of a line group; null for the project's.
    /// </summary>
    /// <userdoc>How this runner picks among the nodes of a node group. Leave unset to use the project's strategy.</userdoc>
    [DataMember(50)]
    public YarnSaliency? Saliency { get; set; }

    /// <summary>
    /// Whether options that are all unavailable are skipped: the dialogue goes on after them, as if no choice was
    /// made. When false, the presenters get them all the same.
    /// </summary>
    /// <userdoc>When every option of a choice is unavailable, skip the choice and go on. Off, the presenters still show the choice.</userdoc>
    [DataMember(60)]
    public bool OptionsFallThrough { get; set; } = true;

    /// <summary>
    /// Variables of this runner's own, in place of the game's (<see cref="DialogueSystem.Variables"/>).
    /// </summary>
    [DataMemberIgnore]
    public VariableStorage? VariablesOverride { get; set; }

    /// <summary>
    /// Lines of this runner's own, in place of the project's string tables (<see cref="DialogueSystem.GetLineProvider"/>).
    /// </summary>
    [DataMemberIgnore]
    public ILineProvider? LinesOverride { get; set; }

    /// <summary>
    /// Whether a dialogue is running: from <see cref="StartDialogue"/> to just before <see cref="DialogueCompleted"/>.
    /// </summary>
    [DataMemberIgnore]
    public bool IsRunning => run is not null;

    /// <summary>
    /// The name, in the program, of the node the dialogue is in; null when none runs.
    /// </summary>
    [DataMemberIgnore]
    public string? CurrentNode => run?.Session.CurrentNode;

    /// <summary>
    /// The variables the runner's dialogues read and write.
    /// </summary>
    [DataMemberIgnore]
    public VariableStorage Variables => VariablesOverride ?? System.Variables;

    private DialogueSystem System => DialogueSystem.Of(Services ?? throw NotRunning());

    /// <inheritdoc />
    public override void Start()
    {
        if (!AutoStart)
            return;

        if (string.IsNullOrEmpty(StartNode))
        {
            Log.Warning($"The dialogue runner of '{Entity?.Name}' starts by itself and has no start node: nothing runs.");
            return;
        }

        StartDialogue(StartNode);
    }

    /// <inheritdoc />
    public override void Cancel()
    {
        Stop();
        base.Cancel();
    }

    /// <summary>
    /// Starts a dialogue at a node, or at a node of a node group.
    /// </summary>
    /// <param name="node">The title of the node or of the node group.</param>
    /// <returns>A task that completes when the dialogue is over, a stop included.</returns>
    /// <exception cref="InvalidOperationException">The runner is already running a dialogue, has no project, or is not in a running scene.</exception>
    /// <exception cref="ArgumentException">The project has no node of this name.</exception>
    public Task StartDialogue(string node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (run is not null)
            throw new InvalidOperationException($"The dialogue runner of '{Entity?.Name}' is already running a dialogue (in the node '{CurrentNode}'): stop it before starting '{node}'.");
        var starting = GetSession();
        if (!starting.NodeExists(node))
            throw new ArgumentException($"The Yarn project of the dialogue runner of '{Entity?.Name}' has no node named '{node}'.", nameof(node));

        starting.Saliency = Saliency ?? Project!.Saliency;
        var current = new Run(starting);
        run = current;
        Script.AddTask(() => RunAsync(current, node));
        return current.Completion.Task;
    }

    /// <summary>
    /// Stops the dialogue: what the presenters await is cancelled, and the dialogue is over. Does nothing when none runs.
    /// </summary>
    public void Stop() => run?.Stopping.Cancel();

    /// <summary>
    /// Asks the presenters to finish showing the line quickly (<see cref="LineCancellation.HurryUp"/>).
    /// </summary>
    public void RequestHurryUp() => run?.Session.RequestHurryUp();

    /// <summary>
    /// Asks the presenters to leave the line now (<see cref="LineCancellation.NextLine"/>).
    /// </summary>
    public void RequestNextLine() => run?.Session.RequestNextLine();

    /// <summary>
    /// Whether starting this node group now would run a node: at least one has all its conditions true. A plain node
    /// always would.
    /// </summary>
    /// <exception cref="ArgumentException">The project has no node of this name.</exception>
    public bool HasSalientContent(string nodeGroup) => GetSession().HasSalientContent(nodeGroup);

    private DialogueSession GetSession()
    {
        var system = System;
        if (Script is null)
            throw NotRunning();

        var project = Project ?? throw new InvalidOperationException($"The dialogue runner of '{Entity?.Name}' has no Yarn project.");
        var variables = VariablesOverride ?? system.Variables;
        var lines = LinesOverride ?? system.GetLineProvider(project);
        if (session is not null && sessionOf == (project, variables, lines))
            return session;

        sessionOf = (project, variables, lines);
        session = new DialogueSession(project.GetProgram(), variables, lines, system.Actions, new Responder(this), new DialogueSessionOptions
        {
            Saliency = Saliency ?? project.Saliency,
            Clock = new GameDialogueClock(Script, Game),
            Log = Log,
            Nodes = project.Nodes,
            Targets = new EntityTargetResolver(Entity),
        });
        return session;
    }

    private InvalidOperationException NotRunning()
        => new($"The dialogue runner of '{Entity?.Name}' is not in a scene of a running game: its dialogues start once its scene runs.");

    private async Task RunAsync(Run current, string node)
    {
        ExceptionDispatchInfo? failure = null;
        try
        {
            foreach (var presenter in Presenters)
            {
                if (presenter is not null)
                    presenter.Runner = this;
            }

            DialogueStarted?.Invoke();
            await All(p => p.OnDialogueStartedAsync());
            await current.Session.RunAsync(node, current.Stopping.Token);
        }
        catch (Exception exception)
        {
            failure = ExceptionDispatchInfo.Capture(exception);
        }

        // Free before the end is told: who listens can start the next dialogue
        run = null;
        current.Stopping.Dispose();
        try
        {
            DialogueCompleted?.Invoke();
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }

        if (failure is null)
        {
            current.Completion.SetResult();
            return;
        }

        current.Completion.SetException(failure.SourceException);
        failure.Throw();
    }

    /// <summary>
    /// Calls every presenter at once, and waits for all.
    /// </summary>
    private Task All(Func<DialoguePresenter, Task> call)
    {
        List<Task>? tasks = null;
        foreach (var presenter in Presenters)
        {
            if (presenter is null)
                continue;

            var task = call(presenter);
            if (!task.IsCompletedSuccessfully)
                (tasks ??= []).Add(task);
        }

        return tasks is null ? Task.CompletedTask : Task.WhenAll(tasks);
    }

    private async ValueTask<int?> AskOptionsAsync(IReadOnlyList<DialogueOption> options, CancellationToken cancellation)
    {
        if (OptionsFallThrough && !options.Any(o => o.IsAvailable))
            return null;

        // The first presenter that gives a choice answers; the others are told to leave
        using var asking = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var pending = new List<Task<int?>>();
        foreach (var presenter in Presenters)
        {
            if (presenter is not null)
                pending.Add(presenter.RunOptionsAsync(options, asking.Token));
        }

        int? choice = null;
        try
        {
            while (pending.Count > 0 && choice is null)
            {
                var answered = await Task.WhenAny(pending);
                pending.Remove(answered);
                choice = await answered;
            }
        }
        finally
        {
            asking.Cancel();
            foreach (var other in pending)
            {
                try
                {
                    await other;
                }
                catch (OperationCanceledException)
                {
                    // It left as asked
                }
            }
        }

        if (choice is null && !cancellation.IsCancellationRequested && !reportedNoAnswer)
        {
            reportedNoAnswer = true;
            Log.Error($"No presenter of the dialogue runner of '{Entity?.Name}' answered the options of the node '{CurrentNode}': the dialogue goes on as if no choice was made. Add a presenter that shows options.");
        }

        return choice;
    }

    private void OnUnhandledCommand(string commandText)
    {
        if (UnhandledCommand is { } handler)
        {
            handler(commandText);
            return;
        }

        var words = YarnCommandText.Split(commandText);
        var name = words.Count > 0 ? words[0] : commandText;
        if (reportedCommands.Add(name))
            Log.Warning($"The command <<{commandText}>> has no handler: add a method with [YarnCommand(\"{name}\")] to the game, or listen to the runner's UnhandledCommand. The dialogue goes on.");
    }

    /// <summary>
    /// One dialogue of the runner, from its start to its end.
    /// </summary>
    private sealed class Run(DialogueSession session)
    {
        public DialogueSession Session { get; } = session;

        public CancellationTokenSource Stopping { get; } = new();

        public TaskCompletionSource Completion { get; } = new();
    }

    /// <summary>
    /// What the session asks, given to the presenters and told by the runner's events.
    /// </summary>
    private sealed class Responder(DialogueRunnerComponent runner) : IDialogueResponder
    {
        public async ValueTask PrepareForLinesAsync(IReadOnlyList<string> lineIds) => await runner.All(p => p.PrepareForLinesAsync(lineIds));

        public async ValueTask HandleLineAsync(LocalizedLine line, LineCancellation cancellation) => await runner.All(p => p.RunLineAsync(line, cancellation));

        public ValueTask<int?> HandleOptionsAsync(IReadOnlyList<DialogueOption> options, CancellationToken cancellation) => runner.AskOptionsAsync(options, cancellation);

        public ValueTask HandleCommandAsync(string commandText, CancellationToken cancellation)
        {
            runner.OnUnhandledCommand(commandText);
            return default;
        }

        public async ValueTask HandleNodeStartAsync(string node)
        {
            runner.NodeStarted?.Invoke(node);
            await runner.All(p => p.OnNodeEnterAsync(node));
        }

        public async ValueTask HandleNodeCompleteAsync(string node)
        {
            runner.NodeCompleted?.Invoke(node);
            await runner.All(p => p.OnNodeExitAsync(node));
        }

        public async ValueTask HandleDialogueCompleteAsync() => await runner.All(p => p.OnDialogueCompletedAsync());
    }
}
