using System.Globalization;
using System.Runtime.CompilerServices;
using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Editor.Services;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Core.Assets.Presentation.ViewModels;

namespace ColorRise.Skein.Editor.Player;

/// <summary>
/// The dialogue played in the session: a recorded run of the scripts of a project as they are in memory, with a cursor
/// that goes back and forth through its steps. The dialogue player panel drives it; the timeline panel and the script
/// editors follow it through <see cref="Changed"/>.
/// </summary>
/// <remarks>
/// A run is its inputs in order: the start node, each "go on", each choice, each variable set by hand, each answer
/// given for a function of the game. Going back only moves the cursor; an input given at an earlier step drops the
/// inputs after it and plays the others again on a new dialogue session, with no wait. An edit of the scripts makes the
/// run stale (<see cref="IsStale"/>): it keeps its build until <see cref="Replay"/> plays the same inputs on the scripts
/// as they are. There is one instance per session (<see cref="Of"/>). Everything happens on the session's dispatcher.
/// </remarks>
public sealed class DialoguePlayerService
{
    private static readonly ConditionalWeakTable<ISessionViewModel, DialoguePlayerService> Instances = new();

    private readonly ISessionViewModel session;
    private readonly List<PlayerInput> script = [];
    private readonly Dictionary<string, object> answers = [];
    private YarnAnalysisService? analysis;
    private PlayerBuild? build;
    private PlayerRun? run;
    private string language = "";
    private bool honourWait;
    private int consumed;
    private int generation;

    private DialoguePlayerService(ISessionViewModel session)
    {
        this.session = session;
    }

    /// <summary>
    /// Raised after any change: the run, its steps, the cursor, a question, the run going stale, the language.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// The project of the run, or the last one played.
    /// </summary>
    public YarnProjectViewModel? Project { get; private set; }

    /// <summary>
    /// The node the run started at, or the last one played.
    /// </summary>
    public string? StartNode { get; private set; }

    /// <summary>
    /// Whether a run is there.
    /// </summary>
    public bool IsPlaying => run is not null;

    /// <summary>
    /// Whether the run is playing an input: a wait that is honoured takes its time. Inputs given meanwhile are dropped.
    /// </summary>
    public bool IsBusy { get; private set; }

    /// <summary>
    /// The steps of the run, those after the cursor included.
    /// </summary>
    public IReadOnlyList<PlayerStep> Steps => run?.Steps ?? [];

    /// <summary>
    /// The index of the step the run is shown at, or -1 when nothing plays.
    /// </summary>
    public int Cursor { get; private set; } = -1;

    /// <summary>
    /// The step at the cursor.
    /// </summary>
    public PlayerStep? CurrentStep => Cursor >= 0 && Cursor < Steps.Count ? Steps[Cursor] : null;

    /// <summary>
    /// The name, in the program, of the node the run is in at the cursor.
    /// </summary>
    public string? CurrentNode => CurrentStep?.Node;

    /// <summary>
    /// The file, as the compiler names it, of the line the run is at: the last step up to the cursor that has a place
    /// in the node of the cursor.
    /// </summary>
    public string? CurrentFile => PlaceStep()?.File;

    /// <summary>
    /// The line, from 1, the run is at in <see cref="CurrentFile"/>.
    /// </summary>
    public int? CurrentLine => PlaceStep()?.Line;

    /// <summary>
    /// The nodes the run went through up to the cursor, by their names in the program, each once, in order.
    /// </summary>
    public IReadOnlyList<string> NodePath
    {
        get
        {
            var path = new List<string>();
            for (var i = 0; i <= Cursor && i < Steps.Count; i++)
            {
                if (Steps[i].Kind == PlayerStepKind.NodeEnter && Steps[i].Node is { } node && !path.Contains(node))
                    path.Add(node);
            }

            return path;
        }
    }

    /// <summary>
    /// The options offered at the cursor; empty when the step there is not a set of options.
    /// </summary>
    public IReadOnlyList<PlayerOption> Options => CurrentStep is { Kind: PlayerStepKind.Options } step ? step.Options : [];

