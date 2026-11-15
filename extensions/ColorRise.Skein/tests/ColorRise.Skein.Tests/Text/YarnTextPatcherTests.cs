using ColorRise.Skein.Assets.Text;

namespace ColorRise.Skein.Tests.Text;

/// <summary>
/// A change made on the graph or in the property grid changes the lines it is about and no other character of the file,
/// whatever the line breaks of the file, and leaves a script that compiles.
/// </summary>
public sealed class YarnTextPatcherTests
{
    private const string Script = YarnFixtures.Script;

    /// <summary>
    /// Applies an operation to the script written as the variant, and checks the whole text that comes out: the
    /// expected one is given with <c>\n</c>.
    /// </summary>
    private static void Check(string variant, Func<YarnDocument, IReadOnlyList<YarnTextEdit>> operation, string expected, string script = Script, bool compiles = true)
    {
        var text = YarnFixtures.As(variant, script);

        var patched = YarnTextEdit.Apply(text, operation(YarnDocument.Parse(text)));

        Assert.Equal(YarnFixtures.As(variant, expected), patched);
        if (compiles)
            YarnFixtures.AssertCompiles(patched);
    }

    [Theory]
    [MemberData(nameof(YarnFixtures.Variants), MemberType = typeof(YarnFixtures))]
    public void A_position_replaces_the_value_of_its_header_or_comes_after_the_title(string variant)
    {
        Check(variant, d => YarnTextPatcher.SetPosition(d, d.Find("Start")!, 120, -40), Script.Replace("position: 0,0\n", "position: 120,-40\n"));
        Check(variant, d => YarnTextPatcher.SetPosition(d, d.Find("Lighthouse")!, 320, 0), Script.Replace("title: Lighthouse\n", "title: Lighthouse\nposition: 320,0\n"));
        Check(variant, d => YarnTextPatcher.SetPosition(d, d.Find("Odile_Key", 1)!, 7, 8), Script.Replace("when: always\n", "position: 7,8\nwhen: always\n"));
        Check(variant, d => YarnTextPatcher.SetPosition(d, d.Find("Start")!, 0, 0), Script);
    }

    [Theory]
    [MemberData(nameof(YarnFixtures.Variants), MemberType = typeof(YarnFixtures))]
    public void A_header_takes_its_value_on_its_line_and_a_new_header_comes_last(string variant)
    {
        // The spaces after the value and the comment after it stay
        Check(variant, d => YarnTextPatcher.SetHeader(d, d.Find("Start")!, "tags", "night"), Script.Replace("tags: intro  act1   \n", "tags: night   \n"));
        Check(variant, d => YarnTextPatcher.SetHeader(d, d.Find("Lighthouse")!, "color", "red"), Script.Replace("color: blue //", "color: red //"));

        Check(variant, d => YarnTextPatcher.SetHeader(d, d.Find("Start")!, "quest", "The lamp"), Script.Replace("position: 0,0\n", "position: 0,0\nquest: The lamp\n"));
        Check(variant, d => YarnTextPatcher.SetHeader(d, d.Find("Odile_Key", 1)!, "color", "green"), Script.Replace("when: always\n", "when: always\ncolor: green\n"));
        Check(variant, d => YarnTextPatcher.SetHeader(d, d.Find("Start")!, "tags", "intro  act1"), Script);
    }

    [Theory]
    [MemberData(nameof(YarnFixtures.Variants), MemberType = typeof(YarnFixtures))]
    public void A_header_without_a_value_takes_one_after_a_space(string variant)
    {
        const string Empty = "title: Start\ncolor:\ncluster: \ngroup:   // none yet\n---\nOdile: Hello.\n===\n";

        Check(variant, d => YarnTextPatcher.SetHeader(d, d.Nodes[0], "color", "red"), Empty.Replace("color:\n", "color: red\n"), Empty);
        Check(variant, d => YarnTextPatcher.SetHeader(d, d.Nodes[0], "cluster", "Act 1"), Empty.Replace("cluster: \n", "cluster: Act 1\n"), Empty);
        Check(variant, d => YarnTextPatcher.SetHeader(d, d.Nodes[0], "group", "Odile"), Empty.Replace("group:   //", "group: Odile //"), Empty);
    }

