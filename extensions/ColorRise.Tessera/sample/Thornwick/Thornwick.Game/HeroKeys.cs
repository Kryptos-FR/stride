using ColorRise.Tessera;
using Stride.Input;

namespace Thornwick;

/// <summary>
/// The side the hero's keys ask for.
/// </summary>
public static class HeroKeys
{
    /// <summary>
    /// The side the pressed keys ask for: Left or Right when one of the two is down and not the other, else Up or Down the
    /// same way, else None.
    /// </summary>
    public static FlipbookDirection Wanted(bool left, bool right, bool up, bool down)
    {
        if (left != right)
        {
            return left ? FlipbookDirection.Left : FlipbookDirection.Right;
        }
        if (up != down)
        {
            return up ? FlipbookDirection.Up : FlipbookDirection.Down;
        }
        return FlipbookDirection.None;
    }

    /// <summary>
    /// The side a keyboard's arrows and W, A, S and D ask for; None without a keyboard. A key asks when it is down, or was
    /// pressed since the last frame: a tap shorter than a frame is let go before the frame reads it, and still moves the
    /// hero.
    /// </summary>
    public static FlipbookDirection Wanted(IKeyboardDevice? keyboard)
    {
        if (keyboard is null)
        {
            return FlipbookDirection.None;
        }
        return Wanted(Asks(keyboard, Keys.Left, Keys.A), Asks(keyboard, Keys.Right, Keys.D), Asks(keyboard, Keys.Up, Keys.W), Asks(keyboard, Keys.Down, Keys.S));
    }

    /// <summary>
    /// The side the arrows and W, A, S and D pressed since the last frame ask for, whether they are still down or not;
    /// None without a keyboard.
    /// </summary>
    public static FlipbookDirection Pressed(IKeyboardDevice? keyboard)
    {
        if (keyboard is null)
        {
            return FlipbookDirection.None;
        }
        return Wanted(WasPressed(keyboard, Keys.Left, Keys.A), WasPressed(keyboard, Keys.Right, Keys.D), WasPressed(keyboard, Keys.Up, Keys.W), WasPressed(keyboard, Keys.Down, Keys.S));
    }

    private static bool Asks(IKeyboardDevice keyboard, Keys key, Keys other)
        => keyboard.IsKeyDown(key) || keyboard.IsKeyPressed(key) || keyboard.IsKeyDown(other) || keyboard.IsKeyPressed(other);

    private static bool WasPressed(IKeyboardDevice keyboard, Keys key, Keys other) => keyboard.IsKeyPressed(key) || keyboard.IsKeyPressed(other);
}
