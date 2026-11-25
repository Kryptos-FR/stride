namespace ColorRise.Tessera.Tests;

/// <summary>
/// Which frame of a clip a time shows, in the three loop modes.
/// </summary>
public sealed class FlipbookClipTests
{
    // A clip of three frames of 0.1, 0.2 and 0.3 s
    [Theory]
    [InlineData(FlipbookLoopMode.Loop, 0.0, 0)]
    [InlineData(FlipbookLoopMode.Loop, 0.1, 1)]
    [InlineData(FlipbookLoopMode.Loop, 0.29, 1)]
    [InlineData(FlipbookLoopMode.Loop, 0.3, 2)]
    [InlineData(FlipbookLoopMode.Loop, 0.6, 0)]
    [InlineData(FlipbookLoopMode.Loop, -0.05, 2)]
    [InlineData(FlipbookLoopMode.Once, 0.59, 2)]
    [InlineData(FlipbookLoopMode.Once, 0.6, 2)]
    [InlineData(FlipbookLoopMode.Once, 99.0, 2)]
    [InlineData(FlipbookLoopMode.Once, -1.0, 0)]
    [InlineData(FlipbookLoopMode.PingPong, 0.59, 2)]
    [InlineData(FlipbookLoopMode.PingPong, 0.6, 1)]
    [InlineData(FlipbookLoopMode.PingPong, 0.79, 1)]
    [InlineData(FlipbookLoopMode.PingPong, 0.8, 0)]
    public void The_frame_of_a_time(FlipbookLoopMode mode, double time, int expected)
        => Assert.Equal(expected, Clip(0.1f, 0.2f, 0.3f).FrameAt(time, mode));

    [Fact]
    public void The_pass_of_a_ping_pong_shows_its_end_frames_once()
    {
        var clip = Clip(0.1f, 0.2f, 0.3f);
        Assert.Equal(0.6, clip.GetPassLength(FlipbookLoopMode.Loop), 6);
        Assert.Equal(0.6, clip.GetPassLength(FlipbookLoopMode.Once), 6);
        Assert.Equal(0.8, clip.GetPassLength(FlipbookLoopMode.PingPong), 6);
        Assert.Equal(0.3, Clip(0.1f, 0.2f).GetPassLength(FlipbookLoopMode.PingPong), 6);
    }

    [Fact]
    public void A_frame_that_does_not_last_is_never_shown()
    {
        var clip = Clip(0.1f, 0, -1, 0.1f);
        Assert.Equal(3, clip.FrameAt(0.1, FlipbookLoopMode.Loop));
        Assert.Equal(3, clip.FrameAt(0.15, FlipbookLoopMode.Loop));
    }

    [Fact]
    public void A_clip_whose_frames_do_not_last_shows_its_first()
    {
        var clip = Clip(0, 0);
        foreach (var mode in new[] { FlipbookLoopMode.Loop, FlipbookLoopMode.Once, FlipbookLoopMode.PingPong })
        {
            Assert.Equal(0, clip.FrameAt(0.5, mode));
            Assert.Equal(0, clip.GetPassLength(mode));
        }
    }

    [Fact]
    public void A_clip_without_a_frame_shows_nothing()
    {
        var clip = new FlipbookClip();
        Assert.Equal(-1, clip.FrameAt(0, FlipbookLoopMode.Loop));
        Assert.Equal(-1, clip.FrameAt(1, FlipbookLoopMode.PingPong));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void A_time_that_is_not_finite_shows_the_first_frame(double time)
    {
        var clip = Clip(0.1f, 0.2f, 0.3f);
        Assert.Equal(0, clip.FrameAt(time, FlipbookLoopMode.Loop));
        Assert.Equal(0, clip.FrameAt(time, FlipbookLoopMode.Once));
        Assert.Equal(0, clip.FrameAt(time, FlipbookLoopMode.PingPong));
    }

    internal static FlipbookClip Clip(params float[] durations)
    {
        var clip = new FlipbookClip();
        foreach (var duration in durations)
        {
            clip.Frames.Add(new FlipbookFrame { Duration = duration });
        }
        return clip;
    }
}
