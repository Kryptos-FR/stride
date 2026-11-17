using ColorRise.Skein.Assets.Text;
using ColorRise.Skein.Editor.Conditions;
using ColorRise.Skein.Editor.ViewModels;

namespace ColorRise.Skein.Editor.Tests;

/// <summary>
/// The condition builder and the <c>&lt;&lt;set&gt;&gt;</c> builder: rows that know the variables, functions and nodes
/// of the project, and write Yarn into the text.
/// </summary>
public sealed class ConditionBuilderTests
{
    private const string Act1 =
        "title: Vars\n---\n"
        + "<<enum Act>>\n    <<case Arrival>>\n    <<case Wreck>>\n<<endenum>>\n"
        + "<<declare $met = false>>\n<<declare $coins = 0>>\n<<declare $act = Act.Arrival>>\n<<declare $name = \"\">>\n"
        + "<<declare $rich = $coins > 10>>\n"
        + "===\n"
        + "title: Start\n---\n"
        + "Odile: Hello. #line:hello\n"
        + "-> Ask <<if $met>> #line:ask\n"
        + "    Odile: Sure.\n"
        + "-> Leave <<if $met and $coins > 1 or $act == Act.Wreck>>\n"
        + "<<set $coins to 1>>\n"
        + "<<give letter>>\n"
        + "===\n"
        + "title: Pier\n---\nFisher: Hi.\n===\n";

