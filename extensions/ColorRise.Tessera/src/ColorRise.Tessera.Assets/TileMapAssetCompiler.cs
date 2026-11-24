using Stride.Core.Assets;
using Stride.Core.Assets.Compiler;
using Stride.Core.BuildEngine;
using Stride.Core.Serialization.Contents;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// Compiles a tile map asset to a <see cref="TileMap"/> at the asset's URL, whose tile sets are references to their own
/// content. A chunk whose text is not a chunk fails the build, with the map, the layer, the chunk and the line.
/// </summary>
[AssetCompiler(typeof(TileMapAsset), typeof(AssetCompilationContext))]
public sealed class TileMapAssetCompiler : AssetCompilerBase
{
    /// <summary>
    /// The tile sets a map uses: the build of a game compiles them with the map.
    /// </summary>
    public override IEnumerable<Type> GetRuntimeTypes(AssetItem assetItem)
    {
        yield return typeof(TileSet);
    }

    protected override void Prepare(AssetCompilerContext context, AssetItem assetItem, string targetUrlInStorage, AssetCompilerResult result)
    {
        result.BuildSteps = new AssetBuildStep(assetItem);
        result.BuildSteps.Add(new TileMapCommand(targetUrlInStorage, (TileMapAsset)assetItem.Asset, assetItem.Package!));
    }

    // The build engine serializes a command only to run it in another process, which this one never asks for
#pragma warning disable STRDIAG010
    private sealed class TileMapCommand : AssetCommand<TileMapAsset>
#pragma warning restore STRDIAG010
    {
        public TileMapCommand(string url, TileMapAsset parameters, IAssetFinder assetFinder)
            : base(url, parameters, assetFinder)
        {
            // To raise when the content of a map changes for the same asset
            Version = 1;
        }

        protected override Task<ResultStatus> DoCommandOverride(ICommandContext commandContext)
        {
            var errors = new List<string>();
            var map = Parameters.ToTileMap(slot => slot.TileSet, errors);
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    commandContext.Logger.Error($"{Url}: {error}");
                }
                return Task.FromResult(ResultStatus.Failed);
            }

            new ContentManager(MicrothreadLocalDatabases.ProviderService).Save(Url, map);
            return Task.FromResult(ResultStatus.Successful);
        }
    }
}
