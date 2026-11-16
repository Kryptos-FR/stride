using System.Globalization;
using System.Reflection;

namespace ColorRise.Skein.Editor.Player;

/// <summary>
/// Where a run of the player waits.
/// </summary>
internal enum PlayerStop
{
    /// <summary>
    /// The run did not start, or is playing.
    /// </summary>
    None,

    /// <summary>
    /// After a line, for the user to go on.
    /// </summary>
    Line,

    /// <summary>
    /// At options, for a choice.
    /// </summary>
    Options,

    /// <summary>
    /// The dialogue is over.
    /// </summary>
    Ended,

    /// <summary>
    /// The dialogue failed.
    /// </summary>
    Failed,

    /// <summary>
    /// The dialogue called a function of the game that has no answer yet: the run is dead, and is played again once
    /// the answer is known.
    /// </summary>
    Question,
}

/// <summary>
/// One dialogue session of the player on a build: it records what the dialogue says as steps, and waits after each
/// line and at each set of options until it is told to go on. The commands go on at once, the functions of the game
/// take their answers from the player, and the random numbers come from the seed.
/// </summary>
/// <remarks>
/// A run goes forward only. Everything happens on the thread that drives it: when no wait is honoured, each call
/// returns with the run at its next stop.
/// </remarks>
internal sealed class PlayerRun : IDialogueResponder, IDialogueClock, IDisposable
{
    private readonly PlayerBuild build;
    private readonly IReadOnlyDictionary<string, object> answers;
    private readonly DialogueSession session;
    private readonly VariableStorage variables = new();
    private readonly Dictionary<string, object> defaults = [];
    private IReadOnlyDictionary<string, object>? snapshot;
    private TaskCompletionSource<int?>? waiting;
    private TaskCompletionSource? stopped;
    private PlayerStop stop;
    private string? node;
    private bool disposed;

    /// <param name="build">The scripts to play.</param>
    /// <param name="language">The language of the lines.</param>
    /// <param name="seed">The seed of the random numbers and of the random picks.</param>
    /// <param name="answers">The answers of the functions of the game, by call; the run reads it at each call.</param>
    public PlayerRun(PlayerBuild build, string language, int seed, IReadOnlyDictionary<string, object> answers)
    {
        this.build = build;
        this.answers = answers;

        var content = build.Content;
        var lines = new TextLineProvider(build.TableOf, content.BaseLanguage)
        {
            LineMetadata = content.LineMetadata,
            ShadowLines = content.ShadowLines,
            Language = language,
        };
        session = new DialogueSession(build.Program, variables, lines, new YarnActionRegistry(), this, new DialogueSessionOptions
        {
            Saliency = content.Saliency,
            RandomSeed = seed,
            Clock = this,
            Nodes = content.Nodes,
            MissingFunction = Ask,
        });
        session.SaliencyPicked += OnSaliencyPicked;
        variables.VariableChanged += (_, _) => snapshot = null;

        foreach (var variable in build.Compilation.Variables)
        {
            if (!variable.IsSmart && !variable.IsInternal && variable.Default is { } value)
                defaults[variable.Name] = value;
        }
    }

    /// <summary>
    /// The steps of the run so far.
    /// </summary>
    public List<PlayerStep> Steps { get; } = [];

    /// <summary>
    /// Where the run waits.
    /// </summary>
    public PlayerStop Stop => Question is not null ? PlayerStop.Question : stop;

    /// <summary>
    /// The call that has no answer, once the run is dead of it.
    /// </summary>
    public PlayerQuestion? Question { get; private set; }

    /// <summary>
    /// The options the run waits at.
    /// </summary>
    public IReadOnlyList<PlayerOption> Offered { get; private set; } = [];

    /// <summary>
    /// The index of the input being played, which the steps made from now on carry.
    /// </summary>
    public int Input { get; set; }

    /// <summary>
    /// Whether <c>wait</c> takes its time.
    /// </summary>
    public bool HonourWait { get; set; }

