using ColorRise.Skein.Assets.Text;

namespace ColorRise.Skein.Tests.Text;

/// <summary>
/// The scan of a script finds its nodes and their parts without the compiler, whatever the state of the bodies.
/// </summary>
public sealed class YarnDocumentTests
{
    [Theory]
    [MemberData(nameof(YarnFixtures.Variants), MemberType = typeof(YarnFixtures))]
    public void The_nodes_of_a_file_come_with_the_places_of_their_parts(string variant)
    {
        var text = YarnFixtures.As(variant, YarnFixtures.Script);
        var newLine = variant.EndsWith("CRLF", StringComparison.Ordinal) ? "\r\n" : "\n";

        var document = YarnDocument.Parse(text);

        Assert.Equal(newLine, document.NewLine);
        Assert.Equal(["Start", "Lighthouse", "Odile_Key", "Odile_Key"], document.Nodes.Select(n => n.Title));
        Assert.Equal([0, 0, 0, 1], document.Nodes.Select(n => n.Index));
        Assert.Equal([2, 15, 24, 30], document.Nodes.Select(n => n.Line));
        Assert.All(document.Nodes, n => Assert.True(n.IsClosed));

        var start = document.Nodes[0];
        Assert.Equal(["title", "tags", "position"], start.Headers.Select(h => h.Key));
        Assert.Equal(["Start", "intro  act1", "0,0"], start.Headers.Select(h => h.Value));
        Assert.Equal((0, 0), start.Position);
        Assert.StartsWith("title: Start" + newLine, text[start.HeaderStart..]);
        Assert.Equal("---" + newLine, text[start.DelimiterStart..start.BodyStart]);
        Assert.StartsWith("Odile: The lamp's out again.", text[start.BodyStart..]);
        Assert.EndsWith("\t<<detour Odile_Key>>" + newLine, text[start.BodyStart..start.BodyEnd]);
        Assert.Equal("===" + newLine, text[start.BodyEnd..start.End]);

        var tags = start.Headers[1];
        Assert.Equal("tags: intro  act1   " + newLine, text[tags.LineStart..tags.LineEnd]);
        Assert.Equal("intro  act1", text.Substring(tags.ValueStart, tags.Value.Length));

        // A comment after a value is not of the value
        var lighthouse = document.Nodes[1];
        Assert.Equal("blue", lighthouse.Header("color"));
        Assert.Null(lighthouse.Position);
        Assert.Null(lighthouse.Header("tags"));

        Assert.Same(document.Nodes[3], document.Find("Odile_Key", 1));
        Assert.Null(document.Find("Odile_Key", 2));
        Assert.Same(start, document.NodeAt(start.BodyStart));
        Assert.Same(start, document.NodeAt(start.HeaderStart));
        Assert.Null(document.NodeAt(start.End));
        Assert.Null(document.NodeAt(0 + (variant.StartsWith("BOM", StringComparison.Ordinal) ? 1 : 0)));
    }

    [Fact]
    public void The_lines_of_the_nodes_are_the_ones_the_compiler_gives()
    {
        var compiled = Scripts.Compile(YarnFixtures.Script);
        var document = YarnDocument.Parse(YarnFixtures.Script);

        Assert.Equal(compiled.Nodes.Select(n => (n.Title, n.HeaderStartLine)), document.Nodes.Select(n => (n.Title, n.Line)));
    }

    [Fact]
    public void A_script_that_does_not_compile_still_has_its_nodes()
    {
        const string Broken =
            "title: Start\nposition: 0,0\n---\nOdile: Hello.\n===\n" +
            "title: Middle\nposition: 320,0\n---\n<<if $lamp\nOdile: The lamp {\n-> \n===\n" +
            "title: End\nposition: 640,0\n---\nOdile: Bye.\n===\n";

        var document = YarnDocument.Parse(Broken);

        Assert.Equal(["Start", "Middle", "End"], document.Nodes.Select(n => n.Title));
        Assert.Equal([(0, 0), (320, 0), (640, 0)], document.Nodes.Select(n => n.Position!.Value));
    }

