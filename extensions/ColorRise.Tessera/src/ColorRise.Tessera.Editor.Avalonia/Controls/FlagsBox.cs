using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Stride.Core;

namespace ColorRise.Tessera.Editor.Avalonia.Controls;

/// <summary>
/// The flags of a <c>[Flags]</c> enum, a check box each, that read and write <see cref="Value"/>: the property grid's editor
/// of Tessera's flags, as Game Studio's own editor of flags shows boxes that write nothing.
/// </summary>
/// <remarks>
/// A flag is a value of one bit. A value that is not of the type, such as the property grid's mark of different values,
/// ticks no box and lets none be changed.
/// </remarks>
public sealed class FlagsBox : StackPanel
{
    /// <summary>
    /// The enum type, or its nullable type.
    /// </summary>
    public static readonly StyledProperty<Type?> EnumTypeProperty = AvaloniaProperty.Register<FlagsBox, Type?>(nameof(EnumType));

    /// <summary>
    /// The value, bound both ways.
    /// </summary>
    public static readonly StyledProperty<object?> ValueProperty = AvaloniaProperty.Register<FlagsBox, object?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    // True while the boxes are set from the value, so that they write nothing back
    private bool refreshing;

    /// <summary>
    /// The enum type, or its nullable type.
    /// </summary>
    public Type? EnumType
    {
        get => GetValue(EnumTypeProperty);
        set => SetValue(EnumTypeProperty, value);
    }

    /// <summary>
    /// The value, bound both ways.
    /// </summary>
    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    // The enum type without its nullable
    private Type? FlagsType => EnumType is { } type ? Nullable.GetUnderlyingType(type) ?? type : null;

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EnumTypeProperty)
        {
            MakeBoxes();
        }
        else if (change.Property == ValueProperty)
        {
            Refresh();
        }
    }

    private void MakeBoxes()
    {
        foreach (var old in Children.OfType<CheckBox>())
        {
            old.IsCheckedChanged -= BoxChanged;
        }
        Children.Clear();
        if (FlagsType is not { IsEnum: true } type)
        {
            return;
        }
        foreach (var flag in Enum.GetValues(type))
        {
            var bits = Bits(flag);
            if (bits == 0 || (bits & (bits - 1)) != 0)
            {
                continue;
            }
            var name = flag.ToString()!;
            var box = new CheckBox { Content = type.GetField(name)?.GetCustomAttribute<DisplayAttribute>()?.Name ?? name, Tag = flag };
            box.IsCheckedChanged += BoxChanged;
            Children.Add(box);
        }
        Refresh();
    }

    private void Refresh()
    {
        refreshing = true;
        try
        {
            var value = Value is Enum known && known.GetType() == FlagsType ? Bits(known) : (ulong?)null;
            foreach (var box in Children.OfType<CheckBox>())
            {
                box.IsChecked = value is { } bits ? (bits & Bits(box.Tag!)) != 0 : null;
                box.IsEnabled = value is not null;
            }
        }
        finally
        {
            refreshing = false;
        }
    }

    private void BoxChanged(object? sender, RoutedEventArgs e)
    {
        if (refreshing || sender is not CheckBox { Tag: { } flag } box || FlagsType is not { } type || Value is not Enum known || known.GetType() != type)
        {
            return;
        }
        var bits = box.IsChecked == true ? Bits(known) | Bits(flag) : Bits(known) & ~Bits(flag);
        Value = Enum.ToObject(type, bits);
    }

    private static ulong Bits(object flag) => Convert.ToUInt64(flag, System.Globalization.CultureInfo.InvariantCulture);
}
