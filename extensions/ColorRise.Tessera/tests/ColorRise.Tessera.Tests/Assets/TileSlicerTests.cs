using ColorRise.Tessera.Assets;
using Stride.Core.IO;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Tests.Assets;

public sealed class TileSlicerTests
{
    private static readonly UFile Image = new("Tiles/town.png");

    [Fact]
    public void A_grid_gives_a_tile_per_position_inside_the_image()
    {
        var slices = TileSlicer.Grid(new GridTileSetSource { Id = 1, Image = Image }, new Int2(16, 16), new Int2(64, 48));
        Assert.Equal(12, slices.Count);
        Assert.Equal(TileId.FromGrid(1, 0, 0), slices[0].Tile);
        Assert.Equal(TileId.FromGrid(1, 1, 0), slices[1].Tile);
        Assert.Equal(new TileSlice(TileId.FromGrid(1, 3, 2), Image, new Rectangle(48, 32, 16, 16)), slices[11]);
    }

    [Fact]
    public void Margin_and_spacing_move_the_grid()
    {
        var source = new GridTileSetSource { Id = 2, Image = Image, Margin = new Int2(2, 1), Spacing = new Int2(1, 2) };
        var slices = TileSlicer.Grid(source, new Int2(16, 16), new Int2(70, 36));
        Assert.Equal(8, slices.Count);
        Assert.Equal([2, 19, 36, 53], slices.Take(4).Select(slice => slice.Region.X));
        Assert.Equal([1, 19], slices.Where(slice => slice.Region.X == 2).Select(slice => slice.Region.Y));
        Assert.Equal(new TileSlice(TileId.FromGrid(2, 3, 1), Image, new Rectangle(53, 19, 16, 16)), slices[^1]);
    }

    [Fact]
    public void A_part_of_a_tile_at_the_edge_is_dropped()
        => Assert.Equal(2, TileSlicer.Grid(new GridTileSetSource { Id = 1 }, new Int2(16, 16), new Int2(40, 16)).Count);

    [Fact]
    public void The_tile_size_of_a_source_wins_over_the_tile_sets()
    {
        var slices = TileSlicer.Grid(new GridTileSetSource { Id = 1, TileSize = new Int2(16, 24) }, new Int2(16, 16), new Int2(32, 48));
        Assert.Equal(4, slices.Count);
        Assert.All(slices, slice => Assert.Equal(new Size2(16, 24), new Size2(slice.Region.Width, slice.Region.Height)));
    }

    [Theory]
    [InlineData(0, 16, 0, 0, 0, 0)]      // a width of zero
    [InlineData(16, -4, 0, 0, 0, 0)]     // a height below zero
    [InlineData(128, 16, 0, 0, 0, 0)]    // a tile wider than the image
    [InlineData(16, 16, 80, 0, 0, 0)]    // a margin wider than the image
    [InlineData(16, 16, 0, 0, -20, 0)]   // a spacing that would walk backwards
    [InlineData(16, 16, 0, 0, -16, 0)]   // a spacing that would stand still
    [InlineData(16, 16, -8, 0, 0, 0)]    // a margin below zero
    public void Numbers_that_make_no_tile_give_no_tile_and_do_not_throw(int w, int h, int mx, int my, int sx, int sy)
    {
        var source = new GridTileSetSource { Id = 1, TileSize = new Int2(w, h), Margin = new Int2(mx, my), Spacing = new Int2(sx, sy) };
        Assert.Empty(TileSlicer.Grid(source, new Int2(16, 16), new Int2(64, 48)));
    }

    [Fact]
    public void A_grid_stops_at_the_last_column_and_row_an_id_can_hold()
    {
        Assert.Equal(TileId.MaxColumn + 1, TileSlicer.Grid(new GridTileSetSource { Id = 1, TileSize = new Int2(1, 1) }, default, new Int2(5000, 1)).Count);
        Assert.Equal(TileId.MaxRow + 1, TileSlicer.Grid(new GridTileSetSource { Id = 1, TileSize = new Int2(1, 1) }, default, new Int2(1, 3000)).Count);
    }

    [Fact]
    public void A_source_whose_id_no_tile_can_have_gives_no_tile()
        => Assert.Empty(TileSlicer.Grid(new GridTileSetSource { Id = 0 }, new Int2(16, 16), new Int2(64, 48)));

