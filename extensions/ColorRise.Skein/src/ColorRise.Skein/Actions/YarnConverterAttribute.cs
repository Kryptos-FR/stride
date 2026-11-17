using System.Diagnostics.CodeAnalysis;

namespace ColorRise.Skein;

/// <summary>
/// Makes a static method the converter of a type for the parameters of commands: <c>&lt;&lt;dig shovel&gt;&gt;</c> gives
/// a <c>Tool</c> to <c>Dig(Tool tool)</c>.
/// </summary>
/// <remarks>
/// The method is <c>static bool TryX(string text, out T value)</c>, as with Yarn Spinner for Unity: it returns false when
/// the word is not a <c>T</c>. A converter takes the place of Skein's own reading of the type (an enum by its case, an
/// object found by name).
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class YarnConverterAttribute : Attribute
{
    /// <summary>
    /// Names the type the method converts to.
    /// </summary>
    /// <param name="type">The type of the parameters the method makes.</param>
    public YarnConverterAttribute(Type type)
    {
        Type = type;
    }

    /// <summary>
    /// The type of the parameters the method makes.
    /// </summary>
    public Type Type { get; }
}

/// <summary>
/// Converts a word of a command to a value of a type, for <see cref="YarnActionRegistry.AddConverter{T}"/>.
/// </summary>
/// <param name="text">The word the script wrote, the values of the script already in it.</param>
/// <param name="value">The value, when the word is one.</param>
/// <returns>Whether the word is a value of the type.</returns>
public delegate bool YarnConvert<T>(string text, [MaybeNullWhen(false)] out T value);
