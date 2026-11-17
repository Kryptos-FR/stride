using ColorRise.Skein.Assets.Text;

namespace ColorRise.Skein.Tests.Text;

/// <summary>
/// Conditions and <c>&lt;&lt;set&gt;&gt;</c> commands read as rows with Yarn Spinner's own parser, and written back.
/// </summary>
public sealed class YarnConditionTests
{
    [Fact]
    public void A_condition_reads_as_terms_joined_by_one_joiner()
    {
        var condition = YarnCondition.Read("$act == Act.Wreck and visited(\"Pier\")");

        Assert.NotNull(condition);
        Assert.Equal(YarnJoiner.And, condition.Joiner);
        Assert.Equal(2, condition.Terms.Count);
        var (act, pier) = (condition.Terms[0], condition.Terms[1]);
        Assert.Equal(("$act", false, YarnComparison.Equal, "Act.Wreck"), (act.Subject, act.IsFunction, act.Comparison, act.Value));
        Assert.Equal(("visited", true, YarnComparison.IsTrue, null), (pier.Subject, pier.IsFunction, pier.Comparison, pier.Value));
        Assert.Equal(["\"Pier\""], pier.Arguments);
        Assert.Equal("visited(\"Pier\")", pier.SubjectText);
        Assert.Equal("$act == Act.Wreck and visited(\"Pier\")", condition.Write());
    }

    [Theory]
    [InlineData("$odile_here", "$odile_here", YarnComparison.IsTrue, null)]
    [InlineData("not $lamp_lit", "$lamp_lit", YarnComparison.IsFalse, null)]
    [InlineData("!$lamp_lit", "$lamp_lit", YarnComparison.IsFalse, null)]
    [InlineData("not visited(\"JossCatch\")", "visited", YarnComparison.IsFalse, null)]
    [InlineData("$wren_trust >= 2", "$wren_trust", YarnComparison.GreaterOrEqual, "2")]
    [InlineData("$a gte 2", "$a", YarnComparison.GreaterOrEqual, "2")]
    [InlineData("$a > 2", "$a", YarnComparison.Greater, "2")]
    [InlineData("$a gt 2", "$a", YarnComparison.Greater, "2")]
    [InlineData("$a < 2", "$a", YarnComparison.Less, "2")]
    [InlineData("$a lt 2", "$a", YarnComparison.Less, "2")]
    [InlineData("$a <= 2", "$a", YarnComparison.LessOrEqual, "2")]
    [InlineData("$a lte 2", "$a", YarnComparison.LessOrEqual, "2")]
    [InlineData("$a is 2", "$a", YarnComparison.Equal, "2")]
    [InlineData("$a eq 2", "$a", YarnComparison.Equal, "2")]
    [InlineData("$side != Side.Neither", "$side", YarnComparison.NotEqual, "Side.Neither")]
    [InlineData("$a neq .Neither", "$a", YarnComparison.NotEqual, ".Neither")]
    [InlineData("$coins > $price + 1", "$coins", YarnComparison.Greater, "$price + 1")]
    [InlineData("$name == \"Odile and Maud\"", "$name", YarnComparison.Equal, "\"Odile and Maud\"")]
    [InlineData("has_item(\"oil\") == true", "has_item", YarnComparison.Equal, "true")]
    [InlineData("($a == 2)", "$a", YarnComparison.Equal, "2")]
    [InlineData("  $a  ", "$a", YarnComparison.IsTrue, null)]
    public void A_term_is_a_subject_and_what_it_is_compared_with(string text, string subject, YarnComparison comparison, string? value)
    {
        var term = Assert.Single(YarnCondition.Read(text)!.Terms);

        Assert.Equal((subject, comparison, value), (term.Subject, term.Comparison, term.Value));
    }

    [Theory]
    [InlineData("$a is 2 && !$b", "$a == 2 and not $b")]
    [InlineData("$a eq 2 || $b neq 3", "$a == 2 or $b != 3")]
    [InlineData("($a lt 2) and ($b gte 3)", "$a < 2 and $b >= 3")]
    [InlineData("($a and $b)", "$a and $b")]
    [InlineData("dice( 6 ) >  3", "dice(6) > 3")]
    [InlineData("max($a,$b + 1) == 3", "max($a, $b + 1) == 3")]
    [InlineData("", "")]
    public void A_condition_is_written_with_the_usual_operators(string text, string written)
    {
        Assert.Equal(written, YarnCondition.Read(text)!.Write());
    }

    [Theory]
    [InlineData("$a and $b or $c")]
    [InlineData("$a xor $b")]
    [InlineData("not ($a and $b)")]
    [InlineData("$a and ($b or $c)")]
    [InlineData("$a + 1 > 2")]
    [InlineData("3 == $a")]
    [InlineData("not $a == 3")]
    [InlineData("\"text\"")]
    [InlineData("true")]
    [InlineData("$a and")]
    [InlineData("$a >> 2")]
    [InlineData("once")]
    public void A_condition_that_is_not_terms_stays_text(string text)
    {
        Assert.Null(YarnCondition.Read(text));
    }

