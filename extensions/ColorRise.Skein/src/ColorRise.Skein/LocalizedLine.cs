using Yarn.Markup;

namespace ColorRise.Skein;

/// <summary>
/// A line ready to show: the text of a language with the script's values in it and its markup read.
/// </summary>
public sealed class LocalizedLine
{
    /// <summary>
    /// The id of the line.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The text of the language with the script's values in it, markup unread.
    /// </summary>
    public required string RawText { get; init; }

    /// <summary>
    /// The text to show, speaker included, and the attributes of its markup with their places in that text.
    /// </summary>
    public required MarkupParseResult Text { get; init; }

    /// <summary>
    /// The name of who speaks (what is before the colon), or null.
    /// </summary>
    public string? CharacterName { get; init; }

    /// <summary>
    /// <see cref="Text"/> without the speaker's name, for a presenter that shows the name apart.
    /// </summary>
    public required MarkupParseResult TextWithoutCharacterName { get; init; }

    /// <summary>
    /// The hashtags of the line (<c>#shout</c>), without the <c>#</c>.
    /// </summary>
    public IReadOnlyList<string> Metadata { get; init; } = [];

    /// <summary>
    /// The language the text is in: the provider's, or the base language when the provider's lacks the line.
    /// </summary>
    public string Language { get; init; } = "";

    /// <inheritdoc />
    public override string ToString() => Text.Text;
}
