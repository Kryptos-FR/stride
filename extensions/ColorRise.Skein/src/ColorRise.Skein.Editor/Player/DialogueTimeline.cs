namespace ColorRise.Skein.Editor.Player;

/// <summary>
/// What an item of the dialogue timeline is, for its colour.
/// </summary>
public enum TimelineItemKind
{
    /// <summary>
    /// The run is in a node.
    /// </summary>
    Node,

    /// <summary>
    /// A node group or a line group picked.
    /// </summary>
    Pick,

    /// <summary>
    /// A line was said.
    /// </summary>
    Line,

    /// <summary>
    /// The user chose an option.
    /// </summary>
    Choice,

    /// <summary>
    /// The scripts sent a command to the game.
    /// </summary>
    Command,

    /// <summary>
    /// The user answered for a function of the game.
    /// </summary>
    Answer,

    /// <summary>
    /// The user set a variable.
    /// </summary>
    Set,

    /// <summary>
    /// A variable changed.
    /// </summary>
    Variable,

    /// <summary>
    /// The dialogue failed.
    /// </summary>
    Error,
}

/// <summary>
/// An item of a lane of the timeline: a span over the steps during which something holds, from <see cref="Start"/> to
/// <see cref="End"/>, or a mark at the step where something happened (<see cref="IsMark"/>, at <see cref="Start"/>).
/// </summary>
/// <param name="Start">The first step of the span, or the step of the mark.</param>
/// <param name="End">The last step of the span.</param>
/// <param name="Label">What the item shows.</param>
/// <param name="Kind">What the item is.</param>
/// <param name="Tip">The item in words.</param>
/// <param name="IsMark">Whether the item is a mark at one step.</param>
/// <param name="IsOpen">Whether the span still holds after the last step of the run.</param>
public sealed record TimelineItem(int Start, int End, string Label, TimelineItemKind Kind, string Tip, bool IsMark, bool IsOpen);

/// <summary>
/// A lane of the timeline: one kind of thing across the steps of the run.
/// </summary>
/// <param name="Title">The name of the lane.</param>
/// <param name="Items">Its items, in the order of the steps.</param>
public sealed record TimelineLane(string Title, IReadOnlyList<TimelineItem> Items);

/// <summary>
/// The lanes of the dialogue timeline, built from the steps of a run: the nodes the run was in and the picks, the lines,
/// the choices, what the game did (commands, answers of its functions, variables set by hand), the variables that changed.
/// </summary>
public static class DialogueTimeline
{
    /// <summary>
    /// Builds the lanes of a run.
    /// </summary>
    public static IReadOnlyList<TimelineLane> Build(IReadOnlyList<PlayerStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Count == 0)
            return [];

