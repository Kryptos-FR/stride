using ColorRise.Tessera.Rendering;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Tests;

public sealed class TileQuadsTests
{
    [Fact]
    public void A_corner_shared_by_two_cells_is_the_same_bits()
    {
        var world = Matrix.Translation(0.1f, 0.7f, 0) * Matrix.RotationZ(MathUtil.DegreesToRadians(17));
        var cellSize = new Vector2(1.3f, 0.7f);
        for (var x = -40; x <= 40; x++)
        {
            for (var y = -40; y <= 40; y++)
            {
                Assert.Equal(TileQuads.Corner(world, cellSize, default, 0, x + 1, y), TileQuads.Corner(world, cellSize, default, 0, (x + 2) - 1, (y + 3) - 3));
            }
        }
    }

    [Fact]
    public void A_corner_is_the_grid_point_moved_by_the_offset_at_the_layer_z()
        => Assert.Equal(new Vector3(2 * 1.5f + 0.25f, 3 * 0.5f - 1, 0.2f), TileQuads.Corner(Matrix.Identity, new Vector2(1.5f, 0.5f), new Vector2(0.25f, -1), 0.2f, 2, 3));

    [Theory]
    // region (16, 32, 16, 16) of a 64 by 64 texture: left 0.25, right 0.5, top 0.5, bottom 0.75
    // columns: flip, then u,v of the bottom-left, bottom-right, top-right and top-left corners
    [InlineData(TileFlip.None, 0.25, 0.75, 0.5, 0.75, 0.5, 0.5, 0.25, 0.5)]
    [InlineData(TileFlip.Horizontal, 0.5, 0.75, 0.25, 0.75, 0.25, 0.5, 0.5, 0.5)]
    [InlineData(TileFlip.Vertical, 0.25, 0.5, 0.5, 0.5, 0.5, 0.75, 0.25, 0.75)]
    [InlineData(TileFlip.Diagonal, 0.5, 0.5, 0.5, 0.75, 0.25, 0.75, 0.25, 0.5)]
    public void Uvs_follow_the_flips(TileFlip flip, params double[] expected)
    {
        Span<Vector2> uvs = stackalloc Vector2[4];
        TileQuads.Uvs(new Rectangle(16, 32, 16, 16), 64, 64, flip, uvs);
        for (var corner = 0; corner < 4; corner++)
        {
            Assert.Equal(new Vector2((float)expected[corner * 2], (float)expected[corner * 2 + 1]), uvs[corner]);
        }
    }
}
