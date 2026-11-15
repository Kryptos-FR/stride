namespace ColorRise.Skein.Assets.Compilation;

/// <summary>
/// What to compile: the scripts of one Yarn project, and what the project and the game tell the compiler.
/// </summary>
public sealed record YarnCompilationRequest
{
    /// <summary>
    /// The scripts: a name (what diagnostics, nodes and lines say as their file) and the text.
    /// </summary>
    public IReadOnlyList<(string FileName, string Text)> Files { get; init; } = [];

    /// <summary>
    /// The functions of the game. Yarn Spinner's own (<c>visited</c>, <c>dice</c>...) are always known.
    /// </summary>
    public IReadOnlyList<YarnFunctionDeclaration> Functions { get; init; } = [];

    /// <summary>
    /// The severity of diagnostics the project changes, by code. The compiler keeps a diagnostic it cannot go without at
    /// its own severity.
    /// </summary>
    public IReadOnlyDictionary<string, YarnSeverity> Severities { get; init; } = new Dictionary<string, YarnSeverity>();

    /// <summary>
    /// Stops after the lines and the nodes: no type check, no variables, no program. What the string tables need.
    /// </summary>
    public bool StringsOnly { get; init; }

    /// <summary>
    /// Stops the compilation.
    /// </summary>
    public CancellationToken Cancellation { get; init; }
}

/// <summary>
/// What the scripts of a Yarn project give: the program when there is no error, and what the editor, the player and the
/// asset compiler show and store, in Skein's own types (nothing else of Skein reads Yarn Spinner's compiler).
/// </summary>
public sealed record YarnCompilation
{
    /// <summary>
    /// The compiled program; null when a diagnostic is an error, and for <see cref="YarnCompilationRequest.StringsOnly"/>.
    /// </summary>
    public Yarn.Program? Program { get; init; }

    /// <summary>
    /// The nodes, in the order of the files and of their text. They are there even when the scripts have errors.
    /// </summary>
    public IReadOnlyList<YarnNodeData> Nodes { get; init; } = [];

    /// <summary>
    /// The lines and the options' texts, by line id.
    /// </summary>
    public IReadOnlyDictionary<string, YarnLineData> Lines { get; init; } = new Dictionary<string, YarnLineData>();

    /// <summary>
    /// The variables the scripts declare, and the ones they only use (<see cref="YarnVariableData.IsImplicit"/>).
    /// </summary>
    public IReadOnlyList<YarnVariableData> Variables { get; init; } = [];

    /// <summary>
    /// What the compiler has to say, in the order it said it, at the project's severities.
    /// </summary>
    public IReadOnlyList<YarnDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// Whether a diagnostic is an error.
    /// </summary>
    public bool HasErrors => Diagnostics.Any(d => d.Severity == YarnSeverity.Error);
}

/// <summary>
/// A line of dialogue or the text of an option, in the base language.
/// </summary>
public sealed record YarnLineData
{
    /// <summary>
    /// The text as written, with its markup and its <c>{0}</c> placeholders for the values the script puts in.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// The title of the node the line is in (for a node of a node group, the title the group shares).
    /// </summary>
    public required string Node { get; init; }

    /// <summary>
    /// The name of the file, as the request gave it.
    /// </summary>
    public required string File { get; init; }

    /// <summary>
    /// The line of the text, from 1.
    /// </summary>
    public required int Line { get; init; }

    /// <summary>
    /// Whether the compiler made the id up because the text has no <c>#line:</c> tag: such an id changes when the text
    /// around it does, so it cannot key a translation.
    /// </summary>
    public required bool IsImplicitId { get; init; }

    /// <summary>
    /// The line's other hashtags (<c>#shout</c>), without the <c>#</c>, and the ones the compiler adds (<c>lastline</c> on
    /// the line before options).
    /// </summary>
    public IReadOnlyList<string> Metadata { get; init; } = [];

    /// <summary>
    /// The id of the line this one repeats (<c>#shadow:</c>), or null.
    /// </summary>
    public string? ShadowOf { get; init; }
}

/// <summary>
/// A variable of the scripts.
/// </summary>
public sealed record YarnVariableData
{
    /// <summary>
    /// The name, with its <c>$</c>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// <c>number</c>, <c>string</c>, <c>bool</c>, or the name of an enum the scripts declare.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// The value before any script sets it: a <see cref="float"/>, a <see cref="string"/> or a <see cref="bool"/>; null
    /// for a smart variable, whose value is computed.
    /// </summary>
    public object? Default { get; init; }

    /// <summary>
    /// The <c>///</c> comment above the declaration, or null.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The file of the declaration, as the request gave it; null when no script declares the variable.
    /// </summary>
    public string? File { get; init; }

    /// <summary>
    /// The line of the declaration, from 1; 0 without one.
    /// </summary>
    public int Line { get; init; }

    /// <summary>
    /// Whether the declaration is an expression (<c>&lt;&lt;declare $rich = $gold &gt; 100&gt;&gt;</c>): the value is
    /// computed each time, never stored.
    /// </summary>
    public bool IsSmart { get; init; }

    /// <summary>
    /// Whether no <c>&lt;&lt;declare&gt;&gt;</c> names the variable: the compiler worked its type out from its uses.
    /// </summary>
    public bool IsImplicit { get; init; }

    /// <summary>
    /// Whether the compiler made the variable for its own use (what a <c>once</c> remembers): saved with the others, never
    /// shown as a variable of the story.
    /// </summary>
    public bool IsInternal => Name.StartsWith(InternalPrefix, StringComparison.Ordinal);

    /// <summary>
    /// How the names of the compiler's own variables start.
    /// </summary>
    public const string InternalPrefix = "$Yarn.Internal.";
}
