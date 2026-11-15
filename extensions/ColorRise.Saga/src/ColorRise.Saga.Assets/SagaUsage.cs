using System.Text.RegularExpressions;
using ColorRise.Saga.Expressions;

namespace ColorRise.Saga.Assets;

/// <summary>
/// The names a story and its dialogues use, collected while they are built, for the checks across them
/// (<see cref="SagaContentBuilder.CheckStory"/>).
/// </summary>
internal sealed partial class SagaUsage
{
    /// <summary>
    /// The names read by conditions and actions: variables, states, symbols.
    /// </summary>
    public HashSet<string> Paths { get; } = [];

    /// <summary>
    /// The names actions assign.
    /// </summary>
    public HashSet<string> Assigned { get; } = [];

    /// <summary>
    /// The names lines and choices show (<c>{day}</c>).
    /// </summary>
    public HashSet<string> Shown { get; } = [];

    /// <summary>
    /// The events conditions check with <c>raised</c>.
    /// </summary>
    public HashSet<string> EventsChecked { get; } = [];

    /// <summary>
    /// The functions of the game called.
    /// </summary>
    public HashSet<string> Calls { get; } = [];

    /// <summary>
    /// The signals emitted.
    /// </summary>
    public HashSet<string> Signals { get; } = [];

    /// <summary>
    /// The characters dialogues belong to, have as participants, or give lines.
    /// </summary>
    public HashSet<string> Characters { get; } = [];

    /// <summary>
    /// The quests an action may make active.
    /// </summary>
    public HashSet<string> QuestsStarted { get; } = [];

    /// <summary>
    /// The keys given to <c>visited</c> and <c>visits</c>, with where.
    /// </summary>
    public List<VisitedKey> VisitedKeys { get; } = [];

    public void AddCondition(SagaExpression? condition, string asset, string card)
    {
        if (condition is not null)
            Add(condition, asset, card);
    }

    public void AddActions(IEnumerable<SagaStatement> statements, string asset, string card)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case AssignStatement assign:
                    Assigned.Add(assign.Path);
                    // quest.<id>.state = <state>: it may start the quest unless the state is known and isn't active
                    if (assign.Path.StartsWith("quest.", StringComparison.Ordinal) && assign.Path.EndsWith(".state", StringComparison.Ordinal)
                        && SymbolOf(assign.Value) is null or "active")
                    {
                        QuestsStarted.Add(assign.Path["quest.".Length..^".state".Length]);
                    }
                    Add(assign.Value, asset, card);
                    break;
                case EmitStatement emit:
                    Signals.Add(emit.Signal);
                    foreach (var argument in emit.Arguments)
                        Add(argument, asset, card);
                    break;
            }
        }
    }

    /// <summary>
    /// The names a text shows, the way the runtime fills them in.
    /// </summary>
    public void AddText(string text)
    {
        foreach (Match match in ShownPattern().Matches(text))
            Shown.Add(match.Groups[1].Value);
    }

    private void Add(SagaExpression expression, string asset, string card)
    {
        switch (expression)
        {
            case PathExpression path:
                Paths.Add(path.Path);
                break;
            case CallExpression call:
                if (call.Function is SagaTypeChecker.VisitedFunction or SagaTypeChecker.VisitsFunction or SagaTypeChecker.RaisedFunction)
                {
                    if (call.Arguments is [LiteralExpression { Value.Kind: SagaValueKind.String } key])
                    {
                        if (call.Function == SagaTypeChecker.RaisedFunction)
                            EventsChecked.Add(key.Value.AsString());
                        else
                            VisitedKeys.Add(new VisitedKey(key.Value.AsString(), call.Function, asset, card, key.Position));
                    }
                }
                else
                {
                    Calls.Add(call.Function);
                }
                foreach (var argument in call.Arguments)
                    Add(argument, asset, card);
                break;
            case UnaryExpression unary:
                Add(unary.Operand, asset, card);
                break;
            case BinaryExpression binary:
                Add(binary.Left, asset, card);
                Add(binary.Right, asset, card);
                break;
        }
    }

    // The type checker turns a state written as a name into a symbol literal; anything else is known only when the story plays
    private static string? SymbolOf(SagaExpression value) => value is LiteralExpression { Value.Kind: SagaValueKind.Symbol } literal ? literal.Value.AsSymbol() : null;

    // As the runtime fills in a line (DialogueSession.Substitute)
    [GeneratedRegex(@"\{([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\}")]
    private static partial Regex ShownPattern();

    /// <summary>
    /// A key given to <c>visited</c> or <c>visits</c>: the key, the function, and the asset, card and position of the call.
    /// </summary>
    public sealed record VisitedKey(string Key, string Function, string Asset, string Card, int Position);
}
