using System.Runtime.CompilerServices;

namespace ColorRise.Tessera.Tests;

public sealed class TileCellTests
{
    [Fact]
    public void A_cell_is_eight_bytes() => Assert.Equal(8, Unsafe.SizeOf<TileCell>());

    [Fact]
    public void The_default_cell_is_empty() => Assert.True(default(TileCell).IsEmpty);

    [Fact]
    public void A_cell_keeps_its_slot_tile_and_flips()
    {
        var cell = new TileCell(3, TileId.FromGrid(1, 2, 5), TileFlip.Horizontal | TileFlip.Diagonal);
        Assert.Equal(((ushort)3, TileId.FromGrid(1, 2, 5), TileFlip.Horizontal | TileFlip.Diagonal), (cell.Slot, cell.Tile, cell.Flip));
        Assert.False(cell.IsEmpty);
    }

    [Fact]
    public void A_cell_without_a_slot_is_the_empty_cell()
        => Assert.Equal(TileCell.Empty, new TileCell(0, TileId.FromGrid(1, 2, 5), TileFlip.Vertical));
}
