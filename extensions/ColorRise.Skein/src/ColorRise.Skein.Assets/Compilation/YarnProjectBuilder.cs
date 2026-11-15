using ColorRise.Skein.Assets.Localisation;
using Google.Protobuf;

namespace ColorRise.Skein.Assets.Compilation;

/// <summary>
/// What building a Yarn project gives: the compiler's result, and the content of the game when the scripts have no error.
/// </summary>
public sealed record YarnProjectBuild
{
    /// <summary>
    /// What the compiler said of the scripts, with the severities of the project.
    /// </summary>
    public required YarnCompilation Compilation { get; init; }

    /// <summary>
    /// What the build says of the project itself, as warnings: no script, lines without an id in a translated project.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// The compiled project; null when the scripts have an error.
    /// </summary>
    public YarnProject? Project { get; init; }

    /// <summary>
    /// The lines of the project in its base language; null when the scripts have an error.
    /// </summary>
    public YarnStringTable? BaseStrings { get; init; }

    /// <summary>
    /// The lines of the project in each of its other languages that has a strings file; empty when the scripts have an error.
    /// </summary>
    public IReadOnlyList<YarnStringTable> Translations { get; init; } = [];

    /// <summary>
    /// Whether the build failed.
    /// </summary>
    public bool HasErrors => Project is null;
}

/// <summary>
/// Builds the content of a Yarn project from its scripts: the same for the game's build and for Game Studio.
/// </summary>
public static class YarnProjectBuilder
{
    private const int NamedLines = 3;

    /// <summary>
    /// Compiles the scripts of a project and makes its content.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="projectName">The name of the project in the messages about it.</param>
    /// <param name="scripts">The scripts the project includes, with the names the messages give them.</param>
    /// <param name="functions">The functions of the game.</param>
    /// <param name="translations">The rows of the strings file of each other language of the project, by language; a language without a strings file is not in it.</param>
    /// <param name="cancellation">Stops the compilation.</param>
    public static YarnProjectBuild Build(
        YarnProjectAsset project,
        string projectName,
        IReadOnlyList<(string FileName, string Text)> scripts,
        IReadOnlyList<YarnFunctionDeclaration> functions,
        IReadOnlyDictionary<string, IReadOnlyList<YarnStringRow>>? translations = null,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(scripts);
        ArgumentNullException.ThrowIfNull(functions);

        var compilation = YarnCompilerBridge.Compile(new YarnCompilationRequest
        {
            Files = scripts,
            Functions = functions,
            Severities = project.DiagnosticSeverities,
            Cancellation = cancellation,
        });

        var warnings = new List<string>();
        if (scripts.Count == 0)
            warnings.Add($"{projectName}: the project includes no script. Its source files are {(project.SourceFiles.Count == 0 ? "empty" : string.Join(", ", project.SourceFiles))}, from the folder of the project.");

        if (project.Localisations.Count > 0 && UntaggedLines(compilation) is { Count: > 0 } untagged)
        {
            var named = string.Join(", ", untagged.Take(NamedLines).Select(l => $"{l.File}({l.Line})"));
            var others = untagged.Count > NamedLines ? $" and {untagged.Count - NamedLines} more" : "";
            warnings.Add($"{projectName}: {untagged.Count} {(untagged.Count == 1 ? "line has" : "lines have")} no line id (#line:), so {(untagged.Count == 1 ? "it stays" : "they stay")} in {project.BaseLanguage} in every language: {named}{others}. Add line ids to the scripts of the project.");
        }

        // Without a script the compiler makes no program: the project is an empty one
        var program = compilation.Program ?? (scripts.Count == 0 ? new Yarn.Program() : null);
        if (compilation.HasErrors || program is null)
            return new YarnProjectBuild { Compilation = compilation, Warnings = warnings };

        var content = new YarnProject
        {
            Program = program.ToByteArray(),
            BaseLanguage = project.BaseLanguage,
            Saliency = project.Saliency,
        };
        content.Languages.Add(project.BaseLanguage);
        content.Languages.AddRange(project.Localisations.Select(l => l.Language).Where(l => l.Length > 0 && l != project.BaseLanguage).Distinct());
        foreach (var node in compilation.Nodes)
        {
            var info = new YarnNodeInfo { Name = node.UniqueTitle, Title = node.Title };
            info.Headers.AddRange(node.Headers.Select(h => new YarnNodeHeader(h.Key, h.Value)));
            info.Tags.AddRange(node.Tags);
            content.Nodes.Add(info);
        }

        var strings = new YarnStringTable { Language = project.BaseLanguage };
        foreach (var (id, line) in compilation.Lines.OrderBy(l => l.Key, StringComparer.Ordinal))
        {
            if (line.ShadowOf is { } source)
                content.ShadowLines[id] = source;
            else
                strings.Strings[id] = line.Text;
            if (line.Metadata.Count > 0)
                content.LineMetadata[id] = [.. line.Metadata];
        }

        var tables = new List<YarnStringTable>();
        var baseRows = YarnStringsCsv.RowsOf(compilation, project.BaseLanguage);
        foreach (var language in content.Languages.Skip(1))
        {
            if (translations is null || !translations.TryGetValue(language, out var rows))
            {
                warnings.Add($"{projectName}: the language '{language}' has no strings file, so its lines show in {project.BaseLanguage}. Export the strings of the project and set the file in its localisations.");
                continue;
            }

            tables.Add(Translate(projectName, project.BaseLanguage, language, baseRows, rows, warnings));
        }

        return new YarnProjectBuild { Compilation = compilation, Warnings = warnings, Project = content, BaseStrings = strings, Translations = tables };
    }

