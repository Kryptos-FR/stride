using ColorRise.Tessera;
using Stride.Core.Mathematics;

namespace Thornwick.Tests;

/// <summary>
/// The cells of the layers of a map that hold a tile.
/// </summary>
internal static class MapCells
{
    /// <summary>
    /// The cells of a layer that are not empty, with their values.
    /// </summary>
    public static IEnumerable<(Int2 Cell, TileCell Value)> Of(TileLayer layer)
    {
        foreach (var chunk in layer.Chunks)
        {
            var values = chunk.Cells.ToArray();
            for (var index = 0; index < values.Length; index++)
            {
                if (!values[index].IsEmpty)
                {
                    yield return (chunk.Position * TileChunk.Size + new Int2(index % TileChunk.Size, index / TileChunk.Size), values[index]);
                }
            }
        }
    }

    /// <summary>
    /// The cells of a map that hold, on some layer, a tile that matches.
    /// </summary>
    public static HashSet<Int2> Where(TileMap map, Func<Tile, bool> match)
        => [.. map.Layers.SelectMany(Of).Where(pair => map.GetTile(pair.Value) is { } tile && match(tile)).Select(pair => pair.Cell)];
}
