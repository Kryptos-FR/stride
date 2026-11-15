using Stride.Core.Assets;
using Stride.Core.Assets.Analysis;
using Stride.Core.Assets.Compiler;
using Stride.Core.BuildEngine;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;

namespace ColorRise.Saga.Assets;

/// <summary>
/// Compiles a dialogue asset to a <see cref="Content.DialogueDefinition"/>, checked against its story. The story asset is an
/// input: a change to its declarations compiles the dialogue again.
/// </summary>
[AssetCompiler(typeof(DialogueAsset), typeof(AssetCompilationContext))]
public sealed class DialogueAssetCompiler : AssetCompilerBase
{
    public override IEnumerable<BuildDependencyInfo> GetInputTypes(AssetItem assetItem)
    {
        yield return new BuildDependencyInfo(typeof(StoryAsset), typeof(AssetCompilationContext), BuildDependencyType.CompileAsset);
    }

    protected override void Prepare(AssetCompilerContext context, AssetItem assetItem, string targetUrlInStorage, AssetCompilerResult result)
    {
        var dialogue = (DialogueAsset)assetItem.Asset;
        var story = dialogue.Story is { } reference ? assetItem.Package?.FindAsset(reference.Id) ?? assetItem.Package?.Session?.FindAsset(reference.Id) : null;
        if (story?.Asset is not StoryAsset storyAsset)
        {
            result.Error($"{assetItem.Location}: Story: {(dialogue.Story is null ? "the dialogue belongs to no story; pick its story asset." : $"the story '{dialogue.Story.Location}' can't be found.")}");
            return;
        }

        result.BuildSteps = new AssetBuildStep(assetItem);
        result.BuildSteps.Add(new DialogueCommand(targetUrlInStorage, dialogue, storyAsset, assetItem.Package!));
    }

    private sealed class DialogueCommand(string url, DialogueAsset parameters, StoryAsset story, IAssetFinder assetFinder) : AssetCommand<DialogueAsset>(url, parameters, assetFinder)
    {
        protected override void ComputeParameterHash(BinarySerializationWriter writer)
        {
            base.ComputeParameterHash(writer);
            // The story's declarations and ids decide whether the dialogue checks
            var declarations = story.Declarations;
            writer.SerializeExtended(ref declarations, ArchiveMode.Serialize);
            var ids = string.Join("\n", story.Acts.SelectMany(x => x.Scenarios.Select(s => $"scenario.{s.Id}:{string.Join(",", s.Outcomes.Select(o => o.Name))}").Concat(x.Quests.Select(q => $"quest.{q.Id}")).Prepend($"act.{x.Id}")));
            writer.Serialize(ref ids, ArchiveMode.Serialize);
        }

        protected override Task<ResultStatus> DoCommandOverride(ICommandContext commandContext)
        {
            var diagnostics = new List<SagaBuildDiagnostic>();
            var dialogue = SagaContentBuilder.BuildDialogue(Parameters, Url, story, diagnostics);
            if (SagaCompilation.Report(diagnostics, commandContext))
                return Task.FromResult(ResultStatus.Failed);

            var contentManager = new ContentManager(MicrothreadLocalDatabases.ProviderService);
            contentManager.Save(Url, dialogue);
            return Task.FromResult(ResultStatus.Successful);
        }
    }
}