    /// <summary>
    /// Starts the dialogue at a node; completes at the first stop.
    /// </summary>
    public Task Start(string startNode)
    {
        var next = stopped = new TaskCompletionSource();
        _ = RunAsync(startNode);
        return next.Task;
    }

    /// <summary>
    /// Goes on after the line the run waits at; completes at the next stop.
    /// </summary>
    public Task Advance() => Resume(null);

    /// <summary>
    /// Chooses one of <see cref="Offered"/> by its index; completes at the next stop.
    /// </summary>
    public Task Choose(int option) => Resume(option);

    /// <summary>
    /// Sets a variable to a boolean, a number or a text, as a step.
    /// </summary>
    public void SetVariable(string name, object value)
    {
        switch (value)
        {
            case bool flag:
                variables.SetValue(name, flag);
                break;
            case string text:
                variables.SetValue(name, text);
                break;
            default:
                variables.SetValue(name, Convert.ToSingle(value, CultureInfo.InvariantCulture));
                break;
        }

        AddStep(PlayerStepKind.SetVariable, $"{name} = {Format(value)}");
    }

    /// <summary>
    /// Adds a step to the run, with the node and the variables of the moment.
    /// </summary>
    /// <param name="kind">What the step is.</param>
    /// <param name="text">The step in words.</param>
    /// <param name="lineId">The line whose place in the scripts is the step's.</param>
    public void AddStep(PlayerStepKind kind, string text, string? lineId = null) => Steps.Add(NewStep(kind, text, lineId));

    private PlayerStep NewStep(PlayerStepKind kind, string text, string? lineId = null)
    {
        var place = lineId is not null && build.Compilation.Lines.TryGetValue(lineId, out var line) ? line : null;
        return new PlayerStep
        {
            Kind = kind,
            Node = session.CurrentNode ?? node,
            File = place?.File,
            Line = place?.Line,
            Text = text,
            Body = text,
            Variables = Snapshot(),
            Input = Input,
        };
    }

    /// <summary>
    /// A value as a script writes it.
    /// </summary>
    public static string Format(object? value) => value switch
    {
        bool flag => flag ? "true" : "false",
        string text => $"\"{text}\"",
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => value?.ToString() ?? "",
    };

    /// <summary>
    /// A call as a script writes it, with its values.
    /// </summary>
    public static string CallText(string function, IEnumerable<object> arguments) => $"{function}({string.Join(", ", arguments.Select(Format))})";

    /// <inheritdoc />
    public void Dispose()
    {
        disposed = true;
        session.SaliencyPicked -= OnSaliencyPicked;
        session.Stop();
    }

    ValueTask IDialogueResponder.PrepareForLinesAsync(IReadOnlyList<string> lineIds) => ValueTask.CompletedTask;

    async ValueTask IDialogueResponder.HandleLineAsync(LocalizedLine line, LineCancellation cancellation)
    {
        Steps.Add(NewStep(PlayerStepKind.Line, line.Text.Text, line.Id) with { Speaker = line.CharacterName, Body = line.TextWithoutCharacterName.Text });
        await WaitAsync(PlayerStop.Line, cancellation.NextLine);
    }

    async ValueTask<int?> IDialogueResponder.HandleOptionsAsync(IReadOnlyList<DialogueOption> options, CancellationToken cancellation)
    {
        Offered = [.. options.Select(o => new PlayerOption(o.Index, o.Line.Id, o.Line.Text.Text, o.IsAvailable))];
        Steps.Add(NewStep(PlayerStepKind.Options, string.Join(" / ", Offered.Select(o => o.Text)), Offered.Count > 0 ? Offered[0].LineId : null) with { Options = Offered });
        var choice = await WaitAsync(PlayerStop.Options, cancellation);
        Offered = [];
        return choice;
    }

    ValueTask IDialogueResponder.HandleCommandAsync(string command, CancellationToken cancellation)
    {
        AddStep(PlayerStepKind.Command, command);
        return ValueTask.CompletedTask;
    }

    ValueTask IDialogueResponder.HandleNodeStartAsync(string node)
    {
        this.node = node;
        AddStep(PlayerStepKind.NodeEnter, build.TitleOf(node));
        return ValueTask.CompletedTask;
    }

