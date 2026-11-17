using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Assets.Text;
using ColorRise.Skein.Editor.Conditions;
using ColorRise.Skein.Editor.Services;
using Stride.Core.Mathematics;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// The converters of the bindings of Skein's views.
/// </summary>
public static class YarnConverters
{
    /// <summary>
    /// The text of a comparison in the condition builder: "is", "is not", "&lt;"...
    /// </summary>
    public static readonly IValueConverter ComparisonLabel = new FuncValueConverter<YarnComparison, string>(YarnConditionTermViewModel.LabelOf);

    /// <summary>
    /// The text of a joiner in the condition builder.
    /// </summary>
    public static readonly IValueConverter JoinerLabel = new FuncValueConverter<YarnJoiner, string>(joiner => joiner == YarnJoiner.And ? "all of these (and)" : "one of these (or)");

    /// <summary>
    /// What the set builder writes, as the text shows it.
    /// </summary>
    public static readonly IValueConverter CommandPreview = new FuncValueConverter<string?, string>(command => command is null ? "A set needs a variable and a value." : $"<<{command}>>");

    /// <summary>
    /// The text of an operator in the set builder: "to", "+="...
    /// </summary>
    public static readonly IValueConverter AssignmentLabel = new FuncValueConverter<YarnAssignmentOperator, string>(YarnAssignmentBuilderViewModel.LabelOf);

    /// <summary>
    /// The brush of the header of a card, from the colour of its node and whether it is a note.
    /// </summary>
    public static readonly IMultiValueConverter HeaderBrush = new FuncMultiValueConverter<object?, IBrush>(
        values => values.ToList() is [var color, var isNote, ..] ? YarnPalette.HeaderOf(color as string, isNote is true) : YarnPalette.Header);

    /// <summary>
    /// The brush of a chip that shows a value.
    /// </summary>
    public static readonly IValueConverter ValueBrush = new FuncValueConverter<string?, IBrush?>(
        value => string.IsNullOrEmpty(value) ? null : new ImmutableSolidColorBrush(YarnPalette.OfValue(value), 0.45));

    /// <summary>
    /// A list of words as one text.
    /// </summary>
    public static readonly IValueConverter Join = new FuncValueConverter<IReadOnlyList<string>?, string>(words => string.Join(", ", words ?? []));

    /// <summary>
    /// A rank from 0 as a rank from 1.
    /// </summary>
    public static readonly IValueConverter PlusOne = new FuncValueConverter<int, string>(rank => (rank + 1).ToString(CultureInfo.CurrentCulture));

    /// <summary>
    /// Whether a count is of several.
    /// </summary>
    public static readonly IValueConverter MoreThanOne = new FuncValueConverter<int, bool>(count => count > 1);

    /// <summary>
    /// The brush of the severity of a message of the compiler.
    /// </summary>
    public static readonly IValueConverter SeverityBrush = new FuncValueConverter<YarnSeverity, IBrush>(YarnPalette.OfSeverity);

    /// <summary>
    /// The colour of a count of errors: the one of an error when there are some, the text's own otherwise.
    /// </summary>
    public static readonly IValueConverter ErrorCountBrush = new FuncValueConverter<int, object>(count => count > 0 ? YarnPalette.OfSeverity(YarnSeverity.Error) : AvaloniaProperty.UnsetValue);

    /// <summary>
    /// The room on the left of a row of a card, from how deep its statement is.
    /// </summary>
    public static readonly IValueConverter Indent = new FuncValueConverter<int, Thickness>(depth => new Thickness(depth * 12, 0, 0, 0));

    /// <summary>
    /// The mark a row of a card starts with: the arrow of an option or of a line of a line group.
    /// </summary>
    public static readonly IValueConverter RowMark = new FuncValueConverter<YarnStatementKind, string>(kind => kind switch
    {
        YarnStatementKind.Option => "\u2192 ",
        YarnStatementKind.LineGroupItem => "\u21D2 ",
        _ => "",
    });

    /// <summary>
    /// The speaker of a line as a row starts with it: <c>Odile: </c>, or nothing.
    /// </summary>
    public static readonly IValueConverter SpeakerLabel = new FuncValueConverter<string?, string>(speaker => speaker is null ? "" : speaker + ": ");

    /// <summary>
    /// The colour of a speaker on a card, readable on its dark background; nothing without a colour.
    /// </summary>
    public static readonly IValueConverter SpeakerOnCard = new FuncValueConverter<Color3?, object?>(
        color => color is { } value ? YarnPalette.BrushOf(YarnCharacterColors.Readable(value, onDark: true)) : AvaloniaProperty.UnsetValue);

    /// <summary>
    /// The colour of a speaker in the dialogue player, readable on a light and on a dark background; nothing without a colour.
    /// </summary>
    public static readonly IValueConverter SpeakerOnAny = new FuncValueConverter<Color3?, object?>(
        color => color is { } value ? YarnPalette.BrushOf(YarnCharacterColors.ReadableOnBoth(value)) : AvaloniaProperty.UnsetValue);

    /// <summary>
    /// The glyph of the button that folds a card: what a click does.
    /// </summary>
    public static readonly IValueConverter FoldGlyph = new FuncValueConverter<bool, string>(folded => folded ? "\u25B8" : "\u25BE");
}
