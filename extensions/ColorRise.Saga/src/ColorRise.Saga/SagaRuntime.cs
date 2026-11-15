using ColorRise.Saga.Content;
using ColorRise.Saga.Expressions;
using Stride.Core.Diagnostics;

namespace ColorRise.Saga;

/// <summary>
/// Runs a story: gameplay code raises events, sets variables, registers functions and listens to signals, all by the names the
/// story declares; the runtime moves the plot, scenarios and quests forward.
/// </summary>
/// <remarks>
/// After each change the runtime settles: it re-evaluates the story until nothing changes. Signals and <see cref="Changed"/>
/// reach the game once the story has settled, in the order they happened; a handler can call the runtime back.
/// </remarks>
public sealed partial class SagaRuntime
{
    private readonly ILogger logger;
    private readonly SagaScope scope;
    private readonly Dictionary<string, VariableDeclaration> variables;
    private readonly Dictionary<string, FunctionDeclaration> functionDeclarations;
    private readonly Dictionary<string, Func<IReadOnlyList<SagaValue>, SagaValue>> functions = [];
    private readonly HashSet<string> unregisteredFunctionsReported = [];
    private readonly HashSet<string> events;
    private readonly HashSet<string> signals;
    private readonly HashSet<string> characters;
    private readonly Dictionary<string, List<Action<SagaSignal>>> handlers = [];
    private readonly Queue<object> notifications = new();
    private readonly Context context;
    private bool flushing;

    /// <summary>
    /// Creates a runtime for a story.
    /// </summary>
    /// <param name="story">The compiled story.</param>
    /// <param name="state">A saved state to resume from. What the story no longer has is dropped, and what it gained starts
    /// from its default.</param>
    /// <param name="logger">Where to report problems the story can live with.</param>
    public SagaRuntime(StoryDefinition story, SagaState? state = null, ILogger? logger = null)
    {
        Story = story ?? throw new ArgumentNullException(nameof(story));
        this.logger = logger ?? GlobalLogger.GetLogger("Saga");
        scope = SagaScope.FromStory(story);
        var declarations = story.Declarations;
        variables = declarations.Variables.GroupBy(x => x.Name).ToDictionary(x => x.Key, x => x.First());
        functionDeclarations = declarations.Functions.GroupBy(x => x.Name).ToDictionary(x => x.Key, x => x.First());
        events = [.. declarations.Events.Select(x => x.Name)];
        signals = [.. declarations.Signals.Select(x => x.Name)];
        characters = [.. declarations.Characters.Select(x => x.Id)];
        IndexPlot();
        context = new Context(this);
        State = state ?? new SagaState();
        Normalize(state is not null);
    }

    /// <summary>
    /// Raised for each change of an act, a scenario or a quest, once the story has settled.
    /// </summary>
    public event EventHandler<SagaChangedEventArgs>? Changed;

    public StoryDefinition Story { get; }

    /// <summary>
    /// The live state. Use <see cref="CaptureState"/> to save it.
    /// </summary>
    public SagaState State { get; }

    /// <summary>
    /// Whether the story started: an act was entered.
    /// </summary>
    public bool IsStarted => State.CurrentActId is not null || State.CompletedActs.Count > 0;

    /// <summary>
    /// Whether the story is over: its last act ended and no link led further.
    /// </summary>
    public bool IsOver => State.CurrentActId is null && State.CompletedActs.Count > 0;

    /// <summary>
    /// Starts the story, entering its first act, or resumes a restored one; then settles.
    /// </summary>
    public void Start()
    {
        Change(() =>
        {
            if (!IsStarted && acts.ContainsKey(Story.Plot.FirstActId))
                EnterAct(Story.Plot.FirstActId);
        });
    }

    /// <summary>
    /// Raises a declared event.
    /// </summary>
    /// <exception cref="ArgumentException">The story does not declare the event.</exception>
    public void Raise(string eventName)
    {
        if (!events.Contains(eventName))
            throw new ArgumentException($"The story declares no event '{eventName}'.", nameof(eventName));
        Change(() => CountEvent(eventName));
    }

