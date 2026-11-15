namespace ColorRise.Saga.Expressions;

/// <summary>
/// Parses the conditions and the actions of a story.
/// </summary>
/// <remarks>
/// A condition is an expression: literals (<c>12</c>, <c>2.5</c>, <c>"text"</c>, <c>true</c>), names (<c>day</c>,
/// <c>quest.mine.state</c>), calls (<c>has_item("sword")</c>), <c>! -</c>, <c>+ -</c>, <c>&lt; &lt;= &gt; &gt;=</c>,
/// <c>== !=</c>, <c>&amp;&amp;</c>, <c>||</c> (by increasing precedence from the end), and parentheses. Actions are statements
/// separated by <c>;</c> or new lines: <c>path = value</c>, <c>raise event</c>, <c>emit signal(arguments)</c>. Parsing stops
/// at the first error.
/// </remarks>
public static class SagaParser
{
    private const string RaiseKeyword = "raise";
    private const string EmitKeyword = "emit";

    /// <summary>
    /// Parses a condition. An empty source gives a null expression, which means "always".
    /// </summary>
    public static ParseResult<SagaExpression?> ParseCondition(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return new ParseResult<SagaExpression?>(null, []);

        try
        {
            var parser = new Parser(SagaLexer.Tokenize(source).Where(x => x.Kind != SagaTokenKind.NewLine).ToList());
            var expression = parser.ParseExpression();
            parser.ExpectEnd();
            return new ParseResult<SagaExpression?>(expression, []);
        }
        catch (SagaParseException exception)
        {
            return new ParseResult<SagaExpression?>(null, [new SagaDiagnostic(exception.Position, exception.Length, exception.Message)]);
        }
    }

    /// <summary>
    /// Parses a list of actions. An empty source gives an empty list.
    /// </summary>
    public static ParseResult<List<SagaStatement>> ParseActions(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return new ParseResult<List<SagaStatement>>([], []);

        try
        {
            var parser = new Parser(SagaLexer.Tokenize(source));
            return new ParseResult<List<SagaStatement>>(parser.ParseStatements(), []);
        }
        catch (SagaParseException exception)
        {
            return new ParseResult<List<SagaStatement>>([], [new SagaDiagnostic(exception.Position, exception.Length, exception.Message)]);
        }
    }

    private sealed class Parser(List<SagaToken> tokens)
    {
        private int index;

        private SagaToken Current => tokens[index];

        public void ExpectEnd()
        {
            if (Current.Kind != SagaTokenKind.End)
                throw Unexpected(Current, "the end");
        }

        public List<SagaStatement> ParseStatements()
        {
            var statements = new List<SagaStatement>();
            while (true)
            {
                SkipSeparators();
                if (Current.Kind == SagaTokenKind.End)
                    return statements;

                statements.Add(ParseStatement());
                if (Current.Kind is not (SagaTokenKind.Semicolon or SagaTokenKind.NewLine or SagaTokenKind.End))
                    throw Unexpected(Current, "';' or a new line");
            }
        }

        public SagaExpression ParseExpression() => ParseOr();

        private void SkipSeparators()
        {
            while (Current.Kind is SagaTokenKind.Semicolon or SagaTokenKind.NewLine)
                index++;
        }

        private SagaStatement ParseStatement()
        {
            var start = Current;
            if (start.Kind == SagaTokenKind.Identifier && start.Text == RaiseKeyword && Peek(1).Kind == SagaTokenKind.Identifier)
            {
                index++;
                var name = Expect(SagaTokenKind.Identifier, "an event name");
                return new RaiseStatement { Event = name.Text, Position = start.Position, Length = End(name) - start.Position };
            }
            if (start.Kind == SagaTokenKind.Identifier && start.Text == EmitKeyword && Peek(1).Kind == SagaTokenKind.Identifier)
            {
                index++;
                var name = Expect(SagaTokenKind.Identifier, "a signal name");
                var arguments = new List<SagaExpression>();
                var end = End(name);
                if (Current.Kind == SagaTokenKind.OpenParenthesis)
                    end = ParseArguments(arguments);
                return new EmitStatement { Signal = name.Text, Arguments = arguments, Position = start.Position, Length = end - start.Position };
            }
            if (start.Kind == SagaTokenKind.Identifier && Peek(1).Kind == SagaTokenKind.Equal)
            {
                index += 2;
                var value = ParseExpression();
                return new AssignStatement { Path = start.Text, Value = value, Position = start.Position, Length = End(value) - start.Position };
            }
            throw new SagaParseException(start.Position, start.Length, "Expected a statement: 'name = value', 'raise event' or 'emit signal(...)'.");
        }

        private SagaExpression ParseOr() => ParseLeftAssociative(ParseAnd, kind => kind == SagaTokenKind.OrOr ? SagaBinaryOperator.Or : null);

        private SagaExpression ParseAnd() => ParseLeftAssociative(ParseEquality, kind => kind == SagaTokenKind.AndAnd ? SagaBinaryOperator.And : null);

