using ColorRise.Tessera;
using Stride.Input;

namespace Thornwick.Tests;

/// <summary>
/// The side the hero's keys ask for.
/// </summary>
public sealed class HeroKeysTests
{
    [Theory]
    [InlineData(true, false, false, false, FlipbookDirection.Left)]
    [InlineData(false, true, false, false, FlipbookDirection.Right)]
    [InlineData(false, false, true, false, FlipbookDirection.Up)]
    [InlineData(false, false, false, true, FlipbookDirection.Down)]
    [InlineData(false, false, false, false, FlipbookDirection.None)]
    [InlineData(true, true, false, false, FlipbookDirection.None)]
    [InlineData(true, true, true, false, FlipbookDirection.Up)]
    [InlineData(true, false, true, false, FlipbookDirection.Left)]
    [InlineData(false, false, true, true, FlipbookDirection.None)]
    [InlineData(true, true, true, true, FlipbookDirection.None)]
    public void The_keys_ask_for_one_side(bool left, bool right, bool up, bool down, FlipbookDirection side)
        => Assert.Equal(side, HeroKeys.Wanted(left, right, up, down));

    [Theory]
    [InlineData(Keys.Left, FlipbookDirection.Left)]
    [InlineData(Keys.A, FlipbookDirection.Left)]
    [InlineData(Keys.Right, FlipbookDirection.Right)]
    [InlineData(Keys.D, FlipbookDirection.Right)]
    [InlineData(Keys.Up, FlipbookDirection.Up)]
    [InlineData(Keys.W, FlipbookDirection.Up)]
    [InlineData(Keys.Down, FlipbookDirection.Down)]
    [InlineData(Keys.S, FlipbookDirection.Down)]
    public void A_key_held_asks_for_its_side(Keys key, FlipbookDirection side)
    {
        var keyboard = new InputSourceSimulated().AddKeyboard();
        keyboard.SimulateDown(key);
        keyboard.Update([]);
        keyboard.Update([]);

        Assert.Equal(side, HeroKeys.Wanted(keyboard));
    }

    // A tap shorter than a frame: the key went down and up between two frames, so it is not down in the frame that reads it
    [Theory]
    [InlineData(Keys.Up, FlipbookDirection.Up)]
    [InlineData(Keys.A, FlipbookDirection.Left)]
    public void A_key_pressed_and_let_go_since_the_last_frame_asks_for_its_side(Keys key, FlipbookDirection side)
    {
        var keyboard = new InputSourceSimulated().AddKeyboard();
        keyboard.SimulateDown(key);
        keyboard.SimulateUp(key);
        keyboard.Update([]);

        Assert.Equal(side, HeroKeys.Wanted(keyboard));
    }

    [Fact]
    public void A_key_let_go_before_the_last_frame_asks_for_nothing()
    {
        var keyboard = new InputSourceSimulated().AddKeyboard();
        keyboard.SimulateDown(Keys.Up);
        keyboard.SimulateUp(Keys.Up);
        keyboard.Update([]);
        keyboard.Update([]);

        Assert.Equal(FlipbookDirection.None, HeroKeys.Wanted(keyboard));
    }

    [Fact]
    public void No_keyboard_asks_for_nothing()
        => Assert.Equal(FlipbookDirection.None, HeroKeys.Wanted(null));
}
