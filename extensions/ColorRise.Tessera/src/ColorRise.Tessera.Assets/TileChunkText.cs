using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// The text of a chunk in a tile map asset: 16 lines of 16 cells, the top row first, so that the file looks like the map.
/// </summary>
/// <remarks>
/// A cell is <c>.</c> when empty, else <c>slot/id</c> followed by its flips in the order they are applied: <c>d</c>, then
/// <c>h</c>, then <c>v</c>. For example <c>1/1:3,4</c>, <c>1/1:3,4h</c> or <c>2/1:#17dv</c>. One space between cells, and a
/// line feed after each line.
/// </remarks>
public static class TileChunkText
{
    private const int Size = TileChunk.Size;
    private const string ExampleCells = "write . for an empty cell, or slot/id with its flips, such as 1/1:3,4 or 2/1:#17dv";

    /// <summary>
    /// Writes the 256 cells of a chunk, whose index is <c>y * 16 + x</c> with y up. A cell whose tile is none is written empty.
    /// </summary>
    public static string Encode(ReadOnlySpan<TileCell> cells)
    {
        CheckLength(cells.Length);
        var text = new StringBuilder(Size * Size * 4);
        for (var y = Size - 1; y >= 0; y--)
        {
            for (var x = 0; x < Size; x++)
            {
                if (x > 0)
                {
                    text.Append(' ');
                }
                AppendCell(text, cells[y * Size + x]);
            }
            text.Append('\n');
        }
        return text.ToString();
    }

    /// <summary>
    /// Reads the 256 cells of a chunk.
    /// </summary>
    /// <exception cref="FormatException">The text is not a chunk: the message names the line, the cell and its text.</exception>
    public static void Decode(string text, Span<TileCell> cells)
    {
        if (!TryDecode(text, cells, out var error))
        {
            throw new FormatException(error);
        }
    }

    /// <summary>
    /// Reads the 256 cells of a chunk, and leaves them as they were when the text is not a chunk.
    /// </summary>
    /// <param name="error">What is wrong, with the line and the cell, counted from 1, and the text of the cell.</param>
    public static bool TryDecode(string text, Span<TileCell> cells, [NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(text);
        CheckLength(cells.Length);

        var lines = text.Split('\n');
        // The line feed that ends the last line makes an empty last item; a text without it is read as well
        var count = lines.Length > 0 && lines[^1].Length == 0 ? lines.Length - 1 : lines.Length;
        if (count != Size)
        {
            error = $"The chunk has {count} lines; it needs {Size}, of {Size} cells each.";
            return false;
        }

        var read = new TileCell[Size * Size];
        for (var line = 0; line < Size; line++)
        {
            if (!TryDecodeLine(lines[line].TrimEnd('\r'), line, read, out error))
            {
                return false;
            }
        }
        read.CopyTo(cells);
        error = null;
        return true;
    }

    private static bool TryDecodeLine(string line, int index, TileCell[] cells, [NotNullWhen(false)] out string? error)
    {
        var number = index + 1;
        if (line.StartsWith("<<<<<<<", StringComparison.Ordinal) || line.StartsWith("=======", StringComparison.Ordinal)
            || line.StartsWith(">>>>>>>", StringComparison.Ordinal))
        {
            error = $"line {number}: '{line}' is a mark that a merge left; choose the cells to keep and remove the marks.";
            return false;
        }

        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var y = Size - 1 - index;
        for (var x = 0; x < tokens.Length; x++)
        {
            if (x >= Size)
            {
                error = $"line {number}, token {x + 1}: '{tokens[x]}' is one cell too many; a line has {Size} cells.";
                return false;
            }
            if (!TryDecodeCell(tokens[x], out var cell))
            {
                error = $"line {number}, token {x + 1}: '{tokens[x]}' is not a cell; {ExampleCells}.";
                return false;
            }
            cells[y * Size + x] = cell;
        }

        if (tokens.Length < Size)
        {
            error = $"line {number} has {tokens.Length} cells; a line has {Size}.";
            return false;
        }
        error = null;
        return true;
    }

    private static bool TryDecodeCell(string token, out TileCell cell)
    {
        cell = TileCell.Empty;
        if (token == ".")
        {
            return true;
        }

        var slash = token.IndexOf('/');
        if (slash <= 0
            || !ushort.TryParse(token.AsSpan(0, slash), NumberStyles.None, CultureInfo.InvariantCulture, out var slot) || slot == 0)
        {
            return false;
        }

        // The flips are the letters at the end, each at most once
        var end = token.Length;
        var flip = TileFlip.None;
        while (end > slash + 1 && char.IsAsciiLetter(token[end - 1]))
        {
            var letter = token[end - 1] switch
            {
                'd' => TileFlip.Diagonal,
                'h' => TileFlip.Horizontal,
                'v' => TileFlip.Vertical,
                _ => (TileFlip?)null,
            };
            if (letter is not { } known || (flip & known) != 0)
            {
                return false;
            }
            flip |= known;
            end--;
        }

        if (!TileId.TryParse(token.AsSpan(slash + 1, end - slash - 1), out var tile))
        {
            return false;
        }
        cell = new TileCell(slot, tile, flip);
        return true;
    }

    private static void AppendCell(StringBuilder text, TileCell cell)
    {
        if (cell.IsEmpty || cell.Tile.IsNone)
        {
            text.Append('.');
            return;
        }
        text.Append(cell.Slot.ToString(CultureInfo.InvariantCulture)).Append('/').Append(cell.Tile.ToString());
        if ((cell.Flip & TileFlip.Diagonal) != 0)
        {
            text.Append('d');
        }
        if ((cell.Flip & TileFlip.Horizontal) != 0)
        {
            text.Append('h');
        }
        if ((cell.Flip & TileFlip.Vertical) != 0)
        {
            text.Append('v');
        }
    }

    private static void CheckLength(int length)
    {
        if (length != Size * Size)
        {
            throw new ArgumentException($"A chunk has {Size * Size} cells, not {length}.", "cells");
        }
    }
}
