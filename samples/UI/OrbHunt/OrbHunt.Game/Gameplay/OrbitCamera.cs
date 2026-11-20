// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace OrbHunt.Gameplay;

/// <summary>
/// Moves its entity, a camera, around a target it keeps looking at: the background, a skybox, turns with it.
/// </summary>
public sealed class OrbitCamera : SyncScript
{
    /// <summary>
    /// Gets or sets the entity the camera turns around and looks at.
    /// </summary>
    /// <userdoc>The entity the camera turns around and looks at.</userdoc>
    [DataMember(10)]
    public Entity? Target { get; set; }

    /// <summary>
    /// Gets or sets the distance from the target.
    /// </summary>
    /// <userdoc>The distance from the target.</userdoc>
    [DataMember(20)]
    public float Distance { get; set; } = 9;

    /// <summary>
    /// Gets or sets how high the camera is above the target, in degrees.
    /// </summary>
    /// <userdoc>How high the camera is above the target, in degrees.</userdoc>
    [DataMember(30)]
    public float Elevation { get; set; } = 12;

    /// <summary>
    /// Gets or sets how fast the camera turns around the target, in degrees per second.
    /// </summary>
    /// <userdoc>How fast the camera turns around the target, in degrees per second.</userdoc>
    [DataMember(40)]
    public float Speed { get; set; } = 4;

    public override void Update()
    {
        var center = Target?.Transform.WorldMatrix.TranslationVector ?? Vector3.Zero;
        var angle = MathUtil.DegreesToRadians(Speed) * (float)Game.UpdateTime.Total.TotalSeconds;
        var elevation = MathUtil.DegreesToRadians(Elevation);
        var offset = new Vector3(MathF.Sin(angle) * MathF.Cos(elevation), MathF.Sin(elevation), MathF.Cos(angle) * MathF.Cos(elevation)) * Distance;

        Entity.Transform.Position = center + offset;
        // A camera looks along its -Z axis
        Entity.Transform.Rotation = Quaternion.LookRotation(Vector3.Normalize(offset), Vector3.UnitY);
    }
}
