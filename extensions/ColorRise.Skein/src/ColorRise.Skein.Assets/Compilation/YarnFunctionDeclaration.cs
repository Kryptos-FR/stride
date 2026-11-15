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
    /// The type of the values a function takes after its parameters, as many as a script writes (a <c>params</c> array),
    /// or null.
    /// </summary>
    public string? VariadicType { get; init; }

    /// <summary>
    /// The type of the result: <see cref="Number"/>, <see cref="String"/> or <see cref="Bool"/>.
    /// </summary>
    public required string ReturnType { get; init; }

    /// <summary>
    /// The declaration of a function the game's registry describes.
    /// </summary>
    /// <exception cref="ArgumentException">The description is a command's.</exception>
    public static YarnFunctionDeclaration From(YarnActionDescription function)
    {
        ArgumentNullException.ThrowIfNull(function);
        if (function.Kind != YarnActionKind.Function || function.ReturnType is null)
            throw new ArgumentException($"'{function.Name}' is not a function.", nameof(function));

        return new YarnFunctionDeclaration
        {
            Name = function.Name,
            ParameterTypes = [.. function.Parameters.Where(p => !p.IsParams).Select(p => p.Type)],
            VariadicType = function.Parameters.FirstOrDefault(p => p.IsParams)?.Type,
            ReturnType = function.ReturnType,
        };
    }
}
