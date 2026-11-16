using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using ColorRise.Skein.Assets.Compilation;

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
}
