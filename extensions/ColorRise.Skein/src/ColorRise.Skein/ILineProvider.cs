namespace ColorRise.Skein;

/// <summary>
/// Turns a line id into the line of the current language. <see cref="TextLineProvider"/> serves the string tables of a
/// project; a game gives its own to add voice-over or to take the text from elsewhere.
/// </summary>
public interface ILineProvider
{
    /// <summary>
    /// The language the lines are asked in (<c>en</c>, <c>fr</c>, <c>pt-BR</c>).
    /// </summary>
    string Language { get; set; }

    /// <summary>
    /// Gives a line ready to show.
    /// </summary>
    /// <param name="lineId">The id of the line.</param>
    /// <param name="substitutions">The values the script puts in the line, in the order of its <c>{0}</c> placeholders.</param>
    LocalizedLine GetLine(string lineId, IReadOnlyList<string> substitutions);

    /// <summary>
    /// The URL of the voice-over clip of a line in the current language, before the line runs: a presenter loads it while
    /// the dialogue enters the node. Null when the line has none, and from a provider without voice-over.
    /// </summary>
    string? GetVoiceOver(string lineId) => null;
}
