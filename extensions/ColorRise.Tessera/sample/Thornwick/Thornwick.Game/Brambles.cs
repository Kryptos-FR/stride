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
    /// missing, or is wrong: no tile id, or a tile the set does not have. False when the cell has no bush.
    /// </summary>
    public static bool TryCut(TileMap map, Int2 cell) => TryCut(map, cell, out _);

    /// <summary>
    /// Cuts the bush of a cell as <see cref="TryCut(TileMap, Int2)"/> does, and says what is wrong with a "cut" that names
    /// no tile; the problem is null otherwise.
    /// </summary>
    public static bool TryCut(TileMap map, Int2 cell, out string? problem)
    {
        problem = null;
        for (var index = map.Layers.Count - 1; index >= 0; index--)
        {
            var layer = map.Layers[index];
            var value = layer.GetCell(cell);
            if (map.GetTile(value) is not { Kind: TileKinds.Bush } bush)
            {
                continue;
            }
            var left = TileCell.Empty;
            if (bush.Properties.TryGetValue(TileProperties.Cut, out var cut))
            {
                if (cut is string text && TileId.TryParse(text, out var tile) && map.TileSets.TryGetValue(value.Slot, out var tileSet)
                    && tileSet.Tiles.ContainsKey(tile))
                {
                    left = new TileCell(value.Slot, tile, value.Flip);
                }
                else
                {
                    problem = $"The bush at ({cell.X}, {cell.Y}) has '{TileProperties.Cut}' = '{cut}', which is no tile of its tile set: the cell is emptied.";
                }
            }
            layer.SetCell(cell, left);
            return true;
        }
        return false;
    }

    /// <summary>
    /// How many cells of the map hold a bush.
    /// </summary>
    public static int Count(TileMap map) => TileKinds.CellsOf(map, TileKinds.Bush).Count;
}
