using ColorRise.Tessera;
using Stride.Input;

namespace Thornwick;

/// <summary>
/// What the hero does next, from the keyboard read every frame: Space cuts, the arrows and W, A, S and D step. A key
/// pressed while the hero walks is kept for the arrival, so that a tap during a step is not lost.
/// </summary>
public sealed class HeroCommands
{
    // The last side pressed during the step, and whether Space was
    private FlipbookDirection keptSide;
    private bool keptCut;

    /// <summary>
    /// Reads a frame's keys. While the hero is <paramref name="walking"/>, keeps the last side pressed and a press of
    /// Space, and asks for nothing. Otherwise asks to cut when Space is pressed now or was kept; else for the side the keys
    /// ask for now (<see cref="HeroKeys.Wanted(IKeyboardDevice)"/>), or the side kept. What is asked for is no longer kept,
    /// but a side kept waits for the frame after a cut.
    /// </summary>
    public (bool Cut, FlipbookDirection Side) Read(IKeyboardDevice? keyboard, bool walking)
    {
        var spacePressed = keyboard?.IsKeyPressed(Keys.Space) == true;
        if (walking)
        {
            if (HeroKeys.Pressed(keyboard) is var pressed and not FlipbookDirection.None)
            {
                keptSide = pressed;
            }
            keptCut |= spacePressed;
            return (false, FlipbookDirection.None);
        }

        if (spacePressed || keptCut)
        {
            keptCut = false;
            return (true, FlipbookDirection.None);
        }
        var side = HeroKeys.Wanted(keyboard);
        if (side == FlipbookDirection.None)
        {
            side = keptSide;
        }
        keptSide = FlipbookDirection.None;
        return (false, side);
    }
}
