using ColorRise.Skein.Assets.Compilation;

namespace ColorRise.Skein.Tests.Compilation;

/// <summary>
/// The one door to Yarn Spinner's compiler: what a set of scripts gives as a program, nodes, lines, variables and diagnostics.
/// </summary>
public sealed class YarnCompilerBridgeTests
{
    private const string Harbour = """
        title: Harbour
        position: 120,40
        tags: act1 harbour
        color: blue
        ---
        Odile: The tide is out.
        -> Walk to the wreck
            <<jump Wreck>>
        -> Stay
            Odile: Not today.
        ===

        """;

    private const string Wreck = """
        title: Wreck
        ---
        Kye: Mind the ribs of her.
        <<detour Harbour>>
        ===

        """;

    private static YarnCompilation Compile(params (string FileName, string Text)[] files)
        => YarnCompilerBridge.Compile(new YarnCompilationRequest { Files = files });

    [Fact]
    public void Two_files_give_both_nodes_and_the_jump_across_them()
    {
        var result = Compile(("Harbour.yarn", Harbour), ("Wreck.yarn", Wreck));

        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Program);
        Assert.Equal(["Harbour", "Wreck"], result.Nodes.Select(n => n.Title));

        var harbour = result.Nodes[0];
        Assert.Equal("Harbour.yarn", harbour.File);
        Assert.Equal("Harbour", harbour.UniqueTitle);
        Assert.Null(harbour.NodeGroup);
        var jump = Assert.Single(harbour.Jumps);
        Assert.Equal("Wreck", jump.Target);
        Assert.False(jump.IsDetour);
        Assert.Equal(8, jump.Line);

        var detour = Assert.Single(result.Nodes[1].Jumps);
        Assert.Equal("Harbour", detour.Target);
        Assert.True(detour.IsDetour);

