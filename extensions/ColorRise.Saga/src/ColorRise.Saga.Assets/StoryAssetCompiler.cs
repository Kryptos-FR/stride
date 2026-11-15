using Stride.Core.Assets;
using Stride.Core.Assets.Compiler;
using Stride.Core.BuildEngine;
using Stride.Core.Diagnostics;
using Stride.Core.Serialization;
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
        var story = (StoryAsset)assetItem.Asset;
        // The checks across the story and its dialogues read the dialogues it references (each once); one that can't be found is
        // reported when the story's content is loaded
        var dialogues = new List<(DialogueAsset Asset, string Url)>();
        foreach (var reference in story.Acts.SelectMany(x => x.Scenarios.SelectMany(s => s.Dialogues).Concat(x.Quests.SelectMany(q => q.Dialogues))))
        {
            if (AttachedReferenceManager.GetAttachedReference(reference) is { } target
                && (assetItem.Package?.FindAsset(target.Id) ?? assetItem.Package?.Session?.FindAsset(target.Id)) is { Asset: DialogueAsset dialogue } item
                && dialogues.All(x => x.Asset != dialogue))
            {
                dialogues.Add((dialogue, item.Location.ToString()));
            }
        }
        result.BuildSteps = new AssetBuildStep(assetItem);
        result.BuildSteps.Add(new StoryCommand(targetUrlInStorage, story, dialogues, assetItem.Package!));
    }

    private sealed class StoryCommand(string url, StoryAsset parameters, List<(DialogueAsset Asset, string Url)> dialogues, IAssetFinder assetFinder)
        : AssetCommand<StoryAsset>(url, parameters, assetFinder)
    {
        protected override void ComputeParameterHash(BinarySerializationWriter writer)
        {
            base.ComputeParameterHash(writer);
            // The warnings depend on the dialogues: a change to one checks the story again
            foreach (var (asset, dialogueUrl) in dialogues)
            {
                var name = dialogueUrl;
                var dialogue = asset;
                writer.Serialize(ref name, ArchiveMode.Serialize);
                writer.SerializeExtended(ref dialogue, ArchiveMode.Serialize);
            }
        }

        protected override Task<ResultStatus> DoCommandOverride(ICommandContext commandContext)
        {
            var diagnostics = new List<SagaBuildDiagnostic>();
            var story = SagaContentBuilder.BuildStory(Parameters, Url, diagnostics);
            SagaContentBuilder.CheckStory(Parameters, Url, dialogues, diagnostics);
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
