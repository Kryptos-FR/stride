namespace ColorRise.Skein.Tests.Session;

/// <summary>
/// A responder that writes down what the dialogue asks, and answers at once unless a test gives it something to await.
/// </summary>
internal sealed class RecordingResponder : IDialogueResponder
{
    /// <summary>
    /// What was asked, in order: <c>line: text</c>, <c>option: text</c> (with <c>[disabled]</c>), <c>command: text</c>,
    /// <c>complete</c>, and with <see cref="RecordNodes"/> <c>node: name</c>, <c>end: name</c>, <c>prepare: ids</c>.
    /// </summary>
    public List<string> Transcript { get; } = [];

    /// <summary>
    /// The choices to give, by <see cref="DialogueOption.Index"/>; when empty, the first option that is available.
    /// </summary>
    public Queue<int?> Choices { get; } = new();

    /// <summary>
    /// The options of each offer.
    /// </summary>
    public List<IReadOnlyList<DialogueOption>> Offers { get; } = [];

    public Func<LocalizedLine, LineCancellation, ValueTask>? OnLine { get; set; }

    public Func<IReadOnlyList<DialogueOption>, CancellationToken, ValueTask<int?>>? OnOptions { get; set; }

    public Func<string, CancellationToken, ValueTask>? OnCommand { get; set; }

    public Action? OnComplete { get; set; }

    public bool RecordNodes { get; set; }

    public int Completions { get; private set; }

    public ValueTask PrepareForLinesAsync(IReadOnlyList<string> lineIds)
    {
        if (RecordNodes)
            Transcript.Add($"prepare: {string.Join(' ', lineIds)}");
        return default;
    }

    public async ValueTask HandleLineAsync(LocalizedLine line, LineCancellation cancellation)
    {
        Transcript.Add($"line: {line.Text.Text}");
        if (OnLine is not null)
            await OnLine(line, cancellation);
    }

    public async ValueTask<int?> HandleOptionsAsync(IReadOnlyList<DialogueOption> options, CancellationToken cancellation)
    {
        Offers.Add(options);
        foreach (var option in options)
        {
            Transcript.Add($"option: {option.Line.Text.Text}{(option.IsAvailable ? "" : " [disabled]")}");
        }

        if (OnOptions is not null)
            return await OnOptions(options, cancellation);
        if (Choices.TryDequeue(out var choice))
            return choice;
        return options.FirstOrDefault(o => o.IsAvailable)?.Index;
    }

    public async ValueTask HandleCommandAsync(string commandText, CancellationToken cancellation)
    {
        Transcript.Add($"command: {commandText}");
        if (OnCommand is not null)
            await OnCommand(commandText, cancellation);
    }

    public ValueTask HandleNodeStartAsync(string node)
    {
        if (RecordNodes)
            Transcript.Add($"node: {node}");
        return default;
    }

    public ValueTask HandleNodeCompleteAsync(string node)
    {
        if (RecordNodes)
            Transcript.Add($"end: {node}");
        return default;
    }

    public ValueTask HandleDialogueCompleteAsync()
    {
        Completions++;
        Transcript.Add("complete");
        OnComplete?.Invoke();
        return default;
    }
}

/// <summary>
/// A time that does not wait: it writes down what it was asked.
/// </summary>
internal sealed class FakeClock : IDialogueClock
{
    public List<TimeSpan> Delays { get; } = [];

    public ValueTask Delay(TimeSpan duration, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        Delays.Add(duration);
        return default;
    }
}
