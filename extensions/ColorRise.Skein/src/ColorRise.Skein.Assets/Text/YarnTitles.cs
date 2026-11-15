namespace ColorRise.Skein.Assets.Text;

/// <summary>
/// The rules of node titles.
/// </summary>
public static class YarnTitles
{
    /// <summary>
    /// Says why a title cannot be the title of a new node, or of a node being renamed.
    /// </summary>
    /// <param name="title">The title to check.</param>
    /// <param name="existing">The titles the project already has, without the one of the node being renamed.</param>
    /// <returns>Null when the title is fine; otherwise the reason, as a sentence for the user.</returns>
    public static string? Validate(string title, IEnumerable<string> existing)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(existing);

        if (title.Length == 0)
            return "A node needs a title.";

        // Yarn Spinner's identifiers: no space, no dot, no punctuation
        if (!(char.IsLetter(title[0]) || title[0] == '_') || title.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
            return $"'{title}' cannot be a title: a title is one word of letters, digits and '_' that does not start with a digit.";

        if (existing.Contains(title, StringComparer.Ordinal))
            return $"A node named '{title}' already exists.";

        return null;
    }

    /// <summary>
    /// Gives a title no node has yet: the wanted one, or the wanted one with the first number that makes it free.
    /// </summary>
    public static string Unique(string wanted, IEnumerable<string> existing)
    {
        ArgumentNullException.ThrowIfNull(wanted);
        ArgumentNullException.ThrowIfNull(existing);

        var taken = existing.ToHashSet(StringComparer.Ordinal);
        if (!taken.Contains(wanted))
            return wanted;

        for (var i = 2; ; i++)
        {
            var candidate = wanted + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!taken.Contains(candidate))
                return candidate;
        }
    }
}