    /// <summary>
    /// The lines of a language from the rows of its strings file, with one warning for what the file lacks.
    /// </summary>
    private static YarnStringTable Translate(string projectName, string baseLanguage, string language, IReadOnlyList<YarnStringRow> source, IReadOnlyList<YarnStringRow> rows, List<string> warnings)
    {
        var table = new YarnStringTable { Language = language };
        var translated = new Dictionary<string, YarnStringRow>();
        foreach (var row in rows)
        {
            translated.TryAdd(row.Id, row);
        }

        var missing = 0;
        var outdated = 0;
        foreach (var line in source)
        {
            if (!translated.TryGetValue(line.Id, out var row))
            {
                missing++;
                continue;
            }

            // The mark of a translation to update is for the translator, not for the player
            var marked = row.Text.StartsWith(YarnStringsCsv.NeedsUpdate, StringComparison.Ordinal);
            var text = marked ? row.Text[YarnStringsCsv.NeedsUpdate.Length..] : row.Text;
            if (text.Length == 0)
            {
                missing++;
                continue;
            }

            if (marked || (row.Lock.Length > 0 && row.Lock != line.Lock))
                outdated++;
            table.Strings[line.Id] = text;
        }

        var known = source.Select(l => l.Id).ToHashSet();
        var dead = translated.Keys.Count(id => !known.Contains(id));
        var notes = new List<string>();
        if (missing > 0)
            notes.Add($"{Count(missing, "line has", "lines have")} no translation and {(missing == 1 ? "shows" : "show")} in {baseLanguage}");
        if (outdated > 0)
            notes.Add($"{Count(outdated, "translation was", "translations were")} made from a text that changed since");
        if (dead > 0)
            notes.Add($"{Count(dead, "row is", "rows are")} of lines the scripts no longer have");
        if (notes.Count > 0)
            warnings.Add($"{projectName}: in '{language}', {string.Join(", ", notes)}. Update the strings files of the project.");

        return table;
    }

    private static string Count(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";

    // A shadow line shows the text of the line it repeats: it needs no id of its own
    private static List<YarnLineData> UntaggedLines(YarnCompilation compilation)
        => [.. compilation.Lines.Values.Where(l => l.IsImplicitId && l.ShadowOf is null).OrderBy(l => l.File, StringComparer.Ordinal).ThenBy(l => l.Line)];
}
