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
    public void A_card_goes_under_the_one_before_it_whatever_its_height()
    {
        // 0 jumps to 1, 2 and 3, which are a column of cards of their own heights
        const int Gap = YarnGraphLayout.RowGap;
        var places = YarnGraphLayout.Compute(5, [(0, 1), (0, 2), (0, 3)], heights: [100, 400, 50, 120, 90]);

        Assert.Equal(new YarnGraphPoint(0, 0), places[0]);
        Assert.Equal([new YarnGraphPoint(W, 0), new YarnGraphPoint(W, 400 + Gap), new YarnGraphPoint(W, 400 + Gap + 50 + Gap)], places[1..4]);

        // The next strand starts under the tallest column of the one before, or beside it
        Assert.True(places[4] == new YarnGraphPoint(0, 400 + 50 + 120 + 3 * Gap) || places[4].Y == 0 && places[4].X >= 2 * W);

        // Without heights, every card is of the default height
        Assert.Equal(YarnGraphLayout.Compute(5, [(0, 1)]), YarnGraphLayout.Compute(5, [(0, 1)], heights: [.. Enumerable.Repeat(YarnGraphLayout.DefaultHeight, 5)]));
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
    public void The_nodes_of_a_section_are_a_block_of_their_own()
    {
        // 0 jumps to 1, and 2 jumps to 3: the jumps cross the sections. 4 has no section, and 5 is in the group of 3
        string?[] sections = ["harbour", "lighthouse", "harbour", "lighthouse", null, "harbour"];
        var places = YarnGraphLayout.Compute(6, [(0, 1), (2, 3)], [-1, -1, -1, 0, -1, 0], sections);

        // The group of 3 is in the section of 3, its first node
        var blocks = new[] { new[] { 0, 2 }, [1, 3, 5], [4] }.Select(Bounds).ToList();
        for (var i = 0; i < blocks.Count; i++)
        {
            for (var j = i + 1; j < blocks.Count; j++)
            {
                Assert.False(blocks[i].IntersectsWith(blocks[j]), $"Blocks {i} and {j} overlap.");
            }
        }

        // Inside a block, the layout is the one of the block alone, from the top left of the block
        Assert.Equal((places[1].X + W, places[1].Y), (places[3].X, places[3].Y));
        Assert.Equal(places[3].X, places[5].X);
        Assert.Equal(places[3].Y + H, places[5].Y);

        // The bounds of a block's cards, with the room the frames take around them
        System.Drawing.Rectangle Bounds(int[] nodes)
        {
            var (left, top) = (nodes.Min(n => places[n].X), nodes.Min(n => places[n].Y));
            var (right, bottom) = (nodes.Max(n => places[n].X) + W, nodes.Max(n => places[n].Y) + H);
            return System.Drawing.Rectangle.FromLTRB(left, top, right, bottom);
        }
    }

    [Fact]
    public void Without_a_section_the_layout_is_the_one_of_the_jumps()
    {
        Assert.Equal(YarnGraphLayout.Compute(4, [(0, 1), (0, 2), (1, 3)]), YarnGraphLayout.Compute(4, [(0, 1), (0, 2), (1, 3)], null, [null, "", null, null]));
    }

    [Fact]
    public void No_node_has_no_place()
    {
        Assert.Empty(YarnGraphLayout.Compute(0, []));
    }
}
