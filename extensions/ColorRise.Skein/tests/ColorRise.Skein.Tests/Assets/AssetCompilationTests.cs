using ColorRise.Skein.Assets;
using Stride.Core.Assets;
using Stride.Core.Assets.Compiler;
using Stride.Core.BuildEngine;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Reflection;
using Stride.Core.Serialization.Contents;

namespace ColorRise.Skein.Tests.Assets;

/// <summary>
/// The build of a game compiles a Yarn project asset with the scripts it includes.
/// </summary>
[Collection(BuildCollection.Name)]
public sealed class AssetCompilationTests : IDisposable
{
    private const string Act1 = "title: Start\n---\nOdile: The post is late. #line:post\n<<jump Pier>>\n===\n";
    private const string Pier = "title: Pier\n---\nFisher: Mind the nets. #line:nets\n===\n";

    private readonly string buildPath = Path.Combine(Path.GetTempPath(), "SkeinBuild-" + Path.GetRandomFileName());
    private readonly Package package;
    private readonly AssetItem project;

    static AssetCompilationTests()
    {
        // What the asset compiler and Game Studio do when they load Skein and its Assets companion
        AssemblyRegistry.Register(typeof(YarnProject).Assembly, AssemblyCommonCategories.Assets);
        AssemblyRegistry.Register(typeof(YarnProjectAsset).Assembly, AssemblyCommonCategories.Assets);

        // A package session needs MSBuild, as in the engine's own compiler tests
        PackageSessionPublicHelper.FindAndSetMSBuildVersion();
    }

    public AssetCompilationTests()
    {
        // The package is never written; the compilers only need its paths to be absolute
        package = new Package { FullPath = new UFile(Path.Combine(buildPath, "Game", "Game.sdpkg")) };
        _ = new PackageSession(package);
        project = Add("Dialogue/Story", new YarnProjectAssetFactory().New());
        package.RootAssets.Add(new AssetReference(project.Id, project.Location));
        Add("Dialogue/Act1", new YarnScriptAsset { Text = Act1 });
        Add("Dialogue/Chatter/Pier", new YarnScriptAsset { Text = Pier });
    }

    public void Dispose()
    {
        if (Directory.Exists(buildPath))
            Directory.Delete(buildPath, recursive: true);
    }

    private AssetItem Add(string location, Asset asset)
    {
        var item = new AssetItem(location, asset);
        package.Assets.Add(item);
        return item;
    }

    private YarnScriptAsset Script(string location) => (YarnScriptAsset)package.Assets.Find(location)!.Asset;

    /// <summary>
    /// Builds the package as the asset compiler does, into the same folder at each call.
    /// </summary>
    private (AssetCompilerResult Prepared, ResultStatus Status, List<string> Log, Builder Builder) Build()
    {
        var prepared = new PackageCompiler(new RootPackageAssetEnumerator(package)).Prepare(new AssetCompilerContext { CompilationContext = typeof(AssetCompilationContext) });
        var log = new LoggerResult();

        // Opened here, the database stays open after the run, for the test to read what was built
        Builder.OpenObjectDatabase(buildPath, "index");
        var builder = new Builder(log, buildPath, "index");
        builder.Root.Add(prepared.BuildSteps);
        builder.Run(Builder.Mode.Build, false);
        var command = Steps(prepared.BuildSteps).OfType<CommandBuildStep>().Single();
        return (prepared, command.Status, [.. log.Messages.Where(m => m.Type >= LogMessageType.Warning).Select(m => $"{m.Type}: {m.Text}")], builder);
    }

    private static IEnumerable<BuildStep> Steps(BuildStep step) => step is ListBuildStep list ? list.Steps.SelectMany(Steps) : [step];

