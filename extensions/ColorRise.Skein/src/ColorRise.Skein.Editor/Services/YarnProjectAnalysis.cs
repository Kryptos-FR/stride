using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Editor.ViewModels;

namespace ColorRise.Skein.Editor.Services;

/// <summary>
/// What the compiler says of the scripts of one Yarn project as the session has them, unsaved changes included.
/// </summary>
public sealed record YarnProjectAnalysis
{
    /// <summary>
    /// The title of the node a story usually starts at.
    /// </summary>
    public const string DefaultStartNode = "Start";

    /// <summary>
    /// The project; null for a script that no project includes, compiled alone.
    /// </summary>
    public YarnProjectViewModel? Project { get; init; }

    /// <summary>
    /// The scripts that were compiled, each with the file name the compiler knows it by.
    /// </summary>
    public required IReadOnlyList<(string FileName, YarnScriptViewModel Script)> Scripts { get; init; }

    /// <summary>
    /// The text of each script as it was compiled.
    /// </summary>
    public required IReadOnlyList<(string FileName, string Text)> Texts { get; init; }

    /// <summary>
    /// What the build of the game would make of the scripts.
    /// </summary>
    public required YarnProjectBuild Build { get; init; }

    /// <summary>
    /// The nodes of the last compilation without an error: the graph keeps its links while a script is being typed.
    /// </summary>
    public required IReadOnlyList<YarnNodeData> Nodes { get; init; }

    /// <summary>
    /// The commands and the functions of the game's code the compilation knew.
    /// </summary>
    public required GameActions Actions { get; init; }

    /// <summary>
    /// The colours of the characters in Game Studio.
    /// </summary>
    public YarnCharacterColors Characters { get; init; } = YarnCharacterColors.Default;

    /// <summary>
    /// The result of the compiler.
    /// </summary>
    public YarnCompilation Compilation => Build.Compilation;

    /// <summary>
    /// The errors, warnings and remarks of the compiler.
    /// </summary>
    public IReadOnlyList<YarnDiagnostic> Diagnostics => Build.Compilation.Diagnostics;

    /// <summary>
    /// Whether <see cref="Nodes"/> are of an earlier compilation, because this one has errors.
    /// </summary>
    public bool NodesAreOlder => !ReferenceEquals(Nodes, Build.Compilation.Nodes);

    /// <summary>
    /// The titles of the nodes a dialogue can start at, by name: a note of the writer is a node the story never runs.
    /// </summary>
    public IReadOnlyList<string> StartNodes => [.. Nodes
        .Where(n => !n.IsNote)
        .Select(n => n.Title)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// The titles of <see cref="StartNodes"/> that pass the filter of a <see cref="YarnNodeAttribute"/>: a node group
    /// passes when one of its nodes does.
    /// </summary>
    public IReadOnlyList<string> StartNodesFor(YarnNodeAttribute? attribute)
    {
        if (attribute is not { Filters: true })
            return StartNodes;

        var byTitle = string.Equals(attribute.FilterHeader, "title", StringComparison.OrdinalIgnoreCase);
        return [.. Nodes
            .Where(n => !n.IsNote && (byTitle
                ? attribute.Matches(n.Title)
                : n.Headers.Any(h => string.Equals(h.Key, attribute.FilterHeader, StringComparison.OrdinalIgnoreCase) && attribute.Matches(h.Value))))
            .Select(n => n.Title)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// The node a dialogue starts at when none is chosen, or null when there is none.
    /// </summary>
    /// <param name="script">A script, for its first node in the order of its text; null for the node
    /// <see cref="DefaultStartNode"/> of the project, else its first node by name.</param>
    public string? FirstNode(YarnScriptViewModel? script = null)
    {
        if (script is not null)
        {
            var file = FileNameOf(script);
            return Nodes.Where(n => n.File == file && !n.IsNote).MinBy(n => n.HeaderStartLine)?.Title;
        }

        var nodes = StartNodes;
        return nodes.Contains(DefaultStartNode) ? DefaultStartNode : nodes.FirstOrDefault();
    }

    /// <summary>
    /// The script of a file name of the compiler, or null.
    /// </summary>
    public YarnScriptViewModel? ScriptOf(string fileName)
    {
        foreach (var (name, script) in Scripts)
        {
            if (name == fileName)
                return script;
        }

        return null;
    }

    /// <summary>
    /// The text of a script as it was compiled, or null for a script that was not compiled.
    /// </summary>
    public string? TextOf(YarnScriptViewModel script)
    {
        var fileName = FileNameOf(script);
        foreach (var (name, text) in Texts)
        {
            if (name == fileName)
                return text;
        }

        return null;
    }

    /// <summary>
    /// The file name the compiler knows a script by, or null for a script that was not compiled.
    /// </summary>
    public string? FileNameOf(YarnScriptViewModel script)
    {
        foreach (var (name, compiled) in Scripts)
        {
            if (compiled == script)
                return name;
        }

        return null;
    }
}