    [Theory]
    [MemberData(nameof(YarnFixtures.Variants), MemberType = typeof(YarnFixtures))]
    public void The_headers_of_one_name_take_their_values_in_order(string variant)
    {
        const string Two = "when: once\nwhen: visited_count(\"Start\") > 1\n";

        Check(variant, d => YarnTextPatcher.SetHeaders(d, d.Find("Odile_Key")!, "when", ["$has_key == false"]), Script.Replace(Two, "when: $has_key == false\n"));
        var declared = Script.Replace("Odile: Take the key.", "<<declare $a = true>>\n<<declare $b = true>>\nOdile: Take the key.");
        Check(variant, d => YarnTextPatcher.SetHeaders(d, d.Find("Odile_Key")!, "when", ["once", "$a", "$b"]), declared.Replace(Two, "when: once\nwhen: $a\nwhen: $b\n"), declared);
        Check(variant, d => YarnTextPatcher.SetHeaders(d, d.Find("Odile_Key")!, "when", []), Script.Replace(Two, ""), compiles: false);
        Check(variant, d => YarnTextPatcher.SetHeaders(d, d.Find("Lighthouse")!, "when", ["once"]), Script.Replace("a value\n", "a value\nwhen: once\n"));
        Check(variant, d => YarnTextPatcher.SetHeaders(d, d.Find("Odile_Key")!, "when", ["once", "visited_count(\"Start\") > 1"]), Script);
    }

    [Theory]
    [MemberData(nameof(YarnFixtures.Variants), MemberType = typeof(YarnFixtures))]
    public void A_removed_header_goes_with_its_line(string variant)
    {
        Check(variant, d => YarnTextPatcher.RemoveHeader(d, d.Find("Start")!, "tags"), Script.Replace("tags: intro  act1   \n", ""));
        Check(variant, d => YarnTextPatcher.RemoveHeader(d, d.Find("Lighthouse")!, "color"), Script.Replace("color: blue // a comment after a value\n", ""));
        Check(variant, d => YarnTextPatcher.RemoveHeader(d, d.Find("Odile_Key", 1)!, "when"), Script.Replace("when: always\n", ""), compiles: false);
        Check(variant, d => YarnTextPatcher.RemoveHeader(d, d.Find("Start")!, "color"), Script);
    }

    [Theory]
    [MemberData(nameof(YarnFixtures.Variants), MemberType = typeof(YarnFixtures))]
    public void A_new_node_comes_at_the_end_of_the_file(string variant)
    {
        Check(variant, d => YarnTextPatcher.AddNode(d, "Wreck", 640, 120), Script + "title: Wreck\nposition: 640,120\n---\n===\n");
        Check(variant, d => YarnTextPatcher.AddNode(d, "Wreck", 0, 0, [("color", "red"), ("quest", "The wreck")]), Script + "title: Wreck\nposition: 0,0\ncolor: red\nquest: The wreck\n---\n===\n");

        // A file that does not end with a line break gets one first
        Check(variant, d => YarnTextPatcher.AddNode(d, "Wreck", 1, 2), Script[..^1] + "\ntitle: Wreck\nposition: 1,2\n---\n===\n", Script[..^1]);
    }

    [Fact]
    public void An_empty_file_starts_with_its_first_node()
    {
        Check("LF", d => YarnTextPatcher.AddNode(d, "Wreck", 1, 2), "title: Wreck\nposition: 1,2\n---\n===\n", "");
        Check("BOM+LF", d => YarnTextPatcher.AddNode(d, "Wreck", 1, 2), "title: Wreck\nposition: 1,2\n---\n===\n", "");
    }

