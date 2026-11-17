using System.Text;
using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Assets.Localisation;
using Yarn.Markup;

namespace ColorRise.Skein.Assets.Voice;

/// <summary>
/// A line of a recording script: what an actor records, and the file the recording goes in.
/// </summary>
/// <param name="File">The name of the file to record, which is the name of the clip in the voice-over folder.</param>
/// <param name="Id">The id of the line.</param>
/// <param name="Character">Who says it; empty for a line without a speaker.</param>
/// <param name="Text">What is said, without the speaker's name.</param>
/// <param name="Node">The node of the line.</param>
/// <param name="Previous">The line before it in its node, speaker included, for context; empty for the first.</param>
/// <param name="Tags">The hashtags of the line (<c>#angry</c>), which tell how it is said.</param>
/// <param name="Recorded">Whether the voice-over folder has its clip already.</param>
public sealed record YarnRecordingRow(string File, string Id, string Character, string Text, string Node, string Previous, string Tags, bool Recorded);

/// <summary>
/// The recording script of a language of a Yarn project: one row per line, for the actors and the studio.
/// </summary>
/// <remarks>
/// It is close to the recording spreadsheet of Yarn Spinner's VS Code extension, with the file name each clip takes.
/// </remarks>
public static class YarnRecordingScript
{
    /// <summary>
    /// The extension the file names of the script have.
    /// </summary>
    public const string FileExtension = ".wav";

    private static readonly string[] Columns = ["file", "id", "character", "text", "node", "previous", "tags", "recorded"];

    /// <summary>
    /// The rows of a language, in the order of the scripts: every line that has an id of its own and a text in the language,
    /// out of the notes of the writer (<c>style: note</c>), which the story never runs.
    /// </summary>
    /// <param name="compilation">The scripts of the project.</param>
    /// <param name="language">The language of the script.</param>
    /// <param name="translation">The text of each line in the language, or null for the base language: a line without one is left out.</param>
    /// <param name="clips">The clips of the language, by name (<see cref="YarnVoiceFolder.Clips"/>).</param>
    public static IReadOnlyList<YarnRecordingRow> RowsOf(YarnCompilation compilation, string language, IReadOnlyDictionary<string, string>? translation, IReadOnlyDictionary<string, string> clips)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(clips);

        var rows = new List<YarnRecordingRow>();
        var parser = new LineParser();
        var notes = compilation.Nodes.Where(n => n.IsNote).SelectMany(n => (string[])[n.Title, n.UniqueTitle]).ToHashSet(StringComparer.Ordinal);
        (string File, string Node, string Text)? previous = null;
        foreach (var source in YarnStringsCsv.RowsOf(compilation, language).Where(r => !notes.Contains(r.Node)))
        {
            var said = translation is null ? source.Text : translation.GetValueOrDefault(source.Id);
            var context = previous is { } before && before.File == source.File && before.Node == source.Node ? before.Text : "";
            if (said is null)
            {
                // The context of the next line is still this one, in the base language
                previous = (source.File, source.Node, Shown(Split(parser, source.Text, language)));
                continue;
            }

            var (character, text) = Split(parser, said, language);
            var name = YarnVoiceClips.NameOf(source.Id);
            var tags = YarnStringsCsv.TagsOf(source.Id, compilation.Lines[source.Id].Metadata);
            rows.Add(new YarnRecordingRow(
                name + FileExtension,
                source.Id,
                character,
                text,
                source.Node,
                context,
                string.Join(' ', tags.Select(t => "#" + t)),
                clips.ContainsKey(name)));
            previous = (source.File, source.Node, Shown((character, text)));
        }

        return rows;
    }

    /// <summary>
    /// Writes rows as a CSV file.
    /// </summary>
    public static string Write(IEnumerable<YarnRecordingRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var csv = new StringBuilder();
        YarnStringsCsv.WriteRecord(csv, Columns);
        foreach (var row in rows)
        {
            YarnStringsCsv.WriteRecord(csv, [row.File, row.Id, row.Character, row.Text, row.Node, row.Previous, row.Tags, row.Recorded ? "yes" : ""]);
        }

        return csv.ToString();
    }

    private static string Shown((string Character, string Text) line) => line.Character.Length > 0 ? $"{line.Character}: {line.Text}" : line.Text;

    // The speaker as Yarn Spinner reads it, and the text as the player sees it, markup read
    private static (string Character, string Text) Split(LineParser parser, string said, string language)
    {
        try
        {
            var parsed = parser.ParseString(said, language, addImplicitCharacterAttribute: true);
            if (parsed.TryGetAttributeWithName(LineParser.CharacterAttribute, out var attribute)
                && attribute.TryGetProperty(LineParser.CharacterAttributeNameProperty, out string? name))
            {
                return (name.Trim(), parsed.DeleteRange(attribute).Text.Trim());
            }

            return ("", parsed.Text.Trim());
        }
        catch (Exception exception) when (exception is MarkupParseException or ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            // Markup the parser cannot read without the line's values is shown as written
            var colon = said.IndexOf(':', StringComparison.Ordinal);
            return colon > 0 ? (said[..colon].Trim(), said[(colon + 1)..].Trim()) : ("", said.Trim());
        }
    }
}
