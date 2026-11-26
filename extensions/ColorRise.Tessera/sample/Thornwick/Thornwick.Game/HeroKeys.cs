using ColorRise.Tessera;

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
}
