namespace ColorRise.Skein.Editor.Graph;

/// <summary>
/// An end of the links of a card: a card receives links on one slot and starts them from another.
/// </summary>
public sealed class YarnCardSlot
{
    internal YarnCardSlot(object card, bool isInput)
    {
        Card = card;
        IsInput = isInput;
    }

    /// <summary>
    /// The card of the slot: a <see cref="YarnNodeCardViewModel"/> or a <see cref="YarnStubCardViewModel"/>.
    /// </summary>
    public object Card { get; }

    /// <summary>
    /// Whether the slot receives links.
    /// </summary>
    public bool IsInput { get; }
}
