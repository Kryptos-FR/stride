using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.ExceptionServices;
using Stride.Core.Diagnostics;
using Yarn;
using Yarn.Saliency;

namespace ColorRise.Skein;

/// <summary>
/// What a <see cref="DialogueSession"/> runs with, beyond its program and its services.
/// </summary>
public sealed class DialogueSessionOptions
{
    /// <summary>
    /// How the session picks among the nodes of a group. <see cref="DialogueSession.Saliency"/> changes it later.
    /// </summary>
    public YarnSaliency Saliency { get; init; } = YarnSaliency.RandomBestLeastRecentlyViewed;

    /// <summary>
    /// When set, <c>random</c>, <c>random_range</c>, <c>random_range_float</c>, <c>dice</c> and the random saliency pick
    /// take their numbers from this seed, anew at each run: the same choices give the same run.
    /// </summary>
    public int? RandomSeed { get; init; }

    /// <summary>
    /// The time <c>&lt;&lt;wait&gt;&gt;</c> waits in. The computer's by default.
    /// </summary>
    public IDialogueClock Clock { get; init; } = SystemDialogueClock.Instance;

    /// <summary>
    /// What finds the objects the commands of this session name; the registry's own by default.
    /// </summary>
    public IYarnTargetResolver? Targets { get; init; }

    /// <summary>
    /// Where the session tells what goes wrong: a command that cannot be called, a function nothing provides.
    /// </summary>
    public ILogger? Log { get; init; }

    /// <summary>
    /// What the nodes say of themselves (<see cref="YarnProject.Nodes"/>): with it, a <see cref="SaliencyPick"/> gives
    /// the failed <c>when:</c> as written.
    /// </summary>
    public IReadOnlyList<YarnNodeInfo>? Nodes { get; init; }
}

/// <summary>
/// Runs the nodes of a compiled Yarn program and asks a responder to show what they say. It needs no game: the Stride
/// runner, Game Studio's player and the tests all drive this class.
/// </summary>
/// <remarks>
/// Everything happens on the thread that calls <see cref="RunAsync"/> and where its awaits resume; the other members are
/// called from that thread too (from a responder, or between two of its awaits).
/// </remarks>
public sealed class DialogueSession : IYarnDialogueEvents
{
    private static readonly string[] RandomFunctions = ["random", "random_range", "random_range_float", "dice"];

    private readonly IDialogueResponder responder;
    private readonly YarnActionRegistry actions;
    private readonly DialogueSessionOptions options;
    private readonly ILogger? log;
    private readonly Yarn32Adapter adapter;
    private readonly SeededSaliencyStrategy saliency;
    private readonly HashSet<string> calledFunctions = [];
    private readonly HashSet<string> reportedFunctions = [];
    private readonly Dictionary<string, YarnNodeInfo> nodeInfos = [];
    private Random random = Random.Shared;
    private CancellationTokenSource? stop;
    private CancellationTokenSource? hurryUp;
    private CancellationTokenSource? nextLine;

