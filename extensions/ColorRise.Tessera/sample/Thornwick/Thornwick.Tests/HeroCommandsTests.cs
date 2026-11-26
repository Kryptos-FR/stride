using ColorRise.Tessera;
using Stride.Input;

namespace Thornwick.Tests;

/// <summary>
/// What the hero does next, from the keyboard read every frame as the hero's script reads it: a key pressed during a step
/// is kept for the arrival.
/// </summary>
public sealed class HeroCommandsTests
{
    private readonly KeyboardSimulated keyboard = new InputSourceSimulated().AddKeyboard();
    private readonly HeroCommands commands = new();

    [Fact]
    public void A_tap_during_a_step_is_kept_for_the_arrival()
    {
        Assert.Equal((false, FlipbookDirection.Right), Frame(walking: false, down: Keys.Right));
        Assert.Equal((false, FlipbookDirection.None), Frame(walking: true, up: Keys.Right));
        Assert.Equal((false, FlipbookDirection.None), Frame(walking: true, down: Keys.Right));
        Assert.Equal((false, FlipbookDirection.None), Frame(walking: true, up: Keys.Right));

        Assert.Equal((false, FlipbookDirection.Right), Frame(walking: false));
        Assert.Equal((false, FlipbookDirection.None), Frame(walking: false));
    }

    [Fact]
    public void The_tap_that_starts_a_step_is_not_kept()
    {
        Assert.Equal((false, FlipbookDirection.Right), Frame(walking: false, down: Keys.Right));
        Assert.Equal((false, FlipbookDirection.None), Frame(walking: true));
        Assert.Equal((false, FlipbookDirection.None), Frame(walking: true, up: Keys.Right));

        Assert.Equal((false, FlipbookDirection.None), Frame(walking: false));
    }

    [Fact]
    public void A_key_held_at_the_arrival_goes_before_a_kept_tap()
    {
        Frame(walking: true, down: Keys.Up);
        Frame(walking: true, up: Keys.Up);
        Frame(walking: true, down: Keys.Left);

        Assert.Equal((false, FlipbookDirection.Left), Frame(walking: false));
    }

    [Fact]
    public void Space_during_a_step_cuts_at_the_arrival_and_a_kept_side_steps_after()
    {
        Assert.Equal((false, FlipbookDirection.None), Frame(walking: true, down: Keys.Space));
        Assert.Equal((false, FlipbookDirection.None), Frame(walking: true, up: Keys.Space));
        Assert.Equal((false, FlipbookDirection.None), Frame(walking: true, down: Keys.D));
        Assert.Equal((false, FlipbookDirection.None), Frame(walking: true, up: Keys.D));

        Assert.Equal((true, FlipbookDirection.None), Frame(walking: false));
        Assert.Equal((false, FlipbookDirection.Right), Frame(walking: false));
        Assert.Equal((false, FlipbookDirection.None), Frame(walking: false));
    }

    [Fact]
    public void No_keyboard_asks_for_nothing()
    {
        Assert.Equal((false, FlipbookDirection.None), commands.Read(null, walking: true));
        Assert.Equal((false, FlipbookDirection.None), commands.Read(null, walking: false));
    }

    // One frame of the game: the keys that went down or up since the last frame, then the hero's script reads them
    private (bool Cut, FlipbookDirection Side) Frame(bool walking, Keys? down = null, Keys? up = null)
    {
        if (down is { } pressed)
        {
            keyboard.SimulateDown(pressed);
        }
        if (up is { } released)
        {
            keyboard.SimulateUp(released);
        }
        keyboard.Update([]);
        return commands.Read(keyboard, walking);
    }
}
