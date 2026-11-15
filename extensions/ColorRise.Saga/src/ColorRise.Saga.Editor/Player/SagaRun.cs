using ColorRise.Saga.Assets;
using ColorRise.Saga.Content;
using ColorRise.Saga.Expressions;
using Stride.Core.Diagnostics;

namespace ColorRise.Saga.Editor.Player;

/// <summary>
/// A story played step by step (<see cref="SagaStep"/>) on a runtime of its own, recording what each step did
/// (<see cref="SagaRunStep"/>). The functions of the game answer what the <see cref="SagaStepKind.Answer"/> steps gave, so the same
/// steps on the same story always play the same way.
/// </summary>
public sealed class SagaRun : IDisposable
{
    private readonly LoggerResult log = new("Saga run");
    private readonly Dictionary<string, SagaValue> answers = [];
    private readonly Dictionary<string, string> speakers;
    private readonly List<IDisposable> subscriptions = [];
    private readonly List<SagaChangedEventArgs> changes = [];
    private readonly List<SagaSignal> signals = [];

    /// <param name="story">The story, built with its dialogues.</param>
    public SagaRun(StoryDefinition story)
    {
        ArgumentNullException.ThrowIfNull(story);
        Runtime = new SagaRuntime(story, null, log);
        var declarations = story.Declarations;
        foreach (var function in declarations.Functions)
        {
            answers[function.Name] = SagaLiterals.DefaultOf(function.ReturnType);
            Runtime.RegisterFunction(function.Name, _ => answers[function.Name]);
        }
        foreach (var signal in declarations.Signals)
            subscriptions.Add(Runtime.On(signal.Name, signals.Add));
        Runtime.Changed += OnChanged;
        speakers = declarations.Characters.ToDictionary(x => x.Id, x => x.DisplayName.Length > 0 ? x.DisplayName : x.Id);
        speakers.TryAdd(SagaContentBuilder.PlayerId, "Player");
    }

    /// <summary>
    /// The runtime that plays the story.
    /// </summary>
    public SagaRuntime Runtime { get; }

    /// <summary>
    /// The dialogue being played, or null.
    /// </summary>
    public DialogueSession? Dialogue { get; private set; }

    /// <summary>
    /// The answer a function of the game gives now.
    /// </summary>
    public SagaValue Answer(string function) => answers[function];

    /// <summary>
    /// The name of a character as the transcript shows it.
    /// </summary>
    public string SpeakerName(string characterId) => speakers.GetValueOrDefault(characterId, characterId);

    /// <summary>
    /// Why a step can't apply now, or null when it can.
    /// </summary>
    public string? CanApply(SagaStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        var declarations = Runtime.Story.Declarations;
        if (step.Kind == SagaStepKind.Start)
            return Runtime.IsStarted ? "The story has already started." : null;
        if (!Runtime.IsStarted)
            return "The story has not started.";
        switch (step.Kind)
        {
            case SagaStepKind.Raise:
                return declarations.Events.Any(x => x.Name == step.Name) ? null : $"The story declares no event '{step.Name}'.";
            case SagaStepKind.Set:
                return declarations.Variables.FirstOrDefault(x => x.Name == step.Name) is { } variable
                    ? SagaLiterals.TryParse(step.Value, variable.Type, out _)
                    : $"The story declares no variable '{step.Name}'.";
            case SagaStepKind.Answer:
                return declarations.Functions.FirstOrDefault(x => x.Name == step.Name) is { } function
                    ? SagaLiterals.TryParse(step.Value, function.ReturnType, out _)
                    : $"The story declares no function '{step.Name}'.";
            case SagaStepKind.Talk:
                return declarations.Characters.Any(x => x.Id == step.Name) ? null : $"The story declares no character '{step.Name}'.";
            case SagaStepKind.Continue:
                if (Dialogue is null)
                    return "No dialogue is playing.";
                return Dialogue.Choices.Count > 0 || Dialogue.CurrentLine is null ? "The dialogue waits for a choice." : null;
            case SagaStepKind.Choose:
                if (Dialogue is null)
                    return "No dialogue is playing.";
                if (Dialogue.Choices.Count == 0)
                    return "The dialogue shows a line: continue first.";
                return FindChoice(step, out var reason) is null ? reason : null;
            default:
                return $"Unknown step {step.Kind}.";
        }
    }

