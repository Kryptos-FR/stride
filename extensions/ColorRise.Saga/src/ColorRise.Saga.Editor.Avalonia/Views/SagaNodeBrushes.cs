using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ColorRise.Saga.Editor.ViewModels;

namespace ColorRise.Saga.Editor.Avalonia.Views;

/// <summary>
/// The color of a node of a Saga graph, by what it stands for (<see cref="SagaGraphNodeKind"/>); <c>Themes/SagaStyles.axaml</c>
/// sets the colors.
/// </summary>
public sealed class SagaNodeBrushes : IValueConverter
{
    public IBrush? Act { get; set; }

    public IBrush? Scenario { get; set; }

    public IBrush? Quest { get; set; }

    public IBrush? Dialogue { get; set; }

    public IBrush? Line { get; set; }

    public IBrush? Choices { get; set; }

    public IBrush? Branch { get; set; }

    public IBrush? End { get; set; }

    /// <summary>
    /// The color of a kind without its own.
    /// </summary>
    public IBrush? Default { get; set; }

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => (value switch
    {
        SagaGraphNodeKind.Act => Act,
        SagaGraphNodeKind.Scenario => Scenario,
        SagaGraphNodeKind.Quest => Quest,
        SagaGraphNodeKind.Dialogue => Dialogue,
        SagaGraphNodeKind.Line => Line,
        SagaGraphNodeKind.Choices => Choices,
        SagaGraphNodeKind.Branch => Branch,
        SagaGraphNodeKind.End => End,
        _ => null,
    }) ?? Default;

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
