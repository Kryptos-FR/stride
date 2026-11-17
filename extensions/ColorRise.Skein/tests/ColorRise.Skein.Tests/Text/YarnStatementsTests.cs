using ColorRise.Skein.Assets.Text;

namespace ColorRise.Skein.Tests.Text;

/// <summary>
/// The statements of a node's body, read without the compiler, and the changes the cards of the graph make to them.
/// </summary>
public sealed class YarnStatementsTests
{
    private const string Script = YarnFixtures.Script;

    private const string Docks =
        "title: Docks\n" +
        "---\n" +
        "=> Gulls. #line:g1\n" +
        "=> More gulls.\n" +
        "-> Ask <<if $met>> #line:ask\n" +
        "    <<if $coins > 2>>\n" +
        "        Fisher: Sure. // he means it\n" +
        "        <<jump Pier>>\n" +
        "    <<elseif $coins > 0>>\n" +
        "        Fisher: Maybe.\n" +
        "    <<else>>\n" +
        "        <<jump {$next}>>\n" +
        "    <<endif>>\n" +
        "-> Leave <<once>>\n" +
        "\n" +
        "<<once if $tired>>\n" +
        "    Odile: Once\\#only. #line:once\n" +
        "<<endonce>>\n" +
        "<<set $met to true>>\n" +
        "Fisher: a {$name}: b\n" +
        "Narration, without a speaker.\n" +
        "===\n";

    private static void Check(string variant, Func<YarnDocument, IReadOnlyList<YarnTextEdit>> operation, string expected, string script = Script, bool compiles = true)
    {
        var text = YarnFixtures.As(variant, script);

        var patched = YarnTextEdit.Apply(text, operation(YarnDocument.Parse(text)));

        Assert.Equal(YarnFixtures.As(variant, expected), patched);
        if (compiles)
            YarnFixtures.AssertCompiles(patched);
    }

    private static (YarnDocument Document, YarnNodeSpan Node, IReadOnlyList<YarnStatementSpan> Statements) Read(string text, string title, int index = 0)
    {
        var document = YarnDocument.Parse(text);
        var node = document.Find(title, index)!;
        return (document, node, document.StatementsOf(node));
    }

    [Theory]
    [MemberData(nameof(YarnFixtures.Variants), MemberType = typeof(YarnFixtures))]
    public void The_options_hold_the_lines_indented_under_them(string variant)
    {
        var text = YarnFixtures.As(variant, Script);
        var (_, _, statements) = Read(text, "Start");

        Assert.Equal(
            [
                (YarnStatementKind.Line, "Odile: The lamp's out again.", 0, -1),
                (YarnStatementKind.Option, "I'll look.", 0, -1),
                (YarnStatementKind.Line, "Odile: Mind the stairs.", 1, 1),
                (YarnStatementKind.Jump, "jump Lighthouse", 1, 1),
                (YarnStatementKind.Option, "Not my problem.", 0, -1),
                (YarnStatementKind.Detour, "detour Odile_Key", 1, 4),
            ],
            statements.Select(s => (s.Kind, s.Text, s.Depth, s.Parent)));
        Assert.Equal(["line:lamp"], statements[0].Hashtags);
        Assert.Equal("Odile", statements[0].Speaker);
        Assert.Equal("Lighthouse", statements[3].Target);
        Assert.Equal("Odile_Key", statements[5].Target);
        Assert.Equal([6, 7, 8, 9, 10, 11], statements.Select(s => s.Line));
        Assert.Equal("\t", statements[2].Indentation);

        // The places are of the text
        foreach (var statement in statements)
        {
            Assert.Equal(statement.Text, text[statement.TextStart..statement.TextEnd]);
            Assert.True(text[statement.LineEnd - 1] == '\n' && statement.LineStart <= statement.TextStart);
        }

        // A comment is not a part of the line, nor a jump in it
        var (_, _, lighthouse) = Read(text, "Lighthouse");
        Assert.Equal("Narrator: The stairs wind up into the dark.", lighthouse[0].Text);
        Assert.Equal([YarnStatementKind.Line, YarnStatementKind.Line, YarnStatementKind.If, YarnStatementKind.Jump], lighthouse.Select(s => s.Kind));
        Assert.Equal("visited(\"Odile_Key\")", lighthouse[2].Condition);
        Assert.Equal(2, lighthouse[3].Parent);
    }

