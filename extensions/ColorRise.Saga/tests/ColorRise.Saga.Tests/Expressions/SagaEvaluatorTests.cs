using ColorRise.Saga.Expressions;
using Xunit;

namespace ColorRise.Saga.Tests.Expressions;

public sealed class SagaEvaluatorTests
{
    [Theory]
    [InlineData("day >= 12 && name == \"Mara\"", true)]
    [InlineData("day > 12 || !(speed <= 2.5)", false)]
    [InlineData("day - 2 == 10 && speed + 1 > 3", true)]
    [InlineData("-day < 0 && day == 12.0", true)]
    [InlineData("name != \"Mara\"", false)]
    public void EvaluatesLogicAndComparison(string source, bool expected)
    {
        var context = new FakeContext();

        Assert.Equal(expected, Condition(source).Evaluate(context));
    }

    [Fact]
    public void ShortCircuits()
    {
        var context = new FakeContext();

        Assert.False(Condition("false && f()").Evaluate(context));
        Assert.True(Condition("true || f()").Evaluate(context));
        Assert.Empty(context.Calls);
        Assert.True(Condition("true && f()").Evaluate(context));
        Assert.Single(context.Calls);
    }

    [Fact]
    public void BuiltInsReadEventsAndVisits()
    {
        var context = new FakeContext();
        context.Events["x"] = 1;
        context.VisitCounts["d"] = 2;

        Assert.True(Condition("raised(\"x\") && !raised(\"y\")").Evaluate(context));
        Assert.True(Condition("visits(\"d\") >= 2 && visited(\"d\") && !visited(\"e\")").Evaluate(context));
        Assert.Empty(context.Calls);
    }

    [Fact]
    public void ComparesSymbols()
    {
        var context = new FakeContext();
        context.Values["quest.q.state"] = SagaValue.Symbol("active");
        var condition = new SagaCondition
        {
            Source = "quest.q.state == active",
            Tree = new BinaryExpression
            {
                Operator = SagaBinaryOperator.Equal,
                Left = new PathExpression { Path = "quest.q.state" },
                Right = new LiteralExpression { Value = SagaValue.Symbol("active") },
            },
        };

        Assert.True(condition.Evaluate(context));
    }

    [Fact]
    public void ActionsAssignRaiseEmitInOrder()
    {
        var context = new FakeContext();
        var actions = Actions("day = day + 1; raise dawn\nemit give_item(\"sword\", day + 2)");

        actions.Execute(context);

        Assert.Equal(["assign day = 13", "raise dawn", "emit give_item(\"sword\", 15)"], context.Log);
    }

    [Fact]
    public void NullTreeIsTrue()
    {
        Assert.True(new SagaCondition().Evaluate(new FakeContext()));
        new SagaActions().Execute(new FakeContext());
    }

    private static SagaCondition Condition(string source)
    {
        var parsed = SagaParser.ParseCondition(source);
        Assert.Empty(parsed.Diagnostics);
        return new SagaCondition { Source = source, Tree = parsed.Value };
    }

    private static SagaActions Actions(string source)
    {
        var parsed = SagaParser.ParseActions(source);
        Assert.Empty(parsed.Diagnostics);
        return new SagaActions { Source = source, Statements = parsed.Value };
    }

    private sealed class FakeContext : ISagaExecutionContext
    {
        public Dictionary<string, SagaValue> Values { get; } = new()
        {
            ["day"] = SagaValue.Int(12),
            ["speed"] = SagaValue.Float(2.5f),
            ["name"] = SagaValue.String("Mara"),
        };

        public Dictionary<string, int> Events { get; } = [];

        public Dictionary<string, int> VisitCounts { get; } = [];

        public List<string> Calls { get; } = [];

        public List<string> Log { get; } = [];

        public SagaValue Resolve(string path) => Values[path];

        public SagaValue Call(string function, IReadOnlyList<SagaValue> arguments)
        {
            Calls.Add(function);
            return SagaValue.Bool(true);
        }

        public int EventCount(string name) => Events.GetValueOrDefault(name);

        public int Visits(string name) => VisitCounts.GetValueOrDefault(name);

        public void Assign(string path, SagaValue value)
        {
            Values[path] = value;
            Log.Add($"assign {path} = {value}");
        }

        public void Raise(string name) => Log.Add($"raise {name}");

        public void Emit(string signal, IReadOnlyList<SagaValue> arguments) => Log.Add($"emit {signal}({string.Join(", ", arguments)})");
    }
}