    /// <summary>
    /// Applies a step and says what it did.
    /// </summary>
    /// <exception cref="InvalidOperationException">The step can't apply now (<see cref="CanApply"/> says why).</exception>
    public SagaRunStep Apply(SagaStep step)
    {
        if (CanApply(step) is { } reason)
            throw new InvalidOperationException(reason);

        changes.Clear();
        signals.Clear();
        log.Clear();
        var lines = new List<string>();
        var messages = new List<string>();
        var applied = step;
        var shownLine = Dialogue?.CurrentLine;
        try
        {
            switch (step.Kind)
            {
                case SagaStepKind.Start:
                    Runtime.Start();
                    break;
                case SagaStepKind.Raise:
                    Runtime.Raise(step.Name);
                    break;
                case SagaStepKind.Set:
                    SagaLiterals.TryParse(step.Value, Runtime.Story.Declarations.Variables.First(x => x.Name == step.Name).Type, out var value);
                    Runtime.Set(step.Name, value);
                    break;
                case SagaStepKind.Answer:
                    SagaLiterals.TryParse(step.Value, Runtime.Story.Declarations.Functions.First(x => x.Name == step.Name).ReturnType, out var answer);
                    answers[step.Name] = answer;
                    break;
                case SagaStepKind.Talk:
                    lines.Add($"Talking to {SpeakerName(step.Name)}");
                    shownLine = null;
                    Dialogue = Runtime.Talk(step.Name);
                    if (Dialogue is null)
                        lines.Add("(nothing to say now)");
                    break;
                case SagaStepKind.Continue:
                    Dialogue!.Continue();
                    break;
                case SagaStepKind.Choose:
                    var choice = FindChoice(step, out _)!;
                    applied = SagaStep.Choose(choice.Index, choice.Text, [.. Dialogue!.Choices.Select(x => x.Text)]);
                    lines.Add($"> {choice.Text}");
                    Dialogue!.Choose(choice.Index);
                    break;
            }
        }
        catch (Exception e) when (e is SagaException or ArgumentException or InvalidOperationException)
        {
            messages.Add($"Error: {e.Message}");
        }
        messages.AddRange(log.Messages.Select(x => $"{x.Type}: {x.Text}"));

        var dialogue = Dialogue;
        if (dialogue is not null)
        {
            if (dialogue.CurrentLine is { } line && !ReferenceEquals(line, shownLine))
                lines.Add($"{SpeakerName(line.SpeakerId)}: {line.Text}");
            if (dialogue.IsEnded)
            {
                lines.Add("(the dialogue ends)");
                Dialogue = null;
            }
        }
        return new SagaRunStep(applied, Runtime.CaptureState(), [.. changes], [.. signals], lines, messages,
            dialogue?.Dialogue.Name, dialogue is null ? [] : [.. dialogue.Path], dialogue is { IsEnded: false });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Runtime.Changed -= OnChanged;
        foreach (var subscription in subscriptions)
            subscription.Dispose();
        subscriptions.Clear();
    }

    // The choice a step takes: the shown choice with its text, else the one at its index when that one was reworded: the node shows
    // as many choices as when the step was taken, and the text at the index is not one it showed then
    private DialogueChoiceView? FindChoice(SagaStep step, out string reason)
    {
        var choices = Dialogue!.Choices;
        var choice = choices.FirstOrDefault(x => x.Text == step.Value)
            ?? choices.FirstOrDefault(x => x.Index == step.ChoiceIndex && x.IsEnabled
                && (step.Offered is null || (choices.Count == step.Offered.Count && !step.Offered.Contains(x.Text))));
        reason = choice switch
        {
            null => $"The choice \"{step.Value}\" is no longer offered.",
            { IsEnabled: false } => $"The choice \"{step.Value}\" can't be taken now.",
            _ => string.Empty,
        };
        return choice is { IsEnabled: true } ? choice : null;
    }

    private void OnChanged(object? sender, SagaChangedEventArgs e) => changes.Add(e);
}
