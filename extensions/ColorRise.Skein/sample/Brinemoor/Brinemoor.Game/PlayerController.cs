using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Input;

namespace Brinemoor;

/// <summary>
/// Moves the courier on the island with WASD or the arrow keys, and the camera with the courier.
/// </summary>
public sealed class PlayerController : SyncScript
{
    private Vector3 cameraOffset;

    /// <summary>
    /// Metres per second.
    /// </summary>
    public float Speed { get; set; } = 5f;

    /// <summary>
    /// How far from the centre of the island the courier walks, along X and along Z, in metres.
    /// </summary>
    public Vector2 Bounds { get; set; } = new(22f, 18f);

    /// <summary>
    /// The camera that follows the courier, from where it is at the start.
    /// </summary>
    public Entity? Camera { get; set; }

    /// <summary>
    /// Whether the keys move the courier: not while a dialogue plays.
    /// </summary>
    [DataMemberIgnore]
    public bool CanMove { get; set; } = true;

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
                position.X = MathUtil.Clamp(position.X, -Bounds.X, Bounds.X);
                position.Z = MathUtil.Clamp(position.Z, -Bounds.Y, Bounds.Y);
                Entity.Transform.Position = position;
            }
        }

        if (Camera is not null)
            Camera.Transform.Position = Entity.Transform.Position + cameraOffset;
    }
}
