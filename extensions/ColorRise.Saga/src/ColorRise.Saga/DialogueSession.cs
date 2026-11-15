using System.Text.RegularExpressions;
using ColorRise.Saga.Content;
using ColorRise.Saga.Expressions;
using Stride.Core.Diagnostics;

namespace ColorRise.Saga;

/// <summary>
/// A dialogue being played. The game's UI shows <see cref="CurrentLine"/> and <see cref="Choices"/>, and calls
/// <see cref="Continue"/> after a line or <see cref="Choose"/> on choices. Saga has no UI of its own.
/// </summary>
/// <remarks>
/// While choices are shown, <see cref="CurrentLine"/> keeps the line that led to them. The actions of lines and choices change
/// the story at once (it settles after each).
/// </remarks>
public sealed partial class DialogueSession
{
    /// <summary>
    /// How many silent steps (branches) a dialogue may take between two things to show.
    /// </summary>
    private const int MaxSilentSteps = 1000;

    private readonly SagaRuntime runtime;
    private readonly ILogger logger;
    private readonly Dictionary<string, DialogueNode> nodes;
    private readonly List<string> path = [];
    private ChoicesNode? choicesNode;

    internal DialogueSession(SagaRuntime runtime, ILogger logger, DialogueDefinition dialogue)
    {
        this.runtime = runtime;
        this.logger = logger;
        Dialogue = dialogue;
        nodes = dialogue.Nodes.GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.First());
    }

    public DialogueDefinition Dialogue { get; }

    /// <summary>
    /// The line to show, or null once the dialogue ended (or before its first line).
    /// </summary>
    public DialogueLine? CurrentLine { get; private set; }

    /// <summary>
    /// The choices to show; empty while a line waits for <see cref="Continue"/>.
    /// </summary>
    public IReadOnlyList<DialogueChoiceView> Choices { get; private set; } = [];

    public bool IsEnded { get; private set; }

    /// <summary>
    /// The id of the node the dialogue is at: the line it shows or the choices it offers; null once it ended.
    /// </summary>
    public string? CurrentNodeId { get; private set; }

    /// <summary>
    /// The ids of the nodes the dialogue went through, in order: the lines, choices and branches, and the end node when it
    /// reached one.
    /// </summary>
    public IReadOnlyList<string> Path => path;

    private string Card => $"dialogue {Dialogue.Name}";

    /// <summary>
    /// Goes on after a line.
    /// </summary>
    /// <exception cref="InvalidOperationException">The dialogue ended, or waits for a choice.</exception>
    public void Continue()
    {
        if (IsEnded)
            throw new InvalidOperationException("The dialogue has ended.");
        if (choicesNode is not null)
            throw new InvalidOperationException("The dialogue waits for a choice.");
        var line = nodes.GetValueOrDefault(CurrentNodeId!) as LineNode;
        MoveTo(line?.NextId);
    }

    /// <summary>
    /// Takes a choice, by its <see cref="DialogueChoiceView.Index"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The dialogue ended, waits for <see cref="Continue"/>, or the choice is disabled.</exception>
    /// <exception cref="ArgumentOutOfRangeException">No shown choice has this index.</exception>
    public void Choose(int index)
    {
        if (IsEnded)
            throw new InvalidOperationException("The dialogue has ended.");
        if (choicesNode is null)
            throw new InvalidOperationException("The dialogue shows a line; call Continue.");
        var view = Choices.FirstOrDefault(x => x.Index == index) ?? throw new ArgumentOutOfRangeException(nameof(index), index, "No choice shown has this index.");
        if (!view.IsEnabled)
            throw new InvalidOperationException($"The choice '{view.Text}' is disabled.");

        var choice = choicesNode.Choices[index];
        var node = choicesNode;
        choicesNode = null;
        Choices = [];
        runtime.Change(() =>
        {
            if (choice.Once)
                runtime.State.UsedOnce.Add(OnceKey(node, index));
            runtime.RunActionsUnsettled(choice.Actions, Card, $"choice '{choice.Text}'");
        });
        MoveTo(choice.TargetId);
    }

    internal void Begin()
    {
        runtime.Change(() =>
        {
            if (Dialogue.Once)
                runtime.State.UsedOnce.Add(Dialogue.Name);
            Visit(Dialogue.Name);
        });
        MoveTo(Dialogue.EntryNodeId);
    }

    private void MoveTo(string? nodeId)
    {
        for (var step = 0; ; step++)
        {
            if (step == MaxSilentSteps)
                throw new SagaException($"The dialogue {Dialogue.Name} loops through branches without showing anything (at node {nodeId}).");
            if (nodeId is null || !nodes.TryGetValue(nodeId, out var node))
            {
                if (nodeId is not null)
                    logger.Warning($"Saga: the dialogue {Dialogue.Name} leads to a node '{nodeId}' it does not have; it ends there.");
                End();
                return;
            }
            path.Add(node.Id);
            if (node is EndNode)
            {
                End();
                return;
            }

            CurrentNodeId = node.Id;
            switch (node)
            {
                case LineNode line:
                    runtime.Change(() =>
                    {
                        VisitNode(line);
                        runtime.RunActionsUnsettled(line.Actions, Card, $"line {line.Id}");
                    });
                    CurrentLine = new DialogueLine(line.SpeakerId, Substitute(line.Text));
                    return;

                case ChoicesNode choices:
                    runtime.Change(() => VisitNode(choices));
                    var views = new List<DialogueChoiceView>();
                    for (var i = 0; i < choices.Choices.Count; i++)
                    {
                        var choice = choices.Choices[i];
                        if (choice.Once && runtime.State.UsedOnce.Contains(OnceKey(choices, i)))
                            continue;
                        var enabled = runtime.Evaluate(choice.When, Card, $"choice '{choice.Text}'");
                        if (enabled || !choice.HideWhenFalse)
                            views.Add(new DialogueChoiceView(i, Substitute(choice.Text), enabled));
                    }
                    // Nothing to choose (every choice hidden or disabled): the dialogue would wait forever
                    if (!views.Any(x => x.IsEnabled))
                    {
                        logger.Warning($"Saga: the dialogue {Dialogue.Name} has no choice to take at node {choices.Id}; it ends there.");
                        End();
                        return;
                    }
                    choicesNode = choices;
                    Choices = views;
                    return;

                case BranchNode branch:
                    runtime.Change(() => VisitNode(branch));
                    var link = branch.Links.FirstOrDefault(x => runtime.Evaluate(x.When, Card, $"branch {branch.Id}"));
                    if (link is null)
                        logger.Warning($"Saga: no link of the branch {branch.Id} of the dialogue {Dialogue.Name} holds; it ends there.");
                    nodeId = link?.TargetId;
                    break;

                default:
                    nodeId = null;
                    break;
            }
        }
    }

    private void End()
    {
        IsEnded = true;
        CurrentLine = null;
        Choices = [];
        choicesNode = null;
        CurrentNodeId = null;
    }

    private void VisitNode(DialogueNode node)
    {
        if (!string.IsNullOrEmpty(node.Name))
            Visit($"{Dialogue.Name}.{node.Name}");
    }

    private void Visit(string key) => runtime.State.Visits[key] = runtime.State.Visits.GetValueOrDefault(key) + 1;

    private string OnceKey(ChoicesNode node, int index) => $"{Dialogue.Name}.{node.Id}#{index}";

    /// <summary>
    /// Fills in <c>{variable}</c> with the values of declared variables; anything else stays as written.
    /// </summary>
    private string Substitute(string text) => VariablePattern().Replace(text, match =>
        runtime.State.Variables.TryGetValue(match.Groups[1].Value, out var value)
            ? value.Kind is SagaValueKind.String or SagaValueKind.Symbol ? value.Text! : value.ToString()
            : match.Value);

    [GeneratedRegex(@"\{([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\}")]
    private static partial Regex VariablePattern();
}