    /// <summary>
    /// Makes a session.
    /// </summary>
    /// <param name="program">The compiled program.</param>
    /// <param name="variables">Where the variables live. Several sessions, of one project or of several, can share it.</param>
    /// <param name="lines">What turns a line id into a line of the current language.</param>
    /// <param name="actions">The commands and the functions of the game.</param>
    /// <param name="responder">Who shows the dialogue.</param>
    /// <param name="options">The rest; null for the defaults.</param>
    public DialogueSession(Program program, VariableStorage variables, ILineProvider lines, YarnActionRegistry actions, IDialogueResponder responder, DialogueSessionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(variables);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(responder);

        Program = program;
        Variables = variables;
        Lines = lines;
        this.actions = actions;
        this.responder = responder;
        this.options = options ?? new DialogueSessionOptions();
        log = this.options.Log;

        var view = new SessionVariables(variables, program);
        adapter = new Yarn32Adapter(view, program);
        adapter.Dialogue.LogErrorMessage = message => log?.Error(message);
        saliency = new SeededSaliencyStrategy(view, () => random, OnPicked) { Kind = this.options.Saliency };
        adapter.Dialogue.ContentSaliencyStrategy = saliency;

        // Yarn Spinner's own asks the strategy: ours asks without telling a pick
        var library = adapter.Dialogue.Library;
        library.DeregisterFunction("has_any_content");
        library.RegisterFunction("has_any_content", (string nodeGroup) => program.Nodes.ContainsKey(nodeGroup) && HasSalientContent(nodeGroup));
        if (this.options.RandomSeed is not null)
        {
            // The same formulas as Yarn Spinner's, on the session's numbers
            foreach (var name in RandomFunctions)
            {
                library.DeregisterFunction(name);
            }

            library.RegisterFunction("random", () => (float)random.NextDouble());
            library.RegisterFunction("random_range", (float min, float max) => random.Next((int)max - (int)min + 1) + min);
            library.RegisterFunction("random_range_float", (float min, float max) => random.Next((int)max - (int)min + 1) + min);
            library.RegisterFunction("dice", (int sides) => random.Next(sides) + 1);
        }

        foreach (var node in program.Nodes.Values)
        {
            foreach (var instruction in node.Instructions)
            {
                if (instruction.InstructionTypeCase == Instruction.InstructionTypeOneofCase.CallFunc)
                    calledFunctions.Add(instruction.CallFunc.FunctionName);
            }
        }

        foreach (var info in this.options.Nodes ?? [])
        {
            nodeInfos.TryAdd(info.Name, info);
        }
    }

    /// <summary>
    /// A pick was made among the nodes of a node group or the lines of a line group.
    /// </summary>
    public event Action<SaliencyPick>? SaliencyPicked;

    /// <summary>
    /// The compiled program.
    /// </summary>
    public Program Program { get; }

    /// <summary>
    /// Where the variables live.
    /// </summary>
    public VariableStorage Variables { get; }

    /// <summary>
    /// What turns a line id into a line of the current language.
    /// </summary>
    public ILineProvider Lines { get; }

    /// <summary>
    /// Whether a dialogue is running.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// The name, in the program, of the node the dialogue is in; null when none runs.
    /// </summary>
    public string? CurrentNode => IsRunning ? adapter.Dialogue.CurrentNode : null;

    /// <summary>
    /// How the session picks among the nodes of a group. It can change between two runs or during one.
    /// </summary>
    public YarnSaliency Saliency
    {
        get => saliency.Kind;
        set => saliency.Kind = value;
    }

    /// <summary>
    /// Whether the program has a node, or a node group, of this name.
    /// </summary>
    public bool NodeExists(string node) => Program.Nodes.ContainsKey(node);

    /// <summary>
    /// Whether running this node group now would run a node: at least one has all its conditions true. A plain node
    /// always would.
    /// </summary>
    /// <exception cref="ArgumentException">The program has no node of this name.</exception>
    public bool HasSalientContent(string nodeGroup)
    {
        var wasAsking = saliency.IsAsking;
        saliency.IsAsking = true;
        try
        {
            return adapter.Dialogue.HasSalientContent(nodeGroup);
        }
        finally
        {
            saliency.IsAsking = wasAsking;
        }
    }

    /// <summary>
    /// Runs a node, or a node of a node group, to the end of the dialogue. The task completes when the dialogue is
    /// over, after <see cref="IDialogueResponder.HandleDialogueCompleteAsync"/>; a stop is an end like another, not an
    /// exception.
    /// </summary>
    /// <param name="node">The title of the node or of the node group.</param>
    /// <param name="cancellation">Stops the dialogue, as <see cref="Stop"/> does.</param>
    /// <exception cref="ArgumentException">The program has no node of this name.</exception>
    /// <exception cref="InvalidOperationException">The session is already running a dialogue.</exception>
    public async Task RunAsync(string node, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (IsRunning)
            throw new InvalidOperationException($"The session is already running a dialogue (in the node '{CurrentNode}'): stop it before running '{node}'.");
        if (!Program.Nodes.ContainsKey(node))
            throw new ArgumentException($"The program has no node named '{node}'.", nameof(node));

        IsRunning = true;
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        stop = stopping;
        ExceptionDispatchInfo? failure = null;
        try
        {
            if (options.RandomSeed is { } seed)
                random = new Random(seed);
            PrepareFunctions();
            await adapter.RunAsync(node, this, stopping.Token);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // A responder left by its cancellation: the stop it was asked for
        }
        catch (Exception exception)
        {
            failure = ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            stop = null;
            IsRunning = false;
        }

        // Once per run, whatever ended it, and with the session free to run again
        await responder.HandleDialogueCompleteAsync();
        failure?.Throw();
    }

