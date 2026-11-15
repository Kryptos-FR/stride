namespace ColorRise.Saga.Expressions;

/// <summary>
/// An error in an expression, at a position of its source.
/// </summary>
/// <param name="Position">The index of the first character in error.</param>
/// <param name="Length">The number of characters in error (0 at the end of the source).</param>
/// <param name="Message">What is wrong.</param>
public sealed record SagaDiagnostic(int Position, int Length, string Message);

/// <summary>
/// What parsing a source gave: the value, or the errors.
/// </summary>
public sealed record ParseResult<T>(T Value, IReadOnlyList<SagaDiagnostic> Diagnostics)
{
    /// <summary>
    /// Whether the source parsed without error.
    /// </summary>
    public bool Succeeded => Diagnostics.Count == 0;
}