    [Fact]
    public void A_project_builds_its_content_and_the_lines_of_its_base_language()
    {
        var (prepared, status, log, builder) = Build();
        using var built = builder;

        Assert.False(prepared.HasErrors);
        Assert.Equal(ResultStatus.Successful, status);
        Assert.Empty(log);

        MicrothreadLocalDatabases.MountDatabase([builder.Root.OutputObjects]);
        try
        {
            var content = new ContentManager(MicrothreadLocalDatabases.ProviderService);
            var loaded = content.Load<YarnProject>("Dialogue/Story");
            Assert.Equal(["Pier", "Start"], loaded.Nodes.Select(n => n.Name).Order());
            Assert.Equal(2, loaded.GetProgram().Nodes.Count);

            var strings = content.Load<YarnStringTable>(YarnProject.StringTableUrl("Dialogue/Story", "en"));
            Assert.Equal("Fisher: Mind the nets.", strings.Strings["line:nets"]);
        }
        finally
        {
            MicrothreadLocalDatabases.UnmountDatabase();
        }
    }

    [Fact]
    public void A_change_to_a_script_builds_the_project_again_and_nothing_else_does()
    {
        var (_, first, _, builder1) = Build();
        builder1.Dispose();
        Assert.Equal(ResultStatus.Successful, first);

        var (_, same, _, builder2) = Build();
        builder2.Dispose();
        Assert.Equal(ResultStatus.NotTriggeredWasSuccessful, same);

        Script("Dialogue/Chatter/Pier").Text = Pier.Replace("nets", "ropes");
        var (_, changed, _, builder3) = Build();
        builder3.Dispose();
        Assert.Equal(ResultStatus.Successful, changed);
    }

    [Fact]
    public void An_error_of_a_script_fails_the_build_with_the_script_the_place_and_the_code()
    {
        Script("Dialogue/Act1").Text = "title: Start\n---\n<<if $gold >>\nOdile: Hm.\n===\n";

        var (_, status, log, builder) = Build();
        builder.Dispose();

        Assert.Equal(ResultStatus.Failed, status);
        // The script is named by its file on the disk
        var file = Path.Combine(buildPath, "Game", "Assets", "Dialogue", "Act1.yarn");
        Assert.Contains(log, m => m.StartsWith($"Error: {file}(", StringComparison.Ordinal) && m.Contains("error YS", StringComparison.Ordinal));
    }

    [Fact]
    public void A_warning_of_a_script_passes_the_build_and_is_in_its_log()
    {
        Script("Dialogue/Act1").Text = "title: Start\n---\n<<set $seen = true>>\nOdile: The post is late.\n<<jump Pier>>\n===\n";

        var (_, status, log, builder) = Build();
        builder.Dispose();

        Assert.Equal(ResultStatus.Successful, status);
        Assert.Contains(log, m => m.StartsWith("Warning: ", StringComparison.Ordinal) && m.Contains("Act1.yarn(3,", StringComparison.Ordinal));
    }

    [Fact]
    public void A_script_in_no_project_is_a_warning_of_the_build_said_once()
    {
        Add("Other/Lost", new YarnScriptAsset { Text = Pier.Replace("Pier", "Lost").Replace("nets", "lost") });
        var second = Add("Dialogue/Chatter/Background", new YarnProjectAssetFactory().New());
        package.RootAssets.Add(new AssetReference(second.Id, second.Location));

        var prepared = new PackageCompiler(new RootPackageAssetEnumerator(package)).Prepare(new AssetCompilerContext { CompilationContext = typeof(AssetCompilationContext) });

        Assert.False(prepared.HasErrors);
        var warning = Assert.Single(prepared.Messages, m => m.Type == LogMessageType.Warning);
        Assert.Contains("Other/Lost.yarn is in no Yarn project", warning.Text);

        // Both projects compile the script they share
        Assert.Equal(2, Steps(prepared.BuildSteps).OfType<CommandBuildStep>().Count());
    }
}

/// <summary>
/// The tests that build assets run one after the other: a build mounts its database for the whole process.
/// </summary>
[CollectionDefinition(Name)]
public sealed class BuildCollection
{
    public const string Name = "Build";
}