        private SagaExpression ParseEquality() => ParseLeftAssociative(ParseComparison, kind => kind switch
        {
            SagaTokenKind.EqualEqual => SagaBinaryOperator.Equal,
            SagaTokenKind.BangEqual => SagaBinaryOperator.NotEqual,
            _ => null,
        });

        private SagaExpression ParseComparison() => ParseLeftAssociative(ParseAdditive, kind => kind switch
        {
            SagaTokenKind.Less => SagaBinaryOperator.Less,
            SagaTokenKind.LessEqual => SagaBinaryOperator.LessOrEqual,
            SagaTokenKind.Greater => SagaBinaryOperator.Greater,
            SagaTokenKind.GreaterEqual => SagaBinaryOperator.GreaterOrEqual,
            _ => null,
        });

        private SagaExpression ParseAdditive() => ParseLeftAssociative(ParseUnary, kind => kind switch
        {
            SagaTokenKind.Plus => SagaBinaryOperator.Add,
            SagaTokenKind.Minus => SagaBinaryOperator.Subtract,
            _ => null,
        });

        private SagaExpression ParseLeftAssociative(Func<SagaExpression> operand, Func<SagaTokenKind, SagaBinaryOperator?> toOperator)
        {
            var left = operand();
            while (toOperator(Current.Kind) is { } binaryOperator)
            {
                index++;
                var right = operand();
                left = new BinaryExpression { Operator = binaryOperator, Left = left, Right = right, Position = left.Position, Length = End(right) - left.Position };
            }
            return left;
        }

        private SagaExpression ParseUnary()
        {
            var start = Current;
            SagaUnaryOperator? unaryOperator = start.Kind switch
            {
                SagaTokenKind.Bang => SagaUnaryOperator.Not,
                SagaTokenKind.Minus => SagaUnaryOperator.Negate,
                _ => null,
            };
            if (unaryOperator is null)
                return ParsePrimary();

            index++;
            var operand = ParseUnary();
            return new UnaryExpression { Operator = unaryOperator.Value, Operand = operand, Position = start.Position, Length = End(operand) - start.Position };
        }

        private SagaExpression ParsePrimary()
        {
            var token = Current;
            switch (token.Kind)
            {
                case SagaTokenKind.Int:
                case SagaTokenKind.Float:
                case SagaTokenKind.String:
                    index++;
                    return new LiteralExpression { Value = token.Value, Position = token.Position, Length = token.Length };

                case SagaTokenKind.Identifier when token.Text is "true" or "false":
                    index++;
                    return new LiteralExpression { Value = SagaValue.Bool(token.Text == "true"), Position = token.Position, Length = token.Length };

                case SagaTokenKind.Identifier when Peek(1).Kind == SagaTokenKind.OpenParenthesis:
                {
                    index++;
                    var arguments = new List<SagaExpression>();
                    var end = ParseArguments(arguments);
                    return new CallExpression { Function = token.Text, Arguments = arguments, Position = token.Position, Length = end - token.Position };
                }

                case SagaTokenKind.Identifier:
                    index++;
                    return new PathExpression { Path = token.Text, Position = token.Position, Length = token.Length };

                case SagaTokenKind.OpenParenthesis:
                {
                    index++;
                    var inner = ParseExpression();
                    Expect(SagaTokenKind.CloseParenthesis, "')'");
                    return inner;
                }

                default:
                    throw Unexpected(token, "an expression");
            }
        }

        /// <summary>
        /// Parses <c>(a, b, ...)</c> from the opening parenthesis; returns the end of the closing one.
        /// </summary>
        private int ParseArguments(List<SagaExpression> arguments)
        {
            Expect(SagaTokenKind.OpenParenthesis, "'('");
            if (Current.Kind != SagaTokenKind.CloseParenthesis)
            {
                arguments.Add(ParseExpression());
                while (Current.Kind == SagaTokenKind.Comma)
                {
                    index++;
                    arguments.Add(ParseExpression());
                }
            }
            return End(Expect(SagaTokenKind.CloseParenthesis, "')' or ','"));
        }

        private SagaToken Expect(SagaTokenKind kind, string what)
        {
            var token = Current;
            if (token.Kind != kind)
                throw Unexpected(token, what);
            index++;
            return token;
        }

        private SagaToken Peek(int offset) => tokens[Math.Min(index + offset, tokens.Count - 1)];

        private static int End(SagaToken token) => token.Position + token.Length;

        private static int End(SagaExpression expression) => expression.Position + expression.Length;

        private static SagaParseException Unexpected(SagaToken token, string expected) => token.Kind switch
        {
            SagaTokenKind.End => new SagaParseException(token.Position, 0, $"Expected {expected}, but the text ends here."),
            SagaTokenKind.NewLine => new SagaParseException(token.Position, 1, $"Expected {expected}, but the line ends here."),
            _ => new SagaParseException(token.Position, token.Length, $"Expected {expected}, not '{token.Text}'."),
        };
    }
}
