using Yarn;

namespace ColorRise.Skein;

/// <summary>
/// What a running dialogue asks, each call awaited before the dialogue goes on: the shape of Yarn Spinner 3.3's
/// responder, in Yarn Spinner's types.
/// </summary>
internal interface IYarnDialogueEvents
{
    ValueTask NodeStartAsync(string node);

    ValueTask NodeCompleteAsync(string node);

    ValueTask PrepareForLinesAsync(IReadOnlyList<string> lineIds);

    ValueTask LineAsync(Line line);

    /// <returns>The id of the chosen option, or <see cref="Dialogue.NoOptionSelected"/>.</returns>
    ValueTask<int> OptionsAsync(OptionSet options);

    ValueTask CommandAsync(string text);
}

/// <summary>
/// Runs a Yarn Spinner 3.2 <see cref="Dialogue"/> as asynchronous calls. The 3.2 dialogue talks through synchronous
/// handlers and goes on when told to <see cref="Dialogue.Continue"/>, which does nothing from inside a handler: the
/// handlers here only queue what the dialogue says, and the queue is delivered, awaited, between two steps. With
/// Yarn Spinner 3.3, whose dialogue awaits a responder itself, this class goes and nothing else changes.
/// </summary>
/// <remarks>
/// One difference from an awaiting dialogue: a node start or end is delivered after the dialogue already ran up to the
/// next line, options or command, so a variable set at the top of a node is set before the node's start is told.
/// </remarks>
internal sealed class Yarn32Adapter
{
    private readonly Queue<Event> events = new();

    public Yarn32Adapter(IVariableStorage storage, Program program)
    {
        Dialogue = new Dialogue(storage)
        {
            LineHandler = line => events.Enqueue(new Event(EventKind.Line, Line: line)),
            OptionsHandler = options => events.Enqueue(new Event(EventKind.Options, Options: options)),
            CommandHandler = command => events.Enqueue(new Event(EventKind.Command, Text: command.Text)),
            NodeStartHandler = node => events.Enqueue(new Event(EventKind.NodeStart, Text: node)),
            NodeCompleteHandler = node => events.Enqueue(new Event(EventKind.NodeComplete, Text: node)),
            PrepareForLinesHandler = ids => events.Enqueue(new Event(EventKind.Prepare, LineIds: [.. ids])),
            // The end is known from the dialogue's state once the queue is delivered
            DialogueCompleteHandler = () => { },
        };
        Dialogue.SetProgram(program);
    }

    private enum EventKind
    {
        NodeStart,
        NodeComplete,
        Prepare,
        Line,
        Options,
        Command,
    }

    public Dialogue Dialogue { get; }

    /// <summary>
    /// Runs a node to the end of the dialogue, or until the stop is requested.
    /// </summary>
    public async Task RunAsync(string node, IYarnDialogueEvents sink, CancellationToken stop)
    {
        events.Clear();
        try
        {
            Dialogue.SetNode(node);
            await DeliverAsync(sink, stop);
            while (!stop.IsCancellationRequested)
            {
                Dialogue.Continue();
                await DeliverAsync(sink, stop);
                if (!Dialogue.IsActive)
                    break;
            }
        }
        finally
        {
            // Stopped half-way, or an exception: the dialogue is left ready for its next run
            if (Dialogue.IsActive)
                Dialogue.Stop();
            events.Clear();
        }
    }

    private async ValueTask DeliverAsync(IYarnDialogueEvents sink, CancellationToken stop)
    {
        while (events.TryDequeue(out var next))
        {
            if (stop.IsCancellationRequested)
                return;

            switch (next.Kind)
            {
                case EventKind.NodeStart:
                    await sink.NodeStartAsync(next.Text!);
                    break;
                case EventKind.NodeComplete:
                    await sink.NodeCompleteAsync(next.Text!);
                    break;
                case EventKind.Prepare:
                    await sink.PrepareForLinesAsync(next.LineIds!);
                    break;
                case EventKind.Line:
                    await sink.LineAsync(next.Line);
                    break;
                case EventKind.Command:
                    await sink.CommandAsync(next.Text!);
                    break;
                case EventKind.Options:
                    var choice = await sink.OptionsAsync(next.Options);
                    if (!stop.IsCancellationRequested)
                        Dialogue.SetSelectedOption(choice);
                    break;
            }
        }
    }

    private readonly record struct Event(EventKind Kind, string? Text = null, Line Line = default, OptionSet Options = default, IReadOnlyList<string>? LineIds = null);
}
