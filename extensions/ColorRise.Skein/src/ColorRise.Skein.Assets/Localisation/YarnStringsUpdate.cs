namespace ColorRise.Skein.Assets.Localisation;

/// <summary>
/// A strings file brought up to date with the scripts.
/// </summary>
/// <param name="Rows">The rows of the file, by script and line.</param>
/// <param name="Added">The lines of the scripts the file did not have; their text is empty, for the translator to fill.</param>
/// <param name="Removed">The rows whose line the scripts no longer have.</param>
/// <param name="NeedsUpdate">The rows whose line changed in the scripts since its translation; their text now starts with <see cref="YarnStringsCsv.NeedsUpdate"/>.</param>
public sealed record YarnStringsUpdateResult(IReadOnlyList<YarnStringRow> Rows, int Added, int Removed, int NeedsUpdate)
{
    /// <summary>
    /// Whether the file is to be written again.
    /// </summary>
    public bool Changed => Added > 0 || Removed > 0 || NeedsUpdate > 0;
}

/// <summary>
/// "Update strings files": what Yarn Spinner's tools do to the strings file of a language when the scripts changed.
/// </summary>
public static class YarnStringsUpdate
{
    /// <summary>
    /// Brings the rows of a language up to date with the rows of the base language.
    /// </summary>
    /// <param name="existing">The rows of the language's strings file.</param>
    /// <param name="baseRows">The rows of the base language (<see cref="YarnStringsCsv.RowsOf"/>).</param>
    /// <param name="language">The language of the file.</param>
    public static YarnStringsUpdateResult Update(IReadOnlyList<YarnStringRow> existing, IReadOnlyList<YarnStringRow> baseRows, string language)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(baseRows);
        ArgumentNullException.ThrowIfNull(language);

        // A file that names a line twice keeps the first
        var translated = new Dictionary<string, YarnStringRow>();
        foreach (var row in existing)
        {
            translated.TryAdd(row.Id, row);
        }

        var rows = new List<YarnStringRow>();
        var added = 0;
        var needsUpdate = 0;
        foreach (var source in baseRows)
        {
            if (!translated.TryGetValue(source.Id, out var row))
            {
                rows.Add(source with { Language = language, Text = "" });
                added++;
                continue;
            }

            // Where the line is, and what is said of it, follow the scripts; the text is the translator's
            row = row with { Language = language, File = source.File, Node = source.Node, LineNumber = source.LineNumber, Comment = source.Comment };
            if (row.Lock != source.Lock)
            {
                var marked = row.Text.Length == 0 || row.Text.StartsWith(YarnStringsCsv.NeedsUpdate, StringComparison.Ordinal);
                row = row with { Lock = source.Lock, Text = marked ? row.Text : YarnStringsCsv.NeedsUpdate + row.Text };
                needsUpdate++;
            }

            rows.Add(row);
        }

        var known = baseRows.Select(r => r.Id).ToHashSet();
        var removed = translated.Keys.Count(id => !known.Contains(id));
        return new YarnStringsUpdateResult(rows, added, removed, needsUpdate);
    }
}
