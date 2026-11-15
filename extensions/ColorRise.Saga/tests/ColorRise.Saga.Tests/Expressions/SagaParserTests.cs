using ColorRise.Saga.Expressions;
using Xunit;

namespace ColorRise.Saga.Tests.Expressions;

public sealed class SagaParserTests
{
    [Fact]
    public void ParsesComparisonAndLogic()
    {
        var result = SagaParser.ParseCondition("quest.secure_position.state == failed && day >= 12");

        Assert.Empty(result.Diagnostics);
        var and = Assert.IsType<BinaryExpression>(result.Value);
        Assert.Equal(SagaBinaryOperator.And, and.Operator);
        var equal = Assert.IsType<BinaryExpression>(and.Left);
        Assert.Equal(SagaBinaryOperator.Equal, equal.Operator);
        Assert.Equal("quest.secure_position.state", Assert.IsType<PathExpression>(equal.Left).Path);
        Assert.Equal("failed", Assert.IsType<PathExpression>(equal.Right).Path);
        var greater = Assert.IsType<BinaryExpression>(and.Right);
        Assert.Equal(SagaBinaryOperator.GreaterOrEqual, greater.Operator);
        Assert.Equal("day", Assert.IsType<PathExpression>(greater.Left).Path);
        Assert.Equal(SagaValue.Int(12), Assert.IsType<LiteralExpression>(greater.Right).Value);
    }

    [Fact]
    public void ParsesCallWithArguments()
    {
        var result = SagaParser.ParseCondition("has_item(\"sword\", 2)");

        Assert.Empty(result.Diagnostics);
        var call = Assert.IsType<CallExpression>(result.Value);
        Assert.Equal("has_item", call.Function);
        Assert.Equal(2, call.Arguments.Count);
        Assert.Equal(SagaValue.String("sword"), Assert.IsType<LiteralExpression>(call.Arguments[0]).Value);
        Assert.Equal(SagaValue.Int(2), Assert.IsType<LiteralExpression>(call.Arguments[1]).Value);
    }

    [Fact]
    public void PrecedenceAndBeforeOr()
    {
        var result = SagaParser.ParseCondition("a || b && !c");

        Assert.Empty(result.Diagnostics);
        var or = Assert.IsType<BinaryExpression>(result.Value);
        Assert.Equal(SagaBinaryOperator.Or, or.Operator);
        Assert.IsType<PathExpression>(or.Left);
        var and = Assert.IsType<BinaryExpression>(or.Right);
        Assert.Equal(SagaBinaryOperator.And, and.Operator);
        var not = Assert.IsType<UnaryExpression>(and.Right);
        Assert.Equal(SagaUnaryOperator.Not, not.Operator);
    }

    [Fact]
    public void ParenthesesAndArithmetic()
    {
        var result = SagaParser.ParseCondition("(gold - 5 + 2.5) > 10");

        Assert.Empty(result.Diagnostics);
        var greater = Assert.IsType<BinaryExpression>(result.Value);
        var plus = Assert.IsType<BinaryExpression>(greater.Left);
        Assert.Equal(SagaBinaryOperator.Add, plus.Operator);
        Assert.Equal(SagaBinaryOperator.Subtract, Assert.IsType<BinaryExpression>(plus.Left).Operator);
        Assert.Equal(SagaValue.Float(2.5f), Assert.IsType<LiteralExpression>(plus.Right).Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyConditionIsNull(string? source)
    {
        var result = SagaParser.ParseCondition(source);

        Assert.Empty(result.Diagnostics);
        Assert.Null(result.Value);
    }

    [Fact]
    public void BooleanLiterals()
    {
        var result = SagaParser.ParseCondition("true != false");

        Assert.Empty(result.Diagnostics);
        var notEqual = Assert.IsType<BinaryExpression>(result.Value);
        Assert.Equal(SagaValue.Bool(true), Assert.IsType<LiteralExpression>(notEqual.Left).Value);
        Assert.Equal(SagaValue.Bool(false), Assert.IsType<LiteralExpression>(notEqual.Right).Value);
    }

    [Fact]
    public void ParsesActions()
    {
        var result = SagaParser.ParseActions("gold = gold + 10; raise mine_collapsed\nemit give_item(\"sword\", 2)");

        Assert.Empty(result.Diagnostics);
        Assert.Equal(3, result.Value.Count);
        var assign = Assert.IsType<AssignStatement>(result.Value[0]);
        Assert.Equal("gold", assign.Path);
        Assert.IsType<BinaryExpression>(assign.Value);
        Assert.Equal("mine_collapsed", Assert.IsType<RaiseStatement>(result.Value[1]).Event);
        var emit = Assert.IsType<EmitStatement>(result.Value[2]);
        Assert.Equal("give_item", emit.Signal);
        Assert.Equal(2, emit.Arguments.Count);
    }

    [Fact]
    public void EmptyActionsAreEmpty()
    {
        var result = SagaParser.ParseActions("  \n ; ");

        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.Value);
    }

    [Fact]
    public void ReportsPositionOfError()
    {
        var result = SagaParser.ParseCondition("day >= ");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(7, diagnostic.Position);
        Assert.Contains("expression", diagnostic.Message);
    }

    [Fact]
    public void ReportsUnterminatedString()
    {
        var result = SagaParser.ParseCondition("name == \"Mara");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(8, diagnostic.Position);
        Assert.Contains("string", diagnostic.Message);
    }

    [Fact]
    public void ReportsTrailingTokens()
    {
        var result = SagaParser.ParseCondition("a b");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(2, diagnostic.Position);
    }

    [Fact]
    public void ReportsAssignmentToNonPath()
    {
        var result = SagaParser.ParseActions("3 = gold");

        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(0, result.Diagnostics[0].Position);
    }

    [Fact]
    public void NodesKeepTheirPosition()
    {
        var result = SagaParser.ParseCondition("a && bc");

        var and = Assert.IsType<BinaryExpression>(result.Value);
        Assert.Equal(5, and.Right.Position);
        Assert.Equal(2, and.Right.Length);
    }
}
