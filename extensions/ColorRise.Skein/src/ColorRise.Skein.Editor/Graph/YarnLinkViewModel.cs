namespace ColorRise.Skein.Editor.Graph;

/// <summary>
/// A link of the graph: the jumps, or the detours, of one node, or of one row of it, to another node.
/// </summary>
public sealed class YarnLinkViewModel
{
    internal YarnLinkViewModel(YarnNodeCardViewModel source, object target, bool isDetour, int line, YarnCardRowViewModel? row = null)
    {
        Source = source;
        Row = row;
        Target = target;
        IsDetour = isDetour;
        Line = line;
    }

    /// <summary>
    /// The card of the node the jump is in.
    /// </summary>
    public YarnNodeCardViewModel Source { get; }

    /// <summary>
    /// The card the jump leads to: a <see cref="YarnNodeCardViewModel"/> of the script, or a <see cref="YarnStubCardViewModel"/>.
    /// </summary>
    public object Target { get; }

    /// <summary>
    /// Whether the dialogue comes back to the source once the target ends.
    /// </summary>
    public bool IsDetour { get; }

    /// <summary>
    /// Whether the jump names a node that is nowhere in the project.
    /// </summary>
    public bool IsBroken => Target is YarnStubCardViewModel { IsBroken: true };

    /// <summary>
    /// The line of the first such jump in the script, from 1, as the last compilation without an error saw it.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// The row of the source the jump is in: the option, the line group item or the branch of a block it is under; null
    /// for a jump at the top of the node's body.
    /// </summary>
    public YarnCardRowViewModel? Row { get; }

    /// <summary>
    /// The slot the link starts from: the one of its row while the card has the row, else the card's own.
    /// </summary>
    public YarnCardSlot SourceSlot => Row is { HasConnector: true } row && Source.Rows.Contains(row) ? row.OutSlot : IsDetour ? Source.DetourSlot : Source.OutSlot;

    /// <summary>
    /// The slot the link arrives on.
    /// </summary>
    public YarnCardSlot TargetSlot => Target is YarnNodeCardViewModel card ? card.InSlot : ((YarnStubCardViewModel)Target).InSlot;
}
