using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Stride.Core;

namespace ColorRise.Tessera;

/// <summary>
/// Names one tile of a tile set: one of the set's sources, and either the tile's place in that source's grid or its number.
/// </summary>
/// <remarks>
/// Bits 24 to 31 hold the source (1 to 255, 0 for no tile), and bit 23 is set for a numbered tile. A numbered tile keeps its
/// number in bits 0 to 22; a grid tile keeps its column in bits 0 to 11 and its row in bits 12 to 22. The text form is
/// <c>1:3,4</c> (source, column, row) or <c>2:#17</c> (source, number).
/// </remarks>
[DataContract]
public readonly struct TileId : IEquatable<TileId>, IComparable<TileId>
{
    public const int MaxSource = 255;
    public const int MaxColumn = 4095;
    public const int MaxRow = 2047;
    public const int MaxNumber = 8388607;

    private const uint NumberedFlag = 1u << 23;

    private TileId(uint value) => Value = value;

    /// <summary>
    /// The id in one number, as the remarks of the type lay it out.
    /// </summary>
    public uint Value { get; }

    /// <summary>
    /// Whether this is the default id, which names no tile.
    /// </summary>
    public bool IsNone => Source == 0;

    public int Source => (int)(Value >> 24);

    public bool IsNumbered => (Value & NumberedFlag) != 0;

    /// <summary>
    /// The column in the source's grid, 0 for a numbered tile.
    /// </summary>
    public int Column => IsNumbered ? 0 : (int)(Value & 0xFFF);

    /// <summary>
    /// The row in the source's grid, from the top, 0 for a numbered tile.
    /// </summary>
    public int Row => IsNumbered ? 0 : (int)((Value >> 12) & 0x7FF);

    /// <summary>
    /// The number of a numbered tile, 0 for a grid tile.
    /// </summary>
    public int Number => IsNumbered ? (int)(Value & 0x7FFFFF) : 0;

    /// <summary>
    /// The tile at a column and a row of a grid source.
    /// </summary>
    public static TileId FromGrid(int source, int column, int row)
    {
        CheckSource(source);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(column, MaxColumn);
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(row, MaxRow);
        return new TileId(((uint)source << 24) | ((uint)row << 12) | (uint)column);
    }

    /// <summary>
    /// The tile of a number in a source whose tiles are numbered.
    /// </summary>
    public static TileId FromNumber(int source, int number)
    {
        CheckSource(source);
        ArgumentOutOfRangeException.ThrowIfNegative(number);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, MaxNumber);
        return new TileId(((uint)source << 24) | NumberedFlag | (uint)number);
    }

    /// <summary>
    /// Reads the text form of an id, <c>1:3,4</c> or <c>2:#17</c>.
    /// </summary>
    /// <exception cref="FormatException">The text is not an id.</exception>
    public static TileId Parse(string text)
        => TryParse(text, out var id) ? id : throw new FormatException($"'{text}' is not a tile id: write source:column,row or source:#number, such as 1:3,4 or 2:#17.");

    /// <summary>
    /// Reads the text form of an id, <c>1:3,4</c> or <c>2:#17</c>, with no space and no sign.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> text, out TileId id)
    {
        id = default;
        var colon = text.IndexOf(':');
        if (colon < 0 || !TryParseNumber(text[..colon], out var source) || source is < 1 or > MaxSource)
        {
            return false;
        }

        var rest = text[(colon + 1)..];
        if (rest.StartsWith("#"))
        {
            if (!TryParseNumber(rest[1..], out var number) || number > MaxNumber)
            {
                return false;
            }
            id = FromNumber(source, number);
            return true;
        }

        var comma = rest.IndexOf(',');
        if (comma < 0 || !TryParseNumber(rest[..comma], out var column) || !TryParseNumber(rest[(comma + 1)..], out var row)
            || column > MaxColumn || row > MaxRow)
        {
            return false;
        }
        id = FromGrid(source, column, row);
        return true;
    }

    /// <summary>
    /// The text form, <c>1:3,4</c> or <c>2:#17</c>, or an empty string for no tile.
    /// </summary>
    public override string ToString() => IsNone ? ""
        : IsNumbered ? string.Create(CultureInfo.InvariantCulture, $"{Source}:#{Number}")
        : string.Create(CultureInfo.InvariantCulture, $"{Source}:{Column},{Row}");

    public bool Equals(TileId other) => Value == other.Value;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is TileId other && Equals(other);

    public override int GetHashCode() => (int)Value;

    public int CompareTo(TileId other) => Value.CompareTo(other.Value);

    public static bool operator ==(TileId left, TileId right) => left.Equals(right);

    public static bool operator !=(TileId left, TileId right) => !left.Equals(right);

    private static void CheckSource(int source)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(source, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(source, MaxSource);
    }

    // Digits only: no sign, no space, no separator of thousands
    private static bool TryParseNumber(ReadOnlySpan<char> text, out int value)
        => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
