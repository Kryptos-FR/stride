using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Editor.ViewModels;

namespace ColorRise.Skein.Editor.Services;

/// <summary>
/// What the compiler says of the scripts of one Yarn project as the session has them, unsaved changes included.
/// </summary>
public sealed record YarnProjectAnalysis
{
    /// <summary>
    /// The project; null for a script that no project includes, compiled alone.
    /// </summary>
    public YarnProjectViewModel? Project { get; init; }

    /// <summary>
    /// The scripts that were compiled, each with the file name the compiler knows it by.
    /// </summary>
    public required IReadOnlyList<(string FileName, YarnScriptViewModel Script)> Scripts { get; init; }

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
