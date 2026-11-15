using Stride.Core;

namespace ColorRise.Skein;

/// <summary>
/// The variables of a <see cref="VariableStorage"/> at one moment: what a game writes in its save file, with whatever
/// serializer it uses, and gives back to <see cref="VariableStorage.Load"/>.
/// </summary>
[DataContract("YarnVariableSnapshot")]
public sealed class VariableSnapshot
{
    /// <summary>
    /// The numbers, by variable name. An enum whose cases are numbers is one of them.
    /// </summary>
    public Dictionary<string, float> Numbers { get; } = [];

    /// <summary>
    /// The strings, by variable name.
    /// </summary>
    public Dictionary<string, string> Strings { get; } = [];

    /// <summary>
    /// The booleans, by variable name.
    /// </summary>
    public Dictionary<string, bool> Bools { get; } = [];
}
