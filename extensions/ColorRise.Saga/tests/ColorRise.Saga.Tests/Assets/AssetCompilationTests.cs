using ColorRise.Saga.Assets;
using ColorRise.Saga.Content;
using Stride.Core.Assets;
using Stride.Core.Assets.Compiler;
using Stride.Core.IO;
using Stride.Core.BuildEngine;
using Stride.Core.Reflection;
using Stride.Core.Serialization;
using Xunit;

namespace ColorRise.Saga.Tests.Assets;

public sealed class AssetCompilationTests
{
    static AssetCompilationTests()
    {
        // What the asset compiler and Game Studio do when they load the Assets companion
        AssemblyRegistry.Register(typeof(SagaSystem).Assembly, AssemblyCommonCategories.Assets);
        AssemblyRegistry.Register(typeof(StoryAsset).Assembly, AssemblyCommonCategories.Assets);
        // A package session needs MSBuild, as in the engine's own compiler tests
        PackageSessionPublicHelper.FindAndSetMSBuildVersion();
    }

    [Fact]
    public void CompilingStoryAlsoCompilesItsDialogues()
    {
        // A package path is never written; compilers only need asset paths to be absolute
        var package = new Package { FullPath = new UFile(Path.Combine(Path.GetTempPath(), "SagaCompilation", "SagaCompilation.sdpkg")) };
        _ = new PackageSession(package);
        var story = new StoryAssetFactory().New();
        var storyItem = new AssetItem("Story/Main", story);
        var dialogueItem = new AssetItem("Dialogues/Guards", new DialogueAsset { Story = new AssetReference(storyItem.Id, storyItem.Location) });
        story.Acts[0].Scenarios.Add(new ScenarioCard
        {
            Id = "siege",
            Dialogues = { AttachedReferenceManager.CreateProxyObject<DialogueDefinition>(dialogueItem.Id, dialogueItem.Location) },
        });
        package.Assets.Add(storyItem);
        package.Assets.Add(dialogueItem);
        package.RootAssets.Add(new AssetReference(storyItem.Id, storyItem.Location));

        var result = new PackageCompiler(new RootPackageAssetEnumerator(package)).Prepare(new AssetCompilerContext { CompilationContext = typeof(AssetCompilationContext) });

        Assert.False(result.HasErrors);
        var compiled = Steps(result.BuildSteps).OfType<AssetBuildStep>().Select(x => x.AssetItem.Location.FullPath).ToList();
        Assert.Contains("Story/Main", compiled);
        Assert.Contains("Dialogues/Guards", compiled);
    }

    private static IEnumerable<BuildStep> Steps(ListBuildStep steps) => steps.Steps.SelectMany(x => x is ListBuildStep list ? Steps(list).Prepend(x) : [x]);
}
