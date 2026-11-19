namespace ColorRise.Skein;

/// <summary>
/// Makes a static method a function of Yarn scripts: <c>&lt;&lt;if has_item("oil")&gt;&gt;</c>.
/// </summary>
/// <remarks>
/// The method takes and returns booleans, numbers and strings, and has no effect on the game: Yarn Spinner calls it when
/// it evaluates conditions, at times and as often as it sees fit. It may return a <see cref="Task{TResult}"/> or a
/// <see cref="ValueTask{TResult}"/> of one, and take a <see cref="CancellationToken"/>: the dialogue waits for it, and
/// the token is cancelled when the dialogue stops. In a <c>when:</c> header or a smart variable, which Yarn Spinner
/// computes without waiting, a function that has not answered at once gives false, 0 or an empty string.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class YarnFunctionAttribute : Attribute
{
    /// <summary>
    /// Names the function.
    /// </summary>
    /// <param name="name">The name scripts call.</param>
    public YarnFunctionAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// The name scripts call.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// What the function tells, for the writers: editors show it.
    /// </summary>
    public string? Description { get; set; }
}
