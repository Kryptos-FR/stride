using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.ViewModels;
using Stride.Core.Assets.Presentation.Services;
using Stride.Core.Quantum;

namespace ColorRise.Tessera.Editor.TileMapEditor;

/// <summary>
/// Every change the editor makes to a map asset besides its cells, through its graph, so that the history can undo it. The
/// caller owns the transaction.
/// </summary>
internal static class TileMapGraph
{
    /// <summary>
    /// Adds an empty layer at an index of the asset's layers.
    /// </summary>
    public static TileLayerAsset AddLayer(TileMapViewModel map, int index, string name)
    {
        var layer = new TileLayerAsset { Name = name };
        Layers(map).Add(layer, new NodeIndex(index));
        return layer;
    }

    /// <summary>
    /// Removes a layer and its cells.
    /// </summary>
    public static void RemoveLayer(TileMapViewModel map, int index) => Layers(map).Remove(map.Asset.Layers[index], new NodeIndex(index));

    /// <summary>
    /// Moves a layer to another index of the asset's layers.
    /// </summary>
    public static void MoveLayer(TileMapViewModel map, int from, int to)
    {
        var layer = map.Asset.Layers[from];
        var node = Layers(map);
        node.Remove(layer, new NodeIndex(from));
        node.Add(layer, new NodeIndex(to));
    }

    /// <summary>
    /// Gives a layer a name.
    /// </summary>
    public static void RenameLayer(TileMapViewModel map, int index, string name) => Layer(map, index)[nameof(TileLayerAsset.Name)].Update(name);

    /// <summary>
    /// Shows or hides a layer.
    /// </summary>
    public static void SetVisible(TileMapViewModel map, int index, bool visible) => Layer(map, index)[nameof(TileLayerAsset.Visible)].Update(visible);

    /// <summary>
    /// Adds a tile set in a new slot, <c>LastSlot + 1</c>, which becomes the last slot: a slot is never given twice.
    /// </summary>
    /// <exception cref="InvalidOperationException">The map has given every slot.</exception>
    public static TileSetSlot AddTileSet(TileMapViewModel map, TileSetViewModel tileSet)
    {
        var number = map.Asset.LastSlot + 1;
        if (!TileAssetChecks.IsInRange(number))
        {
            throw new InvalidOperationException($"The map has given every slot, up to {ushort.MaxValue}.");
        }
        var root = map.PropertyGraph.RootNode;
        root[nameof(TileMapAsset.LastSlot)].Update(number);
        var slot = new TileSetSlot { Slot = number, TileSet = ContentReferenceHelper.CreateReference<TileSet>(tileSet) };
        var slots = root[nameof(TileMapAsset.TileSets)].Target!;
        slots.Add(slot, new NodeIndex(map.Asset.TileSets.Count));
        return slot;
    }

    /// <summary>
    /// Removes a slot; the cells that name it stay.
    /// </summary>
    public static void RemoveTileSet(TileMapViewModel map, TileSetSlot slot)
    {
        var index = map.Asset.TileSets.IndexOf(slot);
        if (index >= 0)
        {
            map.PropertyGraph.RootNode[nameof(TileMapAsset.TileSets)].Target!.Remove(slot, new NodeIndex(index));
        }
    }

    private static IObjectNode Layers(TileMapViewModel map) => map.PropertyGraph.RootNode[nameof(TileMapAsset.Layers)].Target!;

    private static IObjectNode Layer(TileMapViewModel map, int index) => Layers(map).IndexedTarget(new NodeIndex(index))!;
}
