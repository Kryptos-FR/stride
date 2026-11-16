using System.Text;
using System.Text.RegularExpressions;
using Yarn.Compiler;

namespace ColorRise.Skein.Assets.Localisation;

/// <summary>
/// Yarn Spinner's descriptive line ids (<c>line:Node_0100_Speaker</c>), made always valid and new: a "speaker" that is
/// narration before a colon is left out, and the lines of a node that another script has lines of too (the nodes of one
/// node group written in several scripts) get the name of their script after the node's
/// (<c>line:Node_Script_0100_Speaker</c>). An id that would still repeat one takes a number (<c>_2</c>).
/// </summary>
internal sealed partial class DescriptiveLineIds : ILineTagGenerator
{
    private const string Prefix = "line:";

    /// <summary>
    /// The most words a speaker's name has, such as "Old Tom".
    /// </summary>
    private const int MaxNameWords = 3;

    private readonly DescriptiveLineTagGenerator generator = new();
    private readonly HashSet<string> taken = new(StringComparer.Ordinal);
    private readonly HashSet<string> scriptNamed = new(StringComparer.Ordinal);
    private readonly string script;

    /// <param name="fileName">The name of the script, whose name tells apart ids that repeat.</param>
    public DescriptiveLineIds(string fileName)
    {
        script = Clean(Path.GetFileNameWithoutExtension(fileName));
    }

    /// <inheritdoc />
    public void PrepareForLines(Dictionary<string, List<ILineTagGenerator.LineTagContext>> lines, HashSet<string> excluded)
    {
        // Yarn Spinner's generator refuses an id it is told to exclude: it only learns the ids of this script, and an id
        // of another script is told apart here
        var own = lines.Values.SelectMany(c => c).Select(c => c.LineID).Where(id => !string.IsNullOrEmpty(id)).ToHashSet(StringComparer.Ordinal);
        generator.PrepareForLines(lines, [.. own]);
        taken.UnionWith(excluded);
        if (script.Length == 0)
            return;

        // A node that another script has lines of too (a node group, or two nodes of one title) takes the script's name
        // in all its ids, as does a node whose ids already carry it
        foreach (var (node, contexts) in lines)
        {
            var nodeIds = $"{Prefix}{node}_";
            if (contexts.Any(c => c.LineID?.StartsWith($"{nodeIds}{script}_", StringComparison.Ordinal) == true)
                || excluded.Any(id => id.StartsWith(nodeIds, StringComparison.Ordinal) && !own.Contains(id)))
                scriptNamed.Add(node);
        }
    }

    /// <inheritdoc />
    public string GenerateLineTag(string nodeName, int lineIndex)
    {
        var id = generator.GenerateLineTag(nodeName, lineIndex);
        var (number, speaker) = Parts(id, nodeName);
        if (number is null)
            return Take(Clean(id));

        var plain = Join(nodeName, number, speaker);
        if (script.Length > 0 && (scriptNamed.Contains(nodeName) || taken.Contains(plain)))
            plain = Join($"{nodeName}_{script}", number, speaker);

        return Take(plain);
    }

    /// <summary>
    /// The number of an id the generator made for a node (with its generation, such as <c>0150_g1</c>) and the name of
    /// its speaker, or null when the id is not of that shape.
    /// </summary>
    private static (string? Number, string? Speaker) Parts(string id, string nodeName)
    {
        var head = $"{Prefix}{nodeName}_";
        if (!id.StartsWith(head, StringComparison.Ordinal))
            return (null, null);

        var match = Shape().Match(id[head.Length..]);
        if (!match.Success)
            return (null, null);

        return (match.Groups["number"].Value, match.Groups["speaker"].Success ? Name(match.Groups["speaker"].Value) : null);
    }

    /// <summary>
    /// A speaker's name in an id: the words before the line's colon, joined, when they read as a name; else nothing, for
    /// narration that has a colon ("A mark in the wall: a circle").
    /// </summary>
    private static string? Name(string speaker)
    {
        var words = speaker.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length is 0 or > MaxNameWords)
            return null;

        foreach (var word in words)
        {
            if (!char.IsUpper(word[0]) || !word.All(c => char.IsLetterOrDigit(c) || c is '_' or '-'))
                return null;
        }

        return string.Concat(words);
    }

    private static string Join(string node, string number, string? speaker)
        => speaker is null ? $"{Prefix}{node}_{number}" : $"{Prefix}{node}_{number}_{speaker}";

    /// <summary>
    /// The id, or the first of <c>id_2</c>, <c>id_3</c>… that no line has yet.
    /// </summary>
    private string Take(string id)
    {
        var free = id;
        for (var n = 2; taken.Contains(free); n++)
        {
            free = $"{id}_{n}";
        }

        taken.Add(free);
        return free;
    }

    /// <summary>
    /// Keeps the letters, the digits, '_', '-' and the colon of <c>line:</c>: what a hashtag can hold without a doubt.
    /// </summary>
    private static string Clean(string text)
    {
        var clean = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c) || c is '_' or '-' or ':')
                clean.Append(c);
        }

        return clean.ToString();
    }

    [GeneratedRegex(@"^(?<number>[0-9]+(_g[0-9]+)?)(_(?<speaker>.*))?$", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();
}