    private static async Task<(SkeinSession Session, YarnScriptEditorViewModel Editor)> Open()
    {
        var session = await SkeinSession.OpenAsync(SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1));
        var editor = session.Edit("Dialogue/Act1");
        await session.Analysis.WhenIdle();
        return (session, editor);
    }

    [Fact]
    public async Task The_builders_know_the_variables_functions_and_nodes_of_the_project()
    {
        var (session, editor) = await Open();
        await using var _ = session;
        var knowledge = editor.Knowledge;

        Assert.Equal(["$act", "$coins", "$met", "$name", "$rich"], knowledge.Variables.Select(v => v.Name));
        Assert.Equal(("Act", "number", "bool", "string"), (knowledge.TypeOf("$act"), knowledge.TypeOf("$coins"), knowledge.TypeOf("$met"), knowledge.TypeOf("$name")));
        Assert.Equal("bool", knowledge.TypeOf("visited"));
        Assert.Equal(["Act.Arrival", "Act.Wreck", "$act"], knowledge.ValuesOf("Act"));
        Assert.Equal(["true", "false", "$met", "$rich"], knowledge.ValuesOf("bool"));
        Assert.Contains("\"Pier\"", knowledge.ArgumentValuesOf("visited", 0));

        // A value typed plainly is made fit its type
        Assert.Equal("Act.Wreck", knowledge.FitValue("Act", " wreck "));
        Assert.Equal("\"Odile \\\"the post\\\"\"", knowledge.FitValue("string", "Odile \"the post\""));
        Assert.Equal("$name", knowledge.FitValue("string", "$name"));
        Assert.Equal("3", knowledge.FitValue("number", "3"));
    }

    [Fact]
    public async Task The_condition_of_an_option_is_edited_as_rows_in_one_step()
    {
        var (session, editor) = await Open();
        await using var _ = session;
        var start = editor.Cards.Single(c => c.Title == "Start");
        var ask = start.Rows.Single(r => r.Display == "Ask");

        var builder = editor.EditCondition(ask)!;
        var met = Assert.Single(builder.Terms);
        Assert.Equal(("$met", YarnComparison.IsTrue), (met.Subject, met.Comparison));
        Assert.Equal([YarnComparison.IsTrue, YarnComparison.IsFalse, YarnComparison.Equal, YarnComparison.NotEqual], met.Comparisons);

        // A new row takes the comparisons of its type
        var coins = builder.AddTerm();
        coins.Subject = "$coins";
        Assert.Equal(YarnComparison.Equal, coins.Comparison);
        Assert.Contains(YarnComparison.GreaterOrEqual, coins.Comparisons);
        Assert.Equal("$met", builder.Condition);
        coins.Comparison = YarnComparison.GreaterOrEqual;
        coins.Value = "2";
        Assert.Equal("$met and $coins >= 2", builder.Condition);
        builder.Joiner = YarnJoiner.Or;
        Assert.Equal("$met or $coins >= 2", builder.Condition);

        Assert.True(editor.CommitCondition(ask, builder));
        Assert.Contains("-> Ask <<if $met or $coins >= 2>> #line:ask\n", editor.TextBuffer.Text);
        Assert.Equal(["Edit condition"], session.Steps);

        // No row, no condition
        builder = editor.EditCondition(ask)!;
        foreach (var term in builder.Terms.ToList())
        {
            builder.RemoveTerm(term);
        }

        Assert.Null(builder.Condition);
        Assert.True(editor.CommitCondition(ask, builder));
        Assert.Contains("-> Ask #line:ask\n", editor.TextBuffer.Text);

        session.UndoRedo.Undo();
        session.UndoRedo.Undo();
        Assert.Equal(Act1, editor.TextBuffer.Text);
    }

    [Fact]
    public async Task A_line_gets_a_condition_on_a_function_that_runs_once()
    {
        var (session, editor) = await Open();
        await using var _ = session;
        var hello = editor.Cards.Single(c => c.Title == "Start").Rows[0];

        var builder = editor.EditCondition(hello)!;
        Assert.Empty(builder.Terms);
        Assert.True(builder.AllowsOnce);

        // A function gets a box for each parameter; the nodes are offered for visited
        var term = builder.AddTerm();
        Assert.Equal("$act", term.Subject);
        term.Value = "Wreck";
        term.Subject = "visited";
        Assert.True(term.IsFunction);
        Assert.Equal(YarnComparison.IsTrue, term.Comparison);
        Assert.Equal("", term.Value);
        var node = Assert.Single(term.Arguments);
        Assert.Contains("\"Pier\"", node.Values);
        node.Text = "Pier";
        builder.IsOnce = true;
        Assert.Equal("visited(\"Pier\")", builder.Condition);

        Assert.True(editor.CommitCondition(hello, builder));
        Assert.Contains("Odile: Hello. <<once if visited(\"Pier\")>> #line:hello\n", editor.TextBuffer.Text);
        await session.Analysis.WhenIdle();
        Assert.DoesNotContain(editor.Diagnostics, d => d.Severity == ColorRise.Skein.Assets.Compilation.YarnSeverity.Error);
    }

    [Fact]
    public async Task A_condition_that_is_not_rows_is_edited_as_text()
    {
        var (session, editor) = await Open();
        await using var _ = session;
        var leave = editor.Cards.Single(c => c.Title == "Start").Rows.Single(r => r.Display == "Leave");

        var builder = editor.EditCondition(leave)!;
        Assert.True(builder.IsText);
        Assert.Equal("$met and $coins > 1 or $act == Act.Wreck", builder.Text);

        // It stays text, and says why
        builder.IsText = false;
        Assert.True(builder.IsText);
        Assert.NotNull(builder.Problem);

        // Text that is rows goes back to rows
        builder.Text = "$met and $coins > 1";
        builder.IsText = false;
        Assert.False(builder.IsText);
        Assert.Null(builder.Problem);
        Assert.Equal(2, builder.Terms.Count);
        builder.IsText = true;
        Assert.Equal("$met and $coins > 1", builder.Text);

        Assert.True(editor.CommitCondition(leave, builder));
        Assert.Contains("-> Leave <<if $met and $coins > 1>>\n", editor.TextBuffer.Text);
    }

    [Fact]
    public async Task A_set_command_is_edited_as_a_row()
    {
        var (session, editor) = await Open();
        await using var _ = session;
        var start = editor.Cards.Single(c => c.Title == "Start");
        var set = start.Rows.Single(r => r.Kind == YarnStatementKind.Command && r.Display.StartsWith("set", StringComparison.Ordinal));
        var give = start.Rows.Single(r => r.Display == "give letter");

        Assert.True(set.IsAssignment);
        Assert.False(give.IsAssignment);
        Assert.Null(editor.EditAssignment(give));
        Assert.Null(editor.EditCondition(give));

        var builder = editor.EditAssignment(set)!;
        Assert.Equal(("$coins", YarnAssignmentOperator.Set, "1"), (builder.Variable, builder.Operator, builder.Value));
        Assert.Equal(Enum.GetValues<YarnAssignmentOperator>(), builder.Operators);
        Assert.DoesNotContain("$rich", builder.Variables);
        builder.Operator = YarnAssignmentOperator.Add;
        builder.Value = "2";
        Assert.Equal("set $coins += 2", builder.Command);

        Assert.True(editor.CommitAssignment(set, builder));
        Assert.Contains("<<set $coins += 2>>\n", editor.TextBuffer.Text);
        Assert.Equal(["Edit set"], session.Steps);

        // An enum is only set, and takes its case by name
        builder = editor.EditAssignment(set)!;
        builder.Variable = "$act";
        Assert.Equal([YarnAssignmentOperator.Set], builder.Operators);
        Assert.Equal(YarnAssignmentOperator.Set, builder.Operator);
        builder.Value = "Wreck";
        Assert.Equal("set $act to Act.Wreck", builder.Command);

        // A set without a value is refused
        builder.Value = "";
        Assert.False(editor.CommitAssignment(set, builder));
        Assert.NotNull(editor.Problem);
    }

    [Fact]
    public void A_when_header_reads_its_once_and_always()
    {
        var knowledge = YarnConditionKnowledge.Empty;

        var onceIf = YarnConditionBuilderViewModel.ForHeader(knowledge, "once if $met");
        Assert.Equal((true, false, "$met"), (onceIf.IsOnce, onceIf.IsAlways, onceIf.Condition));
        Assert.Equal("once if $met", onceIf.HeaderText);
        onceIf.IsAlways = true;
        Assert.Equal("always", onceIf.HeaderText);

        var always = YarnConditionBuilderViewModel.ForHeader(knowledge, "always");
        Assert.Equal((true, true, null), (always.AllowsAlways, always.IsAlways, always.Condition));

        var once = YarnConditionBuilderViewModel.ForHeader(knowledge, "once");
        Assert.Equal((true, null, "once"), (once.IsOnce, once.Condition, once.HeaderText));
        once.IsOnce = false;
        Assert.Null(once.HeaderText);

        // Without knowledge, a term takes every comparison and its value as typed
        var plain = YarnConditionBuilderViewModel.ForHeader(knowledge, "$act == Act.Wreck");
        Assert.Equal(Enum.GetValues<YarnComparison>(), plain.Terms[0].Comparisons);
        Assert.Equal("$act == Act.Wreck", plain.HeaderText);
    }
}
