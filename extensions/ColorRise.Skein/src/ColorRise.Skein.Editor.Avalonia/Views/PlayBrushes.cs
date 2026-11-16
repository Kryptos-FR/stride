using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using ColorRise.Skein.Editor.Player;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// The colours of a played dialogue, in the dialogue player, in the timeline and in the script editor: tones that read
/// on a light and on a dark background.
/// </summary>
public static class PlayBrushes
{
    /// <summary>
    /// Where the run is: the column of the timeline, the node of the graph, the line of the text.
    /// </summary>
    public static readonly IBrush Current = Brush(0xFF, 0x42, 0xA5, 0xF5);

    /// <summary>
    /// The line of the text the run is at, behind the text.
    /// </summary>
    public static readonly IBrush CurrentLine = Brush(0x38, 0x42, 0xA5, 0xF5);

    /// <summary>
    /// The column of the timeline the run is at, behind its items.
    /// </summary>
    public static readonly IBrush Cursor = Brush(0x40, 0x42, 0xA5, 0xF5);

    /// <summary>
    /// The dot of a step that the last replay made, or made differ.
    /// </summary>
    public static readonly IBrush Changed = Brush(0xFF, 0xFF, 0xA7, 0x26);

    /// <summary>
    /// The text on a span of the timeline.
    /// </summary>
    public static readonly IBrush SpanText = Brush(0xFF, 0xF4, 0xF4, 0xF4);

    /// <summary>
    /// The text of what is not said: a node entered, a command, a pick.
    /// </summary>
    public static readonly IBrush Faint = Brush(0xFF, 0x8A, 0x8A, 0x8A);

    /// <summary>
    /// The name of who says a line.
    /// </summary>
    public static readonly IBrush Speaker = Brush(0xFF, 0x2F, 0x8F, 0xD0);

    /// <summary>
    /// The lanes of the timeline at odd ranks.
    /// </summary>
    public static readonly IBrush AlternateLane = Brush(0x14, 0x80, 0x80, 0x80);

    /// <summary>
    /// The lines between the steps of the timeline.
    /// </summary>
    public static readonly IBrush Grid = Brush(0x60, 0x80, 0x80, 0x80);

    /// <summary>
    /// The header of the timeline when the control has no background.
    /// </summary>
    public static readonly IBrush Header = Brush(0xFF, 0x2B, 0x2B, 0x2F);

    private static readonly IBrush Node = Brush(0xFF, 0x3F, 0x58, 0x66);
    private static readonly IBrush Pick = Brush(0xFF, 0x7E, 0x62, 0xC9);
    private static readonly IBrush Line = Brush(0xFF, 0x2F, 0x8F, 0xD0);
    private static readonly IBrush Choice = Brush(0xFF, 0xD0, 0x82, 0x1C);
    private static readonly IBrush Command = Brush(0xFF, 0x3F, 0x9E, 0x4D);
    private static readonly IBrush Answer = Brush(0xFF, 0xA8, 0x5C, 0xBF);
    private static readonly IBrush Set = Brush(0xFF, 0xD0, 0x55, 0x80);
    private static readonly IBrush Variable = Brush(0xFF, 0x70, 0x86, 0x91);
    private static readonly IBrush Error = Brush(0xFF, 0xE5, 0x53, 0x4B);

    /// <summary>
    /// The colour of the text of a step in the transcript; the text's own for a line.
    /// </summary>
    public static readonly IValueConverter StepBrush = new FuncValueConverter<PlayerStepKind, object>(kind => kind switch
    {
        PlayerStepKind.Line => global::Avalonia.AvaloniaProperty.UnsetValue,
        PlayerStepKind.Choose => Choice,
        PlayerStepKind.Function => Answer,
        PlayerStepKind.SetVariable => Set,
        PlayerStepKind.Saliency => Pick,
        PlayerStepKind.Error => Error,
        _ => Faint,
    });

    /// <summary>
    /// The colour of a candidate of a pick: the chosen one, one that passed, one that a condition ruled out.
    /// </summary>
    public static readonly IValueConverter CandidateBrush = new FuncValueConverter<PlayerCandidate?, object>(candidate => candidate switch
    {
        { IsChosen: true } => Command,
        { Passed: true } => global::Avalonia.AvaloniaProperty.UnsetValue,
        _ => Faint,
    });

    /// <summary>
    /// The sign of a candidate of a pick: chosen, passed, ruled out.
    /// </summary>
    public static readonly IValueConverter CandidateSign = new FuncValueConverter<PlayerCandidate?, string>(candidate => candidate switch
    {
        { IsChosen: true } => "✓",
        { Passed: true } => "·",
        _ => "✗",
    });

    /// <summary>
    /// The colour of an item of the timeline by its kind.
    /// </summary>
    public static IBrush Of(TimelineItemKind kind) => kind switch
    {
        TimelineItemKind.Node => Node,
        TimelineItemKind.Pick => Pick,
        TimelineItemKind.Line => Line,
        TimelineItemKind.Choice => Choice,
        TimelineItemKind.Command => Command,
        TimelineItemKind.Answer => Answer,
        TimelineItemKind.Set => Set,
        TimelineItemKind.Variable => Variable,
        _ => Error,
    };

    private static IBrush Brush(byte a, byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromArgb(a, r, g, b));
}
