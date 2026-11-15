using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ColorRise.Saga.Editor.ViewModels;

namespace ColorRise.Saga.Editor.Avalonia.Views;

/// <summary>
/// The color of the outline and badge of a node of a Saga graph, by where the story played in the session is on it
/// (<see cref="SagaPlayMark"/>); <c>Themes/SagaStyles.axaml</c> sets the colors.
/// </summary>
public sealed class SagaPlayBrushes : IValueConverter
{
    public IBrush? Current { get; set; }

    public IBrush? Visited { get; set; }

    public IBrush? Running { get; set; }

    public IBrush? Offered { get; set; }

    public IBrush? Done { get; set; }

    public IBrush? Failed { get; set; }

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => (value switch
    {
        SagaPlayMark.Current => Current,
        SagaPlayMark.Visited => Visited,
        SagaPlayMark.Running => Running,
        SagaPlayMark.Offered => Offered,
        SagaPlayMark.Done => Done,
        SagaPlayMark.Failed => Failed,
        _ => null,
    }) ?? Brushes.Transparent;

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
