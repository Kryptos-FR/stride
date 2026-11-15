namespace ColorRise.Saga.Expressions;

/// <summary>
/// Checks parsed conditions and actions against what a story declares: names, types, functions, events and signals.
/// </summary>
/// <remarks>
/// A bare name that is not a path, compared with or given where a quest state, a scenario state, an outcome or an act is
/// expected, is a symbol of that set: the checker replaces it in the tree with a symbol literal, so that evaluation needs
/// no scope. An error makes the type of its expression unknown, which silences the errors that would only follow from it.
/// </remarks>
public sealed class SagaTypeChecker(ISagaScope scope)
{
    /// <summary>
    /// <c>raised("event")</c>: whether the event was raised at least once.
    /// </summary>
    public const string RaisedFunction = "raised";

    /// <summary>
    /// <c>visited("node")</c>: whether the named dialogue node was played at least once.
    /// </summary>
    public const string VisitedFunction = "visited";

    /// <summary>
    /// <c>visits("node")</c>: how many times the named dialogue node was played.
    /// </summary>
    public const string VisitsFunction = "visits";

    /// <summary>
    /// Checks a condition. A null condition (always true) is valid.
    /// </summary>
    public IReadOnlyList<SagaDiagnostic> CheckCondition(SagaExpression? expression)
    {
        if (expression is null)
            return [];

        var run = new Run(scope);
        if (run.Check(expression) is { } type && type.Kind != SagaTypeKind.Bool)
            run.Report(expression, $"A condition must be true or false, but this is {Describe(type.Kind)}.");
        return run.Diagnostics;
    }

    /// <summary>
    /// Checks a list of actions.
    /// </summary>
    public IReadOnlyList<SagaDiagnostic> CheckActions(IReadOnlyList<SagaStatement> statements)
    {
        var run = new Run(scope);
        foreach (var statement in statements)
            run.Check(statement);
        return run.Diagnostics;
    }

    internal static string Describe(SagaTypeKind type) => type switch
    {
        SagaTypeKind.Bool => "true or false",
        SagaTypeKind.Int => "a whole number",
        SagaTypeKind.Float => "a number",
        SagaTypeKind.String => "a text",
        SagaTypeKind.QuestState => "a quest state",
        SagaTypeKind.ScenarioState => "a scenario state",
        SagaTypeKind.Outcome => "an outcome",
        SagaTypeKind.Act => "an act",
        _ => type.ToString(),
    };

    private static bool IsNumber(SagaTypeKind type) => type is SagaTypeKind.Int or SagaTypeKind.Float;

    private static bool IsSymbol(SagaTypeKind type) => type is SagaTypeKind.QuestState or SagaTypeKind.ScenarioState or SagaTypeKind.Outcome or SagaTypeKind.Act;

    /// <summary>
    /// A checked type, with the set its symbols come from.
    /// </summary>
    private readonly record struct CheckedType(SagaTypeKind Kind, string? SymbolSet = null);

    private sealed class Run(ISagaScope scope)
    {
        public List<SagaDiagnostic> Diagnostics { get; } = [];

        public void Report(SagaExpression expression, string message) => Diagnostics.Add(new SagaDiagnostic(expression.Position, expression.Length, message));

        public void Report(SagaStatement statement, string message) => Diagnostics.Add(new SagaDiagnostic(statement.Position, statement.Length, message));

        public void Check(SagaStatement statement)
        {
            switch (statement)
            {
                case AssignStatement assign:
                    if (!scope.TryGetPathType(assign.Path, out var type, out var symbolSet))
                    {
                        Report(assign, $"Unknown name '{assign.Path}'. Declare it in the story's variables.");
                        Check(assign.Value);
                        return;
                    }
                    if (!scope.IsAssignable(assign.Path))
                        Report(assign, $"'{assign.Path}' can't be assigned: the story sets it.");
                    assign.Value = CheckValue(assign.Value, new CheckedType(type, symbolSet));
                    return;

                case RaiseStatement raise:
                    if (!scope.IsEvent(raise.Event))
                        Report(raise, $"Unknown event '{raise.Event}'. Declare it in the story's events.");
                    return;

                case EmitStatement emit:
                    var signal = scope.FindSignal(emit.Signal);
                    if (signal is null)
                    {
                        Report(emit, $"Unknown signal '{emit.Signal}'. Declare it in the story's signals.");
                        emit.Arguments.ForEach(x => Check(x));
                        return;
                    }
                    CheckArguments(emit.Signal, emit.Arguments, signal.Parameters, emit.Position, emit.Length);
                    return;
            }
        }

