using ColorRise.Tessera;
using Stride.Core.Mathematics;

namespace Thornwick;

/// <summary>
/// The kinds of tile the game knows, as the tile sets give them in <see cref="Tile.Kind"/>.
/// </summary>
public static class TileKinds
{
    /// <summary>
    /// A bush the hero cuts.
    /// </summary>
    public const string Bush = "bush";

    /// <summary>
    /// A door to another place.
    /// </summary>
    public const string Door = "door";

    /// <summary>
    /// The cells of a map that hold a tile of a kind on any layer, each once.
    /// </summary>
    internal static HashSet<Int2> CellsOf(TileMap map, string kind)
    {
        var cells = new HashSet<Int2>();
        foreach (var layer in map.Layers)
        {
            foreach (var chunk in layer.Chunks)
            {
                var values = chunk.Cells;
                for (var index = 0; index < values.Length; index++)
                {
                    if (map.GetTile(values[index])?.Kind == kind)
                    {
                        var (x, y) = (index % TileChunk.Size, index / TileChunk.Size);
                        cells.Add(new Int2(chunk.Position.X * TileChunk.Size + x, chunk.Position.Y * TileChunk.Size + y));
                    }
                }
            }
        }
        return cells;
    }
}

/// <summary>
/// The names of the tile properties the game reads, as the tile sets give them in <see cref="Tile.Properties"/>.
/// </summary>
public static class TileProperties
{
    /// <summary>
    /// On a bush: the id of the tile left when it is cut, such as <c>1:5,1</c>.
    /// </summary>
    public const string Cut = "cut";

    /// <summary>
    /// On a door: the name of the place it leads to.
    /// </summary>
    public const string To = "to";

    /// <summary>
    /// On a door: the URL of the map of the place it leads to, loaded when that place has no map yet.
    /// </summary>
    public const string Map = "map";
}