    [Fact]
    public void Blocks_hold_their_branches_and_lines_say_their_conditions()
    {
        var (_, _, statements) = Read(Docks, "Docks");

        Assert.Equal(
            [
                (YarnStatementKind.LineGroupItem, "Gulls.", 0, -1),
                (YarnStatementKind.LineGroupItem, "More gulls.", 0, -1),
                (YarnStatementKind.Option, "Ask", 0, -1),
                (YarnStatementKind.If, "if $coins > 2", 1, 2),
                (YarnStatementKind.Line, "Fisher: Sure.", 2, 3),
                (YarnStatementKind.Jump, "jump Pier", 2, 3),
                (YarnStatementKind.ElseIf, "elseif $coins > 0", 1, 2),
                (YarnStatementKind.Line, "Fisher: Maybe.", 2, 6),
                (YarnStatementKind.Else, "else", 1, 2),
                (YarnStatementKind.Jump, "jump {$next}", 2, 8),
                (YarnStatementKind.Option, "Leave", 0, -1),
                (YarnStatementKind.Once, "once if $tired", 0, -1),
                (YarnStatementKind.Line, "Odile: Once\\#only.", 1, 11),
                (YarnStatementKind.Command, "set $met to true", 0, -1),
                (YarnStatementKind.Line, "Fisher: a {$name}: b", 0, -1),
                (YarnStatementKind.Line, "Narration, without a speaker.", 0, -1),
            ],
            statements.Select(s => (s.Kind, s.Text, s.Depth, s.Parent)));

        Assert.Equal(["line:g1"], statements[0].Hashtags);
        Assert.Equal("$met", statements[2].Condition);
        Assert.False(statements[2].IsOnce);
        Assert.Equal(["line:ask"], statements[2].Hashtags);
        Assert.Equal("$coins > 2", statements[3].Condition);
        Assert.Equal("$coins > 0", statements[6].Condition);
        Assert.Null(statements[8].Condition);
        Assert.Equal("{$next}", statements[9].Target);
        Assert.True(statements[10].IsOnce);
        Assert.Null(statements[10].Condition);
        Assert.True(statements[11].IsOnce);
        Assert.Equal("$tired", statements[11].Condition);
        Assert.Equal(["line:once"], statements[12].Hashtags);
        Assert.Equal("Fisher", statements[14].Speaker);
        Assert.Null(statements[15].Speaker);
        Assert.All(statements.Where(s => s.Kind is YarnStatementKind.Option or YarnStatementKind.If or YarnStatementKind.Once), s => Assert.True(s.IsBranch));
    }

    [Fact]
    public void A_body_being_typed_still_has_its_statements()
    {
        // No end to the block, an option without text, a command without its end, a node without its ===
        var (_, _, statements) = Read("title: Start\n---\n<<if $x>>\n-> \n<<wait 2\nOdile: Hm", "Start");

        Assert.Equal(
            [YarnStatementKind.If, YarnStatementKind.Option, YarnStatementKind.Command, YarnStatementKind.Line],
            statements.Select(s => s.Kind));
        Assert.Equal("", statements[1].Text);
        Assert.Equal([0, 1, 1, 1], statements.Select(s => s.Depth));

        // A body without lines has no statements
        Assert.Empty(Read("title: Start\n---\n\n// nothing yet\n===\n", "Start").Statements);
        Assert.Throws<ArgumentException>(() => YarnDocument.Parse("title: A\n---\n===\n").StatementsOf(YarnDocument.Parse("title: A\n---\n===\n").Nodes[0]));
    }

