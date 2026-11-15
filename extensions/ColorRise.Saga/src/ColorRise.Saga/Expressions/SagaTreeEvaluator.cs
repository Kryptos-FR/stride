namespace ColorRise.Saga.Expressions;

/// <summary>
/// Evaluates checked trees. <c>&amp;&amp;</c> and <c>||</c> short-circuit; whole numbers widen to numbers when mixed.
/// </summary>
internal static class SagaTreeEvaluator
{
    public static SagaValue Evaluate(SagaExpression expression, ISagaEvaluationContext context) => expression switch
    {
        LiteralExpression literal => literal.Value,
        PathExpression path => context.Resolve(path.Path),
        CallExpression call => Call(call, context),
        UnaryExpression { Operator: SagaUnaryOperator.Not } unary => SagaValue.Bool(!Evaluate(unary.Operand, context).AsBool()),
        UnaryExpression unary => Negate(Evaluate(unary.Operand, context)),
        BinaryExpression binary => Binary(binary, context),
        _ => throw new InvalidOperationException($"Unknown expression {expression.GetType().Name}."),
    };

    public static void Execute(SagaStatement statement, ISagaExecutionContext context)
    {
        switch (statement)
        {
            case AssignStatement assign:
                context.Assign(assign.Path, Evaluate(assign.Value, context));
                break;
            case RaiseStatement raise:
                context.Raise(raise.Event);
                break;
            case EmitStatement emit:
                context.Emit(emit.Signal, emit.Arguments.Select(x => Evaluate(x, context)).ToList());
                break;
            default:
                throw new InvalidOperationException($"Unknown statement {statement.GetType().Name}.");
        }
    }

    private static SagaValue Call(CallExpression call, ISagaEvaluationContext context)
    {
        switch (call.Function)
        {
            case SagaTypeChecker.RaisedFunction:
                return SagaValue.Bool(context.EventCount(Evaluate(call.Arguments[0], context).AsString()) > 0);
            case SagaTypeChecker.VisitedFunction:
                return SagaValue.Bool(context.Visits(Evaluate(call.Arguments[0], context).AsString()) > 0);
            case SagaTypeChecker.VisitsFunction:
                return SagaValue.Int(context.Visits(Evaluate(call.Arguments[0], context).AsString()));
            default:
                return context.Call(call.Function, call.Arguments.Select(x => Evaluate(x, context)).ToList());
        }
    }

    private static SagaValue Negate(SagaValue value) => value.Kind == SagaValueKind.Int ? SagaValue.Int(-value.AsInt()) : SagaValue.Float(-value.AsFloat());

    private static SagaValue Binary(BinaryExpression binary, ISagaEvaluationContext context)
    {
        switch (binary.Operator)
        {
            case SagaBinaryOperator.Or:
                return SagaValue.Bool(Evaluate(binary.Left, context).AsBool() || Evaluate(binary.Right, context).AsBool());
            case SagaBinaryOperator.And:
                return SagaValue.Bool(Evaluate(binary.Left, context).AsBool() && Evaluate(binary.Right, context).AsBool());
        }

        var left = Evaluate(binary.Left, context);
        var right = Evaluate(binary.Right, context);
        return binary.Operator switch
        {
            SagaBinaryOperator.Equal => SagaValue.Bool(AreEqual(left, right)),
            SagaBinaryOperator.NotEqual => SagaValue.Bool(!AreEqual(left, right)),
            SagaBinaryOperator.Less => SagaValue.Bool(left.AsFloat() < right.AsFloat()),
            SagaBinaryOperator.LessOrEqual => SagaValue.Bool(left.AsFloat() <= right.AsFloat()),
            SagaBinaryOperator.Greater => SagaValue.Bool(left.AsFloat() > right.AsFloat()),
            SagaBinaryOperator.GreaterOrEqual => SagaValue.Bool(left.AsFloat() >= right.AsFloat()),
            SagaBinaryOperator.Add when left.Kind == SagaValueKind.Int && right.Kind == SagaValueKind.Int => SagaValue.Int(left.AsInt() + right.AsInt()),
            SagaBinaryOperator.Add => SagaValue.Float(left.AsFloat() + right.AsFloat()),
            SagaBinaryOperator.Subtract when left.Kind == SagaValueKind.Int && right.Kind == SagaValueKind.Int => SagaValue.Int(left.AsInt() - right.AsInt()),
            SagaBinaryOperator.Subtract => SagaValue.Float(left.AsFloat() - right.AsFloat()),
            _ => throw new InvalidOperationException($"Unknown operator {binary.Operator}."),
        };
    }

    private static bool AreEqual(SagaValue left, SagaValue right) => IsNumber(left) && IsNumber(right) ? left.Number.Equals(right.Number) : left.Equals(right);

    private static bool IsNumber(SagaValue value) => value.Kind is SagaValueKind.Int or SagaValueKind.Float;
}
