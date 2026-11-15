namespace ColorRise.Saga.Expressions;

/// <summary>
/// Reads the literals of declarations and attributes (<c>12</c>, <c>-2.5</c>, <c>"Mara"</c>, <c>true</c>, <c>active</c>), and
/// gives the default value of each type.
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
