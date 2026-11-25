using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// A layer of a tile map asset: how it looks, and its cells as text, by chunk.
/// </summary>
[DataContract]
public sealed class TileLayerAsset
{
    /// <summary>
    /// The name of the layer, which game code finds it by.
    /// </summary>
    [DataMember(10)]
    public string Name { get; set; } = "";

    /// <summary>
    /// Whether the layer is drawn.
    /// </summary>
    [DataMember(20)]
    public bool Visible { get; set; } = true;

    /// <userdoc>From 0, invisible, to 1, opaque.</userdoc>
    [DataMember(30)]
    [DataMemberRange(0, 1, 0.01, 0.1, 2)]
    public float Opacity { get; set; } = 1;

    /// <userdoc>The colour every tile of the layer is multiplied by.</userdoc>
    [DataMember(40)]
    public Color Tint { get; set; } = Color.White;

    /// <userdoc>Moves the layer when it is drawn, in world units. It changes nothing of where the cells are for the game.</userdoc>
    [DataMember(50)]
    public Vector2 Offset { get; set; }

    /// <userdoc>Whether the solid tiles of the layer stop what moves on the map.</userdoc>
    [DataMember(60)]
    public bool Collision { get; set; } = true;

    /// <summary>
    /// The chunks that have tiles. The file keeps them top to bottom, then left to right.
    /// </summary>
    [DataMember(70)]
    [Display(Browsable = false)]
    [NonIdentifiableCollectionItems]
    public List<TileChunkAsset> Chunks { get; } = [];

    /// <summary>
    /// Orders the chunks as the file keeps them: top to bottom, then left to right.
    /// </summary>
    public void SortChunks()
    {
        var sorted = Chunks.OrderByDescending(chunk => chunk.Position.Y).ThenBy(chunk => chunk.Position.X).ToList();
        Chunks.Clear();
        Chunks.AddRange(sorted);
    }

    internal TileLayer ToTileLayer() => new()
    {
        Name = Name,
        Visible = Visible,
        Opacity = Opacity,
        Tint = Tint,
        Offset = Offset,
        Collision = Collision,
    };

    internal static TileLayerAsset FromTileLayer(TileLayer layer)
    {
        var asset = new TileLayerAsset
        {
            Name = layer.Name,
            Visible = layer.Visible,
            Opacity = layer.Opacity,
            Tint = layer.Tint,
            Offset = layer.Offset,
            Collision = layer.Collision,
        };
        foreach (var chunk in layer.Chunks)
        {
            asset.Chunks.Add(new TileChunkAsset { Position = chunk.Position, Cells = TileChunkText.Encode(chunk.Cells) });
        }
        asset.SortChunks();
        return asset;
    }
}
