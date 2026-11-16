namespace ColorRise.Skein.Assets.Localisation;

/// <summary>
/// How far the strings file of a language is from the scripts.
/// </summary>
/// <param name="Lines">The lines of the scripts that have an id.</param>
/// <param name="Translated">The lines with a translation made from the text the scripts have now.</param>
/// <param name="ToUpdate">The lines with a translation made from a text that changed since, or marked with <see cref="YarnStringsCsv.NeedsUpdate"/>.</param>
/// <param name="Dead">The rows of the file whose line the scripts no longer have.</param>
public sealed record YarnStringsStatus(int Lines, int Translated, int ToUpdate, int Dead)
{
    /// <summary>
    /// The lines without a translation, which show in the base language.
    /// </summary>
    public int Missing => Lines - Translated - ToUpdate;

    /// <summary>
    /// Compares the rows of a language's strings file with the rows of the base language (<see cref="YarnStringsCsv.RowsOf"/>).
    /// </summary>
    public static YarnStringsStatus Of(IReadOnlyList<YarnStringRow> baseRows, IReadOnlyList<YarnStringRow> rows)
    {
        ArgumentNullException.ThrowIfNull(baseRows);
        ArgumentNullException.ThrowIfNull(rows);

        // A file that names a line twice keeps the first
        var translations = new Dictionary<string, YarnStringRow>();
        foreach (var row in rows)
        {
            translations.TryAdd(row.Id, row);
        }

        var translated = 0;
        var toUpdate = 0;
        foreach (var line in baseRows)
        {
            if (!translations.TryGetValue(line.Id, out var row) || TextOf(row).Length == 0)
                continue;

            if (IsMarked(row) || (row.Lock.Length > 0 && row.Lock != line.Lock))
                toUpdate++;
            else
                translated++;
        }

        var known = baseRows.Select(l => l.Id).ToHashSet();
        return new YarnStringsStatus(baseRows.Count, translated, toUpdate, translations.Keys.Count(id => !known.Contains(id)));
    }

    /// <summary>
    /// The translation of a row, without the mark of a translation to update, which is for the translator.
    /// </summary>
    public static string TextOf(YarnStringRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return IsMarked(row) ? row.Text[YarnStringsCsv.NeedsUpdate.Length..] : row.Text;
    }

    private static bool IsMarked(YarnStringRow row) => row.Text.StartsWith(YarnStringsCsv.NeedsUpdate, StringComparison.Ordinal);
}
