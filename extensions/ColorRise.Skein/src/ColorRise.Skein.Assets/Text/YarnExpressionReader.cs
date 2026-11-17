using System.IO;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Yarn.Compiler;

namespace ColorRise.Skein.Assets.Text;

/// <summary>
/// Reads conditions and <c>&lt;&lt;set&gt;&gt;</c> commands with Yarn Spinner's own parser: the text goes in a node of its
/// own, as it would be in a script, and the syntax tree gives the rows.
/// </summary>
internal static class YarnExpressionReader
{
    private const string ConditionBefore = "title: A\n---\n<<if ";
    private const string CommandBefore = "title: A\n---\n<<";

    public static YarnCondition? ReadCondition(string expression)
    {
        if (expression.Contains('\n') || expression.Contains('\r'))
            return null;

        var source = ConditionBefore + expression + ">>\n<<endif>>\n===\n";
        if (Parse(source) is not { } dialogue
            || Statements(dialogue) is not [{ } statement] || statement.if_statement()?.if_clause() is not { } clause || clause.expression() is not { } root
            || !Covers(root, ConditionBefore.Length, expression))
            return null;

        var terms = new List<YarnConditionTerm>();
        YarnJoiner? joiner = null;
        return Collect(Unwrap(root), source, terms, ref joiner) ? new YarnCondition { Terms = terms, Joiner = joiner ?? YarnJoiner.And } : null;
    }

    public static YarnAssignment? ReadAssignment(string command)
    {
        if (command.Contains('\n') || command.Contains('\r') || command.Contains(">>", StringComparison.Ordinal))
            return null;

        var source = CommandBefore + command + ">>\n===\n";
        if (Parse(source) is not { } dialogue || Statements(dialogue) is not [{ } statement] || statement.set_statement() is not { } set
            || set.variable() is not { } variable || set.expression() is not { } value || set.op is null)
            return null;

        YarnAssignmentOperator? op = set.op.Type switch
        {
            YarnSpinnerParser.OPERATOR_ASSIGNMENT => YarnAssignmentOperator.Set,
            YarnSpinnerParser.OPERATOR_MATHS_ADDITION_EQUALS => YarnAssignmentOperator.Add,
            YarnSpinnerParser.OPERATOR_MATHS_SUBTRACTION_EQUALS => YarnAssignmentOperator.Subtract,
            YarnSpinnerParser.OPERATOR_MATHS_MULTIPLICATION_EQUALS => YarnAssignmentOperator.Multiply,
            YarnSpinnerParser.OPERATOR_MATHS_DIVISION_EQUALS => YarnAssignmentOperator.Divide,
            YarnSpinnerParser.OPERATOR_MATHS_MODULUS_EQUALS => YarnAssignmentOperator.Remainder,
            _ => null,
        };

        return op is { } known ? new YarnAssignment { Variable = TextOf(variable, source), Operator = known, Value = TextOf(value, source) } : null;
    }

    private static YarnSpinnerParser.DialogueContext? Parse(string source)
    {
        var errors = new ErrorCounter();
        var lexer = new YarnSpinnerLexer(new AntlrInputStream(source));
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(errors);
        var parser = new YarnSpinnerParser(new CommonTokenStream(lexer));
        parser.RemoveErrorListeners();
        parser.AddErrorListener(errors);
        var dialogue = parser.dialogue();
        return errors.Count == 0 && parser.NumberOfSyntaxErrors == 0 ? dialogue : null;
    }

    private static YarnSpinnerParser.StatementContext[] Statements(YarnSpinnerParser.DialogueContext dialogue)
        => dialogue.node() is [{ } node] && node.body() is { } body ? body.statement() : [];

    // Whether the tree read the whole text: a ">>" in it would end the command early and leave the rest as a line
    private static bool Covers(ParserRuleContext context, int offset, string text)
    {
        var start = offset + (text.Length - text.TrimStart().Length);
        var end = offset + text.TrimEnd().Length;
        return context.Start.StartIndex == start && context.Stop is { } stop && stop.StopIndex + 1 == end;
    }

    private static string TextOf(ParserRuleContext context, string source)
        => context.Stop is { } stop && stop.StopIndex >= context.Start.StartIndex ? source[context.Start.StartIndex..(stop.StopIndex + 1)].Trim() : "";

