using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Input;

namespace SagaSandbox;

/// <summary>
/// Moves the player on the ground with WASD or the arrow keys.
/// </summary>
public sealed class PlayerController : SyncScript
{
    private const float Bound = 14f;

    /// <summary>
    /// Metres per second.
    /// </summary>
    public float Speed { get; set; } = 4f;

    /// <summary>
    /// Whether the keys move the player (not while a dialogue is open).
    /// </summary>
    [DataMemberIgnore]
    public bool CanMove { get; set; } = true;

    public override void Update()
    {
        if (!CanMove)
            return;

        var direction = Vector3.Zero;
        if (Input.IsKeyDown(Keys.W) || Input.IsKeyDown(Keys.Up))
            direction.Z -= 1;
        if (Input.IsKeyDown(Keys.S) || Input.IsKeyDown(Keys.Down))
            direction.Z += 1;
        if (Input.IsKeyDown(Keys.A) || Input.IsKeyDown(Keys.Left))
            direction.X -= 1;
        if (Input.IsKeyDown(Keys.D) || Input.IsKeyDown(Keys.Right))
            direction.X += 1;
        if (direction == Vector3.Zero)
            return;

        direction.Normalize();
        var position = Entity.Transform.Position + direction * Speed * (float)Game.UpdateTime.Elapsed.TotalSeconds;
        position.X = MathUtil.Clamp(position.X, -Bound, Bound);
        position.Z = MathUtil.Clamp(position.Z, -Bound, Bound);
        Entity.Transform.Position = position;
    }
}