        /// <summary>
        /// Checks an expression and gives its type, or null when it is unknown (after an error).
        /// </summary>
        public CheckedType? Check(SagaExpression expression)
        {
            switch (expression)
            {
                case LiteralExpression literal:
                    return literal.Value.Kind switch
                    {
                        SagaValueKind.Bool => new CheckedType(SagaTypeKind.Bool),
                        SagaValueKind.Int => new CheckedType(SagaTypeKind.Int),
                        SagaValueKind.Float => new CheckedType(SagaTypeKind.Float),
                        SagaValueKind.String => new CheckedType(SagaTypeKind.String),
                        // A symbol already bound by an earlier check: its set is no longer known.
                        _ => null,
                    };

                case PathExpression path:
                    if (scope.TryGetPathType(path.Path, out var type, out var symbolSet))
                        return new CheckedType(type, symbolSet);
                    Report(path, $"Unknown name '{path.Path}'.");
                    return null;

                case CallExpression call:
                    return CheckCall(call);

                case UnaryExpression unary:
                {
                    var operand = Check(unary.Operand);
                    if (operand is not { } operandType)
                        return null;
                    if (unary.Operator == SagaUnaryOperator.Not)
                        return Expect(unary.Operand, operandType, SagaTypeKind.Bool, "'!'") ? operandType : null;
                    if (!IsNumber(operandType.Kind))
                    {
                        Report(unary.Operand, $"'-' needs a number, but this is {Describe(operandType.Kind)}.");
                        return null;
                    }
                    return operandType;
                }

                case BinaryExpression binary:
                    return CheckBinary(binary);

                default:
                    Report(expression, "Unknown expression.");
                    return null;
            }
        }

        private CheckedType? CheckBinary(BinaryExpression binary)
        {
            switch (binary.Operator)
            {
                case SagaBinaryOperator.Or:
                case SagaBinaryOperator.And:
                {
                    var name = binary.Operator == SagaBinaryOperator.Or ? "'||'" : "'&&'";
                    var left = Check(binary.Left);
                    var right = Check(binary.Right);
                    var leftOk = left is { } l && Expect(binary.Left, l, SagaTypeKind.Bool, name);
                    var rightOk = right is { } r && Expect(binary.Right, r, SagaTypeKind.Bool, name);
                    return leftOk && rightOk ? new CheckedType(SagaTypeKind.Bool) : null;
                }

                case SagaBinaryOperator.Equal:
                case SagaBinaryOperator.NotEqual:
                {
                    var (left, right) = CheckComparedSides(binary);
                    if (left is not { } l || right is not { } r)
                        return null;
                    if (l.Kind != r.Kind && !(IsNumber(l.Kind) && IsNumber(r.Kind)))
                    {
                        Report(binary, $"Can't compare {Describe(l.Kind)} with {Describe(r.Kind)}.");
                        return null;
                    }
                    return new CheckedType(SagaTypeKind.Bool);
                }

                default:
                {
                    var left = Check(binary.Left);
                    var right = Check(binary.Right);
                    var symbol = binary.Operator is SagaBinaryOperator.Add ? "'+'" : binary.Operator is SagaBinaryOperator.Subtract ? "'-'" : "a comparison";
                    var leftOk = left is { } l && ExpectNumber(binary.Left, l, symbol);
                    var rightOk = right is { } r && ExpectNumber(binary.Right, r, symbol);
                    if (!leftOk || !rightOk)
                        return null;
                    if (binary.Operator is SagaBinaryOperator.Add or SagaBinaryOperator.Subtract)
                        return new CheckedType(left!.Value.Kind == SagaTypeKind.Int && right!.Value.Kind == SagaTypeKind.Int ? SagaTypeKind.Int : SagaTypeKind.Float);
                    return new CheckedType(SagaTypeKind.Bool);
                }
            }
        }

        /// <summary>
        /// Checks both sides of <c>==</c> or <c>!=</c>, binding a bare name to the symbols of the other side.
        /// </summary>
        private (CheckedType? Left, CheckedType? Right) CheckComparedSides(BinaryExpression binary)
        {
            if (IsUnresolvedName(binary.Right))
            {
                var left = Check(binary.Left);
                if (left is { } l && IsSymbol(l.Kind))
                {
                    binary.Right = BindSymbol((PathExpression)binary.Right, l, out var bound);
                    return (left, bound ? l : null);
                }
                return (left, Check(binary.Right));
            }
            if (IsUnresolvedName(binary.Left))
            {
                var right = Check(binary.Right);
                if (right is { } r && IsSymbol(r.Kind))
                {
                    binary.Left = BindSymbol((PathExpression)binary.Left, r, out var bound);
                    return (bound ? r : null, right);
                }
                return (Check(binary.Left), right);
            }
            return (Check(binary.Left), Check(binary.Right));
        }

