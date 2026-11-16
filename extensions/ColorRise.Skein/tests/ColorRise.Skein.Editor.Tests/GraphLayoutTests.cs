using ColorRise.Skein.Editor.Graph;

namespace ColorRise.Skein.Editor.Tests;

/// <summary>
/// The layout of a graph follows the jumps, and keeps nodes that nothing links from being one tall column.
/// </summary>
public sealed class GraphLayoutTests
{
    private const int W = YarnGraphLayout.ColumnWidth;
    private const int H = YarnGraphLayout.RowHeight;

    private static YarnGraphPoint P(int column, int row) => new(column * W, row * H);

    [Fact]
    public void A_node_is_one_column_after_the_first_node_that_jumps_to_it()
    {
        // 0 jumps to 1 and 2, and 1 to 3 and back to 0
        var places = YarnGraphLayout.Compute(4, [(0, 1), (0, 2), (1, 3), (1, 0)]);

        Assert.Equal([P(0, 0), P(1, 0), P(1, 1), P(2, 0)], places);
    }

    [Fact]
    public void Nodes_that_nothing_links_fill_rows()
    {
        var places = YarnGraphLayout.Compute(9, []);

        Assert.Equal([P(0, 0), P(1, 0), P(2, 0), P(0, 1), P(1, 1), P(2, 1), P(0, 2), P(1, 2), P(2, 2)], places);
    }

    [Fact]
    public void A_strand_starts_a_row_when_the_row_has_no_room_for_it()
    {
        // Two nodes alone, then a strand of three columns and two rows, then a node alone
        var places = YarnGraphLayout.Compute(7, [(2, 3), (2, 4), (3, 5)]);

        // Rows of four columns give the shape nearest to a screen's: the strand does not fit after the two nodes, and
        // the last node fits after the strand
        Assert.Equal([P(0, 0), P(1, 0), P(0, 1), P(1, 1), P(1, 2), P(2, 1), P(3, 1)], places);
    }

    [Fact]
    public void A_loop_starts_at_its_first_node()
    {
        // A node alone, then two nodes that jump to each other
        var places = YarnGraphLayout.Compute(3, [(1, 2), (2, 1)]);

        Assert.Equal([P(0, 0), P(0, 1), P(1, 1)], places);
    }

    [Fact]
    public void The_nodes_of_a_group_stay_together_one_under_the_other()
    {
        // 0 and 3 are one group, 1 jumps to 2, and 4 is of the group of 2
        var places = YarnGraphLayout.Compute(5, [(1, 2)], [0, -1, 1, 0, 1]);

        // One row: the group of 0, then the strand of 1 with the group of 2 in its second column
        Assert.Equal([P(0, 0), P(1, 0), P(2, 0), P(0, 1), P(2, 1)], places);
    }

    [Fact]
    public void No_node_has_no_place()
    {
        Assert.Empty(YarnGraphLayout.Compute(0, []));
    }
}
