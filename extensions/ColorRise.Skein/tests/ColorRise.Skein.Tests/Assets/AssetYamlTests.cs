using ColorRise.Skein.Assets;
using ColorRise.Skein.Assets.Compilation;
using Stride.Core.Assets;
using Stride.Core.Assets.Serializers;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Reflection;

namespace ColorRise.Skein.Tests.Assets;

/// <summary>
/// A Yarn project asset is a file Game Studio reads back as it wrote it.
/// </summary>
[Collection(BuildCollection.Name)]
public sealed class AssetYamlTests
{
    static AssetYamlTests()
    {
        AssemblyRegistry.Register(typeof(YarnProject).Assembly, AssemblyCommonCategories.Assets);
        AssemblyRegistry.Register(typeof(YarnProjectAsset).Assembly, AssemblyCommonCategories.Assets);
    }

    private static string Write(Asset asset)
    {
        using var stream = new MemoryStream();
        AssetFileSerializer.Save(stream, asset, null);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static YarnProjectAsset Read(string yaml)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(yaml));
        return (YarnProjectAsset)AssetFileSerializer.Load<Asset>(stream, new UFile("Story.sdyarn")).Asset;
    }

    [Fact]
    public void A_new_project_is_written_and_read_back_the_same()
    {
        var project = new YarnProjectAssetFactory().New();
        project.BaseLanguage = "fr";
        project.ExcludedFiles.Add("Drafts/**");
        project.Localisations.Add(new YarnLocalisation { Language = "en", Strings = new UFile("Strings/en.csv") });
        project.LineIdStyle = YarnLineIdStyle.Descriptive;
        project.Saliency = YarnSaliency.First;
        project.WriteYarnProjectFile = false;

        var yaml = Write(project);
        var read = Read(yaml);

        Assert.StartsWith("!YarnProjectAsset", yaml);
        Assert.Contains($"ColorRise.Skein: {SkeinAssetFormat.CurrentVersion}", yaml);
        Assert.Equal(project.Id, read.Id);
        Assert.Equal(["**/*.yarn"], read.SourceFiles);
        Assert.Equal(["Drafts/**"], read.ExcludedFiles);
        Assert.Equal("fr", read.BaseLanguage);
        var localisation = Assert.Single(read.Localisations);
        Assert.Equal("en", localisation.Language);
        Assert.Equal("Strings/en.csv", localisation.Strings?.FullPath);
        Assert.Equal(YarnLineIdStyle.Descriptive, read.LineIdStyle);
        Assert.Equal(YarnSaliency.First, read.Saliency);
        Assert.Equal(YarnSeverity.Error, Assert.Single(read.DiagnosticSeverities, s => s.Key == "YS0012").Value);
        Assert.False(read.WriteYarnProjectFile);
        Assert.Equal(yaml, Write(read));
    }

    [Fact]
    public void A_project_of_format_1_0_gets_its_list_of_character_colours_as_a_dictionary()
    {
        const string Old = """
            !YarnProjectAsset
            Id: 7c0a2a55-3b0e-4a43-9d3c-0d3b7a4a8f10
            SerializedVersion: {ColorRise.Skein: 1.0.0.0}
            Tags: []
            CharacterColors:
                0a1b2c3d4e5f60718293a4b5c6d7e8f9:
                    Name: ' Odile '
                    Color: {R: 1.0, G: 0.0, B: 0.0}
                1a1b2c3d4e5f60718293a4b5c6d7e8f9:
                    Name: ODILE
                    Color: {R: 0.0, G: 1.0, B: 0.0}
                2a1b2c3d4e5f60718293a4b5c6d7e8f9:
                    Name: ''
                    Color: {R: 0.0, G: 0.0, B: 1.0}
                3a1b2c3d4e5f60718293a4b5c6d7e8f9:
                    Name: Fisher
            WriteYarnProjectFile: true

            """;
        var file = Path.Combine(Path.GetTempPath(), $"skein-upgrade-{Guid.NewGuid():N}.sdyarn");
        File.WriteAllText(file, Old.ReplaceLineEndings("\n"));
        try
        {
            var loading = new PackageLoadingAssetFile(new UFile(file), new UDirectory(Path.GetTempPath()));
            var log = new LoggerResult();

            Assert.True(AssetMigration.MigrateAssetIfNeeded(new AssetMigrationContext(null, null, file, log), loading), log.ToText());

            using var stream = new MemoryStream(loading.AssetContent);
            var read = (YarnProjectAsset)AssetFileSerializer.Load<Asset>(stream, new UFile(file)).Asset;
            Assert.Equal(new Dictionary<string, Color3> { ["Odile"] = new(1, 0, 0), ["Fisher"] = new(0.56f, 0.78f, 0.91f) }, read.CharacterColors);
            Assert.True(read.WriteYarnProjectFile);
            Assert.Contains($"ColorRise.Skein: {SkeinAssetFormat.CurrentVersion}", Write(read));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void A_project_file_written_by_hand_reads_with_what_it_leaves_out_at_its_default()
    {
        var read = Read("!YarnProjectAsset\nId: 7c0a2a55-3b0e-4a43-9d3c-0d3b7a4a8f10\nSerializedVersion: {ColorRise.Skein: 1.0.0.0}\nTags: []\n");

        Assert.Empty(read.SourceFiles);
        Assert.Equal("en", read.BaseLanguage);
        Assert.Equal(YarnSaliency.RandomBestLeastRecentlyViewed, read.Saliency);
        Assert.Empty(read.DiagnosticSeverities);
        Assert.True(read.WriteYarnProjectFile);
    }
}
