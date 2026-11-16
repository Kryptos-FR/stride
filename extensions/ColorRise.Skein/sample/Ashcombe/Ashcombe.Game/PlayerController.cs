using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Input;

namespace Ashcombe;

/// <summary>
/// Moves the cartographer with WASD or the arrow keys, inside the area of the place, and the camera with the
/// cartographer.
/// </summary>
public sealed class PlayerController : SyncScript
{
    private Vector3 cameraOffset;

    /// <summary>
    /// Metres per second.
    /// </summary>
    public float Speed { get; set; } = 5f;

    /// <summary>
    /// The centre of the area the cartographer walks in, in the world: the place's <see cref="PlaceArea"/>.
    /// </summary>
    [DataMemberIgnore]
    public Vector2 Centre { get; private set; }

    /// <summary>
    /// How far from <see cref="Centre"/> the cartographer walks, along X and along Z, in metres.
    /// </summary>
    [DataMemberIgnore]
    public Vector2 Bounds { get; private set; } = new(22f, 18f);

    /// <summary>
    /// The camera that follows the cartographer, from where it is at the start.
    /// </summary>
    public Entity? Camera { get; set; }

    /// <summary>
    /// Whether the keys move the cartographer: not while a dialogue plays.
    /// </summary>
    [DataMemberIgnore]
    public bool CanMove { get; set; } = true;

    /// <summary>
    /// Sets the area the cartographer walks in: the place the cartographer just got to.
    /// </summary>
    public void SetArea(Vector2 centre, Vector2 halfSize)
    {
        Centre = centre;
        Bounds = halfSize;
    }

    /// <inheritdoc />
    public override void Start()
    {
        if (Camera is not null)
            cameraOffset = Camera.Transform.Position - Entity.Transform.Position;
    }

    /// <inheritdoc />
    public override void Update()
    {
        if (CanMove)
        {
            var direction = Vector3.Zero;
            if (Input.IsKeyDown(Keys.W) || Input.IsKeyDown(Keys.Up))
                direction.Z -= 1;
            if (Input.IsKeyDown(Keys.S) || Input.IsKeyDown(Keys.Down))
                direction.Z += 1;
            if (Input.IsKeyDown(Keys.A) || Input.IsKeyDown(Keys.Left))
                direction.X -= 1;
            if (Input.IsKeyDown(Keys.D) || Input.IsKeyDown(Keys.Right))
                direction.X += 1;

            if (direction != Vector3.Zero)
            {
                direction.Normalize();
                var position = Entity.Transform.Position + direction * Speed * (float)Game.UpdateTime.Elapsed.TotalSeconds;
                position.X = MathUtil.Clamp(position.X, Centre.X - Bounds.X, Centre.X + Bounds.X);
                position.Z = MathUtil.Clamp(position.Z, Centre.Y - Bounds.Y, Centre.Y + Bounds.Y);
                Entity.Transform.Position = position;
            }
        }

        if (Camera is not null)
            Camera.Transform.Position = Entity.Transform.Position + cameraOffset;
    }
}
