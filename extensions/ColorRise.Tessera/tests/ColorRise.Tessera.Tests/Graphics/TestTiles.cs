using Stride.Core.Mathematics;
using Stride.Graphics;

namespace ColorRise.Tessera.Tests.Graphics;

/// <summary>
/// Tile sets and maps made by the tests.
/// </summary>
internal static class TestTiles
{
    /// <summary>
    /// A tile set of one texture: a flat tile of 4 by 4 pixels per colour, in a row. The tile of colours[i] is 1:i,0, one unit wide.
    /// </summary>
    public static TileSet Flat(GraphicsDevice device, params Color[] colors)
    {
        var texture = TestTextures.FromPixels(device, 4 * colors.Length, 4, (x, _) => colors[x / 4]);
        return TileSet.FromTexture(texture, new Int2(4, 4), pixelsPerUnit: 4);
    }

    /// <summary>
    /// A tile set of one tile, 1:0,0, the quarters texture of 8 by 8 pixels, one unit wide.
    /// </summary>
    public static TileSet Quarters(GraphicsDevice device)
        => TileSet.FromTexture(TestTextures.Quarters(device), new Int2(8, 8), pixelsPerUnit: 8);

    /// <summary>
    /// A map of one tile set, in slot 1, and one layer per array of rows, named Layer0, Layer1 and on. A row is tokens split by
    /// spaces, the top row first: <c>.</c> is empty, else the column of the tile in the set's row 0, with flips after it, as in
    /// a chunk's text (<c>0</c>, <c>2h</c>, <c>1dv</c>). The bottom row is y = 0.
    /// </summary>
    public static TileMap Map(TileSet set, params string[][] layers)
    {
        var map = new TileMap();
        map.AddTileSet(set);
        for (var index = 0; index < layers.Length; index++)
        {
            var layer = map.Layers.Add($"Layer{index}");
            var rows = layers[index];
            for (var row = 0; row < rows.Length; row++)
            {
                var tokens = rows[row].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (var x = 0; x < tokens.Length; x++)
                {
                    if (tokens[x] != ".")
                    {
                        var letters = tokens[x].TrimStart("0123456789".ToCharArray());
                        var column = int.Parse(tokens[x][..^letters.Length]);
                        var flip = (letters.Contains('d') ? TileFlip.Diagonal : 0) | (letters.Contains('h') ? TileFlip.Horizontal : 0)
                            | (letters.Contains('v') ? TileFlip.Vertical : 0);
                        layer.SetCell(new Int2(x, rows.Length - 1 - row), set, TileId.FromGrid(1, column, 0), flip);
                    }
                }
            }
        }
        return map;
    }
}
