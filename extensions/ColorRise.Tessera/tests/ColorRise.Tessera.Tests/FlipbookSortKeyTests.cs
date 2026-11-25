using ColorRise.Tessera.Rendering;

namespace ColorRise.Tessera.Tests;

/// <summary>
/// The state sort key that orders the flipbooks of one depth by their y: the sort modes keep its top 24 bits after the
/// depth.
/// </summary>
public sealed class FlipbookSortKeyTests
{
    [Fact]
    public void A_higher_y_gives_a_smaller_key()
    {
        Assert.True(FlipbookSortKey.FromY(1) < FlipbookSortKey.FromY(0));
        Assert.True(FlipbookSortKey.FromY(0) < FlipbookSortKey.FromY(-1));
    }

    [Fact]
    public void A_step_is_a_256th_of_a_unit()
    {
        Assert.Equal(1u << 8, FlipbookSortKey.FromY(0) - FlipbookSortKey.FromY(1 / 256f));
        Assert.Equal(256u << 8, FlipbookSortKey.FromY(-1) - FlipbookSortKey.FromY(0));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.3f)]
    [InlineData(-123.456f)]
    [InlineData(32767.5f)]
    public void The_low_byte_is_empty(float y)
        => Assert.Equal(0u, FlipbookSortKey.FromY(y) & 0xFF);

    [Theory]
    [InlineData(40000f, 0u)]
    [InlineData(-40000f, 16777215u << 8)]
    [InlineData(float.NaN, 0u)]
    [InlineData(float.PositiveInfinity, 0u)]
    [InlineData(float.NegativeInfinity, 16777215u << 8)]
    public void A_y_out_of_range_is_clamped(float y, uint key)
        => Assert.Equal(key, FlipbookSortKey.FromY(y));
}
