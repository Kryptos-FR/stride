namespace ColorRise.Skein.Assets.Text;

/// <summary>
/// What a line of the body of a node is.
/// </summary>
public enum YarnStatementKind
{
    /// <summary>
    /// A line of the dialogue: <c>Odile: The lamp's out.</c>, or a line of narration without a speaker.
    /// </summary>
    Line,

    /// <summary>
    /// A line of a line group: <c>=&gt; Hello.</c>, of which the story picks one.
    /// </summary>
    LineGroupItem,

    /// <summary>
    /// An option: <c>-&gt; Ask about the lamp</c>. The lines indented under it run when it is chosen.
    /// </summary>
    Option,

    /// <summary>
    /// <c>&lt;&lt;if ...&gt;&gt;</c>: the start of a block that runs when its condition holds.
    /// </summary>
    If,

    /// <summary>
    /// <c>&lt;&lt;elseif ...&gt;&gt;</c> in an <c>if</c> block.
    /// </summary>
    ElseIf,

    /// <summary>
    /// <c>&lt;&lt;else&gt;&gt;</c> in an <c>if</c> or a <c>once</c> block.
    /// </summary>
    Else,

    /// <summary>
    /// <c>&lt;&lt;once&gt;&gt;</c> or <c>&lt;&lt;once if ...&gt;&gt;</c>: the start of a block that runs one time.
    /// </summary>
    Once,

    /// <summary>
    /// <c>&lt;&lt;jump ...&gt;&gt;</c>.
    /// </summary>
    Jump,

    /// <summary>
    /// <c>&lt;&lt;detour ...&gt;&gt;</c>.
    /// </summary>
    Detour,

    /// <summary>
    /// Any other command: <c>&lt;&lt;set ...&gt;&gt;</c>, <c>&lt;&lt;stop&gt;&gt;</c>, a command of the game.
    /// </summary>
    Command,
}

/// <summary>
/// One line of the body of a node that says something, as the places of its parts in the text. Blank lines, comments
/// and the lines that close a block (<c>&lt;&lt;endif&gt;&gt;</c>) are not statements.
/// </summary>
public sealed record YarnStatementSpan
{
    /// <summary>
    /// What the line is.
    /// </summary>
    public required YarnStatementKind Kind { get; init; }

    /// <summary>
    /// The line in the file, from 1.
    /// </summary>
    public required int Line { get; init; }

    /// <summary>
    /// The place of the first character of the line.
    /// </summary>
    public required int LineStart { get; init; }

    /// <summary>
    /// The place after the line and its line break.
    /// </summary>
    public required int LineEnd { get; init; }

    /// <summary>
    /// The spaces and tabs the line starts with.
    /// </summary>
    public required string Indentation { get; init; }

    /// <summary>
    /// How many options, line group items and blocks the statement is in.
    /// </summary>
    public required int Depth { get; init; }

    /// <summary>
    /// The rank, among the statements of the node, of the option, line group item or branch of a block the statement is
    /// in; -1 for a statement at the top of the body. An <c>elseif</c> or an <c>else</c> is in what its <c>if</c> is in.
    /// </summary>
    public required int Parent { get; init; }

    /// <summary>
    /// The place of the text of the statement: for a line, an option or a line group item, what follows the arrow,
    /// before its condition, its hashtags and its comment; for a command, what is between <c>&lt;&lt;</c> and
    /// <c>&gt;&gt;</c>.
    /// </summary>
    public required int TextStart { get; init; }

    /// <summary>
    /// The place after the text of the statement, without the spaces at its end.
    /// </summary>
    public required int TextEnd { get; init; }

    /// <summary>
    /// The text of the statement, as written: escapes stay escaped.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// The condition the statement depends on, without <c>if</c>: of a line or an option, the one written after it
    /// (<c>&lt;&lt;if $met&gt;&gt;</c>); of an <c>if</c>, an <c>elseif</c> or a <c>once if</c>, its own. Null without one.
    /// </summary>
    public string? Condition { get; init; }

    /// <summary>
    /// Whether the statement runs one time only: a <c>once</c> block, or a line or an option marked <c>&lt;&lt;once&gt;&gt;</c>.
    /// </summary>
    public bool IsOnce { get; init; }

    /// <summary>
    /// The hashtags after a line, an option or a line group item, without their <c>#</c>: <c>line:lamp</c>, <c>mood:kind</c>.
    /// </summary>
    public IReadOnlyList<string> Hashtags { get; init; } = [];

    /// <summary>
    /// The node a jump or a detour names, as written: a title, or an expression in braces.
    /// </summary>
    public string? Target { get; init; }

    /// <summary>
    /// Whether the statement is a line, an option or a line group item: text the story shows.
    /// </summary>
    public bool IsText => Kind is YarnStatementKind.Line or YarnStatementKind.LineGroupItem or YarnStatementKind.Option;

    /// <summary>
    /// Whether other statements can be in this one: an option, a line group item, or a branch of a block.
    /// </summary>
    public bool IsBranch => Kind is YarnStatementKind.Option or YarnStatementKind.LineGroupItem or YarnStatementKind.If or YarnStatementKind.ElseIf
        or YarnStatementKind.Else or YarnStatementKind.Once;

    /// <summary>
    /// The text before the first colon of a line, which Yarn Spinner takes as the speaker; null without one.
    /// </summary>
    public string? Speaker
    {
        get
        {
            if (Kind != YarnStatementKind.Line)
                return null;

            for (var i = 0; i < Text.Length; i++)
            {
                switch (Text[i])
                {
                    case '\\':
                        i++;
                        break;
                    case '{' or '[':
                        return null;
                    case ':':
                        return i > 0 ? Text[..i].Trim() : null;
                }
            }

            return null;
        }
    }
}
