using ColorRise.Tessera.Editor.Palette;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// The block of cells a tool paints, and how it flips and turns.
/// </summary>
public sealed class TileStampTests
{
    public static TheoryData<TileFlip> Flips => [.. Enumerable.Range(0, 8).Select(flip => (TileFlip)flip)];

    [Theory]
    [MemberData(nameof(Flips))]
    public void Two_flips_give_the_stamp_back(TileFlip start)
    {
        var stamp = Stamp(start);
        Assert.Equal(stamp, stamp.FlipX().FlipX());
        Assert.Equal(stamp, stamp.FlipY().FlipY());
        Assert.NotEqual(stamp, stamp.FlipX());
        Assert.NotEqual(stamp, stamp.FlipY());
    }

    [Theory]
    [MemberData(nameof(Flips))]
    public void Four_quarter_turns_give_the_stamp_back(TileFlip start)
    {
        var stamp = Stamp(start);
        Assert.Equal(stamp, stamp.RotateClockwise().RotateClockwise().RotateClockwise().RotateClockwise());
        Assert.Equal(stamp, stamp.RotateCounterClockwise().RotateCounterClockwise().RotateCounterClockwise().RotateCounterClockwise());
        Assert.NotEqual(stamp, stamp.RotateClockwise().RotateClockwise());
    }

    [Theory]
    [MemberData(nameof(Flips))]
    public void A_turn_and_its_opposite_cancel(TileFlip start)
    {
        var stamp = Stamp(start);
        Assert.Equal(stamp, stamp.RotateClockwise().RotateCounterClockwise());
        Assert.Equal(stamp, stamp.RotateCounterClockwise().RotateClockwise());
        // Two quarter turns are a half turn: both flips
        Assert.Equal(stamp.FlipX().FlipY(), stamp.RotateClockwise().RotateClockwise());
    }

    [Fact]
    public void Flip_X_mirrors_the_block_and_flips_each_cell()
    {
        var stamp = new TileStamp(new Int2(2, 1), [TestTown.Cell(0, 0), TestTown.Cell(1, 0)]).FlipX();
        Assert.Equal(TestTown.Cell(1, 0, TileFlip.Horizontal), stamp[0, 0]);
        Assert.Equal(TestTown.Cell(0, 0, TileFlip.Horizontal), stamp[1, 0]);
    }

    [Fact]
    public void Flip_Y_mirrors_the_block_and_flips_each_cell()
    {
        var stamp = new TileStamp(new Int2(1, 2), [TestTown.Cell(0, 0), TestTown.Cell(1, 0, TileFlip.Vertical)]).FlipY();
        Assert.Equal(TestTown.Cell(1, 0), stamp[0, 0]);
        Assert.Equal(TestTown.Cell(0, 0, TileFlip.Vertical), stamp[0, 1]);
    }

    [Fact]
    public void A_clockwise_turn_stands_a_row_up_with_its_left_cell_on_top()
    {
        var stamp = new TileStamp(new Int2(2, 1), [TestTown.Cell(0, 0), TestTown.Cell(1, 0)]).RotateClockwise();
        Assert.Equal(new Int2(1, 2), stamp.Size);
        Assert.Equal(TestTown.Cell(0, 0, TileFlip.Diagonal | TileFlip.Horizontal), stamp[0, 1]);
        Assert.Equal(TestTown.Cell(1, 0, TileFlip.Diagonal | TileFlip.Horizontal), stamp[0, 0]);
    }

    [Fact]
    public void A_counter_clockwise_turn_stands_a_row_up_with_its_right_cell_on_top()
    {
        var stamp = new TileStamp(new Int2(2, 1), [TestTown.Cell(0, 0), TestTown.Cell(1, 0)]).RotateCounterClockwise();
        Assert.Equal(new Int2(1, 2), stamp.Size);
        Assert.Equal(TestTown.Cell(0, 0, TileFlip.Diagonal | TileFlip.Vertical), stamp[0, 0]);
        Assert.Equal(TestTown.Cell(1, 0, TileFlip.Diagonal | TileFlip.Vertical), stamp[0, 1]);
    }

    [Fact]
    public void An_empty_cell_stays_empty_through_flips_and_turns()
    {
        var stamp = new TileStamp(new Int2(2, 1), [TileCell.Empty, TestTown.Cell(1, 0)]);
        Assert.False(stamp.IsEmpty);
        Assert.True(stamp.FlipX()[1, 0].IsEmpty);
        Assert.True(stamp.FlipY()[0, 0].IsEmpty);
        Assert.True(stamp.RotateClockwise()[0, 1].IsEmpty);
        Assert.True(stamp.RotateCounterClockwise()[0, 0].IsEmpty);
        Assert.True(new TileStamp(new Int2(1, 1), [TileCell.Empty]).IsEmpty);
        Assert.Equal(new TileStamp(new Int2(1, 1), [TestTown.Cell(2, 1)]), TileStamp.Single(TestTown.Cell(2, 1)));
    }

    [Fact]
    public void A_stamp_of_another_size_than_its_cells_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new TileStamp(new Int2(2, 2), [TestTown.Cell(0, 0)]));
        Assert.Throws<ArgumentException>(() => new TileStamp(new Int2(0, 0), []));
    }

    // 3 by 2, six different tiles, each with the flips given
    private static TileStamp Stamp(TileFlip flip)
        => new(new Int2(3, 2), [.. Enumerable.Range(0, 6).Select(index => TestTown.Cell(index % 3, index / 3, flip))]);
}
