using ColorRise.Skein.Assets.Compilation;
using Yarn.Compiler;

namespace ColorRise.Skein.Assets.Localisation;

/// <summary>
/// A script after "Add line ids".
/// </summary>
/// <param name="Text">The text of the script, with a <c>#line:</c> tag on each line that had none.</param>
/// <param name="AddedIds">The ids that were added.</param>
/// <param name="Problems">Why lines were left without an id.</param>
public sealed record YarnTaggedScript(string Text, IReadOnlyList<string> AddedIds, IReadOnlyList<string> Problems);

/// <summary>
/// Adds line ids to scripts with Yarn Spinner's own tagger, so that the ids are the ones its tools would add.
/// </summary>
public static class YarnLineTagger
{
    /// <summary>
    /// Adds a <c>#line:</c> tag to each line and option of a script that has none; nothing else of the text changes.
    /// </summary>
    /// <param name="fileName">The name of the script in the messages.</param>
    /// <param name="text">The text of the script.</param>
    /// <param name="existingIds">The ids the project already has, which the new ones must differ from; the new ones are added to it.</param>
    /// <param name="style">How the new ids are named.</param>
    public static YarnTaggedScript AddLineIds(string fileName, string text, ISet<string> existingIds, YarnLineIdStyle style)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(existingIds);

        // The tagger leaves a script it cannot parse as it is, without a word
        if (Utility.ParseSourceText(text, fileName).Diagnostics.Any(d => d.Severity == Diagnostic.DiagnosticSeverity.Error))
            return new YarnTaggedScript(text, [], [$"{fileName}: the script has errors, so no line id was added to it. Fix the errors first."]);

        var (tagged, ids, failures) = Tag(fileName, text, existingIds, style == YarnLineIdStyle.Descriptive ? new DescriptiveLineTagGenerator() : new RandomLineTagGenerator());
        var problems = new List<string>();
        if (failures.Count > 0 && style == YarnLineIdStyle.Descriptive)
        {
            // A descriptive id that the project already has (two nodes of one title) makes the tagger write an error in
            // the script: random ids never meet that, so the script gets those instead
            (tagged, ids, failures) = Tag(fileName, text, existingIds, new RandomLineTagGenerator());
            problems.Add($"{fileName}: descriptive line ids would repeat ids the project already has, so the script got random ones.");
        }

        if (failures.Count > 0)
            return new YarnTaggedScript(text, [], [.. failures.Select(f => f.Message ?? $"{fileName}: a line could not be given an id.")]);

        var added = ids.Where(id => !existingIds.Contains(id)).Distinct().ToList();
        existingIds.UnionWith(added);

        // The tagger leaves a space after each tag it adds, at the end of the line
        foreach (var id in added)
        {
            tagged = tagged.Replace($"#{id} \r", $"#{id}\r").Replace($"#{id} \n", $"#{id}\n");
            if (tagged.EndsWith($"#{id} ", StringComparison.Ordinal))
                tagged = tagged[..^1];
        }

        return new YarnTaggedScript(tagged, added, problems);
    }

    private static (string Text, ICollection<string> Ids, List<ILineTagGenerator.LineTaggingException> Failures) Tag(string fileName, string text, ISet<string> existingIds, ILineTagGenerator generator)
        => Utility.TagLines(new CompilationJob.File { FileName = fileName, Source = text }, [.. existingIds], generator);

    /// <summary>
    /// The line ids the scripts of a compilation give by hand.
    /// </summary>
    public static HashSet<string> ExistingIds(YarnCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        return [.. compilation.Lines.Where(l => !l.Value.IsImplicitId).Select(l => l.Key)];
    }
}
