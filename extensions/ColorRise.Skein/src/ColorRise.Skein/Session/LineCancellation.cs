namespace ColorRise.Skein;

/// <summary>
/// The two requests a player makes while a line shows, as in Yarn Spinner for Unity.
/// </summary>
/// <param name="HurryUp">Cancelled when the line should finish quickly: a typewriter shows the whole text at once.</param>
/// <param name="NextLine">Cancelled when the line should end now: the presenter returns as soon as it can. Cancelled too
/// when the dialogue stops.</param>
public readonly record struct LineCancellation(CancellationToken HurryUp, CancellationToken NextLine)
{
    /// <summary>
    /// No request ever comes.
    /// </summary>
    public static LineCancellation None => default;
}
