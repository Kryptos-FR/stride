namespace ColorRise.Saga;

/// <summary>
/// An error of the story while it runs: a story that never settles, a condition that fails.
/// </summary>
public sealed class SagaException : Exception
{
    public SagaException(string message)
        : base(message)
    {
    }

    public SagaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Whether the message already names the card the error comes from.
    /// </summary>
    internal bool NamesCard { get; init; }
}
