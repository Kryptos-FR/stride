using ColorRise.Tessera;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace StudioCheck;

/// <summary>
/// Walks its entity around a square of 2 units a side, 2 seconds a side (down, right, up, left, from where it starts), and
/// plays the animation "walk" of its flipbook facing the way it goes. No key is read.
/// </summary>
public sealed class Walker : SyncScript
{
    private const float SideLength = 2;
    private const float SideSeconds = 2;

    // The ways of the four sides, in the map's plane, y up
    private static readonly Vector2[] Sides = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];

    private Vector3 start;
    private FlipbookComponent? flipbook;
    private float elapsed;

    /// <inheritdoc/>
    public override void Start()
    {
        start = Entity.Transform.Position;
        flipbook = Entity.Get<FlipbookComponent>();
        flipbook?.Play("walk");
    }

    /// <inheritdoc/>
    public override void Update()
    {
        elapsed = (elapsed + (float)Game.UpdateTime.Elapsed.TotalSeconds) % (4 * SideSeconds);
        var side = (int)(elapsed / SideSeconds);
        var corner = Vector2.Zero;
        for (var before = 0; before < side; before++)
        {
            corner += Sides[before] * SideLength;
        }
        var position = corner + Sides[side] * (elapsed - side * SideSeconds) / SideSeconds * SideLength;
        Entity.Transform.Position = start + new Vector3(position, 0);
        flipbook?.Direction = FlipbookDirections.FromVector(Sides[side]);
    }
}
