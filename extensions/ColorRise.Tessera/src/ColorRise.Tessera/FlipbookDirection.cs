using Stride.Core;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera;

/// <summary>
/// The direction a clip of a flipbook animation is for, as a character faces it on screen.
/// </summary>
public enum FlipbookDirection
{
    /// <summary>
    /// Any direction: the clip shown when the animation has none for the direction asked.
    /// </summary>
    [Display("Any direction")]
    None,
    /// <summary>
    /// Facing down, toward the viewer.
    /// </summary>
    [Display("Down")]
    Down,
    /// <summary>
    /// Facing left.
    /// </summary>
    [Display("Left")]
    Left,
    /// <summary>
    /// Facing right.
    /// </summary>
    [Display("Right")]
    Right,
    /// <summary>
    /// Facing up, away from the viewer.
    /// </summary>
    [Display("Up")]
    Up,
    /// <summary>
    /// Facing down and left.
    /// </summary>
    [Display("Down left")]
    DownLeft,
    /// <summary>
    /// Facing down and right.
    /// </summary>
    [Display("Down right")]
    DownRight,
    /// <summary>
    /// Facing up and left.
    /// </summary>
    [Display("Up left")]
    UpLeft,
    /// <summary>
    /// Facing up and right.
    /// </summary>
    [Display("Up right")]
    UpRight,
}

/// <summary>
/// What a direction of a flipbook is made of, and the direction of a movement.
/// </summary>
public static class FlipbookDirections
{
    // tan(22.5°): a part of a movement smaller than this times the other part does not count for a diagonal
    private const float DiagonalSlope = 0.41421357f;

    /// <summary>
    /// The name of a direction in words, as Game Studio shows it: "Any direction" for <see cref="FlipbookDirection.None"/>,
    /// else such as "Down" or "Up left".
    /// </summary>
    public static string NameOf(FlipbookDirection direction) => direction switch
    {
        FlipbookDirection.None => "Any direction",
        FlipbookDirection.DownLeft => "Down left",
        FlipbookDirection.DownRight => "Down right",
        FlipbookDirection.UpLeft => "Up left",
        FlipbookDirection.UpRight => "Up right",
        _ => direction.ToString(),
    };

    /// <summary>
    /// The direction of a movement in the map's plane, y up. Zero gives <see cref="FlipbookDirection.None"/>. Without
    /// diagonals, the larger part wins, and x wins a tie; with them, a movement within 22.5 degrees of an axis is that axis.
    /// </summary>
    public static FlipbookDirection FromVector(Vector2 movement, bool diagonals = false)
    {
        var (x, y) = (movement.X, movement.Y);
        if (!(float.IsFinite(x) && float.IsFinite(y)) || (x == 0 && y == 0))
        {
            return FlipbookDirection.None;
        }
        var (ax, ay) = (MathF.Abs(x), MathF.Abs(y));
        if (diagonals && ay > ax * DiagonalSlope && ax > ay * DiagonalSlope)
        {
            return (x < 0, y < 0) switch
            {
                (true, true) => FlipbookDirection.DownLeft,
                (false, true) => FlipbookDirection.DownRight,
                (true, false) => FlipbookDirection.UpLeft,
                _ => FlipbookDirection.UpRight,
            };
        }
        if (ax >= ay)
        {
            return x < 0 ? FlipbookDirection.Left : FlipbookDirection.Right;
        }
        return y < 0 ? FlipbookDirection.Down : FlipbookDirection.Up;
    }

    /// <summary>
    /// The direction mirrored left to right: <see cref="FlipbookDirection.Left"/> for <see cref="FlipbookDirection.Right"/>,
    /// <see cref="FlipbookDirection.UpLeft"/> for <see cref="FlipbookDirection.UpRight"/>, and back; the others are themselves.
    /// </summary>
    public static FlipbookDirection Mirrored(this FlipbookDirection direction) => direction switch
    {
        FlipbookDirection.Left => FlipbookDirection.Right,
        FlipbookDirection.Right => FlipbookDirection.Left,
        FlipbookDirection.DownLeft => FlipbookDirection.DownRight,
        FlipbookDirection.DownRight => FlipbookDirection.DownLeft,
        FlipbookDirection.UpLeft => FlipbookDirection.UpRight,
        FlipbookDirection.UpRight => FlipbookDirection.UpLeft,
        _ => direction,
    };

    /// <summary>
    /// The horizontal part of a diagonal: <see cref="FlipbookDirection.Left"/> or <see cref="FlipbookDirection.Right"/>;
    /// null for a direction that is not a diagonal.
    /// </summary>
    internal static FlipbookDirection? HorizontalPart(this FlipbookDirection direction) => direction switch
    {
        FlipbookDirection.DownLeft or FlipbookDirection.UpLeft => FlipbookDirection.Left,
        FlipbookDirection.DownRight or FlipbookDirection.UpRight => FlipbookDirection.Right,
        _ => null,
    };

    /// <summary>
    /// The vertical part of a diagonal: <see cref="FlipbookDirection.Down"/> or <see cref="FlipbookDirection.Up"/>; null for
    /// a direction that is not a diagonal.
    /// </summary>
    internal static FlipbookDirection? VerticalPart(this FlipbookDirection direction) => direction switch
    {
        FlipbookDirection.DownLeft or FlipbookDirection.DownRight => FlipbookDirection.Down,
        FlipbookDirection.UpLeft or FlipbookDirection.UpRight => FlipbookDirection.Up,
        _ => null,
    };
}
