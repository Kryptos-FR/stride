using ColorRise.Saga.Content;
using ColorRise.Saga.Expressions;
using Xunit;

namespace ColorRise.Saga.Tests.Expressions;

public sealed class SagaTypeCheckerTests
{
    private static readonly SagaTypeChecker Checker = new(new FakeScope());

    [Fact]
    public void UnknownNameIsReported()
    {
        var diagnostics = CheckCondition("gold > 3");

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("gold", diagnostic.Message);
        Assert.Equal(0, diagnostic.Position);
        Assert.Equal(4, diagnostic.Length);
    }

    [Fact]
    public void SymbolMustBelongToSet()
    {
        var diagnostics = CheckCondition("quest.q.state == finished");

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(17, diagnostic.Position);
        foreach (var state in new[] { "not_started", "available", "active", "succeeded", "failed" })
            Assert.Contains(state, diagnostic.Message);
    }

    [Fact]
    public void SymbolIsBoundToLiteral()
    {
        var expression = SagaParser.ParseCondition("act == act_2 && quest.q.state != active").Value;

        Assert.Empty(Checker.CheckCondition(expression));
        var and = Assert.IsType<BinaryExpression>(expression);
        var act = Assert.IsType<BinaryExpression>(and.Left);
        Assert.Equal(SagaValue.Symbol("act_2"), Assert.IsType<LiteralExpression>(act.Right).Value);
        var state = Assert.IsType<BinaryExpression>(and.Right);
        Assert.Equal(SagaValue.Symbol("active"), Assert.IsType<LiteralExpression>(state.Right).Value);
    }

    [Fact]
    public void ConditionMustBeBool()
    {
        var diagnostics = CheckCondition("day + 1");

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(0, diagnostic.Position);
        Assert.Equal(7, diagnostic.Length);
    }

    [Fact]
    public void FunctionArgumentsAreChecked()
    {
        Assert.Empty(CheckCondition("has_item(\"sword\", 2)"));

        var count = Assert.Single(CheckCondition("has_item(\"sword\")"));
        Assert.Contains("has_item", count.Message);
        Assert.Contains("2", count.Message);

        var type = Assert.Single(CheckCondition("has_item(2, 3)"));
        Assert.Equal(9, type.Position);

        Assert.Contains("unknown_function", Assert.Single(CheckCondition("unknown_function()")).Message);
    }

    [Fact]
    public void BuiltInsAreKnown()
    {
        Assert.Empty(CheckCondition("raised(\"mine_collapsed\") && visited(\"greeting\") && visits(\"greeting\") >= 2"));
        Assert.Contains("nothing_happened", Assert.Single(CheckCondition("raised(\"nothing_happened\")")).Message);
    }

    [Fact]
    public void ScenarioStateIsNotAssignable()
    {
        var diagnostic = Assert.Single(CheckActions("scenario.s.state = done"));

        Assert.Contains("scenario.s.state", diagnostic.Message);
        Assert.Empty(CheckActions("quest.q.state = succeeded; quest.q.stage = 2; day = day + 1"));
    }

    [Fact]
    public void AssignedValueMustMatch()
    {
        var diagnostic = Assert.Single(CheckActions("day = \"monday\""));

        Assert.Equal(6, diagnostic.Position);
    }

    [Fact]
    public void RaiseNeedsDeclaredEvent()
    {
        Assert.Empty(CheckActions("raise mine_collapsed"));

        var diagnostic = Assert.Single(CheckActions("raise unknown_event"));
        Assert.Contains("unknown_event", diagnostic.Message);
    }

    [Fact]
    public void EmitChecksSignalParameters()
    {
        Assert.Empty(CheckActions("emit give_item(\"sword\", 2)"));
        Assert.Single(CheckActions("emit give_item(\"sword\")"));
        Assert.Single(CheckActions("emit give_item(\"sword\", \"two\")"));
        Assert.Contains("unknown_signal", Assert.Single(CheckActions("emit unknown_signal")).Message);
    }

    [Fact]
    public void IntWidensToFloat()
    {
        Assert.Empty(CheckCondition("speed > 2"));
        Assert.Empty(CheckCondition("speed + 1 >= day"));
        Assert.Empty(CheckActions("speed = 2"));
        Assert.Single(CheckActions("day = speed"));
    }

    [Fact]
    public void ErrorsDoNotCascade()
    {
        Assert.Single(CheckCondition("!(gold > 3) && day > 1"));
        Assert.Contains("quest.nothing.state", Assert.Single(CheckCondition("quest.nothing.state == available")).Message);
        Assert.Contains("quest.nothing.state", Assert.Single(CheckCondition("available != quest.nothing.state")).Message);
    }

    [Fact]
    public void NullConditionIsValid()
    {
        Assert.Empty(Checker.CheckCondition(null));
    }

    private static IReadOnlyList<SagaDiagnostic> CheckCondition(string source)
    {
        var parsed = SagaParser.ParseCondition(source);
        Assert.Empty(parsed.Diagnostics);
        return Checker.CheckCondition(parsed.Value);
    }

    private static IReadOnlyList<SagaDiagnostic> CheckActions(string source)
    {
        var parsed = SagaParser.ParseActions(source);
        Assert.Empty(parsed.Diagnostics);
        return Checker.CheckActions(parsed.Value);
    }

    private sealed class FakeScope : ISagaScope
    {
        private readonly Dictionary<string, (SagaTypeKind Type, string? Set)> paths = new()
        {
            ["day"] = (SagaTypeKind.Int, null),
            ["speed"] = (SagaTypeKind.Float, null),
            ["act"] = (SagaTypeKind.Act, SagaSymbolSets.Acts),
            ["quest.q.state"] = (SagaTypeKind.QuestState, SagaSymbolSets.QuestStates),
            ["quest.q.stage"] = (SagaTypeKind.Int, null),
            ["scenario.s.state"] = (SagaTypeKind.ScenarioState, SagaSymbolSets.ScenarioStates),
            ["scenario.s.outcome"] = (SagaTypeKind.Outcome, SagaSymbolSets.Outcomes("s")),
        };

        public bool TryGetPathType(string path, out SagaTypeKind type, out string? symbolSet)
        {
            var found = paths.TryGetValue(path, out var entry);
            (type, symbolSet) = entry;
            return found;
        }

        public FunctionDeclaration? FindFunction(string name) => name == "has_item"
            ? new FunctionDeclaration { Name = name, Parameters = [SagaTypeKind.String, SagaTypeKind.Int], ReturnType = SagaTypeKind.Bool }
            : null;

        public bool IsEvent(string name) => name == "mine_collapsed";

        public SignalDeclaration? FindSignal(string name) => name == "give_item"
            ? new SignalDeclaration { Name = name, Parameters = [SagaTypeKind.String, SagaTypeKind.Int] }
            : null;

        public bool IsAssignable(string path) => path is "day" or "speed" or "quest.q.state" or "quest.q.stage";

        public IReadOnlyCollection<string> GetSymbols(string symbolSet) => symbolSet switch
        {
            SagaSymbolSets.QuestStates => SagaSymbolSets.QuestStateNames,
            SagaSymbolSets.ScenarioStates => SagaSymbolSets.ScenarioStateNames,
            SagaSymbolSets.Acts => ["act_1", "act_2"],
            "scenario.s.outcome" => ["easier", "harder"],
            _ => [],
        };
    }
}
