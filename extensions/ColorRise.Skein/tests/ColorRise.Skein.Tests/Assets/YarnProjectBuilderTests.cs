using ColorRise.Skein.Assets;
using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Assets.Localisation;

namespace ColorRise.Skein.Tests.Assets;

/// <summary>
/// Building a project compiles its scripts together and makes the content of the game.
/// </summary>
public sealed class YarnProjectBuilderTests
{
    private const string Act1 = """
        title: Start
        tags: act1 intro
        colour: red
        ---
        Odile: The post is late. #line:post #mood:worried
        Odile: Again. #line:again
        <<jump Pier>>
        ===
        """;

    private const string Pier = """
        title: Pier
        ---
        Fisher: Mind the nets.
        Odile: Again. #shadow:again
        ===
        """;

    private static YarnProjectAsset Project(params string[] languages)
    {
        var project = new YarnProjectAssetFactory().New();
        project.Localisations.AddRange(languages.Select(l => new YarnLocalisation { Language = l }));
        return project;
    }

    private static YarnProjectBuild Build(YarnProjectAsset project, params (string, string)[] scripts) => YarnProjectBuilder.Build(project, "Dialogue/Story", scripts, []);

    [Fact]
    public void The_scripts_of_a_project_make_one_program_with_its_nodes_and_its_lines()
    {
        var project = Project();
        project.Saliency = YarnSaliency.Best;

        var build = Build(project, ("Dialogue/Act1.yarn", Act1), ("Dialogue/Chatter/Pier.yarn", Pier));

        Assert.Empty(build.Compilation.Diagnostics.Select(d => d.ToString()));
        Assert.False(build.HasErrors);
        Assert.Empty(build.Warnings);
        var content = build.Project!;
        Assert.Equal(["Pier", "Start"], content.Nodes.Select(n => n.Name).Order());
        Assert.Equal(2, content.GetProgram().Nodes.Count);
        Assert.Equal("en", content.BaseLanguage);
        Assert.Equal(["en"], content.Languages);
        Assert.Equal(YarnSaliency.Best, content.Saliency);

        var start = content.Nodes.Single(n => n.Name == "Start");
        Assert.Equal(["act1", "intro"], start.Tags);
        Assert.Equal("red", start.Header("colour"));

        var strings = build.BaseStrings!;
        Assert.Equal("en", strings.Language);
        Assert.Equal("Odile: The post is late.", strings.Strings["line:post"]);
        Assert.Contains("mood:worried", content.LineMetadata["line:post"]);

        // A shadow line has no text of its own: it names the line it repeats
        var shadow = Assert.Single(content.ShadowLines);
        Assert.Equal("line:again", shadow.Value);
        Assert.DoesNotContain(shadow.Key, strings.Strings.Keys);
        Assert.Equal(3, strings.Strings.Count);
    }

    [Fact]
    public void An_error_of_a_script_fails_the_build_and_names_the_script()
    {
        var build = Build(Project(), ("Dialogue/Act1.yarn", "title: Start\n---\n<<if $gold >>\nOdile: Hm.\n===\n"), ("Dialogue/Chatter/Pier.yarn", Pier));

        Assert.True(build.HasErrors);
        Assert.Null(build.Project);
        Assert.Null(build.BaseStrings);
        var error = build.Compilation.Diagnostics.First(d => d.Severity == YarnSeverity.Error);
        Assert.StartsWith("Dialogue/Act1.yarn(", error.ToString());
        Assert.Contains($"error {error.Code}", error.ToString());
        Assert.StartsWith("YS", error.Code);
    }

    [Fact]
    public void A_jump_to_a_node_that_is_not_there_fails_a_new_project_and_passes_when_the_project_says_so()
    {
        (string, string)[] scripts = [("Dialogue/Act1.yarn", "title: Start\n---\n<<jump Nowhere>>\n===\n")];

        var strict = Build(Project(), scripts);
        Assert.True(strict.HasErrors);
        Assert.Equal(YarnProjectAssetFactory.MissingNodeCode, Assert.Single(strict.Compilation.Diagnostics).Code);

        var lenient = Project();
        lenient.DiagnosticSeverities[YarnProjectAssetFactory.MissingNodeCode] = YarnSeverity.Warning;
        var passed = Build(lenient, scripts);
        Assert.False(passed.HasErrors);
        Assert.Equal(YarnSeverity.Warning, Assert.Single(passed.Compilation.Diagnostics).Severity);
    }

