using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Assets.Text;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// The converters of the bindings of Skein's views.
/// </summary>
public static class YarnConverters
{
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
    /// The condition of a line or an option as a row ends with it, or nothing.
    /// </summary>
    public static readonly IValueConverter ConditionLabel = new FuncValueConverter<string?, string>(condition => condition is null ? "" : "  \u27E8" + condition + "\u27E9");

    /// <summary>
    /// The glyph of the button that folds a card: what a click does.
    /// </summary>
    public static readonly IValueConverter FoldGlyph = new FuncValueConverter<bool, string>(folded => folded ? "\u25B8" : "\u25BE");
}
