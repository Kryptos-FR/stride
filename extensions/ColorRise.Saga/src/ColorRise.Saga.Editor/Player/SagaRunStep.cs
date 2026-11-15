namespace ColorRise.Saga.Editor.Player;

/// <summary>
/// A step of a story run and what it did: the story state after it, the changes of the story, the signals it emitted, the lines
/// of the dialogue, the warnings and errors, and the dialogue open during the step or ended by it.
/// </summary>
public sealed class SagaRunStep
{
    internal SagaRunStep(SagaStep step, SagaState state, IReadOnlyList<SagaChangedEventArgs> changes, IReadOnlyList<SagaSignal> signals,
        IReadOnlyList<string> lines, IReadOnlyList<string> messages, string? dialogueName, IReadOnlyList<string> dialoguePath, bool isDialogueOpen)
    {
        Step = step;
        State = state;
        Changes = changes;
        Signals = signals;
        Lines = lines;
        Messages = messages;
        DialogueName = dialogueName;
        DialoguePath = dialoguePath;
        IsDialogueOpen = isDialogueOpen;
    }

    /// <summary>
    /// The step as it applied: a choice carries the text and index it was found at.
    /// </summary>
    public SagaStep Step { get; }

    /// <summary>
    /// A copy of the story state after the step.
    /// </summary>
    public SagaState State { get; }

    public IReadOnlyList<SagaChangedEventArgs> Changes { get; }

    public IReadOnlyList<SagaSignal> Signals { get; }

    /// <summary>
    /// The lines of the transcript: whom the player talks to, what is said, the choices taken, the end of the dialogue.
    /// </summary>
    public IReadOnlyList<string> Lines { get; }

    /// <summary>
    /// What went wrong, as <c>Error: ...</c> and <c>Warning: ...</c>.
    /// </summary>
    public IReadOnlyList<string> Messages { get; }

    /// <summary>
    /// The name of the dialogue open during the step or ended by it, or null.
    /// </summary>
    public string? DialogueName { get; }

    /// <summary>
    /// The nodes that dialogue went through up to the end of the step.
    /// </summary>
    public IReadOnlyList<string> DialoguePath { get; }

    /// <summary>
    /// Whether that dialogue is still open after the step.
    /// </summary>
    public bool IsDialogueOpen { get; }

    /// <summary>
    /// Whether the last replay of the run made the step do something else than before.
    /// </summary>
    public bool PlaysDifferently { get; internal set; }

    /// <summary>
    /// The transcript of the step: its lines, then its messages.
    /// </summary>
    public IEnumerable<string> Transcript => Lines.Concat(Messages);

    /// <summary>
    /// What the step did, in one text: two runs of a step that did the same have the same signature.
    /// </summary>
    public string Signature => string.Join("\n", Transcript.Concat(Changes.Select(x => $"{x.Kind} {x.Id}")).Concat(Signals.Select(Describe)));

    /// <summary>
    /// A signal as the story emitted it: <c>play_sound("door")</c>.
    /// </summary>
    public static string Describe(SagaSignal signal) => $"{signal.Name}({string.Join(", ", signal.Arguments)})";

    public override string ToString() => Step.ToString();
}
