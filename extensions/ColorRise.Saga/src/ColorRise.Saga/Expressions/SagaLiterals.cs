using System.Globalization;

namespace ColorRise.Saga.Expressions;

/// <summary>
/// Reads the literals of declarations and attributes (<c>12</c>, <c>-2.5</c>, <c>"Mara"</c>, <c>true</c>, <c>active</c>), writes
/// values back as literals, and gives the default value of each type.
/// </summary>
public static class SagaLiterals
{
    /// <summary>
    /// Gets the value a variable of a type has when nothing set it.
    /// </summary>
    public static SagaValue DefaultOf(SagaTypeKind type) => type switch
    {
        SagaTypeKind.Bool => SagaValue.Bool(false),
        SagaTypeKind.Int => SagaValue.Int(0),
        SagaTypeKind.Float => SagaValue.Float(0),
        SagaTypeKind.String => SagaValue.String(string.Empty),
        SagaTypeKind.QuestState => SagaValue.Symbol(SagaSymbolSets.QuestStateNames[0]),
        SagaTypeKind.ScenarioState => SagaValue.Symbol(SagaSymbolSets.ScenarioStateNames[0]),
        _ => SagaValue.Symbol(string.Empty),
    };

    /// <summary>
    /// Reads a literal of a type. An empty text gives the default of the type.
    /// </summary>
    /// <returns>An error message, or null when the text is a literal of the type.</returns>
    public static string? TryParse(string? text, SagaTypeKind type, out SagaValue value)
    {
        value = DefaultOf(type);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var parsed = SagaParser.ParseCondition(text);
        if (!parsed.Succeeded)
            return parsed.Diagnostics[0].Message;

        SagaValue? literal = parsed.Value switch
        {
            LiteralExpression x => x.Value,
            UnaryExpression { Operator: SagaUnaryOperator.Negate, Operand: LiteralExpression { Value.Kind: SagaValueKind.Int } x } => SagaValue.Int(-x.Value.AsInt()),
            UnaryExpression { Operator: SagaUnaryOperator.Negate, Operand: LiteralExpression { Value.Kind: SagaValueKind.Float } x } => SagaValue.Float(-x.Value.AsFloat()),
            PathExpression x when type is SagaTypeKind.QuestState or SagaTypeKind.ScenarioState or SagaTypeKind.Outcome or SagaTypeKind.Act => SagaValue.Symbol(x.Path),
            _ => null,
        };
        if (literal is not { } found || Convert(found, type) is not { } converted)
            return $"'{text.Trim()}' is not {SagaTypeChecker.Describe(type)}.";
        value = converted;
        return null;
    }

    /// <summary>
    /// Writes a value as a literal that <see cref="TryParse"/> reads back to the same value.
    /// </summary>
    public static string Format(SagaValue value) => value.Kind switch
    {
        SagaValueKind.Float => FormatFloat(value.AsFloat()),
        SagaValueKind.String => "\"" + value.AsString().Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
        _ => value.ToString(),
    };

    // The shortest text that reads back to the same number, without the exponent the language doesn't read, and with a point
    private static string FormatFloat(float number)
    {
        var text = number.ToString("R", CultureInfo.InvariantCulture);
        if (text.IndexOf('E') is var e and >= 0)
        {
            var negative = text[0] == '-';
            var mantissa = text[(negative ? 1 : 0)..e];
            var point = mantissa.IndexOf('.');
            var digits = point < 0 ? mantissa : mantissa.Remove(point, 1);
            var position = (point < 0 ? mantissa.Length : point) + int.Parse(text[(e + 1)..], CultureInfo.InvariantCulture);
            var body = position <= 0 ? "0." + new string('0', -position) + digits
                : position >= digits.Length ? digits + new string('0', position - digits.Length)
                : digits[..position] + "." + digits[position..];
            text = (negative ? "-" : "") + body;
        }
        return text.Contains('.') ? text : text + ".0";
    }

    /// <summary>
    /// Converts a value to a type when the language allows it (a whole number widens to a number).
    /// </summary>
    /// <returns>The converted value, or null when the value is not of the type.</returns>
    public static SagaValue? Convert(SagaValue value, SagaTypeKind type) => (value.Kind, type) switch
    {
        (SagaValueKind.Bool, SagaTypeKind.Bool) => value,
        (SagaValueKind.Int, SagaTypeKind.Int) => value,
        (SagaValueKind.Int or SagaValueKind.Float, SagaTypeKind.Float) => SagaValue.Float(value.AsFloat()),
        (SagaValueKind.String, SagaTypeKind.String) => value,
        (SagaValueKind.Symbol, SagaTypeKind.QuestState or SagaTypeKind.ScenarioState or SagaTypeKind.Outcome or SagaTypeKind.Act) => value,
        _ => null,
    };
}