    [Fact]
    public void A_condition_is_made_of_terms()
    {
        var condition = new YarnCondition
        {
            Joiner = YarnJoiner.Or,
            Terms =
            [
                new YarnConditionTerm { Subject = "$act", Comparison = YarnComparison.Equal, Value = "Act.Fair" },
                new YarnConditionTerm { Subject = "visited", Arguments = ["\"Mine\""], Comparison = YarnComparison.IsFalse },
                new YarnConditionTerm { Subject = "$coins", Comparison = YarnComparison.LessOrEqual, Value = "3" },
            ],
        };

        Assert.Equal("$act == Act.Fair or not visited(\"Mine\") or $coins <= 3", condition.Write());

        // A term without its value says nothing yet: it is left out
        var unfinished = condition with { Terms = [condition.Terms[0], new YarnConditionTerm { Subject = "$coins", Comparison = YarnComparison.Less }] };
        Assert.Equal("$act == Act.Fair", unfinished.Write());
    }

    [Theory]
    [InlineData("set $met to true", "$met", YarnAssignmentOperator.Set, "true", "set $met to true")]
    [InlineData("set $met = true", "$met", YarnAssignmentOperator.Set, "true", "set $met to true")]
    [InlineData("set  $coins  +=  2", "$coins", YarnAssignmentOperator.Add, "2", "set $coins += 2")]
    [InlineData("set $coins -= $price * 2", "$coins", YarnAssignmentOperator.Subtract, "$price * 2", "set $coins -= $price * 2")]
    [InlineData("set $coins *= 2", "$coins", YarnAssignmentOperator.Multiply, "2", "set $coins *= 2")]
    [InlineData("set $coins /= 2", "$coins", YarnAssignmentOperator.Divide, "2", "set $coins /= 2")]
    [InlineData("set $coins %= 2", "$coins", YarnAssignmentOperator.Remainder, "2", "set $coins %= 2")]
    [InlineData("set $act to Act.Wreck", "$act", YarnAssignmentOperator.Set, "Act.Wreck", "set $act to Act.Wreck")]
    [InlineData("set $quest to .Active", "$quest", YarnAssignmentOperator.Set, ".Active", "set $quest to .Active")]
    public void A_set_command_reads_as_a_variable_an_operator_and_a_value(string text, string variable, YarnAssignmentOperator op, string value, string written)
    {
        var assignment = YarnAssignment.Read(text);

        Assert.NotNull(assignment);
        Assert.Equal((variable, op, value), (assignment.Variable, assignment.Operator, assignment.Value));
        Assert.Equal(written, assignment.Write());
    }

    [Theory]
    [InlineData("set $met")]
    [InlineData("set met to true")]
    [InlineData("jump Pier")]
    [InlineData("give letter 3")]
    [InlineData("set $a to 1 >> <<set $b to 2")]
    public void A_command_that_is_not_a_set_is_no_assignment(string text)
    {
        Assert.Null(YarnAssignment.Read(text));
    }

    [Fact]
    public void Nearly_every_condition_of_the_samples_reads_as_rows_and_writes_back_as_written()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "ColorRise.Skein.slnx")))
        {
            root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("The Skein folder is not above the tests.");
        }

        var conditions = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "sample"), "*.yarn", SearchOption.AllDirectories))
        {
            var document = YarnDocument.Parse(File.ReadAllText(file));
            foreach (var node in document.Nodes)
            {
                conditions.AddRange(node.Headers.Where(h => h.Key == "when" && h.Value is not ("once" or "always")).Select(h => h.Value.StartsWith("once if ", StringComparison.Ordinal) ? h.Value[8..] : h.Value));
                // Of a line or an option, the one after it; of an if, an elseif or a once if, its own
                conditions.AddRange(document.StatementsOf(node).Select(s => s.Condition).OfType<string>());
            }
        }

        Assert.True(conditions.Count > 100, $"{conditions.Count} conditions");

        // The few that stay text mix "and" and "or" with brackets
        var text = conditions.Where(c => YarnCondition.Read(c) is null).ToList();
        Assert.All(text, c => Assert.True(c.Contains(" and ", StringComparison.Ordinal) && c.Contains(" or ", StringComparison.Ordinal) && c.Contains('('), c));
        Assert.True(text.Count * 50 < conditions.Count, $"{text.Count} of {conditions.Count} stay text");
        Assert.All(conditions.Except(text), c => Assert.Equal(c.Trim(), YarnCondition.Read(c)!.Write()));
    }
}