    ValueTask IDialogueResponder.HandleNodeCompleteAsync(string node) => ValueTask.CompletedTask;

    ValueTask IDialogueResponder.HandleDialogueCompleteAsync() => ValueTask.CompletedTask;

    // The session's own wait: a command of the transcript like the others
    async ValueTask IDialogueClock.Delay(TimeSpan duration, CancellationToken cancellation)
    {
        AddStep(PlayerStepKind.Command, $"wait {duration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)}");
        if (HonourWait && duration > TimeSpan.Zero)
            await Task.Delay(duration, cancellation);
    }

    private async Task RunAsync(string startNode)
    {
        try
        {
            await session.RunAsync(startNode);
            if (!disposed && Question is null)
            {
                stop = PlayerStop.Ended;
                AddStep(PlayerStepKind.End, "");
            }
        }
        catch (Exception exception)
        {
            if (!disposed && Question is null)
            {
                stop = PlayerStop.Failed;
                AddStep(PlayerStepKind.Error, (exception is TargetInvocationException { InnerException: { } inner } ? inner : exception).Message);
            }
        }
        finally
        {
            waiting = null;
            stopped?.TrySetResult();
        }
    }

    private Task Resume(int? choice)
    {
        var input = waiting ?? throw new InvalidOperationException("The run does not wait for an input.");
        waiting = null;
        stop = PlayerStop.None;
        var next = stopped = new TaskCompletionSource();
        input.SetResult(choice);
        return next.Task;
    }

    private async Task<int?> WaitAsync(PlayerStop at, CancellationToken cancellation)
    {
        var input = new TaskCompletionSource<int?>();
        using var registration = cancellation.Register(() => input.TrySetCanceled(cancellation));
        waiting = input;
        stop = at;
        stopped?.TrySetResult();
        return await input.Task;
    }

    private object Ask(string function, IReadOnlyList<object> arguments)
    {
        var call = CallText(function, arguments);
        if (answers.TryGetValue(call, out var answer))
            return answer;

        // Yarn Spinner calls its functions without waiting: the run ends here, and is played again with the answer
        Question ??= new PlayerQuestion(function, call, [.. arguments], build.ReturnTypeOf(function));
        throw new InvalidOperationException($"The function {call} of the game has no answer yet.");
    }

    private void OnSaliencyPicked(SaliencyPick pick)
    {
        var candidates = new List<PlayerCandidate>(pick.Candidates.Count);
        foreach (var candidate in pick.Candidates)
        {
            var detail = candidate.Passed ? $"score {candidate.Complexity}" : $"{candidate.FailedCondition ?? "a condition"} fails";
            candidates.Add(new PlayerCandidate(Name(candidate.Name), candidate.Name == pick.Chosen, candidate.Passed, detail));
        }

        var text = $"{build.TitleOf(pick.Group)} picks {(pick.Chosen is { } chosen ? Name(chosen) : "nothing")}";
        Steps.Add(NewStep(PlayerStepKind.Saliency, text, pick.IsLineGroup ? pick.Chosen : null) with { Pick = pick, Candidates = candidates });

        // A line of a line group is named by its text, a node by its name in the program
        string Name(string candidate)
            => pick.IsLineGroup && build.Compilation.Lines.TryGetValue(candidate, out var line) ? line.Text : candidate;
    }

    private IReadOnlyDictionary<string, object> Snapshot()
    {
        if (snapshot is not null)
            return snapshot;

        var values = new Dictionary<string, object>(defaults);
        var saved = variables.Save();
        foreach (var (name, value) in saved.Numbers)
        {
            Take(name, value);
        }

        foreach (var (name, value) in saved.Strings)
        {
            Take(name, value);
        }

        foreach (var (name, value) in saved.Bools)
        {
            Take(name, value);
        }

        return snapshot = values;

        void Take(string name, object value)
        {
            if (!name.StartsWith(VariableStorage.InternalPrefix, StringComparison.Ordinal))
                values[name] = value;
        }
    }
}
