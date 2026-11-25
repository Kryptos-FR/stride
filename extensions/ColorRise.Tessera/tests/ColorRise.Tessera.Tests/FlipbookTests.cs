using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Tests;

/// <summary>
/// The look-ups of a flipbook: an animation by its name, a clip by its direction, and the direction of a movement.
/// </summary>
public sealed class FlipbookTests
{
    [Fact]
    public void A_direction_finds_its_clip()
    {
        var animation = Animation(FlipbookDirection.Down, FlipbookDirection.Left, FlipbookDirection.UpRight);

        Assert.Same(animation.Clips[1], animation.FindClip(FlipbookDirection.Left));
        Assert.Same(animation.Clips[2], animation.FindClip(FlipbookDirection.UpRight));
    }

    [Fact]
    public void A_diagonal_falls_back_to_its_horizontal_part_then_its_vertical_part()
    {
        var leftAndUp = Animation(FlipbookDirection.Up, FlipbookDirection.Left);
        Assert.Same(leftAndUp.Clips[1], leftAndUp.FindClip(FlipbookDirection.UpLeft));

        var upAndDown = Animation(FlipbookDirection.Down, FlipbookDirection.Up);
        Assert.Same(upAndDown.Clips[1], upAndDown.FindClip(FlipbookDirection.UpLeft));
        Assert.Same(upAndDown.Clips[0], upAndDown.FindClip(FlipbookDirection.DownRight));
    }

    [Fact]
    public void A_direction_without_a_clip_falls_back_to_None_then_to_the_first()
    {
        var withNone = Animation(FlipbookDirection.Down, FlipbookDirection.None);
        Assert.Same(withNone.Clips[1], withNone.FindClip(FlipbookDirection.Left));
        Assert.Same(withNone.Clips[1], withNone.FindClip(FlipbookDirection.UpRight));

        var withoutNone = Animation(FlipbookDirection.Down, FlipbookDirection.Right);
        Assert.Same(withoutNone.Clips[0], withoutNone.FindClip(FlipbookDirection.Up));
        Assert.Same(withoutNone.Clips[0], withoutNone.FindClip(FlipbookDirection.None));
    }

    [Fact]
    public void An_animation_without_a_clip_has_none()
    {
        var animation = new FlipbookAnimation { Name = "walk" };

        Assert.Null(animation.FindClip(FlipbookDirection.Down));
        Assert.Null(animation.FindClip(FlipbookDirection.None));
    }

    [Fact]
    public void Of_two_clips_of_one_direction_the_first_is_found()
    {
        var animation = Animation(FlipbookDirection.Up, FlipbookDirection.Left, FlipbookDirection.Left);

        Assert.Same(animation.Clips[1], animation.FindClip(FlipbookDirection.Left));
    }

    [Fact]
    public void An_animation_is_found_by_its_name_as_written()
    {
        var flipbook = new Flipbook();
        flipbook.Animations.Add(new FlipbookAnimation { Name = "idle" });
        flipbook.Animations.Add(new FlipbookAnimation { Name = "walk" });
        flipbook.Animations.Add(new FlipbookAnimation { Name = "walk" });

        Assert.Same(flipbook.Animations[1], flipbook.FindAnimation("walk"));
        Assert.Null(flipbook.FindAnimation("Walk"));
        Assert.Null(flipbook.FindAnimation(null));
        Assert.Null(flipbook.FindAnimation("run"));
    }

    [Theory]
    [InlineData(1f, 0f, false, FlipbookDirection.Right)]
    [InlineData(-1f, 0.5f, false, FlipbookDirection.Left)]
    [InlineData(0.5f, 1f, false, FlipbookDirection.Up)]
    [InlineData(0f, -1f, false, FlipbookDirection.Down)]
    [InlineData(0f, 0f, false, FlipbookDirection.None)]
    [InlineData(0f, 0f, true, FlipbookDirection.None)]
    [InlineData(1f, 1f, false, FlipbookDirection.Right)]
    [InlineData(1f, 1f, true, FlipbookDirection.UpRight)]
    [InlineData(-1f, -1f, true, FlipbookDirection.DownLeft)]
    [InlineData(-1f, 1f, true, FlipbookDirection.UpLeft)]
    [InlineData(1f, -1f, true, FlipbookDirection.DownRight)]
    [InlineData(1f, 0.3f, true, FlipbookDirection.Right)]
    [InlineData(0.3f, -1f, true, FlipbookDirection.Down)]
    public void A_movement_gives_its_direction(float x, float y, bool diagonals, FlipbookDirection expected)
        => Assert.Equal(expected, FlipbookDirections.FromVector(new Vector2(x, y), diagonals));

    [Fact]
    public void Mirrored_swaps_left_and_right()
    {
        Assert.Equal(FlipbookDirection.None, FlipbookDirection.None.Mirrored());
        Assert.Equal(FlipbookDirection.Down, FlipbookDirection.Down.Mirrored());
        Assert.Equal(FlipbookDirection.Up, FlipbookDirection.Up.Mirrored());
        Assert.Equal(FlipbookDirection.Right, FlipbookDirection.Left.Mirrored());
        Assert.Equal(FlipbookDirection.Left, FlipbookDirection.Right.Mirrored());
        Assert.Equal(FlipbookDirection.DownRight, FlipbookDirection.DownLeft.Mirrored());
        Assert.Equal(FlipbookDirection.DownLeft, FlipbookDirection.DownRight.Mirrored());
        Assert.Equal(FlipbookDirection.UpRight, FlipbookDirection.UpLeft.Mirrored());
        Assert.Equal(FlipbookDirection.UpLeft, FlipbookDirection.UpRight.Mirrored());
    }

    private static FlipbookAnimation Animation(params FlipbookDirection[] directions)
    {
        var animation = new FlipbookAnimation { Name = "walk" };
        foreach (var direction in directions)
        {
            animation.Clips.Add(new FlipbookClip { Direction = direction });
        }
        return animation;
    }
}