    /// <summary>
    /// Stops the dialogue: what the responder is awaiting is cancelled, and <see cref="RunAsync"/> completes. Does
    /// nothing when no dialogue runs.
    /// </summary>
    public void Stop() => stop?.Cancel();

    /// <summary>
    /// Asks the line being shown to finish quickly (<see cref="LineCancellation.HurryUp"/>).
    /// </summary>
    public void RequestHurryUp() => hurryUp?.Cancel();

    /// <summary>
    /// Asks the line being shown to end now (<see cref="LineCancellation.NextLine"/>).
    /// </summary>
    public void RequestNextLine()
    {
        hurryUp?.Cancel();
        nextLine?.Cancel();
    }

    ValueTask IYarnDialogueEvents.NodeStartAsync(string node) => responder.HandleNodeStartAsync(node);

    ValueTask IYarnDialogueEvents.NodeCompleteAsync(string node) => responder.HandleNodeCompleteAsync(node);

    ValueTask IYarnDialogueEvents.PrepareForLinesAsync(IReadOnlyList<string> lineIds) => responder.PrepareForLinesAsync(lineIds);

    async ValueTask IYarnDialogueEvents.LineAsync(Line line)
    {
        var localized = Lines.GetLine(line.ID, line.Substitutions ?? []);
        using var hurrying = CancellationTokenSource.CreateLinkedTokenSource(stop!.Token);
        using var ending = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
        hurryUp = hurrying;
        nextLine = ending;
        try
        {
            await responder.HandleLineAsync(localized, new LineCancellation(hurrying.Token, ending.Token));
        }
        catch (OperationCanceledException) when (ending.IsCancellationRequested && !stop!.IsCancellationRequested)
        {
            // The presenter left by the cancellation of its line: the line is over
        }
        finally
        {
            hurryUp = null;
            nextLine = null;
        }
    }

    async ValueTask<int> IYarnDialogueEvents.OptionsAsync(OptionSet set)
    {
        var offered = new List<DialogueOption>(set.Options.Length);
        foreach (var option in set.Options)
        {
            offered.Add(new DialogueOption(option.ID, Lines.GetLine(option.Line.ID, option.Line.Substitutions ?? []), option.IsAvailable));
        }

        var choice = await responder.HandleOptionsAsync(offered, stop!.Token);
        if (choice is null)
            return Dialogue.NoOptionSelected;

        foreach (var option in offered)
        {
            if (option.Index == choice)
                return choice.Value;
        }

        throw new InvalidOperationException($"The responder chose the option {choice}, which was not offered: the choice is the Index of one of the {offered.Count} options, or null.");
    }

