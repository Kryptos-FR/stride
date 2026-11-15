namespace ColorRise.Saga.Editor.ViewModels;

/// <summary>
/// Whether a slot takes links in or sends them out.
/// </summary>
public enum SagaGraphSlotDirection
{
    Input,
    Output,
}

/// <summary>
/// A slot of a node of a Saga graph, which links start from or end at.
/// </summary>
/// <param name="node">The node of the slot.</param>
/// <param name="direction">Whether links come in or go out.</param>
/// <param name="name">The name of the slot shown next to it, empty for the main slots.</param>
/// <param name="card">The card the slot stands for, such as a dialogue choice whose target it sets, or <c>null</c>.</param>
public sealed class SagaGraphSlot(SagaGraphNodeViewModel node, SagaGraphSlotDirection direction, string name = "", object? card = null)
{
    public SagaGraphNodeViewModel Node { get; } = node;

    public SagaGraphSlotDirection Direction { get; } = direction;

    public string Name { get; } = name;

    public object? Card { get; } = card;

    public override string ToString() => $"{Node.Id}.{(Name.Length > 0 ? Name : Direction.ToString())}";
}

/// <summary>
/// A link the user asks for, from an output slot to an input slot.
/// </summary>
/// <param name="Source">The output slot.</param>
/// <param name="Target">The input slot.</param>
public sealed record SagaLinkRequest(SagaGraphSlot Source, SagaGraphSlot Target);
