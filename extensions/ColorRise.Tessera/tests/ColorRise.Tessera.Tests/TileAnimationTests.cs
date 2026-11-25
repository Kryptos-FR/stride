namespace ColorRise.Tessera.Tests;

public sealed class TileAnimationTests
{
    [Theory]
    // frames: 1:1,0 for 0.25 s, then 1:2,0 for 0.5 s
    [InlineData(0, "1:1,0")]
    [InlineData(0.2499, "1:1,0")]
    [InlineData(0.25, "1:2,0")]
    [InlineData(0.7499, "1:2,0")]
    [InlineData(0.75, "1:1,0")]
    [InlineData(75.3, "1:2,0")]
    [InlineData(-0.1, "1:2,0")]
    public void The_frame_at_a_time_loops(double time, string expected)
    {
        var tile = Animated((TileId.FromGrid(1, 1, 0), 0.25f), (TileId.FromGrid(1, 2, 0), 0.5f));
        Assert.Equal(TileId.Parse(expected), tile.FrameAt(time));
    }

    [Fact]
    public void A_tile_without_frames_is_itself()
        => Assert.Equal(TileId.FromGrid(1, 0, 0), new Tile { Id = TileId.FromGrid(1, 0, 0) }.FrameAt(12.5));

    [Fact]
    public void Frames_whose_durations_are_not_positive_show_the_first()
    {
        var tile = Animated((TileId.FromGrid(1, 1, 0), 0), (TileId.FromGrid(1, 2, 0), -1));
        Assert.Equal(TileId.FromGrid(1, 1, 0), tile.FrameAt(5));
    }

    [Fact]
    public void A_frame_of_no_time_is_skipped()
    {
        var tile = Animated((TileId.FromGrid(1, 1, 0), 0.5f), (TileId.FromGrid(1, 2, 0), 0), (TileId.FromGrid(1, 3, 0), 0.5f));
        Assert.Equal(TileId.FromGrid(1, 3, 0), tile.FrameAt(0.5));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void A_time_that_is_not_a_number_shows_the_first_frame(double time)
        => Assert.Equal(TileId.FromGrid(1, 1, 0), Animated((TileId.FromGrid(1, 1, 0), 0.25f), (TileId.FromGrid(1, 2, 0), 0.5f)).FrameAt(time));

    // The editors have the frames of a definition and no Tile
    [Fact]
    public void The_frames_of_a_definition_give_the_same_tile_as_the_tile()
    {
        var tile = Animated((TileId.FromGrid(1, 1, 0), 0.25f), (TileId.FromGrid(1, 2, 0), 0.5f));
        foreach (var time in new[] { 0, 0.3, 0.8, -0.1 })
        {
            Assert.Equal(tile.FrameAt(time), Tile.FrameAt(tile.Animation, tile.Id, time));
        }
        Assert.Equal(TileId.FromGrid(1, 0, 0), Tile.FrameAt([], TileId.FromGrid(1, 0, 0), 0.3));
    }

    private static Tile Animated(params (TileId Tile, float Duration)[] frames)
    {
        var tile = new Tile { Id = TileId.FromGrid(1, 0, 0) };
        foreach (var (id, duration) in frames)
        {
            tile.Animation.Add(new TileAnimationFrame { Tile = id, Duration = duration });
        }
        return tile;
    }
}
