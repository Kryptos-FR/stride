namespace ColorRise.Skein.Assets.Compilation;

/// <summary>
/// A function the game gives to Yarn scripts, as the compiler needs to know it to check the calls: its name and its types.
/// </summary>
public sealed record YarnFunctionDeclaration
{
    /// <summary>
    /// The name of Yarn's number type.
    /// </summary>
    public const string Number = "number";

    /// <summary>
    /// The name of Yarn's string type.
    /// </summary>
    public const string String = "string";

    /// <summary>
    /// The name of Yarn's boolean type.
    /// </summary>
    public const string Bool = "bool";

    /// <summary>
    /// The name scripts call the function by.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The type of each parameter: <see cref="Number"/>, <see cref="String"/> or <see cref="Bool"/>.
    /// </summary>
    public IReadOnlyList<string> ParameterTypes { get; init; } = [];

    /// <summary>
    /// The type of the result: <see cref="Number"/>, <see cref="String"/> or <see cref="Bool"/>.
    /// </summary>
    public required string ReturnType { get; init; }
}
