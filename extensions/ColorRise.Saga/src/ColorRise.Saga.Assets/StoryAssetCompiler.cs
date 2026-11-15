using Stride.Core.Assets;
using Stride.Core.Assets.Compiler;
using Stride.Core.BuildEngine;
using Stride.Core.Diagnostics;
using Stride.Core.Serialization.Contents;

namespace ColorRise.Saga.Assets;

/// <summary>
/// Compiles a story asset to a <see cref="Content.StoryDefinition"/>. Each error in the story fails the build step with its
/// asset, card and position; warnings are logged.
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
            if (SagaCompilation.Report(diagnostics, commandContext.Logger))
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
public static class SagaCompilation
{
    /// <summary>
    /// Logs the diagnostics of a build step: the errors as errors, the warnings as warnings.
    /// </summary>
    /// <returns>Whether there was an error, which fails the step.</returns>
    public static bool Report(IReadOnlyCollection<SagaBuildDiagnostic> diagnostics, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(logger);
        var failed = false;
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity == SagaDiagnosticSeverity.Warning)
            {
                logger.Warning(diagnostic.ToString());
            }
            else
            {
                logger.Error(diagnostic.ToString());
                failed = true;
            }
        }
        return failed;
    }
}