    /// <summary>
    /// Sets a declared variable. A whole number is accepted for a number.
    /// </summary>
    /// <exception cref="ArgumentException">The story does not declare the variable, or the value is not of its type.</exception>
    public void Set(string variable, SagaValue value)
    {
        var declaration = FindVariable(variable);
        var converted = SagaLiterals.Convert(value, declaration.Type)
            ?? throw new ArgumentException($"'{variable}' is {SagaTypeChecker.Describe(declaration.Type)}; {value} is not.", nameof(value));
        if (SagaSymbolSets.ForType(declaration.Type) is { } symbolSet && !scope.GetSymbols(symbolSet).Contains(converted.AsSymbol()))
            throw new ArgumentException($"'{converted.AsSymbol()}' is not {SagaTypeChecker.Describe(declaration.Type)}.", nameof(value));
        Change(() => State.Variables[variable] = converted);
    }

    /// <summary>
    /// Gets a declared variable.
    /// </summary>
    /// <exception cref="ArgumentException">The story does not declare the variable.</exception>
    public SagaValue Get(string variable)
    {
        FindVariable(variable);
        return State.Variables[variable];
    }

    /// <summary>
    /// Registers the implementation of a declared function. Until a function is registered, it returns the default of its
    /// type and the runtime logs an error once.
    /// </summary>
    /// <exception cref="ArgumentException">The story does not declare the function.</exception>
    public void RegisterFunction(string name, Func<IReadOnlyList<SagaValue>, SagaValue> function)
    {
        if (!functionDeclarations.ContainsKey(name))
            throw new ArgumentException($"The story declares no function '{name}'.", nameof(name));
        functions[name] = function ?? throw new ArgumentNullException(nameof(function));
    }

    /// <summary>
    /// Listens to a declared signal. Dispose the result to stop.
    /// </summary>
    /// <exception cref="ArgumentException">The story does not declare the signal.</exception>
    public IDisposable On(string signal, Action<SagaSignal> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (!signals.Contains(signal))
            throw new ArgumentException($"The story declares no signal '{signal}'.", nameof(signal));
        if (!handlers.TryGetValue(signal, out var list))
            handlers[signal] = list = [];
        list.Add(handler);
        return new Subscription(() => list.Remove(handler));
    }

    /// <summary>
    /// Copies the state, for the game to save.
    /// </summary>
    public SagaState CaptureState() => new()
    {
        CurrentActId = State.CurrentActId,
        CompletedActs = [.. State.CompletedActs],
        Variables = new Dictionary<string, SagaValue>(State.Variables),
        Quests = State.Quests.ToDictionary(x => x.Key, x => new QuestProgress { State = x.Value.State, Stage = x.Value.Stage }),
        Scenarios = State.Scenarios.ToDictionary(x => x.Key, x => new ScenarioProgress { State = x.Value.State, Outcome = x.Value.Outcome }),
        Events = new Dictionary<string, int>(State.Events),
        Visits = new Dictionary<string, int>(State.Visits),
        UsedOnce = [.. State.UsedOnce],
    };

    /// <summary>
    /// Starts talking to a character: the dialogue attached to it with the highest priority whose condition holds, that is not
    /// used up, and whose owner is live (a running scenario, or a quest from available to its outcome) unless it opted out.
    /// Ties go to the dialogue declared first.
    /// </summary>
    /// <returns>The session, or null when no dialogue of the character can play.</returns>
    /// <exception cref="ArgumentException">The story does not declare the character.</exception>
    public DialogueSession? Talk(string characterId)
    {
        if (!characters.Contains(characterId))
            throw new ArgumentException($"The story declares no character '{characterId}'.", nameof(characterId));

        DialogueDefinition? best = null;
        foreach (var (dialogue, isOwnerLive) in DialoguesOf(characterId))
        {
            if (best is not null && dialogue.Priority <= best.Priority)
                continue;
            if (dialogue.Once && State.UsedOnce.Contains(dialogue.Name))
                continue;
            if (dialogue.OnlyWhileOwnerLive && !isOwnerLive())
                continue;
            if (Holds(dialogue.When, $"dialogue {dialogue.Name}", "activation"))
                best = dialogue;
        }
        if (best is null)
            return null;

        var session = new DialogueSession(this, logger, best);
        session.Begin();
        return session;
    }