        Assert.True(result.Program!.Nodes.ContainsKey("Harbour"));
        Assert.True(result.Program.Nodes.ContainsKey("Wreck"));
    }

    [Fact]
    public void A_node_gives_its_headers_in_order_and_where_it_is()
    {
        var harbour = Compile(("Harbour.yarn", Harbour)).Nodes[0];

        Assert.Equal(
            [("title", "Harbour"), ("position", "120,40"), ("tags", "act1 harbour"), ("color", "blue")],
            harbour.Headers.Select(h => (h.Key, h.Value)));
        Assert.Equal(["act1", "harbour"], harbour.Tags);
        Assert.Equal(1, harbour.HeaderStartLine);
        Assert.Equal(6, harbour.BodyStartLine);
        Assert.Equal(11, harbour.BodyEndLine);
        Assert.Equal(["Odile"], harbour.Speakers);
        Assert.Contains("The tide is out.", harbour.Preview);
        Assert.Equal(["Walk to the wreck", "Stay"], harbour.Options.Select(o => o.Text));
        Assert.Equal(7, harbour.Options[0].Line);
    }

    [Fact]
    public void The_nodes_of_a_group_keep_their_title_and_give_their_conditions()
    {
        const string Gulls = """
            title: Gulls
            when: $tide > 2
            when: once
            ---
            The gulls wheel over the nets.
            ===
            title: Gulls
            when: always
            ---
            A gull watches you.
            ===
            title: Start
            ---
            <<declare $tide = 0>>
            <<jump Gulls>>
            ===

            """;

        var result = Compile(("Gulls.yarn", Gulls));

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == YarnSeverity.Error);
        Assert.NotNull(result.Program);
        var members = result.Nodes.Where(n => n.Title == "Gulls").ToList();
        Assert.Equal(2, members.Count);
        Assert.All(members, m => Assert.Equal("Gulls", m.NodeGroup));
        Assert.NotEqual(members[0].UniqueTitle, members[1].UniqueTitle);
        Assert.All(members, m => Assert.True(result.Program!.Nodes.ContainsKey(m.UniqueTitle)));
        Assert.Equal(
            [("title", "Gulls"), ("when", "$tide > 2"), ("when", "once")],
            members[0].Headers.Select(h => (h.Key, h.Value)));
        Assert.Equal([("title", "Gulls"), ("when", "always")], members[1].Headers.Select(h => (h.Key, h.Value)));
        Assert.True(members[0].Complexity > members[1].Complexity);
        // What a "once" remembers is a variable of the compiler's, apart from the story's
        var once = Assert.Single(result.Variables, v => v.IsInternal);
        Assert.Equal("bool", once.Type);
        Assert.False(Assert.Single(result.Variables, v => v.Name == "$tide").IsInternal);
    }

    [Fact]
    public void Lines_give_their_text_place_and_metadata()
    {
        const string Tagged = """
            title: Start
            ---
            Odile: Take the lamp. #line:lamp #shout
            Kye: Aye.
            ===

            """;

        var result = Compile(("Tagged.yarn", Tagged));

        var lamp = result.Lines["line:lamp"];
        Assert.Equal("Odile: Take the lamp.", lamp.Text);
        Assert.Equal("Start", lamp.Node);
        Assert.Equal("Tagged.yarn", lamp.File);
        Assert.Equal(3, lamp.Line);
        Assert.False(lamp.IsImplicitId);
        Assert.Equal(["shout"], lamp.Metadata);

        var aye = Assert.Single(result.Lines, l => l.Value.Text == "Kye: Aye.").Value;
        Assert.True(aye.IsImplicitId);
        Assert.Equal(4, aye.Line);
    }

    private const string Odile = """
        title: Odile
        ---
        Odile: Kye knows the way.
        <<jump Odile_Kye>>
        ===

        """;

    [Fact]
    public void A_jump_to_a_node_that_is_missing_is_a_diagnostic_at_the_jump()
    {
        var result = Compile(("Odile.yarn", Odile));

        // Yarn Spinner 3.2 reports a missing jump target as a warning: the script still compiles
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("YS0012", diagnostic.Code);
        Assert.Equal(YarnSeverity.Warning, diagnostic.Severity);
        Assert.Equal("Odile.yarn", diagnostic.File);
        Assert.Equal("Odile", diagnostic.Node);
        Assert.Equal(4, diagnostic.Line);
        Assert.Equal(8, diagnostic.Column);
        Assert.Equal(4, diagnostic.EndLine);
        Assert.Contains("Odile_Kye", diagnostic.Message);
        Assert.Equal($"Odile.yarn(4,8): warning YS0012: [Odile] {diagnostic.Message}", diagnostic.ToString());
        Assert.NotNull(result.Program);
    }

    [Fact]
    public void A_severity_set_to_error_leaves_no_program()
    {
        var result = YarnCompilerBridge.Compile(new YarnCompilationRequest
        {
            Files = [("Odile.yarn", Odile)],
            Severities = new Dictionary<string, YarnSeverity> { ["YS0012"] = YarnSeverity.Error },
        });

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(YarnSeverity.Error, diagnostic.Severity);
        Assert.Null(result.Program);
        // What the editor shows stays: the nodes and the lines
        Assert.Single(result.Nodes);
        Assert.NotEmpty(result.Lines);
    }

    [Fact]
    public void A_severity_set_to_none_drops_the_diagnostic()
    {
        var result = YarnCompilerBridge.Compile(new YarnCompilationRequest
        {
            Files = [("Odile.yarn", Odile)],
            Severities = new Dictionary<string, YarnSeverity> { ["YS0012"] = YarnSeverity.None },
        });

        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Program);
    }

    [Fact]
    public void A_syntax_error_is_an_error_without_a_program_and_keeps_the_other_nodes()
    {
        const string Broken = """
            title: Start
            ---
            <<set $gold = >>
            Odile: Hm.
            ===
            title: Other
            ---
            Kye: Fine.
            ===

            """;

        var result = Compile(("Broken.yarn", Broken));

        Assert.Null(result.Program);
        var error = Assert.Single(result.Diagnostics, d => d.Severity == YarnSeverity.Error);
        Assert.Equal(3, error.Line);
        Assert.Equal("Start", error.Node);
        Assert.StartsWith("Broken.yarn(3,", error.ToString());
        Assert.Contains(": error YS", error.ToString());
        Assert.Equal(["Start", "Other"], result.Nodes.Select(n => n.Title));
    }

    [Fact]
    public void A_diagnostic_outside_a_node_has_no_brackets()
    {
        var diagnostic = new YarnDiagnostic
        {
            File = "A.yarn", Line = 1, Column = 1, EndLine = 1, EndColumn = 4, Code = "YS0001", Severity = YarnSeverity.Info, Message = "Text.",
        };

        Assert.Equal("A.yarn(1,1): info YS0001: Text.", diagnostic.ToString());
    }

    [Fact]
    public void A_declared_variable_gives_its_type_default_and_description()
    {
        const string Purse = """
            title: Start
            ---
            /// The purse
            <<declare $gold = 5 as number>>
            <<declare $name = "Odile">>
            <<declare $met_kye = false>>
            <<declare $rich = $gold > 100>>
            <<set $seen = true>>
            ===

            """;

        var result = Compile(("Purse.yarn", Purse));

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == YarnSeverity.Error);
        var gold = Assert.Single(result.Variables, v => v.Name == "$gold");
        Assert.Equal("number", gold.Type);
        Assert.Equal(5f, gold.Default);
        Assert.Equal("The purse", gold.Description);
        Assert.Equal("Purse.yarn", gold.File);
        Assert.Equal(4, gold.Line);
        Assert.False(gold.IsSmart);
        Assert.False(gold.IsImplicit);

        Assert.Equal("Odile", Assert.Single(result.Variables, v => v.Name == "$name").Default);
        Assert.Equal("string", Assert.Single(result.Variables, v => v.Name == "$name").Type);
        Assert.Equal(false, Assert.Single(result.Variables, v => v.Name == "$met_kye").Default);
        Assert.Equal("bool", Assert.Single(result.Variables, v => v.Name == "$met_kye").Type);
        Assert.True(Assert.Single(result.Variables, v => v.Name == "$rich").IsSmart);
        Assert.True(Assert.Single(result.Variables, v => v.Name == "$seen").IsImplicit);
        // Functions are not variables
        Assert.All(result.Variables, v => Assert.StartsWith("$", v.Name));
    }

    private static readonly YarnFunctionDeclaration HasItem = new()
    {
        Name = "has_item", ParameterTypes = [YarnFunctionDeclaration.String], ReturnType = YarnFunctionDeclaration.Bool,
    };

    private static YarnCompilation CompileWithHasItem(string argument)
        => YarnCompilerBridge.Compile(new YarnCompilationRequest
        {
            Files = [("Lamp.yarn", $"title: Start\n---\n<<if has_item({argument})>>\nOdile: Light it.\n<<endif>>\n===\n")],
            Functions = [HasItem],
        });

    [Fact]
    public void A_declared_function_type_checks_its_call()
    {
        var result = CompileWithHasItem("\"oil\"");

        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Program);
    }

    [Fact]
    public void A_declared_function_refuses_an_argument_of_another_type()
    {
        var result = CompileWithHasItem("3");

        Assert.Null(result.Program);
        var error = Assert.Single(result.Diagnostics, d => d.Severity == YarnSeverity.Error);
        Assert.Equal(3, error.Line);
        Assert.Equal("Start", error.Node);
    }

    [Fact]
    public void A_function_takes_as_many_values_as_its_variadic_type_allows()
    {
        var result = YarnCompilerBridge.Compile(new YarnCompilationRequest
        {
            Files = [("Sum.yarn", "title: Start\n---\n<<if sum_of(1, 2, 3) == 6 and sum_of() == 0>>\nOdile: Six.\n<<endif>>\n===\n")],
            Functions = [new YarnFunctionDeclaration { Name = "sum_of", VariadicType = YarnFunctionDeclaration.Number, ReturnType = YarnFunctionDeclaration.Number }],
        });

        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Program);
    }

    [Fact]
    public void A_function_of_an_unknown_type_is_refused_by_name()
    {
        var request = new YarnCompilationRequest
        {
            Files = [("A.yarn", "title: Start\n---\nHi.\n===\n")],
            Functions = [new YarnFunctionDeclaration { Name = "pick", ParameterTypes = ["vector"], ReturnType = YarnFunctionDeclaration.Number }],
        };

        var exception = Assert.Throws<ArgumentException>(() => YarnCompilerBridge.Compile(request));
        Assert.Contains("pick", exception.Message);
        Assert.Contains("vector", exception.Message);
    }

    [Fact]
    public void Strings_only_gives_lines_and_nodes_without_a_program()
    {
        var result = YarnCompilerBridge.Compile(new YarnCompilationRequest
        {
            Files = [("Harbour.yarn", Harbour), ("Wreck.yarn", Wreck)],
            StringsOnly = true,
        });

        Assert.Null(result.Program);
        Assert.Equal(2, result.Nodes.Count);
        Assert.Equal(5, result.Lines.Count);
        Assert.Empty(result.Variables);
    }

    [Fact]
    public void Windows_line_ends_give_the_same_places()
    {
        var result = Compile(("Odile.yarn", Odile.ReplaceLineEndings("\r\n")));

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal((4, 8, 4, 17), (diagnostic.Line, diagnostic.Column, diagnostic.EndLine, diagnostic.EndColumn));
        var node = Assert.Single(result.Nodes);
        Assert.Equal((1, 3, 5), (node.HeaderStartLine, node.BodyStartLine, node.BodyEndLine));
        Assert.Equal("Odile: Kye knows the way.", Assert.Single(result.Lines).Value.Text);
    }

    [Fact]
    public void A_node_the_text_does_not_close_is_still_a_node()
    {
        var result = Compile(("Open.yarn", "title: Open\n---\nOdile: No end.\n"));

        Assert.Null(result.Program);
        Assert.Contains(result.Diagnostics, d => d.Severity == YarnSeverity.Error);
        var node = Assert.Single(result.Nodes);
        Assert.Equal("Open", node.Title);
        Assert.True(node.BodyEndLine >= node.BodyStartLine);
    }

    [Fact]
    public void No_file_gives_an_empty_result()
    {
        var result = YarnCompilerBridge.Compile(new YarnCompilationRequest());

        Assert.Empty(result.Nodes);
        Assert.Empty(result.Lines);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void An_empty_file_and_a_file_without_a_node_do_not_throw()
    {
        var result = Compile(("Empty.yarn", ""), ("Notes.yarn", "Just some words\n"), ("Wreck.yarn", Wreck.Replace("<<detour Harbour>>", "")));

        Assert.Null(result.Program);
        Assert.Contains(result.Diagnostics, d => d.Severity == YarnSeverity.Error && d.File is "Empty.yarn" or "Notes.yarn");
        Assert.Contains(result.Nodes, n => n.Title == "Wreck");
    }

    [Fact]
    public void The_functions_a_dialogue_adds_are_known_wherever_they_are_called()
    {
        var result = Compile(("Visits.yarn", """
            title: Start
            ---
            Odile: You came {visited_count("Start")} times, {visited("Start")}, {has_any_content("Start")}.
            ===
            """));

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == YarnSeverity.Error);
        Assert.NotNull(result.Program);

        // A game may give its own in their place
        var own = YarnCompilerBridge.Compile(new YarnCompilationRequest
        {
            Files = [("Visits.yarn", "title: Start\n---\n<<set $times = visited_count(1) + 1>>\n===\n")],
            Functions = [new YarnFunctionDeclaration { Name = "visited_count", ParameterTypes = [YarnFunctionDeclaration.Number], ReturnType = YarnFunctionDeclaration.Number }],
        });
        Assert.DoesNotContain(own.Diagnostics, d => d.Severity == YarnSeverity.Error);
    }
}