    [Fact]
    public void A_region_of_alpha_zero_is_empty_and_one_opaque_pixel_makes_it_a_tile()
    {
        var pixels = new Color[8 * 4];
        var region = new Rectangle(4, 0, 4, 4);
        Assert.True(TileSlicer.IsEmpty(pixels, 8, region, null));

        pixels[3 * 8 + 7] = new Color(10, 20, 30, 1);
        Assert.False(TileSlicer.IsEmpty(pixels, 8, region, null));
        // The pixel is outside the region on its left
        Assert.True(TileSlicer.IsEmpty(pixels, 8, new Rectangle(0, 0, 4, 4), null));
    }

    [Fact]
    public void The_color_key_counts_as_empty()
    {
        var magenta = new Color(255, 0, 255, 255);
        var pixels = Enumerable.Repeat(magenta, 16).ToArray();
        Assert.True(TileSlicer.IsEmpty(pixels, 4, new Rectangle(0, 0, 4, 4), magenta));
        Assert.False(TileSlicer.IsEmpty(pixels, 4, new Rectangle(0, 0, 4, 4), null));
    }

    [Fact]
    public void The_color_key_compares_alpha_too_as_the_texture_build_does()
    {
        var pixels = Enumerable.Repeat(new Color(255, 0, 255, 128), 16).ToArray();
        Assert.False(TileSlicer.IsEmpty(pixels, 4, new Rectangle(0, 0, 4, 4), new Color(255, 0, 255, 255)));
    }

    [Fact]
    public void Sprites_are_bound_by_name_and_keep_their_numbers()
    {
        var source = new SpriteSheetTileSetSource { Id = 2 };
        Assert.True(TileSlicer.Bind(source, ["grass", "water", "rock"]));
        Assert.Equal([("grass", 1), ("water", 2), ("rock", 3)], source.Bindings.Select(b => (b.SpriteName, b.Number)));
        Assert.False(TileSlicer.Bind(source, ["rock", "grass", "water"]));

        var slices = TileSlicer.Sheet(source, [Sprite("rock", 0), Sprite("water", 1), Sprite("grass", 2)], out var missing, out var duplicates, out var unbound);
        Assert.Empty(missing);
        Assert.Empty(duplicates);
        Assert.Equal(0, unbound);
        Assert.Equal(TileId.FromNumber(2, 1), slices.Single(slice => slice.Region == Region(2)).Tile);
        Assert.Equal(TileId.FromNumber(2, 2), slices.Single(slice => slice.Region == Region(1)).Tile);
        Assert.Equal(TileId.FromNumber(2, 3), slices.Single(slice => slice.Region == Region(0)).Tile);
    }

    [Fact]
    public void A_renamed_sprite_is_missing_and_a_new_one_is_unbound()
    {
        var source = new SpriteSheetTileSetSource { Id = 2 };
        TileSlicer.Bind(source, ["grass", "water"]);
        var slices = TileSlicer.Sheet(source, [Sprite("grass", 0), Sprite("pond", 1)], out var missing, out _, out var unbound);
        Assert.Equal("water", Assert.Single(missing).SpriteName);
        Assert.Equal(1, unbound);
        Assert.Equal(TileId.FromNumber(2, 1), Assert.Single(slices).Tile);
    }

    [Fact]
    public void A_name_used_twice_takes_the_first_sprite()
    {
        var source = new SpriteSheetTileSetSource { Id = 2 };
        TileSlicer.Bind(source, ["grass", "grass", "grass"]);
        var slices = TileSlicer.Sheet(source, [Sprite("grass", 0), Sprite("grass", 1), Sprite("grass", 2)], out _, out var duplicates, out var unbound);
        Assert.Equal(Region(0), Assert.Single(slices).Region);
        Assert.Equal(["grass"], duplicates);
        Assert.Equal(0, unbound);
        Assert.Single(source.Bindings);
    }

    [Fact]
    public void A_number_is_not_given_twice()
    {
        var source = new SpriteSheetTileSetSource { Id = 2 };
        TileSlicer.Bind(source, ["a", "b"]);
        source.Bindings.RemoveAll(binding => binding.SpriteName == "b");
        TileSlicer.Bind(source, ["a", "c"]);
        Assert.Equal(3, source.Bindings.Single(binding => binding.SpriteName == "c").Number);
    }

    // The sprite of a sheet at the place i, 16 by 16 pixels in a row
    private static NamedRegion Sprite(string name, int i) => new(name, Image, Region(i));

    private static Rectangle Region(int i) => new(i * 16, 0, 16, 16);
}
