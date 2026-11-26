using ColorRise.Tessera;
using Stride.Engine;

namespace Thornwick;

/// <summary>
/// A villager: steps to a free cell near where it stood at the start, then stands a while.
/// </summary>
public sealed class Wanderer : SyncScript
{
    private GridWalker? walker;
    private WanderPlan? plan;
    private FlipbookComponent? flipbook;

    // The seconds left to stand before the next step
    private float pause;

    /// <summary>
    /// The name of the place the villager walks in.
    /// </summary>
    /// <userdoc>The name of the place the villager walks in.</userdoc>
    public string Place { get; set; } = "Town";

    /// <summary>
    /// How far the villager goes from the cell it stood on at the start, in cells.
    /// </summary>
    /// <userdoc>How many cells away from where it stands at the start the villager goes.</userdoc>
    public int Radius { get; set; } = 4;

    /// <summary>
    /// The seed of the villager's choices: two runs with the same seed choose the same.
    /// </summary>
    /// <userdoc>The seed of the villager's choices: the same seed, the same walk.</userdoc>
    public int Seed { get; set; }

    /// <summary>
    /// How long a step to the next cell takes, in seconds.
    /// </summary>
    /// <userdoc>The seconds a step to the next cell takes.</userdoc>
    public float StepSeconds { get; set; } = 0.4f;

    /// <inheritdoc />
    public override void Start()
    {
        flipbook = Entity.Get<FlipbookComponent>();
        if (World.Of(Entity)?.Find(Place) is not { } place)
        {
            Log.Error($"The villager '{Entity.Name}' finds no place '{Place}': the scene needs an entity of that name with a tile map, and the script World.");
            return;
        }

        Entity.Transform.UpdateWorldMatrix();
        var cell = place.Component.WorldToCell(Entity.Transform.WorldMatrix.TranslationVector);
        if (!place.IsFree(cell))
        {
            Log.Error($"The villager '{Entity.Name}' stands on ({cell.X}, {cell.Y}) of '{Place}', which is not free: move the entity in the scene.");
            return;
        }
        walker = new GridWalker(place, cell) { StepSeconds = StepSeconds };
        plan = new WanderPlan(cell, Radius, new Random(Seed));
        pause = plan.NextPause();
    }

    /// <inheritdoc />
    public override void Update()
    {
        if (walker is null || plan is null)
        {
            return;
        }
        var seconds = FrameTime.Of(Game.UpdateTime.Elapsed);
        walker.Advance(seconds);
        if (!walker.IsMoving)
        {
            pause -= seconds;
            if (pause <= 0)
            {
                if (plan.Next(walker.Cell, cell => Doors.IsOpenGround(walker.Place, cell)) is { } side)
                {
                    walker.TryStep(side);
                }
                pause = plan.NextPause();
            }
        }
        Entity.Transform.Position = walker.Place.ToWorld(walker.Position);
        flipbook?.Direction = walker.Facing;
        flipbook?.Play(walker.IsMoving ? "walk" : "idle");
    }
}
