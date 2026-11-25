using Stride.Core;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// A value of a tile's property in the asset: a bool, an int, a float, a string or a colour, each with a type of its own, so
/// that the file keeps the type. In a member of type <see cref="object"/>, Stride's YAML reads a float back as a double and a
/// string such as <c>3</c> as an int (docs/plugin-findings.md).
/// </summary>
[DataContract(Inherited = true)]
public abstract class TileProperty
{
    /// <summary>
    /// The value, boxed, as the runtime tile keeps it.
    /// </summary>
    public abstract object GetValue();

    /// <summary>
    /// The property of a value of one of the five types.
    /// </summary>
    /// <exception cref="ArgumentException">The value is of another type.</exception>
    public static TileProperty From(object value) => value switch
    {
        bool flag => new TileBoolProperty { Value = flag },
        int number => new TileIntProperty { Value = number },
        float number => new TileFloatProperty { Value = number },
        string text => new TileStringProperty { Value = text },
        Color color => new TileColorProperty { Value = color },
        _ => throw new ArgumentException($"A tile property is a bool, an int, a float, a string or a Color, not {value?.GetType().Name ?? "null"}.", nameof(value)),
    };
}

/// <summary>
/// A property that is true or false.
/// </summary>
[DataContract("TileBoolProperty")]
[Display("Bool")]
public sealed class TileBoolProperty : TileProperty
{
    /// <summary>
    /// The value.
    /// </summary>
    [DataMember(10)]
    public bool Value { get; set; }

    /// <summary>
    /// The value, boxed.
    /// </summary>
    public override object GetValue() => Value;
}

/// <summary>
/// A property that is a whole number.
/// </summary>
[DataContract("TileIntProperty")]
[Display("Int")]
public sealed class TileIntProperty : TileProperty
{
    /// <summary>
    /// The value.
    /// </summary>
    [DataMember(10)]
    public int Value { get; set; }

    /// <summary>
    /// The value, boxed.
    /// </summary>
    public override object GetValue() => Value;
}

/// <summary>
/// A property that is a number with a fraction.
/// </summary>
[DataContract("TileFloatProperty")]
[Display("Float")]
public sealed class TileFloatProperty : TileProperty
{
    /// <summary>
    /// The value.
    /// </summary>
    [DataMember(10)]
    public float Value { get; set; }

    /// <summary>
    /// The value, boxed.
    /// </summary>
    public override object GetValue() => Value;
}

/// <summary>
/// A property that is a text.
/// </summary>
[DataContract("TileStringProperty")]
[Display("String")]
public sealed class TileStringProperty : TileProperty
{
    /// <summary>
    /// The value.
    /// </summary>
    [DataMember(10)]
    public string Value { get; set; } = "";

    /// <summary>
    /// The value, boxed.
    /// </summary>
    public override object GetValue() => Value;
}

/// <summary>
/// A property that is a colour.
/// </summary>
[DataContract("TileColorProperty")]
[Display("Color")]
public sealed class TileColorProperty : TileProperty
{
    /// <summary>
    /// The value.
    /// </summary>
    [DataMember(10)]
    public Color Value { get; set; } = Color.White;

    /// <summary>
    /// The value, boxed.
    /// </summary>
    public override object GetValue() => Value;
}
