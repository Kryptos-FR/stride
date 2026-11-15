using Stride.Core.Assets;
using Stride.Core.Assets.Compiler;
using Stride.Core.BuildEngine;
using Stride.Core.Serialization.Contents;

namespace ColorRise.Saga.Assets;

/// <summary>
/// Compiles a story asset to a <see cref="Content.StoryDefinition"/>. Each problem in the story fails the build step with its
/// asset, card and position.
/// </summary>
[AssetCompiler(typeof(StoryAsset), typeof(AssetCompilationContext))]
public sealed class StoryAssetCompiler : AssetCompilerBase
{
    /// <summary>
    /// The cards whose content references the compiled story keeps: their dialogues are compiled with the story, since nothing
    /// else roots them (see docs/plugin-findings.md, finding 4).
    /// </summary>
    public override IEnumerable<Type> GetRuntimeTypes(AssetItem assetItem)
    {
        yield return typeof(ScenarioCard);
        yield return typeof(QuestCard);
    }

    protected override void Prepare(AssetCompilerContext context, AssetItem assetItem, string targetUrlInStorage, AssetCompilerResult result)
    {
        result.BuildSteps = new AssetBuildStep(assetItem);
        result.BuildSteps.Add(new StoryCommand(targetUrlInStorage, (StoryAsset)assetItem.Asset, assetItem.Package!));
    }

    private sealed class StoryCommand(string url, StoryAsset parameters, IAssetFinder assetFinder) : AssetCommand<StoryAsset>(url, parameters, assetFinder)
    {
        protected override Task<ResultStatus> DoCommandOverride(ICommandContext commandContext)
        {
            var diagnostics = new List<SagaBuildDiagnostic>();
            var story = SagaContentBuilder.BuildStory(Parameters, Url, diagnostics);
            if (SagaCompilation.Report(diagnostics, commandContext))
                return Task.FromResult(ResultStatus.Failed);

            var contentManager = new ContentManager(MicrothreadLocalDatabases.ProviderService);
            contentManager.Save(Url, story);
            return Task.FromResult(ResultStatus.Successful);
        }
    }
}

/// <summary>
/// What the compilers share.
/// </summary>
internal static class SagaCompilation
{
    /// <summary>
    /// Logs the diagnostics as errors of the build step.
    /// </summary>
    /// <returns>Whether there was any.</returns>
    public static bool Report(IReadOnlyCollection<SagaBuildDiagnostic> diagnostics, ICommandContext commandContext)
    {
        foreach (var diagnostic in diagnostics)
            commandContext.Logger.Error(diagnostic.ToString());
        return diagnostics.Count > 0;
    }
}