        private CheckedType? CheckCall(CallExpression call)
        {
            if (call.Function is RaisedFunction or VisitedFunction or VisitsFunction)
            {
                CheckArguments(call.Function, call.Arguments, [SagaTypeKind.String], call.Position, call.Length);
                if (call.Function == RaisedFunction && call.Arguments is [LiteralExpression { Value.Kind: SagaValueKind.String } name] && !scope.IsEvent(name.Value.AsString()))
                    Report(name, $"Unknown event '{name.Value.AsString()}'. Declare it in the story's events.");
                return new CheckedType(call.Function == VisitsFunction ? SagaTypeKind.Int : SagaTypeKind.Bool);
            }

            var function = scope.FindFunction(call.Function);
            if (function is null)
            {
                Report(call, $"Unknown function '{call.Function}'. Declare it in the story's functions.");
                call.Arguments.ForEach(x => Check(x));
                return null;
            }
            CheckArguments(call.Function, call.Arguments, function.Parameters, call.Position, call.Length);
            return new CheckedType(function.ReturnType, SagaSymbolSets.ForType(function.ReturnType));
        }

        private void CheckArguments(string name, List<SagaExpression> arguments, IReadOnlyList<SagaTypeKind> parameters, int position, int length)
        {
            if (arguments.Count != parameters.Count)
            {
                Diagnostics.Add(new SagaDiagnostic(position, length, $"'{name}' takes {Count(parameters.Count)}, not {arguments.Count}."));
                arguments.ForEach(x => Check(x));
                return;
            }
            for (var i = 0; i < arguments.Count; i++)
                arguments[i] = CheckValue(arguments[i], new CheckedType(parameters[i], SagaSymbolSets.ForType(parameters[i])));
        }

        /// <summary>
        /// Checks a value given where a type is expected (an argument, an assigned value); returns the value, with a bare name
        /// bound to a symbol when a symbol is expected.
        /// </summary>
        private SagaExpression CheckValue(SagaExpression value, CheckedType expected)
        {
            if (IsSymbol(expected.Kind) && IsUnresolvedName(value))
                return BindSymbol((PathExpression)value, expected, out _);

            if (Check(value) is { } actual && actual.Kind != expected.Kind && !(actual.Kind == SagaTypeKind.Int && expected.Kind == SagaTypeKind.Float))
                Report(value, $"Expected {Describe(expected.Kind)}, but this is {Describe(actual.Kind)}.");
            return value;
        }

        private LiteralExpression BindSymbol(PathExpression name, CheckedType type, out bool bound)
        {
            var symbolSet = type.SymbolSet ?? SagaSymbolSets.ForType(type.Kind);
            var symbols = symbolSet is null ? null : scope.GetSymbols(symbolSet);
            bound = symbols is null || symbols.Contains(name.Path);
            if (!bound)
            {
                Report(name, symbols!.Count == 0
                    ? $"'{name.Path}' is not {Describe(type.Kind)}: there is none to choose from."
                    : $"'{name.Path}' is not {Describe(type.Kind)}. Use one of: {string.Join(", ", symbols)}.");
            }
            return new LiteralExpression { Value = SagaValue.Symbol(name.Path), Position = name.Position, Length = name.Length };
        }

        private bool IsUnresolvedName(SagaExpression expression) => expression is PathExpression path && !scope.TryGetPathType(path.Path, out _, out _);

        private bool Expect(SagaExpression expression, CheckedType actual, SagaTypeKind expected, string what)
        {
            if (actual.Kind == expected)
                return true;
            Report(expression, $"{what} needs {Describe(expected)}, but this is {Describe(actual.Kind)}.");
            return false;
        }

        private bool ExpectNumber(SagaExpression expression, CheckedType actual, string what)
        {
            if (IsNumber(actual.Kind))
                return true;
            Report(expression, $"{char.ToUpperInvariant(what[0])}{what[1..]} needs a number, but this is {Describe(actual.Kind)}.");
            return false;
        }

        private static string Count(int count) => count == 1 ? "1 argument" : $"{count} arguments";
    }
}
