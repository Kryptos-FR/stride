using Stride.Core.Assets;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// Makes the map a template creates: one layer, "Ground".
/// </summary>
public sealed class TileMapAssetFactory : AssetFactory<TileMapAsset>
{
    /// <summary>
    /// The name of the layer of a new map.
    /// </summary>
    public const string FirstLayerName = "Ground";

    /// <inheritdoc />
    public override TileMapAsset New() => new()
    {
        Layers = { new TileLayerAsset { Name = FirstLayerName } },
    };
}
