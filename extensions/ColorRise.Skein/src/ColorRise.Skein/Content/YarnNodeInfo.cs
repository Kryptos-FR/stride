using Stride.Core;

namespace ColorRise.Skein;

/// <summary>
/// A node of a compiled project: what a game reads of it without running it, such as the quest a node belongs to.
/// </summary>
[DataContract("YarnNodeInfo")]
public sealed class YarnNodeInfo
{
    /// <summary>
    /// The name of the node in the program: its title, or for a node of a node group the unique name the compiler made.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// The title the script gives the node, what a game runs. The nodes of a node group share it.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// The headers in the order of the script, the title among them.
    /// </summary>
    public List<YarnNodeHeader> Headers { get; } = [];

    /// <summary>
    /// The words of the <c>tags:</c> header.
    /// </summary>
    public List<string> Tags { get; } = [];

    /// <summary>
    /// The value of the first header with this key, or null.
    /// </summary>
    public string? Header(string key)
    {
        foreach (var header in Headers)
        {
            if (header.Key == key)
                return header.Value;
        }

        return null;
    }
}

/// <summary>
/// A header of a node: <c>key: value</c>.
/// </summary>
[DataContract("YarnNodeHeader")]
public struct YarnNodeHeader : IEquatable<YarnNodeHeader>
{
    /// <summary>
    /// Makes a header.
    /// </summary>
    public YarnNodeHeader(string key, string value)
    {
        Key = key;
        Value = value;
    }

    /// <summary>
    /// What is before the colon.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// What is after the colon.
    /// </summary>
    public string Value { get; set; }

    /// <inheritdoc />
    public readonly bool Equals(YarnNodeHeader other) => Key == other.Key && Value == other.Value;

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is YarnNodeHeader other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode() => HashCode.Combine(Key, Value);

    /// <inheritdoc />
    public override readonly string ToString() => $"{Key}: {Value}";

    /// <summary>
    /// Whether two headers have the same key and value.
    /// </summary>
    public static bool operator ==(YarnNodeHeader left, YarnNodeHeader right) => left.Equals(right);

    /// <summary>
    /// Whether two headers differ.
    /// </summary>
    public static bool operator !=(YarnNodeHeader left, YarnNodeHeader right) => !left.Equals(right);
}
