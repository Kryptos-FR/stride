namespace ColorRise.Skein;

/// <summary>
/// A command of a script could not be called: its target was not found, or a word does not fit a parameter.
/// </summary>
public sealed class YarnCommandException : Exception
{
    /// <summary>
    /// Makes the exception.
    /// </summary>
    /// <param name="commandText">The text of the command.</param>
    /// <param name="reason">Why it could not be called.</param>
    public YarnCommandException(string commandText, string reason)
        : base($"The command <<{commandText}>> could not be called: {reason}")
    {
        CommandText = commandText;
        Reason = reason;
    }

    /// <summary>
    /// The text of the command, as the script gave it.
    /// </summary>
    public string CommandText { get; }

    /// <summary>
    /// Why it could not be called.
    /// </summary>
    public string Reason { get; }
}
