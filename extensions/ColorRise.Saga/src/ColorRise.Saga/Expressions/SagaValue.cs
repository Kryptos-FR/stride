using System.Globalization;
using Stride.Core;

namespace ColorRise.Saga.Expressions;

/// <summary>
/// The kinds of value an expression can produce.
/// </summary>
public enum SagaValueKind
{
    Bool,
    Int,
    Float,
    String,

    /// <summary>
    /// A named constant of a set: a quest or scenario state, a scenario outcome, an act.
    /// </summary>
    Symbol,
}

/// <summary>
/// A value of the story: a variable, a literal, the result of an expression or a function.
/// </summary>
[DataContract("SagaValue")]
public struct SagaValue : IEquatable<SagaValue>
{
    /// <summary>
    /// The kind of the value.
    /// </summary>
    public SagaValueKind Kind { get; set; }

    /// <summary>
    /// The number of a <see cref="SagaValueKind.Bool"/> (0 or 1), <see cref="SagaValueKind.Int"/> or
    /// <see cref="SagaValueKind.Float"/> value.
    /// </summary>
    public double Number { get; set; }

    /// <summary>
    /// The text of a <see cref="SagaValueKind.String"/> or <see cref="SagaValueKind.Symbol"/> value.
    /// </summary>
    public string? Text { get; set; }

    public static SagaValue Bool(bool value) => new() { Kind = SagaValueKind.Bool, Number = value ? 1 : 0 };

    public static SagaValue Int(int value) => new() { Kind = SagaValueKind.Int, Number = value };

    public static SagaValue Float(float value) => new() { Kind = SagaValueKind.Float, Number = value };

    public static SagaValue String(string value) => new() { Kind = SagaValueKind.String, Text = value ?? throw new ArgumentNullException(nameof(value)) };

    public static SagaValue Symbol(string value) => new() { Kind = SagaValueKind.Symbol, Text = value ?? throw new ArgumentNullException(nameof(value)) };

    public readonly bool AsBool() => Kind == SagaValueKind.Bool ? Number != 0 : throw WrongKind(SagaValueKind.Bool);

    public readonly int AsInt() => Kind == SagaValueKind.Int ? (int)Number : throw WrongKind(SagaValueKind.Int);

    /// <summary>
    /// The value as a float; an <see cref="SagaValueKind.Int"/> widens.
    /// </summary>
    public readonly float AsFloat() => Kind is SagaValueKind.Float or SagaValueKind.Int ? (float)Number : throw WrongKind(SagaValueKind.Float);

    public readonly string AsString() => Kind == SagaValueKind.String ? Text! : throw WrongKind(SagaValueKind.String);

    public readonly string AsSymbol() => Kind == SagaValueKind.Symbol ? Text! : throw WrongKind(SagaValueKind.Symbol);

    public readonly bool Equals(SagaValue other) => Kind == other.Kind && Number.Equals(other.Number) && string.Equals(Text, other.Text, StringComparison.Ordinal);

    public override readonly bool Equals(object? obj) => obj is SagaValue other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(Kind, Number, Text);

    public static bool operator ==(SagaValue left, SagaValue right) => left.Equals(right);

    public static bool operator !=(SagaValue left, SagaValue right) => !left.Equals(right);

    /// <summary>
    /// The value as the expression language writes it.
    /// </summary>
    public override readonly string ToString() => Kind switch
    {
        SagaValueKind.Bool => Number != 0 ? "true" : "false",
        SagaValueKind.Int => ((int)Number).ToString(CultureInfo.InvariantCulture),
        SagaValueKind.Float => ((float)Number).ToString("0.0###", CultureInfo.InvariantCulture),
        SagaValueKind.String => "\"" + Text + "\"",
        _ => Text ?? string.Empty,
    };

    private readonly InvalidOperationException WrongKind(SagaValueKind expected) => new($"The value {this} is a {Kind}, not a {expected}.");
}
