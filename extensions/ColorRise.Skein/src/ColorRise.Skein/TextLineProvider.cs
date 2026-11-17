using Stride.Core.Diagnostics;
using Yarn.Markup;

namespace ColorRise.Skein;

/// <summary>
/// Serves the lines of a project from its string tables: the table of the current language, loaded when first asked, and
/// the base language's for what the current one lacks.
/// </summary>
public sealed class TextLineProvider : ILineProvider
{
    private readonly Func<string, YarnStringTable?> load;
    private readonly string baseLanguage;
    private readonly ILogger? log;
    private readonly Dictionary<string, YarnStringTable?> tables = [];
    private readonly HashSet<string> reported = [];
    private string language;

    /// <summary>
    /// Makes a provider.
    /// </summary>
    /// <param name="load">Gives the string table of a language, or null when there is none. Asked once per language.</param>
    /// <param name="baseLanguage">The language the scripts are written in, which has every line.</param>
    /// <param name="log">Where a missing translation is told, once per line and language.</param>
    public TextLineProvider(Func<string, YarnStringTable?> load, string baseLanguage, ILogger? log = null)
    {
        ArgumentNullException.ThrowIfNull(load);
        ArgumentException.ThrowIfNullOrEmpty(baseLanguage);
        this.load = load;
        this.baseLanguage = baseLanguage;
        this.log = log;
        language = baseLanguage;
        Markup.RegisterMarkerProcessor("select", new BuiltInMarkupReplacer());
        Markup.RegisterMarkerProcessor("plural", Plural);
        Markup.RegisterMarkerProcessor("ordinal", Plural);
    }

    private static PluralMarkup Plural { get; } = new();

    /// <inheritdoc />
    public string Language
    {
        get => language;
        set
        {
            ArgumentException.ThrowIfNullOrEmpty(value);
            language = value;
        }
    }

    /// <summary>
    /// The hashtags of the lines, by line id: <see cref="YarnProject.LineMetadata"/>.
    /// </summary>
    public IReadOnlyDictionary<string, List<string>>? LineMetadata { get; init; }

    /// <summary>
    /// The lines that take the text of another: <see cref="YarnProject.ShadowLines"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string>? ShadowLines { get; init; }

    /// <summary>
    /// Gives the voice table of a language, or null when it has none; asked once per language. Without it, no line has a
    /// <see cref="LocalizedLine.VoiceOver"/>.
    /// </summary>
    public Func<string, YarnVoiceTable?>? VoiceTables { get; init; }

    private Dictionary<string, YarnVoiceTable?> Voices { get; } = [];

    /// <summary>
    /// Yarn Spinner's markup parser, with <c>select</c>, <c>plural</c> and <c>ordinal</c> registered: a plural or an ordinal
    /// shows its <c>other</c> text for a case it does not give. A game registers its own replacement markers here.
    /// </summary>
    public LineParser Markup { get; } = new();

    /// <inheritdoc />
    public LocalizedLine GetLine(string lineId, IReadOnlyList<string> substitutions)
    {
        ArgumentNullException.ThrowIfNull(lineId);
        ArgumentNullException.ThrowIfNull(substitutions);

        var textId = ShadowLines is not null && ShadowLines.TryGetValue(lineId, out var source) ? source : lineId;
        var (text, textLanguage) = Find(textId);
        var raw = LineParser.ExpandSubstitutions(text, [.. substitutions]);

        MarkupParseResult parsed;
        string? problem;
        try
        {
            (parsed, var diagnostics) = Markup.ParseStringAndIncludeMarkupDiagnostics(raw, textLanguage, addImplicitCharacterAttribute: true);
            problem = diagnostics.Count > 0 ? string.Join("; ", diagnostics.Select(d => d.Message)) : null;
        }
        catch (Exception exception) when (exception is MarkupParseException or ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            parsed = default;
            problem = exception.Message;
        }

        if (problem is not null)
        {
            // What the parser gives of markup it cannot read is not text to show
            if (reported.Add($"markup|{textLanguage}|{textId}"))
                log?.Error($"The markup of the line '{textId}' in '{textLanguage}' cannot be read, the text is shown as written: {problem}");
            parsed = new MarkupParseResult(raw, []);
        }

        string? characterName = null;
        var withoutName = parsed;
        if (parsed.TryGetAttributeWithName(LineParser.CharacterAttribute, out var character))
        {
            // "Kye : ..." as French writes it: the space before the colon is not part of the name
            if (character.TryGetProperty(LineParser.CharacterAttributeNameProperty, out string? name))
                characterName = name.Trim();
            withoutName = parsed.DeleteRange(character);
        }

        return new LocalizedLine
        {
            Id = lineId,
            RawText = raw,
            Text = parsed,
            CharacterName = characterName,
            TextWithoutCharacterName = withoutName,
            Metadata = LineMetadata is not null && LineMetadata.TryGetValue(lineId, out var metadata) ? metadata : [],
            Language = textLanguage,
            VoiceOver = VoiceOf(lineId) ?? (textId != lineId ? VoiceOf(textId) : null),
        };
    }

    /// <inheritdoc />
    public string? GetVoiceOver(string lineId)
    {
        ArgumentNullException.ThrowIfNull(lineId);
        return VoiceOf(lineId) ?? (ShadowLines is not null && ShadowLines.TryGetValue(lineId, out var source) ? VoiceOf(source) : null);
    }

    // In the provider's language only: a line is not spoken in another language than the one asked
    private string? VoiceOf(string lineId)
    {
        if (VoiceTables is null)
            return null;

        if (!Voices.TryGetValue(language, out var table))
        {
            table = VoiceTables(language);
            Voices[language] = table;
        }

        return table is not null && table.Clips.TryGetValue(lineId, out var url) ? url : null;
    }

    private (string Text, string Language) Find(string lineId)
    {
        if (language != baseLanguage)
        {
            var table = TableOf(language);
            if (table is null)
            {
                if (reported.Add($"language|{language}"))
                    log?.Warning($"There is no string table for the language '{language}': the lines are shown in '{baseLanguage}'.");
            }
            else if (table.Strings.TryGetValue(lineId, out var translated))
            {
                return (translated, language);
            }
            else if (reported.Add($"line|{language}|{lineId}"))
            {
                log?.Warning($"The line '{lineId}' has no text in '{language}': it is shown in '{baseLanguage}'.");
            }
        }

        if (TableOf(baseLanguage) is { } baseTable && baseTable.Strings.TryGetValue(lineId, out var text))
            return (text, baseLanguage);

        if (reported.Add($"line|{baseLanguage}|{lineId}"))
            log?.Error($"The line '{lineId}' has no text: its id is shown in its place.");
        return (lineId, baseLanguage);
    }

    private YarnStringTable? TableOf(string tableLanguage)
    {
        if (!tables.TryGetValue(tableLanguage, out var table))
        {
            table = load(tableLanguage);
            tables[tableLanguage] = table;
        }

        return table;
    }
}