        var nodes = new List<TimelineItem>();
        var lines = new List<TimelineItem>();
        var choices = new List<TimelineItem>();
        var game = new List<TimelineItem>();
        var variables = new List<TimelineItem>();
        var titles = new Dictionary<string, string>();
        var ended = false;
        string? node = null;
        var from = 0;
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Kind == PlayerStepKind.NodeEnter && step.Node is not null)
                titles[step.Node] = step.Text;

            if (step.Node != node)
            {
                CloseNode(i - 1, isOpen: false);
                node = step.Node;
                from = i;
            }

            switch (step.Kind)
            {
                case PlayerStepKind.Saliency:
                    nodes.Add(Mark(i, "?", TimelineItemKind.Pick, string.Join("\n", Describe(step, i))));
                    break;
                case PlayerStepKind.Line:
                    lines.Add(Mark(i, step.Speaker ?? "…", TimelineItemKind.Line, $"Step {i}: {step.Text}"));
                    break;
                case PlayerStepKind.Choose:
                    choices.Add(Mark(i, step.Text, TimelineItemKind.Choice, $"Step {i}: the option \"{step.Text}\" is chosen"));
                    break;
                case PlayerStepKind.Command:
                    game.Add(Mark(i, FirstWord(step.Text), TimelineItemKind.Command, $"Step {i}: the scripts send <<{step.Text}>>"));
                    break;
                case PlayerStepKind.Function:
                    game.Add(Mark(i, FirstWord(step.Text), TimelineItemKind.Answer, $"Step {i}: answered {step.Text}"));
                    break;
                case PlayerStepKind.SetVariable:
                    game.Add(Mark(i, FirstWord(step.Text), TimelineItemKind.Set, $"Step {i}: set by hand, {step.Text}"));
                    break;
                case PlayerStepKind.Error:
                    lines.Add(Mark(i, "!", TimelineItemKind.Error, $"Step {i}: {step.Text}"));
                    break;
            }

            ended = step.Kind is PlayerStepKind.End or PlayerStepKind.Error;
            if (i > 0 && !ReferenceEquals(step.Variables, steps[i - 1].Variables))
            {
                foreach (var (name, before, after) in Changes(steps[i - 1].Variables, step.Variables))
                {
                    variables.Add(Mark(i, name, TimelineItemKind.Variable, $"Step {i}: {name}: {PlayerRun.Format(before)} → {PlayerRun.Format(after)}"));
                }
            }
        }

        CloseNode(steps.Count - 1, isOpen: !ended);
        return [new("Nodes", nodes), new("Lines", lines), new("Choices", choices), new("Game", game), new("Variables", variables)];

        void CloseNode(int last, bool isOpen)
        {
            if (node is null || last < from)
                return;

            var title = titles.GetValueOrDefault(node, node);
            var range = last == from ? $"at step {from}" : $"from step {from} to step {last}";
            nodes.Add(new TimelineItem(from, last, title, TimelineItemKind.Node, $"The dialogue is in {(title == node ? title : $"{title} ({node})")} {range}", IsMark: false, isOpen));
        }
    }

    /// <summary>
    /// What a step did, in words: the step, and for a pick each candidate with why it was or was not chosen.
    /// </summary>
    public static IReadOnlyList<string> Describe(PlayerStep step, int index)
    {
        ArgumentNullException.ThrowIfNull(step);
        var text = step.Kind switch
        {
            PlayerStepKind.Start => $"the dialogue starts at {step.Text}",
            PlayerStepKind.NodeEnter => $"the dialogue enters {step.Text}",
            PlayerStepKind.Line => step.Text,
            PlayerStepKind.Options => $"options: {step.Text}",
            PlayerStepKind.Choose => $"the option \"{step.Text}\" is chosen",
            PlayerStepKind.Command => $"<<{step.Text}>>",
            PlayerStepKind.Function => $"answered {step.Text}",
            PlayerStepKind.SetVariable => $"set by hand, {step.Text}",
            PlayerStepKind.End => "the dialogue is over",
            _ => step.Text,
        };
        var lines = new List<string> { $"Step {index}: {text}" };
        lines.AddRange(step.Candidates.Select(c => $"{(c.IsChosen ? "✓" : c.Passed ? "·" : "✗")} {c.Name}: {c.Detail}"));
        if (step.Changed)
            lines.Add("New or different since the last replay");
        return lines;
    }

    private static TimelineItem Mark(int step, string label, TimelineItemKind kind, string tip) => new(step, step, label, kind, tip, IsMark: true, IsOpen: false);

    // The name of a command, of a function or of a variable: what comes before the first space or bracket
    private static string FirstWord(string text)
    {
        var end = text.IndexOfAny([' ', '(']);
        return end < 0 ? text : text[..end];
    }

    private static IEnumerable<(string Name, object? Before, object? After)> Changes(IReadOnlyDictionary<string, object> before, IReadOnlyDictionary<string, object> after)
    {
        foreach (var (name, value) in after.OrderBy(v => v.Key, StringComparer.Ordinal))
        {
            before.TryGetValue(name, out var old);
            if (!Equals(old, value))
                yield return (name, old, value);
        }
    }
}
