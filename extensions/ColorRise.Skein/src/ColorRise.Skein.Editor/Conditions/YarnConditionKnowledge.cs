using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Editor.Services;

namespace ColorRise.Skein.Editor.Conditions;

/// <summary>
/// What the builders of conditions and <c>&lt;&lt;set&gt;&gt;</c> commands offer: the variables, the functions and the
/// nodes of a Yarn project, with their types.
/// </summary>
public sealed class YarnConditionKnowledge
{
    // The functions of every dialogue whose first argument is the title of a node
    private static readonly HashSet<string> NodeFunctions = ["visited", "visited_count", "has_any_content"];

    /// <summary>
    /// Makes the knowledge of a project.
    /// </summary>
    /// <param name="variables">The variables of the scripts; the compiler's own are left out.</param>
    /// <param name="functions">The functions the scripts can call.</param>
    /// <param name="nodeNames">The titles of the nodes.</param>
    public YarnConditionKnowledge(IEnumerable<YarnVariableData> variables, IEnumerable<YarnFunctionDeclaration> functions, IEnumerable<string> nodeNames)
    {
        Variables = [.. variables.Where(v => !v.IsInternal).DistinctBy(v => v.Name).OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)];
        Functions = [.. functions.DistinctBy(f => f.Name).OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)];
        NodeNames = [.. nodeNames.Distinct().Order(StringComparer.OrdinalIgnoreCase)];
        Subjects = [.. Variables.Select(v => v.Name).Concat(Functions.Select(f => f.Name))];
    }

    /// <summary>
    /// Knows nothing: the builders still read and write what is typed.
    /// </summary>
    public static YarnConditionKnowledge Empty { get; } = new([], [], []);

    /// <summary>
    /// The variables, by name.
    /// </summary>
    public IReadOnlyList<YarnVariableData> Variables { get; }

    /// <summary>
    /// The functions, by name.
    /// </summary>
    public IReadOnlyList<YarnFunctionDeclaration> Functions { get; }

    /// <summary>
    /// The titles of the nodes.
    /// </summary>
    public IReadOnlyList<string> NodeNames { get; }

    /// <summary>
    /// What a term can be about: the variables, then the functions.
    /// </summary>
    public IReadOnlyList<string> Subjects { get; }

    /// <summary>
    /// The knowledge of the project of an analysis, or nothing without one.
    /// </summary>
    public static YarnConditionKnowledge From(YarnProjectAnalysis? analysis)
        => analysis is null ? Empty : new YarnConditionKnowledge(analysis.Compilation.Variables, analysis.Compilation.Functions, analysis.Nodes.Select(n => n.Title));

    /// <summary>
    /// The variable of a name, with its <c>$</c>, or null.
    /// </summary>
    public YarnVariableData? Variable(string? name) => Variables.FirstOrDefault(v => v.Name == name);

    /// <summary>
    /// The function of a name, or null.
    /// </summary>
    public YarnFunctionDeclaration? Function(string? name) => Functions.FirstOrDefault(f => f.Name == name);

    /// <summary>
    /// The type of a variable, or what a function returns: <c>number</c>, <c>string</c>, <c>bool</c> or an enum; null when
    /// it is not known.
    /// </summary>
    public string? TypeOf(string? subject) => Variable(subject)?.Type ?? Function(subject)?.ReturnType;

    /// <summary>
    /// The cases of an enum, as a script writes them: <c>Act.Wreck</c>.
    /// </summary>
    public IReadOnlyList<string> CasesOf(string? type)
        => Variables.FirstOrDefault(v => v.Type == type && v.Cases.Count > 0) is { } variable ? [.. variable.Cases.Select(c => $"{type}.{c.Name}")] : [];

    /// <summary>
    /// The values to offer for a type: the cases of an enum, <c>true</c> and <c>false</c>, then the variables of the type.
    /// </summary>
    public IReadOnlyList<string> ValuesOf(string? type)
    {
        var values = new List<string>(CasesOf(type));
        if (type == YarnFunctionDeclaration.Bool)
            values.AddRange(["true", "false"]);

        if (type is not null)
            values.AddRange(Variables.Where(v => v.Type == type).Select(v => v.Name));

        return values;
    }

    /// <summary>
    /// The values to offer for an argument of a function: the titles of the nodes, quoted, for <c>visited</c> and the
    /// other functions that take a node; else those of its type.
    /// </summary>
    public IReadOnlyList<string> ArgumentValuesOf(string? function, int index)
    {
        if (index == 0 && function is not null && NodeFunctions.Contains(function))
            return [.. NodeNames.Select(n => $"\"{n}\"")];

        return ValuesOf(ArgumentTypeOf(function, index));
    }

    /// <summary>
    /// The type of an argument of a function, or null.
    /// </summary>
    public string? ArgumentTypeOf(string? function, int index)
        => Function(function) is { } declaration ? index < declaration.ParameterTypes.Count ? declaration.ParameterTypes[index] : declaration.VariadicType : null;

    /// <summary>
    /// Makes a value typed by the user fit its type: a text gets its quotes, and the name of a case of an enum its enum
    /// (<c>Wreck</c> becomes <c>Act.Wreck</c>). A value that is already an expression stays as typed.
    /// </summary>
    public string FitValue(string? type, string? text)
    {
        var value = text?.Trim() ?? "";
        if (value.Length == 0 || value.StartsWith('$') || value.StartsWith('"') || value.Contains('('))
            return value;

        if (type == YarnFunctionDeclaration.String)
            return $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

        if (!value.Contains('.') && Variables.FirstOrDefault(v => v.Type == type && v.Cases.Count > 0) is { } variable && variable.CaseNamed(value) is { } known)
            return $"{type}.{known.Name}";

        return value;
    }
}
