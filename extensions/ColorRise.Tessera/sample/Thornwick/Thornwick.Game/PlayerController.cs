using ColorRise.Tessera;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Input;

namespace Thornwick;

/// <summary>
/// The hero: walks with the arrows or W, A, S and D, cuts with Space, and takes the doors.
/// </summary>
public sealed class PlayerController : SyncScript
{
    private World? world;
    private FlipbookComponent? flipbook;

    // Whether the last door asked waits for its arrival to be free
    private bool waitsAtDoor;

    /// <summary>
    /// The name of the place the hero starts in.
    /// </summary>
    /// <userdoc>The name of the place the hero starts in.</userdoc>
    public string Place { get; set; } = "Town";

    /// <summary>
    /// The place whose door the hero comes out of at the start; empty to start on the cell under the entity.
    /// </summary>
    /// <userdoc>The hero starts beside the door of its place that leads here, as if coming out of it. Empty: on the cell under the entity.</userdoc>
    public string ComesFrom { get; set; } = "House";

    /// <summary>
    /// How long a step to the next cell takes, in seconds.
    /// </summary>
    /// <userdoc>The seconds a step to the next cell takes.</userdoc>
    public float StepSeconds { get; set; } = 0.2f;

    /// <summary>
    /// The hero on the grid; null until the script starts, and when the hero could not be placed.
    /// </summary>
    [DataMemberIgnore]
    public GridWalker? Walker { get; private set; }

    /// <inheritdoc />
    public override void Start()
    {
        flipbook = Entity.Get<FlipbookComponent>();
        world = World.Of(Entity);
        if (world is null)
        {
            Log.Error($"The hero '{Entity.Name}' finds no entity with the script World in its scene: add one.");
            return;
        }
        if (world.Find(Place) is not { } place)
        {
            Log.Error($"The hero '{Entity.Name}' finds no place '{Place}': the scene needs an entity of that name with a tile map.");
            return;
        }

        var (cell, facing) = ComesFrom.Length > 0 && Doors.Arrival(place, ComesFrom) is { } arrival
            ? arrival
            : (CellUnder(place), FlipbookDirection.Down);
        if (!place.IsFree(cell))
        {
            Log.Error($"The hero stands on ({cell.X}, {cell.Y}) of '{Place}', which is not free: move the entity in the scene.");
            return;
        }
        Walker = new GridWalker(place, cell, facing) { StepSeconds = StepSeconds };
    }

    /// <inheritdoc />
    public override void Update()
    {
        if (Walker is not { } walker || world is null)
        {
            return;
        }
        var arrived = walker.Advance(FrameTime.Of(Game.UpdateTime.Elapsed));
        if (!walker.IsMoving)
        {
            // A door takes the hero when they arrive on it, and again each frame while someone stands where they would arrive
            if (arrived || waitsAtDoor)
            {
                waitsAtDoor = world.TakeDoor(walker) == DoorResult.Waits;
            }
            if (Input.IsKeyPressed(Keys.Space))
            {
                world.Cut(walker);
            }
            else
            {
                walker.TryStep(HeroKeys.Wanted(Input.Keyboard));
            }
        }
        Entity.Transform.Position = walker.Place.ToWorld(walker.Position);
        flipbook?.Direction = walker.Facing;
        flipbook?.Play(walker.IsMoving ? "walk" : "idle");
    }

    // The cell of the place under the entity, as the scene has it
    private Int2 CellUnder(Place place)
    {
        Entity.Transform.UpdateWorldMatrix();
        return place.Component.WorldToCell(Entity.Transform.WorldMatrix.TranslationVector);
    }
}
