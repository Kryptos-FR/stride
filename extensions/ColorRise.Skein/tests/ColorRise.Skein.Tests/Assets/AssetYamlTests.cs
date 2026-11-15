using ColorRise.Skein.Assets;
using ColorRise.Skein.Assets.Compilation;
using Stride.Core.Assets;
using Stride.Core.Assets.Serializers;
using Stride.Core.IO;
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
        Assert.Contains("ColorRise.Skein: 1.0.0.0", yaml);
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
