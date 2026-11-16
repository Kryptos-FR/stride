using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Assets.Localisation;
using ColorRise.Skein.Editor.Services;
using ColorRise.Skein.Editor.ViewModels;

namespace ColorRise.Skein.Editor.Player;

/// <summary>
/// What a run of the player plays: the scripts of a project as one analysis compiled them, and where its translations are.
/// </summary>
internal sealed class PlayerBuild
{
    private readonly Dictionary<string, string> stringsFiles = [];
    private readonly Dictionary<string, string> titles = [];
    private readonly Dictionary<string, string> returnTypes = [];

    /// <param name="analysis">An analysis of the project without an error.</param>
    /// <param name="project">The project, for the strings files of its languages.</param>
    public PlayerBuild(YarnProjectAnalysis analysis, YarnProjectViewModel project)
    {
        Analysis = analysis;
        Content = analysis.Build.Project ?? throw new ArgumentException("The scripts have errors.", nameof(analysis));
        Program = Content.GetProgram();
        foreach (var localisation in project.Asset.Localisations)
        {
            if (localisation.Strings is { } file && localisation.Language.Length > 0)
                stringsFiles.TryAdd(localisation.Language, YarnLocalisationService.Absolute(project, file).ToOSPath());
        }

        foreach (var node in Compilation.Nodes)
        {
            titles.TryAdd(node.UniqueTitle, node.Title);
        }

        Variables = [.. Compilation.Variables.Where(v => !v.IsInternal)];
        foreach (var function in Compilation.Functions)
        {
            returnTypes.TryAdd(function.Name, function.ReturnType);
        }
    }

    /// <summary>
    /// The analysis the build is of.
    /// </summary>
    public YarnProjectAnalysis Analysis { get; }

    /// <summary>
    /// What the compiler said of the scripts.
    /// </summary>
    public YarnCompilation Compilation => Analysis.Compilation;

    /// <summary>
    /// The project as a game loads it.
    /// </summary>
    public YarnProject Content { get; }

    /// <summary>
    /// The variables the scripts declare or use, Yarn Spinner's own aside.
    /// </summary>
    public IReadOnlyList<YarnVariableData> Variables { get; }

    /// <summary>
    /// The compiled program.
    /// </summary>
    public Yarn.Program Program { get; }

    /// <summary>
    /// The lines of a language: the base language's from the scripts, another's from its strings file as it is on the
    /// disk now. Null for a language without a file, or whose file cannot be read: its lines show in the base language.
    /// </summary>
    public YarnStringTable? TableOf(string language)
    {
        if (language == Content.BaseLanguage)
            return Analysis.Build.BaseStrings;

        if (!stringsFiles.TryGetValue(language, out var path))
            return null;

        try
        {
            var rows = YarnStringsCsv.Read(File.ReadAllText(path));
            return YarnProjectBuilder.TableOf(language, YarnStringsCsv.RowsOf(Compilation, Content.BaseLanguage), rows);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// The title of a node from its name in the program: the nodes of a group share a title.
    /// </summary>
    public string TitleOf(string node) => titles.GetValueOrDefault(node, node);

    /// <summary>
    /// The type a function gives, as the compiler knows or guessed it: <c>bool</c>, <c>number</c> or <c>string</c>.
    /// </summary>
    public string ReturnTypeOf(string function) => returnTypes.GetValueOrDefault(function, YarnFunctionDeclaration.Bool);

    /// <summary>
    /// The expression of a smart variable as its declaration writes it, or null when the scripts do not show it.
    /// </summary>
    public string? ExpressionOf(YarnVariableData variable)
    {
        if (!variable.IsSmart || variable.File is null)
            return null;

        foreach (var (name, text) in Analysis.Texts)
        {
            if (name != variable.File)
                continue;

            // <<declare $rich = $gold > 10>>
            var lines = text.Split('\n');
            if (variable.Line < 1 || variable.Line > lines.Length)
                return null;

            var declaration = lines[variable.Line - 1];
            var equals = declaration.IndexOf('=', StringComparison.Ordinal);
            var close = declaration.LastIndexOf(">>", StringComparison.Ordinal);
            return equals >= 0 && close > equals ? declaration[(equals + 1)..close].Trim() : null;
        }

        return null;
    }

    /// <summary>
    /// The declaration of a variable of the scripts, or null.
    /// </summary>
    public YarnVariableData? VariableOf(string name) => Compilation.Variables.FirstOrDefault(v => v.Name == name);
}
