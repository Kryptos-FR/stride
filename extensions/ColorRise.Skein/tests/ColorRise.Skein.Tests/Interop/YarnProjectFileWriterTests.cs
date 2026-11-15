using System.Text.Json.Nodes;
using ColorRise.Skein.Assets;
using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Assets.Interop;
using Stride.Core.IO;

namespace ColorRise.Skein.Tests.Interop;

/// <summary>
/// The project file is Yarn Spinner's: its own code reads what Skein writes, and what another tool keeps there stays.
/// </summary>
public sealed class YarnProjectFileWriterTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "skein-" + Guid.NewGuid().ToString("N"));

    public YarnProjectFileWriterTests()
    {
        Directory.CreateDirectory(folder);
    }

    public void Dispose()
    {
        Directory.Delete(folder, true);
    }

    private YarnProjectAsset Asset() => new()
    {
        SourceFiles = { "**/*.yarn", "../Shared/Common.yarn" },
        ExcludedFiles = { "Drafts/**" },
        BaseLanguage = "en",
        Localisations =
        {
            new YarnLocalisation { Language = "fr", Strings = new UFile(Path.Combine(folder, "Strings", "fr.csv")) },
            new YarnLocalisation { Language = "de", Strings = new UFile("Strings/de.csv") },
            new YarnLocalisation { Language = "it" },
        },
        DiagnosticSeverities = { ["YS0012"] = YarnSeverity.Error, ["YS0002"] = YarnSeverity.None },
    };

    [Fact]
    public void A_new_file_is_one_Yarn_Spinner_reads_with_what_the_asset_holds()
    {
        var path = Path.Combine(folder, "Story.yarnproject");
        File.WriteAllText(path, YarnProjectFileWriter.Merge(null, Asset(), folder, "Story.ysls.json"));

        var project = Yarn.Compiler.Project.LoadFromFile(path);

        Assert.Equal(4, project.FileVersion);
        Assert.Equal(["**/*.yarn", "../Shared/Common.yarn"], project.SourceFilePatterns);
        Assert.Equal(["Drafts/**"], project.ExcludeFilePatterns);
        Assert.Equal("en", project.BaseLanguage);
        Assert.Equal(["fr", "de"], project.Localisation.Keys);
        Assert.Equal("Strings/fr.csv", project.Localisation["fr"].Strings);
        Assert.Equal("Strings/de.csv", project.Localisation["de"].Strings);
        Assert.Equal(["Story.ysls.json"], project.Definitions);
        Assert.Equal(Yarn.Compiler.Diagnostic.DiagnosticSeverity.Error, project.CompilerOptions.DiagnosticsSeverity["YS0012"]);
        Assert.Equal(Yarn.Compiler.Diagnostic.DiagnosticSeverity.None, project.CompilerOptions.DiagnosticsSeverity["YS0002"]);
    }

    [Fact]
    public void A_file_that_is_there_keeps_what_is_not_of_the_asset()
    {
        const string Existing = """
            {
                // Written by another tool
                "projectFileVersion": 3,
                "sourceFiles": ["Old/*.yarn"],
                "excludeFiles": ["Gone/**"],
                "editorOptions": {"x": 1, "list": [1, 2.50, "é"]},
                "baseLanguage": "en-AU",
                "localisation": {
                    "en": {"assets": "../Audio/English"},
                    "fr": {"assets": "../Audio/French", "strings": "old.csv"},
                    "es": {"strings": "es.csv"},
                },
                "definitions": "Other.ysls.json",
                "compilerOptions": {"diagnosticsSeverity": {"YS0001": "Info"}, "allowPreviewFeatures": true},
            }
            """;
        var asset = Asset();
        asset.ExcludedFiles.Clear();

        var merged = JsonNode.Parse(YarnProjectFileWriter.Merge(Existing, asset, folder, "Story.ysls.json"))!.AsObject();

        Assert.Equal(
            ["projectFileVersion", "sourceFiles", "editorOptions", "baseLanguage", "localisation", "definitions", "compilerOptions"],
            merged.Select(p => p.Key));
        Assert.Equal(3, merged["projectFileVersion"]!.GetValue<int>());
        Assert.Equal("""{"x":1,"list":[1,2.50,"é"]}""", merged["editorOptions"]!.ToJsonString(new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Assert.Equal("""["**/*.yarn","../Shared/Common.yarn"]""", merged["sourceFiles"]!.ToJsonString());
        Assert.Equal("en", merged["baseLanguage"]!.GetValue<string>());
        Assert.Equal(
            """{"en":{"assets":"../Audio/English"},"fr":{"assets":"../Audio/French","strings":"Strings/fr.csv"},"de":{"strings":"Strings/de.csv"}}""",
            merged["localisation"]!.ToJsonString());
        Assert.Equal("""["Other.ysls.json","Story.ysls.json"]""", merged["definitions"]!.ToJsonString());
        Assert.Equal("""{"diagnosticsSeverity":{"YS0002":"None","YS0012":"Error"},"allowPreviewFeatures":true}""", merged["compilerOptions"]!.ToJsonString());
    }

    [Fact]
    public void Writing_again_changes_nothing()
    {
        var once = YarnProjectFileWriter.Merge(null, Asset(), folder, "Story.ysls.json");

        Assert.Equal(once, YarnProjectFileWriter.Merge(once, Asset(), folder, "Story.ysls.json"));
        Assert.EndsWith("}\n", once);
    }

    [Fact]
    public void An_asset_with_nothing_more_than_its_scripts_gives_the_smallest_file()
    {
        var text = YarnProjectFileWriter.Merge("", new YarnProjectAssetFactory().New(), folder, "");

        Assert.Equal(
            """
            {
                "projectFileVersion": 4,
                "sourceFiles": [
                    "**/*.yarn"
                ],
                "baseLanguage": "en",
                "compilerOptions": {
                    "diagnosticsSeverity": {
                        "YS0012": "Error"
                    }
                }
            }

            """.ReplaceLineEndings("\n"),
            text.ReplaceLineEndings("\n"));
    }

    [Theory]
    [InlineData("[1, 2]", "The Yarn project file is not a JSON object.")]
    [InlineData("{ \"sourceFiles\": ", "The Yarn project file is not valid JSON")]
    public void A_file_that_is_not_a_project_file_is_said_and_never_overwritten(string existing, string message)
    {
        var error = Assert.Throws<FormatException>(() => YarnProjectFileWriter.Merge(existing, Asset(), folder, ""));

        Assert.StartsWith(message, error.Message);
    }
}
