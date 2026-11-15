namespace ColorRise.Skein;

/// <summary>
/// Makes a static method a function of Yarn scripts: <c>&lt;&lt;if has_item("oil")&gt;&gt;</c>.
/// </summary>
/// <remarks>
/// The method takes and returns booleans, numbers and strings, and has no effect on the game: Yarn Spinner calls it when
/// it evaluates conditions, at times and as often as it sees fit.
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
