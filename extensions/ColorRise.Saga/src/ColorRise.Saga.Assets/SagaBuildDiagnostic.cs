namespace ColorRise.Saga.Assets;

/// <summary>
/// A problem found while building a story or a dialogue: the asset, the card (a path such as
/// <c>act act_2 / scenario overlord / StartWhen</c>), where in the card's text, and what is wrong.
/// </summary>
public sealed record SagaBuildDiagnostic(string Asset, string Card, int Position, string Message)
{
    public override string ToString() => $"{Asset}: {Card} (at {Position}): {Message}";
}