    [Theory]
    [MemberData(nameof(YarnFixtures.Variants), MemberType = typeof(YarnFixtures))]
    public void A_new_text_keeps_the_id_the_hashtags_and_the_comment_of_its_line(string variant)
    {
        YarnStatementSpan At(YarnDocument d, string title, int index) => d.StatementsOf(d.Find(title)!)[index];

        Check(variant, d => YarnTextPatcher.SetStatementText(d, d.Find("Start")!, At(d, "Start", 0), " Odile: The lamp is out. "),
            Script.Replace("Odile: The lamp's out again. #line:lamp", "Odile: The lamp is out. #line:lamp"));
        Check(variant, d => YarnTextPatcher.SetStatementText(d, d.Find("Start")!, At(d, "Start", 1), "I'll go up."),
            Script.Replace("-> I'll look.", "-> I'll go up."));
        Check(variant, d => YarnTextPatcher.SetStatementText(d, d.Find("Lighthouse")!, At(d, "Lighthouse", 0), "Narrator: Dark."),
            Script.Replace("Narrator: The stairs wind up into the dark. //", "Narrator: Dark. //"));

        // What would end the text is escaped; what is escaped already stays
        Check(variant, d => YarnTextPatcher.SetStatementText(d, d.Find("Start")!, At(d, "Start", 0), "Odile: #1 is out // again << \\# >>"),
            Script.Replace("Odile: The lamp's out again. #line:lamp", "Odile: \\#1 is out \\// again \\<< \\# >> #line:lamp"));

        // The same text is no change
        Check(variant, d => YarnTextPatcher.SetStatementText(d, d.Find("Start")!, At(d, "Start", 1), "I'll look."), Script);
    }

    [Fact]
    public void A_text_that_cannot_be_a_line_is_refused()
    {
        var (document, node, statements) = Read(Script, "Start");

        Assert.Throws<ArgumentException>(() => YarnTextPatcher.SetStatementText(document, node, statements[0], "  "));
        Assert.Throws<ArgumentException>(() => YarnTextPatcher.SetStatementText(document, node, statements[0], "one\ntwo"));
        Assert.Throws<ArgumentException>(() => YarnTextPatcher.SetStatementText(document, node, statements[0], "-> sneaky"));
        Assert.Throws<ArgumentException>(() => YarnTextPatcher.SetStatementText(document, node, statements[0], "==="));
        Assert.Throws<ArgumentException>(() => YarnTextPatcher.SetStatementText(document, node, statements[3], "jump Elsewhere"));

        // A statement of another document is refused: the text changed since it was read
        var (_, _, again) = Read(Script, "Start");
        Assert.Throws<ArgumentException>(() => YarnTextPatcher.SetStatementText(document, node, again[0], "Odile: Hi."));
    }

    [Fact]
    public void A_condition_right_after_an_empty_option_stays_apart_from_its_new_text()
    {
        const string script = "title: Start\n---\n-><<if true>>\n    Odile: Hi.\n===\n";
        var (document, node, statements) = Read(script, "Start");

        var patched = YarnTextEdit.Apply(script, YarnTextPatcher.SetStatementText(document, node, statements[0], "Ask"));

        Assert.Equal("title: Start\n---\n->Ask <<if true>>\n    Odile: Hi.\n===\n", patched);
        YarnFixtures.AssertCompiles(patched);
    }

    [Theory]
    [MemberData(nameof(YarnFixtures.Variants), MemberType = typeof(YarnFixtures))]
    public void A_line_is_deleted_with_its_line_and_a_statement_that_holds_others_is_not(string variant)
    {
        Check(variant, d => YarnTextPatcher.DeleteStatement(d, d.Find("Start")!, d.StatementsOf(d.Find("Start")!)[2]),
            Script.Replace("\tOdile: Mind the stairs.\n", ""));

        var (document, node, statements) = Read(YarnFixtures.As(variant, Script), "Start");
        Assert.Throws<ArgumentException>(() => YarnTextPatcher.DeleteStatement(document, node, statements[1]));
    }

