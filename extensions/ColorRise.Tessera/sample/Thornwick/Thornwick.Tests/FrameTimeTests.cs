namespace Thornwick.Tests;

/// <summary>
/// The time a frame of the game counts for.
/// </summary>
public sealed class FrameTimeTests
{
    [Theory]
    [InlineData(16, 0.016f)]
    [InlineData(100, 0.1f)]
    [InlineData(5000, 0.1f)]
    [InlineData(0, 0f)]
    [InlineData(-20, 0f)]
    public void A_frame_counts_for_a_tenth_of_a_second_at_most(int milliseconds, float seconds)
        => Assert.Equal(seconds, FrameTime.Of(TimeSpan.FromMilliseconds(milliseconds)), 6);
}
