using Stride.Core.Mathematics;
using Stride.Engine;

namespace Thornwick;

/// <summary>
/// Keeps an orthographic camera on a target, at a whole zoom, inside the frame of the place the target is in.
/// </summary>
public sealed class FollowCamera : SyncScript
{
    /// <summary>
    /// The entity the camera follows.
    /// </summary>
    /// <userdoc>The entity the camera follows: the hero.</userdoc>
    public Entity? Target { get; set; }

    /// <summary>
    /// The pixels of the art in one unit of the world.
    /// </summary>
    /// <userdoc>How many pixels of the art make one unit: 16, a cell of Thornwick's tiles.</userdoc>
    public float PixelsPerUnit { get; set; } = 16;

    /// <inheritdoc />
    public override void Update()
    {
        if (Target is null || GraphicsDevice?.Presenter?.BackBuffer is not { } screen)
        {
            return;
        }

        // The middle of the target's cell, inside the frame of the place its PlayerController walks in
        var target = Target.Transform.Position;
        var frame = Target.Get<PlayerController>()?.Walker?.Place.Frame;
        var view = CameraFraming.Fit(new Vector2(target.X, target.Y + 0.5f), frame, new Int2(screen.Width, screen.Height), PixelsPerUnit);

        if (Entity.Get<CameraComponent>() is { } camera)
        {
            camera.OrthographicSize = view.OrthographicSize;
        }
        Entity.Transform.Position = new Vector3(view.Center, Entity.Transform.Position.Z);
    }
}
