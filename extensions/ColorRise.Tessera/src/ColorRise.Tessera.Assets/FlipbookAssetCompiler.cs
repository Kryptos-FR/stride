using Stride.Core.Assets;
using Stride.Core.Assets.Compiler;
using Stride.Core.BuildEngine;
using Stride.Core.Serialization.Contents;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// Compiles a flipbook asset to a <see cref="Flipbook"/> at the asset's URL, whose tile set is a reference to its own
/// content. What <see cref="FlipbookAssetChecks"/> finds is a warning: the build goes on, and the game plays what it can.
/// </summary>
[AssetCompiler(typeof(FlipbookAsset), typeof(AssetCompilationContext))]
public sealed class FlipbookAssetCompiler : AssetCompilerBase
{
    /// <summary>
    /// The tile set a flipbook uses: the build of a game compiles it with the flipbook.
    /// </summary>
    public override IEnumerable<Type> GetRuntimeTypes(AssetItem assetItem)
    {
        yield return typeof(TileSet);
    }

    /// <summary>
    /// Warns about what is wrong in the flipbook, and adds the command that writes it.
    /// </summary>
    protected override void Prepare(AssetCompilerContext context, AssetItem assetItem, string targetUrlInStorage, AssetCompilerResult result)
    {
        var asset = (FlipbookAsset)assetItem.Asset;
        foreach (var line in FlipbookAssetChecks.Of(asset))
        {
            result.Warning($"{targetUrlInStorage}: {line}");
        }
        result.BuildSteps = new AssetBuildStep(assetItem);
        result.BuildSteps.Add(new FlipbookCommand(targetUrlInStorage, asset, assetItem.Package!));
    }

    // The build engine serializes a command only to run it in another process, which this one never asks for
#pragma warning disable STRDIAG010
    private sealed class FlipbookCommand : AssetCommand<FlipbookAsset>
#pragma warning restore STRDIAG010
    {
        public FlipbookCommand(string url, FlipbookAsset parameters, IAssetFinder assetFinder)
            : base(url, parameters, assetFinder)
        {
        }

        protected override Task<ResultStatus> DoCommandOverride(ICommandContext commandContext)
        {
            new ContentManager(MicrothreadLocalDatabases.ProviderService).Save(Url, Parameters.ToFlipbook());
            return Task.FromResult(ResultStatus.Successful);
        }
    }
}
