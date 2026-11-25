namespace ColorRise.Tessera.Tests;

/// <summary>
/// What a flipbook component plays: by name and by time, with a queue, a pause and two events. The tests advance it
/// themselves, as its processor does each frame.
/// </summary>
public sealed class FlipbookComponentTests
{
    [Fact]
    public void Play_shows_the_frames_in_time()
    {
        var component = Component();
        component.Play("walk");

        component.Advance(0.15);
        Assert.Equal(1, component.CurrentFrameIndex);
        component.Advance(0.1);
        Assert.Equal(2, component.CurrentFrameIndex);
        component.Advance(0.1);
        Assert.Equal(0, component.CurrentFrameIndex);
        Assert.Same(component.CurrentClip!.Frames[0], component.CurrentFrame);
    }

    [Fact]
    public void Speed_scales_the_time()
    {
        var component = Component();
        component.Play("walk");
        component.Speed = 2;

        component.Advance(0.1);
        Assert.Equal(0.2, component.Time, 6);
        Assert.Equal(2, component.CurrentFrameIndex);

        component.Speed = 0;
        component.Advance(0.1);
        Assert.Equal(0.2, component.Time, 6);
    }

    [Fact]
    public void A_change_of_direction_keeps_the_time()
    {
        var component = Component();
        component.Play("walk");
        component.Advance(0.15);

        component.Direction = FlipbookDirection.Left;

        Assert.Equal(0.15, component.Time, 6);
        Assert.Equal(FlipbookDirection.Left, component.CurrentClip!.Direction);
        Assert.Equal(1, component.CurrentFrameIndex);
    }

    [Fact]
    public void Play_of_the_playing_animation_changes_nothing_and_restart_starts_again()
    {
        var component = Component();
        component.Play("walk");
        component.Advance(0.15);

        component.Play("walk");
        Assert.Equal(0.15, component.Time, 6);
        Assert.True(component.IsPlaying);

        component.Play("walk", restart: true);
        Assert.Equal(0, component.Time);
        Assert.True(component.IsPlaying);
    }

    [Fact]
    public void Pause_holds_the_frame_and_resume_goes_on()
    {
        var component = Component();
        component.Play("walk");
        component.Advance(0.15);

        component.Pause();
        component.Advance(0.1);
        Assert.False(component.IsPlaying);
        Assert.Equal(1, component.CurrentFrameIndex);

        component.Resume();
        component.Advance(0.1);
        Assert.True(component.IsPlaying);
        Assert.Equal(2, component.CurrentFrameIndex);

        // Play resumes a paused animation where it was
        component.Pause();
        component.Play("walk");
        Assert.True(component.IsPlaying);
        Assert.Equal(0.25, component.Time, 6);
    }

    [Fact]
    public void Stop_goes_back_to_the_first_frame()
    {
        var component = Component();
        component.Play("walk");
        component.Advance(0.25);

        component.Stop();

        Assert.False(component.IsPlaying);
        Assert.Equal(0, component.Time);
        Assert.Equal(0, component.CurrentFrameIndex);
        Assert.Equal("walk", component.Animation);
    }

    [Fact]
    public void A_once_animation_stops_on_its_last_frame_and_tells_once()
    {
        var component = Component();
        var ended = new List<string>();
        component.AnimationEnded += (_, e) => ended.Add(e.Animation);
        component.Play("attack");

        component.Advance(1);
        Assert.False(component.IsPlaying);
        Assert.Equal(1, component.CurrentFrameIndex);
        Assert.Equal(0.2, component.Time, 6);
        Assert.Equal(["attack"], ended);

        component.Advance(1);
        Assert.Equal(["attack"], ended);
    }

    [Fact]
    public void Play_of_an_ended_animation_starts_it_again()
    {
        var component = Component();
        component.Play("attack");
        component.Advance(1);

        component.Play("attack");

        Assert.True(component.IsPlaying);
        Assert.Equal(0, component.Time);
        Assert.Equal(0, component.CurrentFrameIndex);
    }

    [Fact]
    public void A_queued_animation_follows_a_once()
    {
        var component = Component();
        var ended = new List<FlipbookEventArgs>();
        component.AnimationEnded += (_, e) => ended.Add(e);
        component.Play("attack");
        component.Queue("idle");

        component.Advance(0.25);

        Assert.Equal("idle", component.Animation);
        Assert.Equal(0.05, component.Time, 6);
        Assert.True(component.IsPlaying);
        var end = Assert.Single(ended);
        Assert.Equal(("attack", FlipbookDirection.None, 1), (end.Animation, end.Direction, end.FrameIndex));
    }

    [Fact]
    public void A_queued_animation_follows_the_current_pass_of_a_loop()
    {
        var component = Component();
        component.Play("walk");
        component.Advance(0.1);
        component.Queue("idle");

        component.Advance(0.1);
        Assert.Equal("walk", component.Animation);

        component.Advance(0.15);
        Assert.Equal("idle", component.Animation);
        Assert.Equal(0.05, component.Time, 6);
    }

    [Fact]
    public void Two_queued_animations_can_end_in_one_step()
    {
        var component = Component();
        var ended = new List<string>();
        component.AnimationEnded += (_, e) => ended.Add(e.Animation);
        component.Play("attack");
        component.Queue("attack");
        component.Queue("idle");

        component.Advance(0.5);

        Assert.Equal("idle", component.Animation);
        Assert.Equal(0.1, component.Time, 6);
        Assert.Equal(["attack", "attack"], ended);
    }

    [Fact]
    public void Play_empties_the_queue()
    {
        var component = Component();
        component.Play("attack");
        component.Queue("idle");

        component.Play("walk");
        component.Advance(1);

        Assert.Equal("walk", component.Animation);
    }