    /// <summary>
    /// Runs actions of a card inside a change (a dialogue line or choice); the change settles afterwards.
    /// </summary>
    internal void RunActionsUnsettled(SagaActions actions, string card, string what) => Run(actions, card, what);

    /// <summary>
    /// Evaluates a condition of a card outside settling (a dialogue's activation, a choice).
    /// </summary>
    internal bool Evaluate(SagaCondition condition, string card, string what) => Holds(condition, card, what);

    /// <summary>
    /// Applies a change, settles, then tells the game what happened.
    /// </summary>
    internal void Change(Action change)
    {
        try
        {
            change();
            Settle();
        }
        catch
        {
            notifications.Clear();
            throw;
        }
        Flush();
    }

    private VariableDeclaration FindVariable(string variable) => variables.GetValueOrDefault(variable)
        ?? throw new ArgumentException($"The story declares no variable '{variable}'.", nameof(variable));

    private void CountEvent(string name) => State.Events[name] = State.Events.GetValueOrDefault(name) + 1;

    private void Notify(SagaChangeKind kind, string id) => notifications.Enqueue(new SagaChangedEventArgs(kind, id));

    private void Flush()
    {
        if (flushing)
            return;
        flushing = true;
        try
        {
            while (notifications.TryDequeue(out var notification))
            {
                switch (notification)
                {
                    case SagaSignal signal when handlers.TryGetValue(signal.Name, out var list):
                        foreach (var handler in list.ToList())
                            handler(signal);
                        break;
                    case SagaChangedEventArgs change:
                        Changed?.Invoke(this, change);
                        break;
                }
            }
        }
        finally
        {
            flushing = false;
        }
    }

    /// <summary>
    /// Fits a state to the story: drops what the story no longer has, adds what it gained.
    /// </summary>
    private void Normalize(bool restored)
    {
        var dropped = new List<string>();

        foreach (var name in State.Variables.Keys.ToList())
        {
            if (!variables.TryGetValue(name, out var declaration))
            {
                State.Variables.Remove(name);
                dropped.Add($"variable {name}");
            }
            else if (SagaLiterals.Convert(State.Variables[name], declaration.Type) is { } converted)
            {
                State.Variables[name] = converted;
            }
            else
            {
                State.Variables.Remove(name);
                dropped.Add($"variable {name} (now {SagaTypeChecker.Describe(declaration.Type)})");
            }
        }
        foreach (var declaration in variables.Values.Where(x => !State.Variables.ContainsKey(x.Name)))
        {
            if (SagaLiterals.TryParse(declaration.Default, declaration.Type, out var value) is { } error)
                logger.Error($"Saga: the default of variable '{declaration.Name}' is wrong ({error}); it starts as {value}.");
            State.Variables[declaration.Name] = value;
        }

        DropUnknown(State.Quests, quests, "quest", dropped);
        foreach (var id in quests.Keys.Where(x => !State.Quests.ContainsKey(x)))
            State.Quests[id] = new QuestProgress();
        foreach (var (id, progress) in State.Quests)
            progress.Stage = Math.Clamp(progress.Stage, 0, quests[id].Quest.Stages.Count);

        DropUnknown(State.Scenarios, scenarios, "scenario", dropped);
        foreach (var id in scenarios.Keys.Where(x => !State.Scenarios.ContainsKey(x)))
            State.Scenarios[id] = new ScenarioProgress();

        foreach (var name in State.Events.Keys.Where(x => !events.Contains(x)).ToList())
        {
            State.Events.Remove(name);
            dropped.Add($"event {name}");
        }
        foreach (var act in State.CompletedActs.Where(x => !acts.ContainsKey(x)).ToList())
        {
            State.CompletedActs.Remove(act);
            dropped.Add($"act {act}");
        }
        if (State.CurrentActId is { } current && !acts.ContainsKey(current))
        {
            logger.Warning($"Saga: the saved act '{current}' is no longer in the story; the story resumes at its first act '{Story.Plot.FirstActId}'.");
            State.CurrentActId = acts.ContainsKey(Story.Plot.FirstActId) ? Story.Plot.FirstActId : null;
        }

        if (restored && dropped.Count > 0)
            logger.Warning($"Saga: the saved state had what the story no longer has; dropped: {string.Join(", ", dropped)}.");
    }