    /// <summary>
    /// Whether <see cref="Continue"/> does something: a later step is recorded, or the run waits after a line.
    /// </summary>
    public bool CanContinue
    {
        get
        {
            if (run is null || IsBusy || Question is not null || CurrentStep is null or { Kind: PlayerStepKind.Options })
                return false;

            return Cursor < Steps.Count - 1 || run.Stop == PlayerStop.Line;
        }
    }

    /// <summary>
    /// The call of a function of the game the run needs the result of, when it waits for <see cref="Answer"/>.
    /// </summary>
    public PlayerQuestion? Question => run?.Question;

    /// <summary>
    /// The answers given for the functions of the game, as they hold at the cursor.
    /// </summary>
    public IReadOnlyList<PlayerAnswer> Answers
    {
        get
        {
            if (build is null || CurrentStep is not { } step)
                return [];

            var given = new Dictionary<string, PlayerAnswer>();
            for (var i = 0; i <= step.Input && i < script.Count; i++)
            {
                if (script[i] is { Kind: PlayerInputKind.Answer } input)
                    given[input.Name] = new PlayerAnswer(input.Name, input.Value!, build.ReturnTypeOf(FunctionOf(input.Name)));
            }

            return [.. given.Values];
        }
    }

    /// <summary>
    /// The variables the scripts of the run declare or use.
    /// </summary>
    public IReadOnlyList<YarnVariableData> Variables => build is null ? [] : [.. build.Compilation.Variables.Where(v => !v.IsInternal)];

    /// <summary>
    /// The languages of the project of the run, the base language first.
    /// </summary>
    public IReadOnlyList<string> Languages => build?.Content.Languages ?? [];

    /// <summary>
    /// The language the lines show in. Setting it plays the run again in that language, and holds for the next runs when
    /// their project has the language.
    /// </summary>
    public string Language
    {
        get => language;
        set
        {
            ArgumentException.ThrowIfNullOrEmpty(value);
            if (value == language || IsBusy)
                return;

            language = value;
            if (run is not null)
            {
                // The same inputs on the same scripts: the same steps, in the other language
                var cursor = Cursor;
                Restart();
                Finish(PlayAsync(live: false));
                Cursor = Math.Min(cursor, Steps.Count - 1);
            }

            OnChanged();
        }
    }

    /// <summary>
    /// Whether <c>wait</c> takes its time when an input is played. Going back, replays and answers never wait.
    /// </summary>
    public bool HonourWait
    {
        get => honourWait;
        set
        {
            if (honourWait == value)
                return;

            honourWait = value;
            OnChanged();
        }
    }

    /// <summary>
    /// The seed of the random numbers and of the random picks of the run: a new one at each <see cref="Start"/>.
    /// </summary>
    public int Seed { get; private set; }

    /// <summary>
    /// Whether a script of the project changed since the build the run plays.
    /// </summary>
    public bool IsStale { get; private set; }

    /// <summary>
    /// Why the run stopped before a recorded input that no longer applies, or null. A new input clears it and drops the
    /// inputs that were not played.
    /// </summary>
    public string? Divergence { get; private set; }

    /// <summary>
    /// Why the last <see cref="Start"/> or <see cref="Replay"/> did nothing, or null.
    /// </summary>
    public string? Problem { get; private set; }

    /// <summary>
    /// The player of a session, made on first use. Skein's panels take it from here rather than from the session's
    /// services: Game Studio makes the panels of a plugin that joins an open session before it initializes the plugin
    /// for the session.
    /// </summary>
    public static DialoguePlayerService Of(ISessionViewModel session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return Instances.GetValue(session, s => new DialoguePlayerService(s));
    }

    /// <summary>
    /// Starts a new run of a project at a node, unless its scripts have errors (<see cref="Problem"/>).
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="node">The title of a node or of a node group.</param>
    public async Task Start(YarnProjectViewModel project, string node)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(node);

        Forget();
        Project = project;
        StartNode = node;
        var mine = generation;
        var latest = await LatestAsync(project);
        if (mine != generation)
            return;

        if (Refusal(latest, project, "play") is { } refusal)
        {
            Problem = refusal;
            OnChanged();
            return;
        }

        var next = new PlayerBuild(latest!, project);
        if (!next.Program.Nodes.ContainsKey(node))
        {
            Problem = $"The project {project.Name} has no node named '{node}'.";
            OnChanged();
            return;
        }

