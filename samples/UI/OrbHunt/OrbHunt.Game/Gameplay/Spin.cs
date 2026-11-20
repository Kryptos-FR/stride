// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace OrbHunt.Gameplay;

/// <summary>
/// Turns its entity around its up axis, as the planet turns. A parent entity can tilt the axis.
/// </summary>
public sealed class Spin : SyncScript
{
    /// <summary>
    /// Gets or sets how fast the entity turns, in degrees per second.
    /// </summary>
    /// <userdoc>How fast the entity turns, in degrees per second.</userdoc>
    [DataMember(10)]
    public float Speed { get; set; } = 3;

    public override void Update()
        => Entity.Transform.Rotation = Quaternion.RotationY(MathUtil.DegreesToRadians(Speed) * (float)Game.UpdateTime.Total.TotalSeconds);
}
