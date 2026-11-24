using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Rendering;

/// <summary>
/// The corners and the texture coordinates of the quads of a layer.
/// </summary>
internal static class TileQuads
{
    /// <summary>
    /// The world position of the grid corner (x, y) of a layer. Every tile that has this corner gets it from this call, so
    /// two cells that share a corner share its bits too, and no gap opens between them.
    /// </summary>
    public static Vector3 Corner(in Matrix world, Vector2 cellSize, Vector2 layerOffset, float layerZ, int x, int y)
        => Vector3.TransformCoordinate(new Vector3(x * cellSize.X + layerOffset.X, y * cellSize.Y + layerOffset.Y, layerZ), world);

    /// <summary>
    /// The texture coordinates of a tile's region after its flips, in the order bottom-left, bottom-right, top-right, top-left.
    /// </summary>
    /// <remarks>
    /// The flips turn the tile diagonal first, then horizontal, then vertical; the corner of the screen finds its texel by
    /// undoing them in the other order.
    /// </remarks>
    public static void Uvs(Rectangle region, int textureWidth, int textureHeight, TileFlip flip, Span<Vector2> uvs)
    {
        var (left, right) = (region.Left / (float)textureWidth, region.Right / (float)textureWidth);
        var (top, bottom) = (region.Top / (float)textureHeight, region.Bottom / (float)textureHeight);

        // The corners of the screen, with x 0 at the left and y 0 at the top
        ReadOnlySpan<(int X, int Y)> corners = [(0, 1), (1, 1), (1, 0), (0, 0)];
        for (var i = 0; i < corners.Length; i++)
        {
            var (x, y) = corners[i];
            if ((flip & TileFlip.Vertical) != 0)
            {
                y = 1 - y;
            }
            if ((flip & TileFlip.Horizontal) != 0)
            {
                x = 1 - x;
            }
            if ((flip & TileFlip.Diagonal) != 0)
            {
                (x, y) = (y, x);
            }
            uvs[i] = new Vector2(x == 0 ? left : right, y == 0 ? top : bottom);
        }
    }
}
