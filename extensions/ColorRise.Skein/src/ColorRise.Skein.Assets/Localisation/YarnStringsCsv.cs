using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ColorRise.Skein.Assets.Compilation;

namespace ColorRise.Skein.Assets.Localisation;

/// <summary>
/// A row of a strings file: a line of the scripts in one language, as Yarn Spinner's tools write it.
/// </summary>
public sealed record YarnStringRow
{
    /// <summary>
    /// The language of the text.
    /// </summary>
    public string Language { get; init; } = "";

    /// <summary>
    /// The id of the line (<c>line:post</c>).
    /// </summary>
    public string Id { get; init; } = "";

    /// <summary>
    /// The text of the line in <see cref="Language"/>; empty while the line is not translated.
    /// </summary>
    public string Text { get; init; } = "";

    /// <summary>
    /// The script the line is in.
    /// </summary>
    public string File { get; init; } = "";

    /// <summary>
    /// The node the line is in.
    /// </summary>
    public string Node { get; init; } = "";

    /// <summary>
    /// The line of the script, from 1.
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// The mark of the text the translation was made from (<see cref="YarnStringsCsv.LockOf"/>): another mark than the
    /// scripts' means the text changed since.
    /// </summary>
    public string Lock { get; init; } = "";

    /// <summary>
    /// A note for the translator.
    /// </summary>
    public string Comment { get; init; } = "";
}

/// <summary>
/// Reads and writes strings files in Yarn Spinner's CSV format: the columns
/// <c>language,id,text,file,node,lineNumber,lock,comment</c>.
/// </summary>
public static class YarnStringsCsv
{
    /// <summary>
    /// What "Update strings files" puts before a translation whose source changed, as Yarn Spinner's tools do.
    /// </summary>
    public const string NeedsUpdate = "(NEEDS UPDATE) ";

    private static readonly string[] Columns = ["language", "id", "text", "file", "node", "lineNumber", "lock", "comment"];

    /// <summary>
    /// The mark of a text, as Yarn Spinner's tools compute it: the first eight hexadecimal digits of its SHA-256.
    /// </summary>
    public static string LockOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..8];
    }

    /// <summary>
    /// The rows of the base language of a compilation: every line with an id of its own, by script and line.
    /// </summary>
    /// <remarks>
    /// A line without a <c>#line:</c> tag has an id that changes with the script, and a shadow line shows the text of
    /// another: neither is a row.
    /// </remarks>
    public static IReadOnlyList<YarnStringRow> RowsOf(YarnCompilation compilation, string language)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(language);

        return [.. compilation.Lines
            .Where(l => !l.Value.IsImplicitId && l.Value.ShadowOf is null)
            .OrderBy(l => l.Value.File, StringComparer.Ordinal).ThenBy(l => l.Value.Line).ThenBy(l => l.Key, StringComparer.Ordinal)
            .Select(l => new YarnStringRow
            {
                Language = language,
                Id = l.Key,
                Text = l.Value.Text,
                File = l.Value.File,
                Node = l.Value.Node,
                LineNumber = l.Value.Line,
                Lock = LockOf(l.Value.Text),
                Comment = CommentOf(l.Key, l.Value.Metadata),
            })];
    }

    /// <summary>
    /// Writes rows as a strings file.
    /// </summary>
    public static string Write(IEnumerable<YarnStringRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var csv = new StringBuilder();
        WriteRecord(csv, Columns);
        foreach (var row in rows)
        {
            WriteRecord(csv, [row.Language, row.Id, row.Text, row.File, row.Node, row.LineNumber.ToString(CultureInfo.InvariantCulture), row.Lock, row.Comment]);
        }

        return csv.ToString();
    }

    /// <summary>
    /// Reads a strings file. Columns are found by their name, in any order; a column the file lacks is empty.
    /// </summary>
    /// <exception cref="FormatException">The text is not a strings file: it has no <c>id</c> column, or a quote is not closed.</exception>
    public static IReadOnlyList<YarnStringRow> Read(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);

        var records = ReadRecords(csv);
        if (records.Count == 0)
            return [];

        var header = records[0].Select(c => c.Trim()).ToList();
        int Column(string name) => header.FindIndex(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase));
        var columns = Columns.Select(Column).ToArray();
        if (columns[1] < 0)
            throw new FormatException("The strings file has no 'id' column: its first line must name the columns language,id,text,file,node,lineNumber,lock,comment.");

        var rows = new List<YarnStringRow>();
        foreach (var record in records.Skip(1))
        {
            // A blank line is not a row
            if (record.Count == 1 && record[0].Length == 0)
                continue;

            string Field(int column) => columns[column] >= 0 && columns[column] < record.Count ? record[columns[column]] : "";
            rows.Add(new YarnStringRow
            {
                Language = Field(0),
                Id = Field(1),
                Text = Field(2),
                File = Field(3),
                Node = Field(4),
                LineNumber = int.TryParse(Field(5), NumberStyles.Integer, CultureInfo.InvariantCulture, out var line) ? line : 0,
                Lock = Field(6),
                Comment = Field(7),
            });
        }

        return rows;
    }

    // The tags of a line tell a translator about it; the id and what the compiler adds do not
    private static string CommentOf(string id, IReadOnlyList<string> metadata)
    {
        var tags = metadata.Where(t => t != id && t != "lastline" && !t.StartsWith("line:", StringComparison.Ordinal) && !t.StartsWith("shadow:", StringComparison.Ordinal)).ToList();
        return tags.Count > 0 ? $"Line metadata: {string.Join(' ', tags)}" : "";
    }

    private static void WriteRecord(StringBuilder csv, IReadOnlyList<string> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0)
                csv.Append(',');

            var field = fields[i];
            var quoted = field.AsSpan().IndexOfAny(",\"\r\n") >= 0 || (field.Length > 0 && (char.IsWhiteSpace(field[0]) || char.IsWhiteSpace(field[^1])));
            if (quoted)
                csv.Append('"').Append(field.Replace("\"", "\"\"")).Append('"');
            else
                csv.Append(field);
        }

        csv.Append("\r\n");
    }

    private static List<List<string>> ReadRecords(string csv)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var any = false;

        // A byte order mark that came with the text is not part of the first column's name
        var i = csv.Length > 0 && csv[0] == '﻿' ? 1 : 0;
        for (; i < csv.Length; i++)
        {
            var c = csv[i];
            if (inQuotes)
            {
                if (c != '"')
                {
                    field.Append(c);
                }
                else if (i + 1 < csv.Length && csv[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = false;
                }

                continue;
            }

            switch (c)
            {
                case '"' when field.Length == 0:
                    inQuotes = true;
                    any = true;
                    break;
                case ',':
                    record.Add(field.ToString());
                    field.Clear();
                    any = true;
                    break;
                case '\r' when i + 1 < csv.Length && csv[i + 1] == '\n':
                    break;
                case '\r' or '\n':
                    record.Add(field.ToString());
                    field.Clear();
                    records.Add(record);
                    record = [];
                    any = false;
                    break;
                default:
                    field.Append(c);
                    any = true;
                    break;
            }
        }

        if (inQuotes)
            throw new FormatException("The strings file has a quote that is not closed.");

        if (any || field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            records.Add(record);
        }

        return records;
    }
}