    [Theory]
    [MemberData(nameof(YarnFixtures.Variants), MemberType = typeof(YarnFixtures))]
    public void A_new_line_comes_at_the_end_of_its_node_before_the_jumps_that_end_it(string variant)
    {
        // After the options, at the top of the body
        Check(variant, d => YarnTextPatcher.AddLine(d, d.Find("Start")!, "Odile: Well?"),
            Script.Replace("\t<<detour Odile_Key>>\n===\n", "\t<<detour Odile_Key>>\nOdile: Well?\n===\n"));

        // Before the jump that ends a node
        const string jumping = "title: Start\n---\n    Odile: Go.\n    <<jump Pier>>\n    <<stop>>\n===\ntitle: Pier\n---\n===\n";
        Check(variant, d => YarnTextPatcher.AddLine(d, d.Find("Start")!, "Odile: Now."),
            jumping.Replace("    <<jump Pier>>", "    Odile: Now.\n    <<jump Pier>>"), jumping);

        // In an empty body, in a node without its ---, and in a node still being typed
        Check(variant, d => YarnTextPatcher.AddLine(d, d.Find("Start")!, "Hi."), "title: Start\n---\nHi.\n===\n", "title: Start\n---\n===\n");
        Check(variant, d => YarnTextPatcher.AddLine(d, d.Find("Start")!, "Hi."), "title: Start\n---\nHi.\n===\n", "title: Start\n===\n");
        Check(variant, d => YarnTextPatcher.AddLine(d, d.Find("Start")!, "Hi."), "title: Start\n---\nOdile: Hm\nHi.\n", "title: Start\n---\nOdile: Hm", compiles: false);
    }

    [Theory]
    [MemberData(nameof(YarnFixtures.Variants), MemberType = typeof(YarnFixtures))]
    public void A_jump_from_an_option_or_a_branch_goes_last_in_it(string variant)
    {
        // Under an option with lines, after them; under one without, one step deeper
        Check(variant, d => YarnTextPatcher.AppendJump(d, d.Find("Start")!, d.StatementsOf(d.Find("Start")!)[1], "Odile_Key"),
            Script.Replace("\t<<jump Lighthouse>>\n", "\t<<jump Lighthouse>>\n\t<<jump Odile_Key>>\n"));
        Check(variant, d => YarnTextPatcher.AppendJump(d, d.Find("Start")!, d.StatementsOf(d.Find("Start")!)[4], "Lighthouse", detour: true),
            Script.Replace("\t<<detour Odile_Key>>\n", "\t<<detour Odile_Key>>\n\t<<detour Lighthouse>>\n"));

        const string options = "title: Start\n---\n-> Stay\n-> Go\n<<if true>>\n<<else>>\n<<endif>>\n===\ntitle: Pier\n---\n===\n";
        Check(variant, d => YarnTextPatcher.AppendJump(d, d.Find("Start")!, d.StatementsOf(d.Find("Start")!)[0], "Pier"),
            options.Replace("-> Stay\n", "-> Stay\n    <<jump Pier>>\n"), options);

        // A branch of a block ends before the next one
        Check(variant, d => YarnTextPatcher.AppendJump(d, d.Find("Start")!, d.StatementsOf(d.Find("Start")!)[2], "Pier"),
            options.Replace("<<if true>>\n", "<<if true>>\n    <<jump Pier>>\n"), options);

        var (document, node, statements) = Read(YarnFixtures.As(variant, Script), "Start");
        Assert.Throws<ArgumentException>(() => YarnTextPatcher.AppendJump(document, node, statements[0], "Pier"));
    }