    async ValueTask IYarnDialogueEvents.CommandAsync(string text)
    {
        var cancellation = stop!.Token;
        try
        {
            if (await actions.RunCommandAsync(text, options.Targets, cancellation))
                return;
        }
        catch (YarnCommandException exception)
        {
            // The script names something the game lacks: the dialogue goes on without the command
            log?.Error(exception.Message);
            return;
        }

        // The command every Yarn Spinner runner has, unless the game gave its own. (<<stop>> never comes here: the
        // compiler makes it an instruction, and the dialogue ends by itself.)
        if (YarnCommandText.Split(text) is ["wait", var duration] && float.TryParse(duration, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            await options.Clock.Delay(TimeSpan.FromSeconds(Math.Max(seconds, 0)), cancellation);
            return;
        }

        await responder.HandleCommandAsync(text, cancellation);
    }

    /// <summary>
    /// Gives the dialogue the functions of the game as they are now, and a stand-in for each function the program calls
    /// and nothing provides, so that a script written ahead of the game's code runs.
    /// </summary>
    private void PrepareFunctions()
    {
        var library = adapter.Dialogue.Library;
        actions.CopyFunctionsTo(library);
        foreach (var name in calledFunctions)
        {
            if (library.FunctionExists(name))
                continue;

            library.RegisterFunction(name, (object[] arguments) =>
            {
                if (reportedFunctions.Add(name))
                    log?.Error($"The scripts call the function '{name}', which the game does not provide: it gives false (0 as a number) until it does.");
                return false;
            });
        }
    }

    private void OnPicked(IReadOnlyList<ContentSaliencyOption> candidates, ContentSaliencyOption? chosen)
    {
        if (SaliencyPicked is not { } handler || candidates.Count == 0)
            return;

        var isLineGroup = candidates[0].ContentType == ContentSaliencyContentType.Line;
        var group = adapter.Dialogue.CurrentNode ?? "";
        var described = new List<SaliencyCandidate>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var passed = candidate.FailingConditionValueCount == 0;
            string? failed = null;
            if (!isLineGroup && Program.Nodes.TryGetValue(candidate.ContentID, out var node))
            {
                group = node.NodeGroup ?? group;
                if (!passed)
                    failed = FailedCondition(node);
            }

            described.Add(new SaliencyCandidate(candidate.ContentID, candidate.ComplexityScore, passed, failed, saliency.ViewCountOf(candidate)));
        }

        handler(new SaliencyPick(group, isLineGroup, described, chosen?.ContentID));
    }

    /// <summary>
    /// The first <c>when:</c> of a node that does not hold: the compiler made one smart variable per <c>when:</c>, in order.
    /// </summary>
    private string? FailedCondition(Node node)
    {
        var index = 0;
        foreach (var variable in node.ContentSaliencyConditionVariables)
        {
            if (adapter.Dialogue.TryGetSmartVariable<bool>(variable, out var holds) && !holds)
            {
                if (nodeInfos.TryGetValue(node.Name, out var info))
                {
                    var conditions = info.Headers.Where(h => h.Key == "when").ToList();
                    if (index < conditions.Count)
                        return conditions[index].Value;
                }

                return $"when #{index + 1}";
            }

            index++;
        }

        return null;
    }

    /// <summary>
    /// The storage as the dialogue of this session reads it: the defaults and the smart variables are this program's,
    /// the values are shared with the other sessions of the storage.
    /// </summary>
    private sealed class SessionVariables : IVariableStorage
    {
        private readonly VariableStorage storage;
        private readonly HashSet<string> smart = [];
        private Program? program;

        public SessionVariables(VariableStorage storage, Program program)
        {
            this.storage = storage;
            Program = program;
        }

        public Program? Program
        {
            get => program;
            set
            {
                // Yarn Spinner sets it null first, then the program
                program = value;
                smart.Clear();
                if (value is null)
                    return;

                storage.Declare(value);
                foreach (var node in value.Nodes.Values)
                {
                    if (node.Tags.Contains("Yarn.SmartVariable"))
                        smart.Add(node.Name);
                }
            }
        }

        public ISmartVariableEvaluator? SmartVariableEvaluator { get; set; }

        public bool TryGetValue<T>(string variableName, [NotNullWhen(true)] out T? result)
        {
            if (storage.TryGetStored(variableName, program, out result, out var declared))
                return true;

            if (!declared && SmartVariableEvaluator is not null && smart.Contains(variableName))
                return SmartVariableEvaluator.TryGetSmartVariable(variableName, out result);

            return false;
        }

        public VariableKind GetVariableKind(string name)
            => smart.Contains(name) ? VariableKind.Smart : storage.GetVariableKind(name, program);

        public void SetValue(string variableName, string stringValue) => storage.SetValue(variableName, stringValue);

        public void SetValue(string variableName, float floatValue) => storage.SetValue(variableName, floatValue);

        public void SetValue(string variableName, bool boolValue) => storage.SetValue(variableName, boolValue);

        public void Clear() => storage.Clear();
    }
}
