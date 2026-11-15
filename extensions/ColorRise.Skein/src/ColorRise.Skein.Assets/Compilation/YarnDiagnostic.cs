namespace ColorRise.Skein.Assets.Compilation;

/// <summary>
/// How much a diagnostic matters. A project sets it per code, as Yarn Spinner's <c>compilerOptions</c> do.
/// </summary>
public enum YarnSeverity
{
    /// <summary>
    /// The scripts don't compile: there is no program.
    /// </summary>
    Error,

    /// <summary>
    /// Something to fix that doesn't stop the scripts from compiling.
    /// </summary>
    Warning,

    /// <summary>
    /// A remark.
    /// </summary>
    Info,

    /// <summary>
    /// Not reported. Only a project's setting gives it: a compilation never holds a diagnostic of this severity.
    /// </summary>
    None,
}

/// <summary>
/// One thing the compiler has to say about a place of a script. Lines and columns count from 1.
/// </summary>
public sealed record YarnDiagnostic
{
    /// <summary>
    /// The name of the file, as the request gave it.
    /// </summary>
    public required string File { get; init; }

    /// <summary>
    /// The title of the node the place is in, or null outside of a node.
    /// </summary>
    public string? Node { get; init; }

    /// <summary>
    /// The line the place starts at.
    /// </summary>
    public required int Line { get; init; }

    /// <summary>
    /// The column the place starts at.
    /// </summary>
    public required int Column { get; init; }

    /// <summary>
    /// The line the place ends at.
    /// </summary>
    public required int EndLine { get; init; }

    /// <summary>
    /// The column after the last character of the place.
    /// </summary>
    public required int EndColumn { get; init; }

    /// <summary>
    /// Yarn Spinner's code of the diagnostic (<c>YS0012</c>), the key of a project's severity settings; empty when the
    /// compiler gave none.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// How much it matters, after the project's settings.
    /// </summary>
    public required YarnSeverity Severity { get; init; }

    /// <summary>
    /// What the compiler says.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The diagnostic the way build tools write theirs, so that a build log and an IDE's error list link to the place:
    /// <c>Harbour.yarn(12,8): warning YS0012: [Harbour] ...</c>.
    /// </summary>
    public override string ToString()
    {
        var severity = Severity switch
        {
            YarnSeverity.Error => "error",
            YarnSeverity.Warning => "warning",
            _ => "info",
        };
        var code = Code.Length > 0 ? $" {Code}" : "";
        var node = Node is not null ? $"[{Node}] " : "";
        return $"{File}({Line},{Column}): {severity}{code}: {node}{Message}";
    }
}