    [Fact]
    public void A_node_being_typed_is_a_node_from_its_first_header()
    {
        var titleOnly = YarnDocument.Parse("title: Start\n---\nOdile: Hello.\n===\ntitle");
        var typed = titleOnly.Nodes[1];
        Assert.Equal("", typed.Title);
        Assert.Equal(("title", ""), (typed.Headers[0].Key, typed.Headers[0].Value));
        Assert.False(typed.IsClosed);
        Assert.Equal(-1, typed.DelimiterStart);
        Assert.Equal((titleOnly.Text.Length, titleOnly.Text.Length, titleOnly.Text.Length), (typed.BodyStart, typed.BodyEnd, typed.End));
        Assert.Same(typed, titleOnly.NodeAt(titleOnly.Text.Length));

        var unclosed = YarnDocument.Parse("title: Start\nposition: 10.4, 19.6\n---\nOdile: Hello.");
        var open = unclosed.Nodes.Single();
        Assert.False(open.IsClosed);
        Assert.Equal((10, 20), open.Position);
        Assert.Equal("Odile: Hello.", unclosed.Text[open.BodyStart..open.BodyEnd]);

        // Closed before its body began
        var closedEarly = YarnDocument.Parse("title: Start\n===\ntitle: Next\n---\n===\n");
        Assert.Equal(["Start", "Next"], closedEarly.Nodes.Select(n => n.Title));
        Assert.Equal(-1, closedEarly.Nodes[0].DelimiterStart);
        Assert.Equal(closedEarly.Nodes[1].BodyStart, closedEarly.Nodes[1].BodyEnd);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n\n")]
    [InlineData("// Only a comment\n#file_tag\n")]
    [InlineData("===\n")]
    public void A_text_without_a_node_has_none(string text)
    {
        Assert.Empty(YarnDocument.Parse(text).Nodes);
        Assert.Equal("\n", YarnDocument.Parse("").NewLine);
    }

    [Fact]
    public void What_looks_like_the_end_of_a_node_in_a_comment_or_a_line_is_not()
    {
        var document = YarnDocument.Parse("title: Start\n---\n// ===\nOdile: === is three signs\n=== // the end\ntitle: Next\n---\n===\n");

        Assert.Equal(["Start", "Next"], document.Nodes.Select(n => n.Title));
        Assert.EndsWith("three signs\n", document.Text[document.Nodes[0].BodyStart..document.Nodes[0].BodyEnd]);
    }

    [Fact]
    public void Titles_follow_the_rules_of_Yarn_Spinner()
    {
        Assert.Null(YarnTitles.Validate("Wreck_2", ["Start"]));
        Assert.Null(YarnTitles.Validate("_Wreck", []));
        Assert.Null(YarnTitles.Validate("Épave", []));
        Assert.Equal("A node needs a title.", YarnTitles.Validate("", []));
        Assert.Equal("'Odile Key' cannot be a title: a title is one word of letters, digits and '_' that does not start with a digit.", YarnTitles.Validate("Odile Key", []));
        Assert.NotNull(YarnTitles.Validate("2nd", []));
        Assert.NotNull(YarnTitles.Validate("Odile.Key", []));
        Assert.NotNull(YarnTitles.Validate("Odile-Key", []));
        Assert.Equal("A node named 'Wreck' already exists.", YarnTitles.Validate("Wreck", ["Wreck"]));

        Assert.Equal("Node", YarnTitles.Unique("Node", ["Start"]));
        Assert.Equal("Node3", YarnTitles.Unique("Node", ["Node", "Node2"]));

        // What the rule accepts, Yarn Spinner compiles
        YarnFixtures.AssertCompiles("title: Épave\n---\nOdile: Hello.\n===\ntitle: _Wreck_2\n---\nOdile: Hello.\n===\n");
    }
}