    private static void DropUnknown<T, TDefinition>(Dictionary<string, T> saved, Dictionary<string, TDefinition> known, string kind, List<string> dropped)
    {
        foreach (var id in saved.Keys.Where(x => !known.ContainsKey(x)).ToList())
        {
            saved.Remove(id);
            dropped.Add($"{kind} {id}");
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? dispose = dispose;

        public void Dispose()
        {
            dispose?.Invoke();
            dispose = null;
        }
    }

    /// <summary>
    /// What conditions read and actions change.
    /// </summary>
    private sealed class Context(SagaRuntime runtime) : ISagaExecutionContext
    {
        private SagaState State => runtime.State;

        public SagaValue Resolve(string path)
        {
            if (State.Variables.TryGetValue(path, out var value))
                return value;
            if (path == "act")
                return SagaValue.Symbol(State.CurrentActId ?? string.Empty);
            if (SagaScope.TrySplit(path, "quest.", out var id, out var member) && State.Quests.TryGetValue(id, out var quest))
            {
                if (member == "state")
                    return SagaValue.Symbol(SagaSymbolSets.QuestStateNames[(int)quest.State]);
                if (member == "stage")
                    return SagaValue.Int(quest.Stage);
            }
            if (SagaScope.TrySplit(path, "scenario.", out id, out member) && State.Scenarios.TryGetValue(id, out var scenario))
            {
                if (member == "state")
                    return SagaValue.Symbol(SagaSymbolSets.ScenarioStateNames[(int)scenario.State]);
                if (member == "outcome")
                    return SagaValue.Symbol(scenario.Outcome ?? string.Empty);
            }
            throw new SagaException($"Unknown name '{path}'.");
        }

        public SagaValue Call(string function, IReadOnlyList<SagaValue> arguments)
        {
            if (runtime.functions.TryGetValue(function, out var implementation))
                return implementation(arguments);
            var declaration = runtime.functionDeclarations.GetValueOrDefault(function) ?? throw new SagaException($"Unknown function '{function}'.");
            var value = SagaLiterals.DefaultOf(declaration.ReturnType);
            if (runtime.unregisteredFunctionsReported.Add(function))
                runtime.logger.Error($"Saga: the story calls the function '{function}', which the game did not register; it gives {value} until it is.");
            return value;
        }

        public int EventCount(string name) => State.Events.GetValueOrDefault(name);

        public int Visits(string name) => State.Visits.GetValueOrDefault(name);

        public void Assign(string path, SagaValue value)
        {
            if (runtime.variables.TryGetValue(path, out var declaration))
            {
                State.Variables[path] = SagaLiterals.Convert(value, declaration.Type) ?? throw new SagaException($"'{path}' is {SagaTypeChecker.Describe(declaration.Type)}; {value} is not.");
                return;
            }
            if (SagaScope.TrySplit(path, "quest.", out var id, out var member) && runtime.quests.ContainsKey(id))
            {
                if (member == "state")
                {
                    var index = value.Kind == SagaValueKind.Symbol ? SagaSymbolSets.QuestStateNames.ToList().IndexOf(value.AsSymbol()) : -1;
                    if (index < 0)
                        throw new SagaException($"{value} is not a quest state.");
                    runtime.SetQuestState(id, (QuestState)index);
                    return;
                }
                if (member == "stage")
                {
                    runtime.SetQuestStage(id, value.AsInt());
                    return;
                }
            }
            throw new SagaException($"'{path}' can't be assigned.");
        }

        public void Raise(string name) => runtime.CountEvent(name);

        public void Emit(string signal, IReadOnlyList<SagaValue> arguments) => runtime.notifications.Enqueue(new SagaSignal(signal, arguments));
    }
}
