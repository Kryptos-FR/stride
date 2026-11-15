namespace ColorRise.Saga.Assets;

/// <summary>
/// How bad a <see cref="SagaBuildDiagnostic"/> is.
/// </summary>
public enum SagaDiagnosticSeverity
{
    /// <summary>
    /// The story can't be built or played until it is fixed.
    /// </summary>
    Error,

    /// <summary>
    /// The story builds and plays, but this is likely a mistake.
    /// </summary>
    Warning,
}

/// <summary>
/// A problem found while building a story or a dialogue: the asset, the card (a path such as
/// <c>act act_2 / scenario overlord / StartWhen</c>), where in the card's text, what is wrong, and whether it is an error or a
/// warning.
/// </summary>
public sealed record SagaBuildDiagnostic(string Asset, string Card, int Position, string Message, SagaDiagnosticSeverity Severity = SagaDiagnosticSeverity.Error)
{
    public override string ToString() => $"{Asset}: {Card} (at {Position}): {Message}";
}
