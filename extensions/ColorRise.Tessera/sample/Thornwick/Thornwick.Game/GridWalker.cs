using ColorRise.Tessera;
using Stride.Core.Mathematics;

namespace Thornwick;

/// <summary>
/// Something that stands on a cell of a place, or walks to a neighbour.
/// </summary>
/// <remarks>
/// A walker holds its cell, and from the start of a step to its end the cell it walks to as well, so that two walkers
/// never share a cell.
/// </remarks>
public sealed class GridWalker
{
    // The time walked of the current step, and the time left over from the last step, for a step that starts at once
    private float progress;
    private float leftover;

    /// <summary>
    /// A walker standing on a free cell of a place.
    /// </summary>
    /// <exception cref="InvalidOperationException">The cell is not free.</exception>
    public GridWalker(Place place, Int2 cell, FlipbookDirection facing = FlipbookDirection.Down)
    {
        if (!place.IsFree(cell) || !place.TryHold(cell, this))
        {
            throw new InvalidOperationException($"The cell {cell} of '{place.Name}' is not free.");
        }
        Place = place;
        Cell = cell;
        Target = cell;
        Facing = facing;
    }

    /// <summary>
    /// The place the walker is in.
    /// </summary>
    public Place Place { get; private set; }

    /// <summary>
    /// The cell the walker stands on, or the cell it is leaving.
    /// </summary>
    public Int2 Cell { get; private set; }

    /// <summary>
    /// The cell the walker walks to; <see cref="Cell"/> while it stands.
    /// </summary>
    public Int2 Target { get; private set; }

    /// <summary>
    /// The side the walker faces.
    /// </summary>
    public FlipbookDirection Facing { get; private set; }

    /// <summary>
    /// Whether the walker is between two cells.
    /// </summary>
    public bool IsMoving => Target != Cell;

    /// <summary>
    /// The neighbour of <see cref="Cell"/> that the walker faces.
    /// </summary>
    public Int2 Ahead => Cell + Offset(Facing);

    /// <summary>
    /// How long a step to a neighbour takes, in seconds. A time that is not a number above 0 makes a step arrive at once.
    /// </summary>
    public float StepSeconds { get; set; } = 0.2f;

    /// <summary>
    /// The walker's position in cells: the bottom centre of <see cref="Cell"/>, moved toward the bottom centre of
    /// <see cref="Target"/> by the part of the step done.
    /// </summary>
    public Vector2 Position
    {
        get
        {
            var step = Step;
            var done = !IsMoving ? 0 : step > 0 ? Math.Clamp(progress / step, 0, 1) : 1;
            var from = new Vector2(Cell.X + 0.5f, Cell.Y);
            var to = new Vector2(Target.X + 0.5f, Target.Y);
            return Vector2.Lerp(from, to, done);
        }
    }

    // The time of a step, 0 when StepSeconds is not a number above 0
    private float Step => float.IsFinite(StepSeconds) && StepSeconds > 0 ? StepSeconds : 0;

    /// <summary>
    /// Turns to a side. Standing, it also starts a step when the next cell is free, and holds that cell. False when no step
    /// starts. Only Down, Left, Right and Up are sides; a walker that is already walking does not turn.
    /// </summary>
    public bool TryStep(FlipbookDirection side)
    {
        if (Offset(side) == Int2.Zero || IsMoving)
        {
            return false;
        }
        Facing = side;
        var next = Cell + Offset(side);
        if (!Place.IsFree(next) || !Place.TryHold(next, this))
        {
            return false;
        }
        Target = next;
        progress = leftover;
        leftover = 0;
        return true;
    }

    /// <summary>
    /// Moves along the step. True in the call that arrives: <see cref="Cell"/> is then the target, the cell left is
    /// released, and the time not used, one step's time at most, is already walked if <see cref="TryStep"/> is called
    /// before the next <see cref="Advance"/>. Time that is negative or not a number counts as 0.
    /// </summary>
    public bool Advance(float seconds)
    {
        if (!(seconds > 0))
        {
            seconds = 0;
        }
        if (!IsMoving)
        {
            leftover = 0;
            return false;
        }
        progress += seconds;
        var step = Step;
        if (progress < step)
        {
            return false;
        }
        Place.Release(Cell, this);
        Cell = Target;
        leftover = Math.Min(progress - step, step);
        progress = 0;
        return true;
    }

    /// <summary>
    /// Standing, goes to a free cell of a place, the same or another, at once. False, with nothing changed, when it walks
    /// or the cell is not free.
    /// </summary>
    public bool TryTravel(Place place, Int2 cell, FlipbookDirection facing)
    {
        if (IsMoving || !place.IsFree(cell) || !place.TryHold(cell, this))
        {
            return false;
        }
        Place.Release(Cell, this);
        Place = place;
        Cell = cell;
        Target = cell;
        Facing = facing;
        leftover = 0;
        return true;
    }

    /// <summary>
    /// The move of a step to a side: (0, -1) for Down, (0, 1) for Up, (-1, 0) for Left, (1, 0) for Right, and (0, 0) for
    /// the others.
    /// </summary>
    public static Int2 Offset(FlipbookDirection side) => side switch
    {
        FlipbookDirection.Down => new Int2(0, -1),
        FlipbookDirection.Up => new Int2(0, 1),
        FlipbookDirection.Left => new Int2(-1, 0),
        FlipbookDirection.Right => new Int2(1, 0),
        _ => Int2.Zero,
    };
}