    [Theory]
    [MemberData(nameof(YarnFixtures.Variants), MemberType = typeof(YarnFixtures))]
    public void A_deleted_node_goes_with_the_blank_lines_after_it_and_nothing_else(string variant)
    {
        var start = Script[Script.IndexOf("title: Start", StringComparison.Ordinal)..Script.IndexOf("// Between", StringComparison.Ordinal)];
        Check(variant, d => YarnTextPatcher.DeleteNode(d, d.Find("Start")!), Script.Replace(start, ""), compiles: false);

        // The second member of a node group: that member only
        const string Second = "title: Odile_Key\nwhen: always\n---\nOdile: You have it already.\n===\n";
        Check(variant, d => YarnTextPatcher.DeleteNode(d, d.Find("Odile_Key", 1)!), Script.Replace(Second, ""));

        var first = Script[Script.IndexOf("title: Odile_Key", StringComparison.Ordinal)..Script.IndexOf(Second, StringComparison.Ordinal)];
        Check(variant, d => YarnTextPatcher.DeleteNode(d, d.Find("Odile_Key")!), Script.Replace(first, ""));
    }

    [Theory]
    [MemberData(nameof(YarnFixtures.Variants), MemberType = typeof(YarnFixtures))]
    public void A_copy_of_a_node_comes_after_it_with_its_title_and_without_line_ids(string variant)
    {
        const string Lighthouse = "title: Lighthouse\ncolor: blue // a comment after a value\n---\n";
        var start = Script[Script.IndexOf("title: Start", StringComparison.Ordinal)..(Script.IndexOf("===\n", StringComparison.Ordinal) + 4)];
        var copy = start
            .Replace("title: Start", "title: Start2")
            .Replace("position: 0,0", "position: 40,40")
            .Replace("out again. #line:lamp   \n", "out again.   \n");

        Check(variant, d => YarnTextPatcher.DuplicateNode(d, d.Find("Start")!, "Start2", (40, 40)), Script.Replace(start, start + copy));

        // Without a place of its own the copy keeps the headers as they are; other tags of a line stay
        const string Key = "title: Odile_Key\nwhen: once\nwhen: visited_count(\"Start\") > 1\n---\nOdile: Take the key. #line:key #mood:kind\n===\n";
        Check(variant, d => YarnTextPatcher.DuplicateNode(d, d.Find("Odile_Key")!, "Odile_Spare"),
            Script.Replace(Key, Key + Key.Replace("title: Odile_Key", "title: Odile_Spare").Replace(" #line:key", "")));

        Assert.Contains(Lighthouse, Script);
    }

    [Theory]
    [MemberData(nameof(YarnFixtures.Variants), MemberType = typeof(YarnFixtures))]
    public void A_jump_is_added_as_the_last_line_of_the_body(string variant)
    {
        Check(variant, d => YarnTextPatcher.AppendJump(d, d.Find("Odile_Key", 1)!, "Start"), Script[..^4] + "<<jump Start>>\n===\n");
        Check(variant, d => YarnTextPatcher.AppendJump(d, d.Find("Start")!, "Lighthouse"), Script.Replace("\t<<detour Odile_Key>>\n", "\t<<detour Odile_Key>>\n<<jump Lighthouse>>\n"));
    }

    [Theory]
    [MemberData(nameof(YarnFixtures.Variants), MemberType = typeof(YarnFixtures))]
    public void A_rename_changes_the_title_and_what_names_the_node_but_not_comments_and_text(string variant)
    {
        var renamed = Script
            .Replace("title: Odile_Key\nwhen: once", "title: Odile_TheKey\nwhen: once")
            .Replace("title: Odile_Key\nwhen: always", "title: Odile_TheKey\nwhen: always");
        Check(variant, d => [.. YarnTextPatcher.RenameNode(d, d.Find("Odile_Key")!, "Odile_TheKey"), .. YarnTextPatcher.RenameNode(d, d.Find("Odile_Key", 1)!, "Odile_TheKey")], renamed, compiles: false);

        // The jump in the comment and the name in Odile's line stay
        var jumps = Script
            .Replace("\t<<detour Odile_Key>>", "\t<<detour Odile_TheKey>>")
            .Replace("<<if visited(\"Odile_Key\")>>", "<<if visited(\"Odile_TheKey\")>>")
            .Replace("    <<jump Odile_Key>>", "    <<jump Odile_TheKey>>");
        Assert.Contains("// <<jump Odile_Key>>", jumps);
        Assert.Contains("Odile: say Odile_Key", jumps);
        Check(variant, d => YarnTextPatcher.RenameJumps(d, "Odile_Key", "Odile_TheKey"), jumps, compiles: false);

        // A when: header that asks about the node follows it
        Check(variant, d => YarnTextPatcher.RenameJumps(d, "Start", "Beginning"), Script.Replace("visited_count(\"Start\")", "visited_count(\"Beginning\")"));

        // A longer title that starts with the old one, and a jump to a value, are other things
        const string Others = "title: A\n---\n<<jump Odile_Key2>>\n<<jump {$next}>>\nOdile: {visited(\"Odile_Key\")} times, not {$Odile_Key}\n===\n";
        Check(variant, d => YarnTextPatcher.RenameJumps(d, "Odile_Key", "X"), Others.Replace("visited(\"Odile_Key\")", "visited(\"X\")"), Others, compiles: false);
    }