    [Fact]
    public void Lines_without_an_id_in_a_translated_project_are_one_warning_that_names_the_first_three()
    {
        var untagged = "title: Docks\n---\nOne.\nTwo. #line:two\nThree.\nFour.\n-> Five\n    Six.\n===\n";

        var build = Build(Project("fr", "de"), ("Dialogue/Act1.yarn", Act1), ("Dialogue/Docks.yarn", untagged), ("Dialogue/Chatter/Pier.yarn", Pier));

        Assert.False(build.HasErrors);
        Assert.Equal(["en", "fr", "de"], build.Project!.Languages);
        // Then one warning for each language without a strings file
        Assert.Equal(3, build.Warnings.Count);
        var warning = build.Warnings[0];
        Assert.StartsWith("Dialogue/Story: 6 lines have no line id", warning);
        Assert.Contains("Dialogue/Chatter/Pier.yarn(3), Dialogue/Docks.yarn(3), Dialogue/Docks.yarn(5) and 3 more", warning);

        // Without another language, lines need no id
        Assert.Empty(Build(Project(), ("Dialogue/Docks.yarn", untagged)).Warnings);
    }

    [Fact]
    public void The_strings_file_of_a_language_makes_its_lines_and_what_it_lacks_is_one_warning()
    {
        var script = "title: Start\n---\nOne. #line:one\nTwo. #line:two\nThree. #line:three\nFour. #line:four\n===\n";
        (string, string)[] scripts = [("Dialogue/Act1.yarn", script)];
        var source = YarnStringsCsv.RowsOf(Build(Project(), scripts).Compilation, "en");
        YarnStringRow[] french =
        [
            source[0] with { Language = "fr", Text = "Un." },
            source[1] with { Language = "fr", Text = "(NEEDS UPDATE) Deux." },
            source[2] with { Language = "fr", Text = "" },
            new() { Language = "fr", Id = "line:gone", Text = "Parti." },
            source[3] with { Language = "fr", Text = "Quatre.", Lock = "00000000" },
        ];

        var build = YarnProjectBuilder.Build(Project("fr", "de"), "Dialogue/Story", scripts, [], new Dictionary<string, IReadOnlyList<YarnStringRow>> { ["fr"] = french });

        Assert.False(build.HasErrors);
        var table = Assert.Single(build.Translations);
        Assert.Equal("fr", table.Language);

        // A line without a translation is left out, for the game to show it in the base language; the translator's mark is not shown
        Assert.Equal(new Dictionary<string, string> { ["line:one"] = "Un.", ["line:two"] = "Deux.", ["line:four"] = "Quatre." }, table.Strings);
        Assert.Equal(4, build.BaseStrings!.Strings.Count);
        Assert.Equal(
            [
                "Dialogue/Story: in 'fr', 1 line has no translation and shows in en, 2 translations were made from a text that changed since, 1 row is of lines the scripts no longer have. Update the strings files of the project.",
                "Dialogue/Story: the language 'de' has no strings file, so its lines show in en. Export the strings of the project and set the file in its localisations.",
            ],
            build.Warnings);
    }

    [Fact]
    public void A_strings_file_that_is_up_to_date_gives_no_warning()
    {
        (string, string)[] scripts = [("Dialogue/Act1.yarn", "title: Start\n---\nOne. #line:one\n===\n")];
        var source = YarnStringsCsv.RowsOf(Build(Project(), scripts).Compilation, "en");

        var build = YarnProjectBuilder.Build(Project("fr"), "Dialogue/Story", scripts, [], new Dictionary<string, IReadOnlyList<YarnStringRow>> { ["fr"] = [source[0] with { Language = "fr", Text = "Un." }] });

        Assert.Empty(build.Warnings);
        Assert.Equal("Un.", Assert.Single(build.Translations).Strings["line:one"]);
    }

    [Fact]
    public void A_project_without_a_script_builds_empty_and_says_so()
    {
        var build = Build(Project());

        Assert.False(build.HasErrors);
        Assert.Empty(build.Project!.Nodes);
        Assert.Contains("includes no script", Assert.Single(build.Warnings));
        Assert.Contains("**/*.yarn", build.Warnings[0]);
    }
}
