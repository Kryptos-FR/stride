using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ColorRise.Tessera.Serialization;
using Stride.Core;
using Stride.Core.Serialization;

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
[DataSerializer(typeof(TileIdSerializer))]
public readonly struct TileId : IEquatable<TileId>, IComparable<TileId>
{
    /// <summary>
    /// The largest id of a source.
    /// </summary>
    public const int MaxSource = 255;
    /// <summary>
    /// The largest column of a grid source.
    /// </summary>
    public const int MaxColumn = 4095;
    /// <summary>
    /// The largest row of a grid source.
    /// </summary>
    public const int MaxRow = 2047;
    /// <summary>
    /// The largest number of a tile of a sprite sheet source.
    /// </summary>
    public const int MaxNumber = 8388607;

    private const uint NumberedFlag = 1u << 23;

    private TileId(uint value) => Value = value;

    // An id read back from its number, as content keeps it
    internal static TileId FromValue(uint value) => new(value);

    /// <summary>
    /// The id in one number, as the remarks of the type lay it out.
    /// </summary>
    public uint Value { get; }

    /// <summary>
    /// Whether this is the default id, which names no tile.
    /// </summary>
    public bool IsNone => Source == 0;

    /// <summary>
    /// The id of the source the tile comes from, 1 to 255; 0 for no tile.
    /// </summary>
    public int Source => (int)(Value >> 24);

    /// <summary>
    /// Whether the tile is a sprite of a sheet, named by a number, and not a place in a grid.
    /// </summary>
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

    /// <summary>
    /// Whether both ids name the same tile.
    /// </summary>
    public bool Equals(TileId other) => Value == other.Value;

    /// <summary>
    /// Whether the object is an id of the same tile.
    /// </summary>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is TileId other && Equals(other);

    /// <summary>
    /// A hash of the id.
    /// </summary>
    public override int GetHashCode() => (int)Value;

    /// <summary>
    /// Orders ids by source, then by place in the grid or by number.
    /// </summary>
    public int CompareTo(TileId other) => Value.CompareTo(other.Value);

    /// <summary>
    /// Whether both ids name the same tile.
    /// </summary>
    public static bool operator ==(TileId left, TileId right) => left.Equals(right);

    /// <summary>
    /// Whether the ids name different tiles.
    /// </summary>
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
