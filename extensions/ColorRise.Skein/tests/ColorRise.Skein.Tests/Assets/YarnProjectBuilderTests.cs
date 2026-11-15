using ColorRise.Skein.Assets;
using ColorRise.Skein.Assets.Compilation;

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
        var warning = Assert.Single(build.Warnings);
        Assert.StartsWith("Dialogue/Story: 6 lines have no line id", warning);
        Assert.Contains("Dialogue/Chatter/Pier.yarn(3), Dialogue/Docks.yarn(3), Dialogue/Docks.yarn(5) and 3 more", warning);

        // Without another language, lines need no id
        Assert.Empty(Build(Project(), ("Dialogue/Docks.yarn", untagged)).Warnings);
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
