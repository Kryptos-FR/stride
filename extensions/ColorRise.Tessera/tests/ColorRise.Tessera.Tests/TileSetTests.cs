using ColorRise.Tessera.Tests.Graphics;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace ColorRise.Tessera.Tests;

[Collection(GraphicsCollection.Name)]
public sealed class TileSetTests
{
    [Fact]
    public void FromTexture_makes_a_tile_per_grid_position()
    {
        var set = WithTexture(64, 48, texture => TileSet.FromTexture(texture, new Int2(16, 16)));
        Assert.Equal(12, set.Tiles.Count);
        Assert.Equal(new Rectangle(48, 32, 16, 16), set.GetTile(TileId.FromGrid(1, 3, 2))!.Region);
        Assert.Equal(16, set.PixelsPerUnit);
        Assert.Equal(new Int2(16, 16), set.TileSize);
        Assert.Null(set.GetTile(TileId.FromGrid(1, 4, 0)));
        Assert.Null(set.GetTile(default));
    }

    [Fact]
    public void FromTexture_drops_the_part_of_the_texture_that_is_not_a_whole_tile()
    {
        var set = WithTexture(40, 20, texture => TileSet.FromTexture(texture, new Int2(16, 16)));
        Assert.Equal(2, set.Tiles.Count);
        Assert.Equal(new Rectangle(16, 0, 16, 16), set.GetTile(TileId.FromGrid(1, 1, 0))!.Region);
    }

    [Fact]
    public void FromTexture_takes_the_pixels_per_unit_it_is_given()
        => Assert.Equal(32, WithTexture(64, 48, texture => TileSet.FromTexture(texture, new Int2(16, 8), pixelsPerUnit: 32)).PixelsPerUnit);

    [Fact]
    public void A_tile_knows_its_texture()
    {
        Texture? made = null;
        var set = WithTexture(32, 16, texture => { made = texture; return TileSet.FromTexture(texture, new Int2(16, 16)); });
        Assert.Same(made, set.GetTile(TileId.FromGrid(1, 0, 0))!.Texture);
    }

    [Theory]
    [InlineData(0, 16)]
    [InlineData(16, 0)]
    [InlineData(-1, 16)]
    public void FromTexture_refuses_a_tile_size_that_is_not_positive(int w, int h)
        => Assert.Throws<ArgumentOutOfRangeException>(() => WithTexture(64, 48, texture => TileSet.FromTexture(texture, new Int2(w, h))));

    [Fact]
    public void A_tile_is_solid_when_its_collision_says_so()
    {
        Assert.True(new Tile { Collision = TileCollision.Solid }.IsSolid);
        Assert.False(new Tile().IsSolid);
    }

    // Makes a white texture on the device of a game without a window, and hands it to the test
    private static T WithTexture<T>(int width, int height, Func<Texture, T> use)
    {
        T result = default!;
        GraphicsHarness.Run(new HarnessOptions(), scene => result = use(TestTextures.FromPixels(scene.Game.GraphicsDevice, width, height, (_, _) => Color.White)));
        return result;
    }
}
