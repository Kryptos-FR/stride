namespace ColorRise.Skein.Editor.Graph;

/// <summary>
/// An end of the links of a card: a card receives links on one slot and starts them from others, its own and the ones
/// of its rows.
/// </summary>
public sealed class YarnCardSlot
{
    internal YarnCardSlot(object card, bool isInput, YarnCardRowViewModel? row = null)
    {
        Card = card;
        IsInput = isInput;
        Row = row;
    }

    /// <summary>
    /// The card of the slot: a <see cref="YarnNodeCardViewModel"/> or a <see cref="YarnStubCardViewModel"/>.
    /// </summary>
    public object Card { get; }

    /// <summary>
    /// Whether the slot receives links.
    /// </summary>
    public bool IsInput { get; }

    /// <summary>
    /// The row of the card the slot is on, or null for a slot of the card itself.
    /// </summary>
    public YarnCardRowViewModel? Row { get; }
}