        build = next;
        if (!next.Content.Languages.Contains(language))
            language = next.Content.BaseLanguage;
        Seed = Random.Shared.Next();
        script.Add(new PlayerInput(PlayerInputKind.Start, node));
        Restart();
        await PlayToEndAsync(live: true);
    }

    /// <summary>
    /// Goes on: to the next recorded line or options when the cursor is before the last step, else after the line the
    /// run waits at.
    /// </summary>
    public async Task Continue()
    {
        if (!CanContinue)
            return;

        if (Cursor < Steps.Count - 1)
        {
            var next = Cursor + 1;
            while (next < Steps.Count - 1 && !Steps[next].IsStop)
            {
                next++;
            }

            Cursor = next;
            OnChanged();
            return;
        }

        Branch();
        script.Add(new PlayerInput(PlayerInputKind.Advance));
        await PlayToEndAsync(live: true);
    }

    /// <summary>
    /// Chooses one of <see cref="Options"/>. The choice the run already recorded there goes to its recorded steps;
    /// another one drops what followed.
    /// </summary>
    /// <param name="option">The <see cref="PlayerOption.Index"/> of the option.</param>
    /// <exception cref="ArgumentException">The option is not offered at the cursor.</exception>
    public async Task Choose(int option)
    {
        if (run is null || IsBusy || Question is not null || CurrentStep is not { Kind: PlayerStepKind.Options } step)
            return;

        var offered = step.Options.FirstOrDefault(o => o.Index == option)
            ?? throw new ArgumentException($"The option {option} is not offered: the options are {string.Join(", ", step.Options.Select(o => o.Index))}.", nameof(option));
        if (!offered.IsAvailable)
            return;

        // The next choice of the recording, when it is this one
        for (var i = Cursor + 1; i < Steps.Count; i++)
        {
            if (Steps[i].Kind != PlayerStepKind.Choose)
                continue;

            var input = Steps[i].Input;
            if (script[input].Option != option)
                break;

            var last = i;
            while (last < Steps.Count - 1 && Steps[last + 1].Input == input)
            {
                last++;
            }

            Cursor = last;
            OnChanged();
            return;
        }

        Branch();
        script.Add(new PlayerInput(PlayerInputKind.Choose, Option: option, LineId: offered.LineId, Text: offered.Text));
        await PlayToEndAsync(live: true);
    }

    /// <summary>
    /// Answers the <see cref="Question"/>: the answer holds for that call from there on, and the input that asked is
    /// played again.
    /// </summary>
    /// <param name="function">The name of the function of the question.</param>
    /// <param name="value">A boolean, a number or a text, as the function gives.</param>
    /// <exception cref="ArgumentException">The question is about another function, or the value is not of its type.</exception>
    public async Task Answer(string function, object value)
    {
        ArgumentNullException.ThrowIfNull(function);
        if (run is null || IsBusy || Question is not { } question)
            return;

        if (question.Function != function)
            throw new ArgumentException($"The question is about {question.Call}, not about '{function}'.", nameof(function));

        var answer = Typed(value, question.ReturnType) ?? throw new ArgumentException($"{question.Call} gives a {question.ReturnType}: {PlayerRun.Format(value)} is not one.", nameof(value));

        // Before the input that asked: the run played from its start finds the answer there
        script.Insert(consumed, new PlayerInput(PlayerInputKind.Answer, question.Call, answer));
        Restart();
        await PlayToEndAsync(live: true);
    }

    /// <summary>
    /// Gives another answer for a call of a function of the game, from the cursor on: what followed is dropped.
    /// </summary>
    /// <param name="call">The call, as <see cref="PlayerAnswer.Call"/> gives it.</param>
    /// <param name="value">A boolean, a number or a text, as the function gives.</param>
    /// <exception cref="ArgumentException">The value is not of the type of the function.</exception>
    public async Task ChangeAnswer(string call, object value)
    {
        ArgumentNullException.ThrowIfNull(call);
        if (run is null || build is null || IsBusy || CurrentStep is null)
            return;

        var type = build.ReturnTypeOf(FunctionOf(call));
        var answer = Typed(value, type) ?? throw new ArgumentException($"{call} gives a {type}: {PlayerRun.Format(value)} is not one.", nameof(value));
        Branch();
        script.Add(new PlayerInput(PlayerInputKind.Answer, call, answer));
        await PlayToEndAsync(live: true);
    }

    /// <summary>
    /// Sets a variable of the scripts at the cursor, as a step: what followed is dropped.
    /// </summary>
    /// <param name="name">The name of the variable, with its <c>$</c>.</param>
    /// <param name="value">A boolean, a number or a text, as the variable is declared.</param>
    /// <exception cref="ArgumentException">The scripts have no such variable, it is a smart variable, or the value is not of its type.</exception>
    public async Task SetVariable(string name, object value)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (run is null || build is null || IsBusy || CurrentStep is null)
            return;

        var variable = build.VariableOf(name) ?? throw new ArgumentException($"The scripts have no variable named '{name}'.", nameof(name));
        if (variable.IsSmart)
            throw new ArgumentException($"{name} is a smart variable: its value comes from its expression.", nameof(name));

        var typed = Typed(value, variable.Type) ?? throw new ArgumentException($"{name} is a {variable.Type}: {PlayerRun.Format(value)} is not one.", nameof(value));
        Branch();
        script.Add(new PlayerInput(PlayerInputKind.SetVariable, name, typed));
        await PlayToEndAsync(live: true);
    }

    /// <summary>
    /// Moves the cursor to a step, the nearest when there is no such step. The later steps stay until an input drops them.
    /// </summary>
    public Task GoTo(int step)
    {
        if (run is null || Steps.Count == 0)
            return Task.CompletedTask;

        var target = Math.Clamp(step, 0, Steps.Count - 1);
        if (target != Cursor)
        {
            Cursor = target;
            OnChanged();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Builds the scripts as they are now and plays the inputs of the run on them: the steps that are new or differ are
    /// marked (<see cref="PlayerStep.Changed"/>), and the run stops, with the reason (<see cref="Divergence"/>), before
    /// the first input that no longer applies. Scripts with errors leave the run as it was (<see cref="Problem"/>).
    /// </summary>
    public async Task Replay()
    {
        if (run is null || IsBusy || Project is not { } project)
            return;

        var mine = generation;
        var latest = await LatestAsync(project);
        if (mine != generation)
            return;

        if (Refusal(latest, project, "replay") is { } refusal)
        {
            Problem = refusal;
            OnChanged();
            return;
        }

        var before = Steps.ToList();
        var cursor = Cursor;
        build = new PlayerBuild(latest!, project);
        IsStale = false;
        Problem = null;
        Restart();
        await PlayAsync(live: false);
        if (run is not { } replayed)
            return;

        Cursor = Compare(before, replayed.Steps, cursor);
        OnChanged();
    }

    /// <summary>
    /// Stops the run and forgets it.
    /// </summary>
    public void Reset()
    {
        Forget();
        OnChanged();
    }

    /// <summary>
    /// Stops the player when its session closes.
    /// </summary>
    public void Close()
    {
        Forget();
        if (analysis is not null)
            analysis.Changed -= OnAnalysisChanged;
        analysis = null;
        Project = null;
        Instances.Remove(session);
    }

    private static string FunctionOf(string call)
    {
        var open = call.IndexOf('(', StringComparison.Ordinal);
        return open < 0 ? call : call[..open];
    }

    /// <summary>
    /// A value as a variable or a function of a Yarn type holds it, or null when it is of another type.
    /// </summary>
    private static object? Typed(object? value, string type) => (type, value) switch
    {
        (YarnFunctionDeclaration.Bool, bool) => value,
        (YarnFunctionDeclaration.String, string) => value,
        (YarnFunctionDeclaration.Number, float or double or decimal or int or long or short or byte or uint) => Convert.ToSingle(value, CultureInfo.InvariantCulture),
        _ => null,
    };

    /// <summary>
    /// Marks the steps of a replay that the run before did not have, and gives the place of the cursor in the replay.
    /// </summary>
    private static int Compare(List<PlayerStep> before, List<PlayerStep> after, int cursor)
    {
        // Where each step of the run before is in the replay: within what follows one choice, a step is the same when
        // it reads the same, and a choice is the same choice
        var places = new int[before.Count];
        Array.Fill(places, -1);
        var old = 0;
        for (var i = 0; i < after.Count; i++)
        {
            var match = -1;
            for (var j = old; j < before.Count; j++)
            {
                if (before[j].Kind == PlayerStepKind.Choose || after[i].Kind == PlayerStepKind.Choose)
                {
                    if (before[j].Kind == after[i].Kind)
                    {
                        match = j;
                        break;
                    }

                    if (before[j].Kind == PlayerStepKind.Choose)
                        break;

                    continue;
                }

                if (before[j].Signature == after[i].Signature)
                {
                    match = j;
                    break;
                }
            }

            if (match < 0)
            {
                after[i] = after[i] with { Changed = true };
                continue;
            }

            if (after[i].Signature != before[match].Signature)
                after[i] = after[i] with { Changed = true };
            places[match] = i;
            old = match + 1;
        }

        if (after.Count == 0)
            return -1;

        // The cursor stays on its step, or as far after the last step that is still there
        for (var j = Math.Min(cursor, before.Count - 1); j >= 0; j--)
        {
            if (places[j] >= 0)
                return Math.Min(places[j] + (cursor - j), after.Count - 1);
        }

        return Math.Min(Math.Max(cursor, 0), after.Count - 1);
    }

    private async Task<YarnProjectAnalysis?> LatestAsync(YarnProjectViewModel project)
    {
        if (analysis is null)
        {
            analysis = YarnAnalysisService.Of(session);
            analysis.Changed += OnAnalysisChanged;
        }

        analysis.AnalysisOf(project);
        await analysis.WhenIdle();
        return analysis.AnalysisOf(project);
    }

    private static string? Refusal(YarnProjectAnalysis? latest, YarnProjectViewModel project, string action)
    {
        if (latest is null)
            return $"The scripts of {project.Name} could not be compiled.";

        if (latest.Build.Project is not null)
            return null;

        var first = latest.Diagnostics.FirstOrDefault(d => d.Severity == YarnSeverity.Error);
        var where = first is null ? "" : $" {first.File}({first.Line}): {first.Message}";
        return $"The scripts of {project.Name} have errors: fix them, then {action}.{where}";
    }

    private void OnAnalysisChanged(YarnProjectAnalysis latest)
    {
        if (run is null || build is null || IsStale || latest.Project != Project || ReferenceEquals(latest, build.Analysis))
            return;

        IsStale = true;
        OnChanged();
    }

    private PlayerStep? PlaceStep()
    {
        if (CurrentStep is not { } current)
            return null;

        for (var i = Cursor; i >= 0; i--)
        {
            var step = Steps[i];
            if (step.Node != current.Node)
                return null;

            if (step.Line is not null && step.Kind != PlayerStepKind.Saliency)
                return step;
        }

        return null;
    }

    private void Forget()
    {
        generation++;
        run?.Dispose();
        run = null;
        build = null;
        script.Clear();
        answers.Clear();
        consumed = 0;
        Cursor = -1;
        IsBusy = false;
        IsStale = false;
        Divergence = null;
        Problem = null;
    }

    /// <summary>
    /// Makes a new dialogue session for the run, with none of its inputs played.
    /// </summary>
    private void Restart()
    {
        generation++;
        run?.Dispose();
        answers.Clear();
        run = new PlayerRun(build!, language, Seed, answers);
        consumed = 0;
        Divergence = null;
        IsBusy = false;
    }

    /// <summary>
    /// Makes the recording end at the cursor, for a new input: the inputs after the one that gave the step at the cursor
    /// are dropped, and the dialogue session is made again when it played further.
    /// </summary>
    private void Branch()
    {
        var keep = CurrentStep is { } step ? step.Input + 1 : 0;
        if (keep < script.Count)
            script.RemoveRange(keep, script.Count - keep);

        Divergence = null;
        if (run is null || run.Stop == PlayerStop.Question || consumed > script.Count)
            Restart();
    }

    private async Task PlayToEndAsync(bool live)
    {
        await PlayAsync(live);
        Cursor = Steps.Count - 1;
        OnChanged();
    }

    /// <summary>
    /// Plays the inputs the dialogue session has not played yet, until one asks a question or no longer applies.
    /// </summary>
    /// <param name="live">Whether the last input honours the waits.</param>
    private async Task PlayAsync(bool live)
    {
        var mine = generation;
        var current = run!;
        IsBusy = true;
        try
        {
            while (consumed < script.Count)
            {
                current.Input = consumed;
                current.HonourWait = live && honourWait && consumed == script.Count - 1;
                var playing = ExecuteAsync(current, script[consumed]);
                if (!playing.IsCompleted)
                    OnChanged();

                var outcome = await playing;
                if (mine != generation)
                    return;

                if (current.Question is not null)
                {
                    // The steps of the input that asked come again once it has its answer
                    current.Steps.RemoveAll(s => s.Input == consumed);
                    current.Dispose();
                    return;
                }

                switch (outcome)
                {
                    case Outcome.Played:
                        consumed++;
                        break;
                    case Outcome.GoOnFirst:
                        // The scripts have a line more before this input: the recording gets the "go on" it lacks
                        script.Insert(consumed, new PlayerInput(PlayerInputKind.Advance));
                        break;
                    case Outcome.Nothing:
                        // The scripts have a line less: the recording loses this "go on"
                        script.RemoveAt(consumed);
                        break;
                    default:
                        return;
                }
            }
        }
        finally
        {
            if (mine == generation)
                IsBusy = false;
        }
    }

    private async Task<Outcome> ExecuteAsync(PlayerRun current, PlayerInput input)
    {
        switch (input.Kind)
        {
            case PlayerInputKind.Start:
                current.AddStep(PlayerStepKind.Start, input.Name);
                if (!build!.Program.Nodes.ContainsKey(input.Name))
                    return Diverge($"The scripts no longer have a node named '{input.Name}'.");

                await current.Start(input.Name);
                return Outcome.Played;

            case PlayerInputKind.Advance:
                if (current.Stop != PlayerStop.Line)
                    return Outcome.Nothing;

                await current.Advance();
                return Outcome.Played;

            case PlayerInputKind.Choose:
                if (current.Stop == PlayerStop.Line)
                    return Outcome.GoOnFirst;

                if (current.Stop != PlayerStop.Options)
                    return Diverge($"The option '{input.Text}' is no longer offered: the dialogue is over before it.");

                // The same line when it has an id of its own, else the same text, at its place first
                var offered = current.Offered;
                var option = offered.FirstOrDefault(o => o.Index == input.Option && (o.LineId == input.LineId || o.Text == input.Text))
                    ?? offered.FirstOrDefault(o => o.LineId == input.LineId)
                    ?? offered.FirstOrDefault(o => o.Text == input.Text);
                if (option is null)
                    return Diverge($"The option '{input.Text}' is no longer offered.");

                if (!option.IsAvailable)
                    return Diverge($"The option '{input.Text}' can no longer be chosen: its condition fails.");

                script[consumed] = input with { Option = option.Index, LineId = option.LineId, Text = option.Text };
                current.AddStep(PlayerStepKind.Choose, option.Text, option.LineId);
                await current.Choose(option.Index);
                return Outcome.Played;

            case PlayerInputKind.SetVariable:
                if (build!.VariableOf(input.Name) is not { IsSmart: false } variable)
                    return Diverge($"The scripts no longer have a variable named '{input.Name}' to set.");

                if (Typed(input.Value, variable.Type) is null)
                    return Diverge($"{input.Name} is now a {variable.Type}: it cannot be set to {PlayerRun.Format(input.Value)}.");

                current.SetVariable(input.Name, input.Value!);
                return Outcome.Played;

            default:
                answers[input.Name] = input.Value!;
                current.AddStep(PlayerStepKind.Function, $"{input.Name} → {PlayerRun.Format(input.Value)}");
                return Outcome.Played;
        }

        Outcome Diverge(string reason)
        {
            Divergence = reason;
            return Outcome.Diverged;
        }
    }

    /// <summary>
    /// Completes what plays with no wait, which is over when it returns.
    /// </summary>
    private static void Finish(Task playing)
    {
        if (!playing.IsCompleted)
            throw new InvalidOperationException("A replay of the dialogue player waited.");

        playing.GetAwaiter().GetResult();
    }

    private void OnChanged() => Changed?.Invoke();

    private enum Outcome
    {
        Played,
        GoOnFirst,
        Nothing,
        Diverged,
    }
}