    [Fact]
    public void A_line_or_an_option_takes_another_condition_and_keeps_its_text_and_its_id()
    {
        const string script =
            "title: Start\n---\n<<declare $met = false>>\n<<declare $coins = 0>>\n"
            + "Odile: Hello. #line:hello\n"
            + "-> Ask <<if $met>> #line:ask\n"
            + "    Odile: Sure.\n"
            + "-> Leave <<once>>\n"
            + "Odile: Bye.\n"
            + "===\n";

        string Patch(int index, string? condition, bool once)
        {
            var (document, node, statements) = Read(script, "Start");
            var patched = YarnTextEdit.Apply(script, YarnTextPatcher.SetStatementCondition(document, node, statements[index], condition, once));
            YarnFixtures.AssertCompiles(patched);
            return patched;
        }

        // The statements are: Hello, Ask, Sure, Leave, Bye (the declarations are commands before them)
        var (_, _, read) = Read(script, "Start");
        var hello = read.ToList().FindIndex(s => s.Text == "Odile: Hello.");
        var (ask, leave, bye) = (hello + 1, hello + 3, hello + 4);
        Assert.Equal("<<if $met>>", script[read[ask].ConditionStart..read[ask].ConditionEnd]);
        Assert.Equal(-1, read[hello].ConditionStart);

        Assert.Equal(script.Replace("Odile: Hello. #line:hello", "Odile: Hello. <<if $coins > 2>> #line:hello"), Patch(hello, "$coins > 2", false));
        Assert.Equal(script.Replace("Odile: Bye.", "Odile: Bye. <<once if not $met>>"), Patch(bye, " not $met ", true));
        Assert.Equal(script.Replace("-> Ask <<if $met>> #line:ask", "-> Ask <<if $met and $coins >= 1>> #line:ask"), Patch(ask, "$met and $coins >= 1", false));
        Assert.Equal(script.Replace("-> Ask <<if $met>> #line:ask", "-> Ask <<once>> #line:ask"), Patch(ask, null, true));
        Assert.Equal(script.Replace("-> Ask <<if $met>> #line:ask", "-> Ask #line:ask"), Patch(ask, "", false));
        Assert.Equal(script.Replace("-> Leave <<once>>", "-> Leave"), Patch(leave, null, false));
        Assert.Equal(script, Patch(ask, "$met", false));

        var (doc, start, statements) = Read(script, "Start");
        Assert.Throws<ArgumentException>(() => YarnTextPatcher.SetStatementCondition(doc, start, statements[ask], "$a >> 2", false));
        Assert.Throws<ArgumentException>(() => YarnTextPatcher.SetStatementCondition(doc, start, statements[ask], "$a\n$b", false));
        Assert.Throws<ArgumentException>(() => YarnTextPatcher.SetStatementCondition(doc, start, statements[0], "$a", false));
    }

    [Fact]
    public void A_command_takes_another_text()
    {
        const string script = "title: Start\n---\n<<declare $coins = 0>>\n    <<set $coins to 1>>   // pay\n===\n";
        var (document, node, statements) = Read(script, "Start");

        var patched = YarnTextEdit.Apply(script, YarnTextPatcher.SetCommandText(document, node, statements[1], " set $coins += 2 "));

        Assert.Equal("title: Start\n---\n<<declare $coins = 0>>\n    <<set $coins += 2>>   // pay\n===\n", patched);
        YarnFixtures.AssertCompiles(patched);
        Assert.Empty(YarnTextPatcher.SetCommandText(document, node, statements[1], "set $coins to 1"));
        Assert.Throws<ArgumentException>(() => YarnTextPatcher.SetCommandText(document, node, statements[1], "set $a to 1>> <<stop"));
        Assert.Throws<ArgumentException>(() => YarnTextPatcher.SetCommandText(document, node, statements[1], " "));
    }
}