    private static YarnSpinnerParser.ExpressionContext Unwrap(YarnSpinnerParser.ExpressionContext expression)
    {
        while (expression is YarnSpinnerParser.ExpParensContext parens && parens.expression() is { } inner)
        {
            expression = inner;
        }

        return expression;
    }

    // The terms of a chain of one joiner; a group in brackets inside the chain is not a term
    private static bool Collect(YarnSpinnerParser.ExpressionContext expression, string source, List<YarnConditionTerm> terms, ref YarnJoiner? joiner)
    {
        if (expression is YarnSpinnerParser.ExpAndOrXorContext chain)
        {
            YarnJoiner? kind = chain.op?.Type switch
            {
                YarnSpinnerParser.OPERATOR_LOGICAL_AND => YarnJoiner.And,
                YarnSpinnerParser.OPERATOR_LOGICAL_OR => YarnJoiner.Or,
                _ => null,
            };
            if (kind is null || (joiner is { } known && known != kind) || chain.expression() is not [{ } left, { } right])
                return false;

            joiner = kind;
            return Collect(left, source, terms, ref joiner) && Term(Unwrap(right), source) is { } last && Add(terms, last);
        }

        return Term(Unwrap(expression), source) is { } term && Add(terms, term);
    }

    private static bool Add(List<YarnConditionTerm> terms, YarnConditionTerm term)
    {
        terms.Add(term);
        return true;
    }

    private static YarnConditionTerm? Term(YarnSpinnerParser.ExpressionContext expression, string source)
    {
        switch (expression)
        {
            case YarnSpinnerParser.ExpValueContext value:
                return Subject(value, source, YarnComparison.IsTrue, null);

            case YarnSpinnerParser.ExpNotContext not when not.expression() is { } inner && Unwrap(inner) is YarnSpinnerParser.ExpValueContext value:
                return Subject(value, source, YarnComparison.IsFalse, null);

            case YarnSpinnerParser.ExpEqualityContext or YarnSpinnerParser.ExpComparisonContext:
                if (expression.ChildCount != 3 || expression.GetChild(0) is not YarnSpinnerParser.ExpressionContext left || expression.GetChild(1) is not ITerminalNode op
                    || expression.GetChild(2) is not YarnSpinnerParser.ExpressionContext right || Unwrap(left) is not YarnSpinnerParser.ExpValueContext subject)
                    return null;

                YarnComparison? comparison = op.Symbol.Type switch
                {
                    YarnSpinnerParser.OPERATOR_LOGICAL_EQUALS => YarnComparison.Equal,
                    YarnSpinnerParser.OPERATOR_LOGICAL_NOT_EQUALS => YarnComparison.NotEqual,
                    YarnSpinnerParser.OPERATOR_LOGICAL_LESS => YarnComparison.Less,
                    YarnSpinnerParser.OPERATOR_LOGICAL_LESS_THAN_EQUALS => YarnComparison.LessOrEqual,
                    YarnSpinnerParser.OPERATOR_LOGICAL_GREATER => YarnComparison.Greater,
                    YarnSpinnerParser.OPERATOR_LOGICAL_GREATER_THAN_EQUALS => YarnComparison.GreaterOrEqual,
                    _ => null,
                };
                return comparison is { } known ? Subject(subject, source, known, TextOf(right, source)) : null;

            default:
                return null;
        }
    }

    // A term's subject is a variable or a call of a function
    private static YarnConditionTerm? Subject(YarnSpinnerParser.ExpValueContext expression, string source, YarnComparison comparison, string? value)
    {
        switch (expression.value())
        {
            case YarnSpinnerParser.ValueVarContext variable:
                return new YarnConditionTerm { Subject = TextOf(variable, source), Comparison = comparison, Value = value };

            case YarnSpinnerParser.ValueFuncContext { } function when function.function_call() is { } call && call.FUNC_ID() is { } name:
                return new YarnConditionTerm
                {
                    Subject = name.GetText(),
                    Arguments = [.. call.expression().Select(a => TextOf(a, source))],
                    Comparison = comparison,
                    Value = value,
                };

            default:
                return null;
        }
    }

    private sealed class ErrorCounter : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        public int Count { get; private set; }

        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e) => Count++;

        public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e) => Count++;
    }
}
