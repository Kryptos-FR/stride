namespace ColorRise.Skein.Editor.Graph;

/// <summary>
/// A link of the graph: the jumps, or the detours, of one node to another.
/// </summary>
public sealed class YarnLinkViewModel
{
    internal YarnLinkViewModel(YarnNodeCardViewModel source, object target, bool isDetour, int line)
    {
        Source = source;
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
    /// The slot the link starts from.
    /// </summary>
    public YarnCardSlot SourceSlot => IsDetour ? Source.DetourSlot : Source.OutSlot;

    /// <summary>
    /// The slot the link arrives on.
    /// </summary>
    public YarnCardSlot TargetSlot => Target is YarnNodeCardViewModel card ? card.InSlot : ((YarnStubCardViewModel)Target).InSlot;
}
