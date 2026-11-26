using ColorRise.Tessera;
using Stride.Core.Mathematics;

namespace Thornwick;

/// <summary>
/// The bushes the hero cuts.
/// </summary>
public static class Brambles
{
    /// <summary>
    /// Cuts the bush of a cell. The topmost layer whose tile there has the kind "bush" gets, in that cell, the tile the
    /// string property "cut" names, of the same tile set and with the same flip; the cell is emptied when the property is
    /// missing, is no tile id, or names a tile the set does not have. False when the cell has no bush.
    /// </summary>
    public static bool TryCut(TileMap map, Int2 cell)
    {
        for (var index = map.Layers.Count - 1; index >= 0; index--)
        {
            var layer = map.Layers[index];
            var value = layer.GetCell(cell);
            if (map.GetTile(value) is not { Kind: TileKinds.Bush } bush)
            {
                continue;
            }
            var left = TryGetCut(map, value.Slot, bush, out var tile) ? new TileCell(value.Slot, tile, value.Flip) : TileCell.Empty;
            layer.SetCell(cell, left);
            return true;
        }
        return false;
    }

    /// <summary>
    /// How many cells of the map hold a bush.
    /// </summary>
    public static int Count(TileMap map) => TileKinds.CellsOf(map, TileKinds.Bush).Count;

    // The tile a bush leaves, when its property names one that its tile set has
    private static bool TryGetCut(TileMap map, ushort slot, Tile bush, out TileId tile)
    {
        tile = default;
        return bush.Properties.TryGetValue(TileProperties.Cut, out var cut)
            && cut is string text
            && TileId.TryParse(text, out tile)
            && map.TileSets.TryGetValue(slot, out var tileSet)
            && tileSet.Tiles.ContainsKey(tile);
    }
}
