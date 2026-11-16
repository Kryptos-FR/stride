using ColorRise.Skein.Assets.Text;

namespace ColorRise.Skein.Editor.Nodes;

/// <summary>
/// Reads the headers of a node of a script into a <see cref="YarnNodeHeaders"/>, and writes what the property grid changed
/// in one back as edits of the text.
/// </summary>
public static class YarnNodeHeadersText
{
    // The headers that are not free: the ones with a member of their own, and the place, which the graph writes
    private static readonly HashSet<string> OwnHeaders = new(StringComparer.Ordinal) { "title", "tags", "color", "cluster", "when", "position" };

    /// <summary>
    /// The headers of a node, as the text has them.
    /// </summary>
    public static YarnNodeHeaders Read(YarnNodeSpan node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var headers = new YarnNodeHeaders
        {
            Title = node.Title,
            Color = NullIfEmpty(node.Header("color")),
            Cluster = NullIfEmpty(node.Header("cluster")),
        };
        headers.Tags.AddRange(Words(node.Header("tags")));
        headers.When.AddRange(node.Headers.Where(h => h.Key == "when").Select(h => h.Value));
        headers.Others.AddRange(node.Headers.Where(h => !OwnHeaders.Contains(h.Key)).Select(h => new YarnHeader { Key = h.Key, Value = h.Value }));
        return headers;
    }

    /// <summary>
    /// Whether headers say what the text of their node says. What the property grid holds before it can be written does
    /// not count: an item just added, still empty.
    /// </summary>
    public static bool Matches(YarnNodeHeaders headers, YarnNodeSpan node)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var text = Read(node);
        return headers.Title == text.Title
               && TagsOf(headers).SequenceEqual(text.Tags)
               && NullIfEmpty(headers.Color) == text.Color
               && NullIfEmpty(headers.Cluster) == text.Cluster
               && WhenOf(headers).SequenceEqual(text.When)
               && OthersOf(headers).SequenceEqual(text.Others.Select(o => (o.Key, o.Value)));
    }

    /// <summary>
    /// The edits that make the headers of a node what <paramref name="headers"/> says, but for the title, which is a
    /// rename.
    /// </summary>
    /// <exception cref="ArgumentException">A name or a value cannot be written as a header.</exception>
    public static IReadOnlyList<YarnTextEdit> Write(YarnDocument document, YarnNodeSpan node, YarnNodeHeaders headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var text = Read(node);
        var edits = new List<YarnTextEdit>();

        var tags = TagsOf(headers);
        if (!tags.SequenceEqual(text.Tags))
        {
            edits.AddRange(tags.Count == 0 ? YarnTextPatcher.RemoveHeader(document, node, "tags") : YarnTextPatcher.SetHeader(document, node, "tags", string.Join(' ', tags)));
        }

        Set("color", NullIfEmpty(headers.Color), text.Color);
        Set("cluster", NullIfEmpty(headers.Cluster), text.Cluster);

        var when = WhenOf(headers);
        if (!when.SequenceEqual(text.When))
        {
            edits.AddRange(YarnTextPatcher.SetHeaders(document, node, "when", when));
        }

        var others = OthersOf(headers);
        if (others.Find(o => OwnHeaders.Contains(o.Key)) is { Key: not null } own)
            throw new ArgumentException($"'{own.Key}' is not a free header: it has a place of its own.");

        foreach (var key in text.Others.Select(o => o.Key).Distinct(StringComparer.Ordinal))
        {
            if (!others.Exists(o => o.Key == key))
            {
                edits.AddRange(YarnTextPatcher.RemoveHeader(document, node, key));
            }
        }

        foreach (var key in others.Select(o => o.Key).Distinct(StringComparer.Ordinal))
        {
            var values = others.Where(o => o.Key == key).Select(o => o.Value).ToList();
            if (!values.SequenceEqual(text.Others.Where(o => o.Key == key).Select(o => o.Value)))
            {
                edits.AddRange(YarnTextPatcher.SetHeaders(document, node, key, values));
            }
        }

        // In the order of the text, as an operation of the patcher gives its edits
        return [.. edits.OrderBy(e => e.Offset)];

        void Set(string key, string? value, string? current)
        {
            if (value != current)
            {
                edits.AddRange(value is null ? YarnTextPatcher.RemoveHeader(document, node, key) : YarnTextPatcher.SetHeader(document, node, key, value));
            }
        }
    }

    private static List<string> TagsOf(YarnNodeHeaders headers) => [.. headers.Tags.SelectMany(Words)];

    private static List<string> WhenOf(YarnNodeHeaders headers) => [.. headers.When.Select(w => w?.Trim() ?? "").Where(w => w.Length > 0)];

    private static List<(string Key, string Value)> OthersOf(YarnNodeHeaders headers)
        => [.. headers.Others.Where(o => o is not null).Select(o => (Key: o.Key?.Trim() ?? "", Value: o.Value?.Trim() ?? "")).Where(o => o.Key.Length > 0)];

    private static string[] Words(string? text) => (text ?? "").Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

    private static string? NullIfEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