    [Fact]
    public void Title_and_jumps_together_give_a_script_that_compiles()
    {
        var document = YarnDocument.Parse(Script);
        var edits = YarnTextPatcher.RenameJumps(document, "Odile_Key", "Odile_TheKey")
            .Concat(document.Nodes.Where(n => n.Title == "Odile_Key").SelectMany(n => YarnTextPatcher.RenameNode(document, n, "Odile_TheKey")))
            .OrderBy(e => e.Offset)
            .ToList();

        var patched = YarnTextEdit.Apply(Script, edits);

        var compiled = Scripts.Compile(patched);
        Assert.Equal(["Start", "Lighthouse", "Odile_TheKey", "Odile_TheKey"], compiled.Nodes.Select(n => n.Title));
        Assert.DoesNotContain(compiled.Diagnostics, d => d.Code == "YS0012");
    }

    [Fact]
    public void A_script_being_typed_takes_the_changes_too()
    {
        // The last header has no line break yet
        Check("LF", d => YarnTextPatcher.SetPosition(d, d.Nodes[0], 5, 6), "title: Start\nposition: 5,6\n", "title: Start", compiles: false);
        Check("LF", d => YarnTextPatcher.SetHeader(d, d.Nodes[0], "color", "red"), "title: Start\ncolor: red\n", "title: Start", compiles: false);

        // No title yet: the title comes first
        Check("LF", d => YarnTextPatcher.RenameNode(d, d.Nodes[0], "Start"), "title: Start\ncolor: red\n---\n", "color: red\n---\n", compiles: false);

        // A body without its end: the jump is its last line, a new node closes it first, and so does a copy
        const string Open = "title: Start\n---\nOdile: Hello.";
        Check("LF", d => YarnTextPatcher.AppendJump(d, d.Nodes[0], "Next"), Open + "\n<<jump Next>>\n", Open, compiles: false);
        Check("LF", d => YarnTextPatcher.AddNode(d, "Next", 0, 0), Open + "\n===\ntitle: Next\nposition: 0,0\n---\n===\n", Open);
        Check("LF", d => YarnTextPatcher.DuplicateNode(d, d.Nodes[0], "Next"), Open + "\n===\ntitle: Next\n---\nOdile: Hello.\n===\n", Open);

        // No body yet
        Check("LF", d => YarnTextPatcher.AppendJump(d, d.Nodes[0], "Next"), "title: Start\n---\n<<jump Next>>\n===\n", "title: Start\n===\n", compiles: false);
    }

    [Fact]
    public void What_cannot_be_written_is_refused()
    {
        var document = YarnDocument.Parse(Script);
        var start = document.Find("Start")!;

        Assert.Throws<ArgumentException>(() => YarnTextPatcher.SetHeader(document, start, "color", "red\nblue"));
        Assert.Throws<ArgumentException>(() => YarnTextPatcher.SetHeader(document, start, "two words", "x"));
        Assert.Throws<ArgumentException>(() => YarnTextPatcher.SetHeader(document, start, "a:b", "x"));
        Assert.Throws<ArgumentException>(() => YarnTextPatcher.AddNode(document, "A\nB", 0, 0));

        // A node is of one parse: places of another text would be wrong here
        Assert.Throws<ArgumentException>(() => YarnTextPatcher.SetPosition(document, YarnDocument.Parse(Script).Find("Start")!, 0, 0));
    }
}
