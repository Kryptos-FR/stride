using Stride.Core;

namespace ColorRise.Saga.Expressions;

/// <summary>
/// A condition as compiled content: its source, for messages and the editor, and its checked tree.
/// </summary>
[DataContract("SagaCondition")]
public sealed class SagaCondition : ISagaCondition
{
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// The checked tree. Null means "always".
    /// </summary>
    public SagaExpression? Tree { get; set; }

    /// <summary>
    /// Whether the condition is empty. Where an empty condition means "never" rather than "always", the runtime checks it.
    /// </summary>
    [DataMemberIgnore]
    public bool IsEmpty => Tree is null;

    public bool Evaluate(ISagaEvaluationContext context) => Tree is null || SagaTreeEvaluator.Evaluate(Tree, context).AsBool();

    public override string ToString() => Source;
}