    [Fact]
    public void FrameChanged_tells_each_new_frame_once()
    {
        var component = Component();
        var frames = new List<int>();
        component.FrameChanged += (_, e) => frames.Add(e.FrameIndex);
        component.Play("walk");

        for (var i = 0; i < 5; i++)
        {
            component.Advance(0.05);
        }

        Assert.Equal([0, 1, 2], frames);
    }

    [Fact]
    public void The_animation_set_at_start_plays_when_the_processor_adds_the_component()
    {
        var still = Component();
        still.Animation = "walk";
        still.PlayOnStart = false;
        FlipbookProcessor.Start(still);
        Assert.Equal(0, still.CurrentFrameIndex);
        Assert.False(still.IsPlaying);

        var playing = Component();
        playing.Animation = "walk";
        FlipbookProcessor.Start(playing);
        playing.Advance(0.15);
        Assert.True(playing.IsPlaying);
        Assert.Equal(1, playing.CurrentFrameIndex);
    }

    // Review Focus 2
    [Fact]
    public void A_name_the_flipbook_does_not_have_shows_nothing()
    {
        var component = Component();
        var events = 0;
        component.FrameChanged += (_, _) => events++;
        component.AnimationEnded += (_, _) => events++;

        component.Play("wlak");
        component.Advance(1);

        Assert.Null(component.CurrentClip);
        Assert.Equal(-1, component.CurrentFrameIndex);
        Assert.Null(component.CurrentFrame);
        Assert.Equal(0, events);
    }

    [Fact]
    public void A_component_without_a_flipbook_advances_without_a_throw()
    {
        var component = new FlipbookComponent { Animation = "walk" };
        component.Play("walk");
        component.Queue("idle");

        component.Advance(1);

        Assert.Equal(-1, component.CurrentFrameIndex);
    }

    [Fact]
    public void A_queue_behind_an_unknown_name_starts_at_once()
    {
        var component = Component();
        component.Play("wlak");
        component.Queue("idle");

        component.Advance(0.1);

        Assert.Equal("idle", component.Animation);
        Assert.Equal(0, component.CurrentFrameIndex);
    }

    [Fact]
    public void An_animation_without_a_clip_or_a_frame_shows_nothing_and_ends_at_once_for_the_queue()
    {
        var component = Component();
        component.Flipbook!.Animations.Add(new FlipbookAnimation { Name = "empty" });
        var noFrame = new FlipbookAnimation { Name = "no frame" };
        noFrame.Clips.Add(new FlipbookClip());
        component.Flipbook.Animations.Add(noFrame);
        var silent = new FlipbookAnimation { Name = "silent" };
        silent.Clips.Add(Clip(FlipbookDirection.None, 0, 0));
        component.Flipbook.Animations.Add(silent);

        component.Play("empty");
        component.Advance(0.1);
        Assert.Equal(-1, component.CurrentFrameIndex);

        component.Play("no frame");
        component.Advance(0.1);
        Assert.Equal(-1, component.CurrentFrameIndex);

        component.Play("silent");
        component.Advance(0.1);
        Assert.Equal(0, component.CurrentFrameIndex);

        component.Play("empty");
        component.Queue("no frame");
        component.Queue("silent");
        component.Queue("walk");
        component.Advance(0.1);
        Assert.Equal("walk", component.Animation);
    }

    // Review Focus 3
    [Fact]
    public void A_flipbook_replaced_while_playing_keeps_the_animation_by_name()
    {
        var component = Component();
        component.Play("walk");
        component.Advance(0.25);
        Assert.Equal(2, component.CurrentFrameIndex);

        var other = new Flipbook();
        var walk = new FlipbookAnimation { Name = "walk" };
        walk.Clips.Add(Clip(FlipbookDirection.Down, 0.1f));
        other.Animations.Add(walk);
        component.Flipbook = other;
        Assert.Equal(0, component.CurrentFrameIndex);
        Assert.Equal(0.25, component.Time, 6);

        component.Flipbook = new Flipbook();
        Assert.Equal(-1, component.CurrentFrameIndex);
        component.Advance(0.1);
    }

    /// <summary>
    /// A flipbook with <c>idle</c> (a loop of one clip None, frames 0.5 and 0.5), <c>walk</c> (a loop of the clips Down and
    /// Left, three frames of 0.1 each) and <c>attack</c> (once, a clip None of two frames of 0.1).
    /// </summary>
    internal static Flipbook Hero()
    {
        var flipbook = new Flipbook();
        var idle = new FlipbookAnimation { Name = "idle" };
        idle.Clips.Add(Clip(FlipbookDirection.None, 0.5f, 0.5f));
        var walk = new FlipbookAnimation { Name = "walk" };
        walk.Clips.Add(Clip(FlipbookDirection.Down, 0.1f, 0.1f, 0.1f));
        walk.Clips.Add(Clip(FlipbookDirection.Left, 0.1f, 0.1f, 0.1f));
        var attack = new FlipbookAnimation { Name = "attack", LoopMode = FlipbookLoopMode.Once };
        attack.Clips.Add(Clip(FlipbookDirection.None, 0.1f, 0.1f));
        flipbook.Animations.AddRange([idle, walk, attack]);
        return flipbook;
    }

    internal static FlipbookClip Clip(FlipbookDirection direction, params float[] durations)
    {
        var clip = FlipbookClipTests.Clip(durations);
        clip.Direction = direction;
        return clip;
    }

    private static FlipbookComponent Component() => new() { Flipbook = Hero(), Direction = FlipbookDirection.Down };
}
