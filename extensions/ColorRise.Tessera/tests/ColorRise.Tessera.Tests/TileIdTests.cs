namespace ColorRise.Tessera.Tests;

public sealed class TileIdTests
{
    [Fact]
    public void A_grid_id_keeps_its_source_column_and_row()
    {
        var id = TileId.FromGrid(1, 3, 4);
        Assert.Equal((1, 3, 4, false), (id.Source, id.Column, id.Row, id.IsNumbered));
        Assert.Equal("1:3,4", id.ToString());
        Assert.Equal(id, TileId.Parse("1:3,4"));
    }

    [Fact]
    public void A_numbered_id_keeps_its_source_and_number()
    {
        var id = TileId.FromNumber(2, 17);
        Assert.Equal((2, 17, true), (id.Source, id.Number, id.IsNumbered));
        Assert.Equal("2:#17", id.ToString());
        Assert.Equal(id, TileId.Parse("2:#17"));
    }

    [Fact]
    public void A_grid_id_and_a_numbered_id_differ()
        => Assert.NotEqual(TileId.FromGrid(1, 17, 0), TileId.FromNumber(1, 17));

    [Fact]
    public void The_largest_values_round_trip()
    {
        Assert.Equal("255:4095,2047", TileId.FromGrid(255, 4095, 2047).ToString());
        Assert.Equal(TileId.FromNumber(255, 8388607), TileId.Parse("255:#8388607"));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(256, 0, 0)]
    [InlineData(1, 4096, 0)]
    [InlineData(1, 0, 2048)]
    [InlineData(1, -1, 0)]
    [InlineData(1, 0, -1)]
    public void A_grid_id_out_of_range_throws(int source, int column, int row)
        => Assert.Throws<ArgumentOutOfRangeException>(() => TileId.FromGrid(source, column, row));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(256, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 8388608)]
    public void A_numbered_id_out_of_range_throws(int source, int number)
        => Assert.Throws<ArgumentOutOfRangeException>(() => TileId.FromNumber(source, number));

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1:")]
    [InlineData("1:3")]
    [InlineData("x:3,4")]
    [InlineData("1:#")]
    [InlineData("1:3,4,5")]
    [InlineData("0:1,1")]
    [InlineData("1: 3,4")]
    [InlineData("1:#-2")]
    [InlineData("256:0,0")]
    public void TryParse_refuses(string text) => Assert.False(TileId.TryParse(text, out _));

    [Fact]
    public void Parse_throws_a_format_exception() => Assert.Throws<FormatException>(() => TileId.Parse("1:3"));

    [Fact]
    public void The_default_id_is_none()
    {
        Assert.True(default(TileId).IsNone);
        Assert.Equal("", default(TileId).ToString());
    }
}
