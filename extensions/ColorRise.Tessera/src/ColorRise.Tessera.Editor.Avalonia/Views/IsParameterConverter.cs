using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace ColorRise.Tessera.Editor.Avalonia.Views;

/// <summary>
/// True when the value is the parameter; back, the parameter when true. A pair of toggle buttons shows an enum this way.
/// </summary>
internal sealed class IsParameterConverter : IValueConverter
{
    /// <summary>
    /// The converter.
    /// </summary>
    public static IsParameterConverter Instance { get; } = new();

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Equals(value, parameter);

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? parameter : BindingOperations.DoNothing;
}
