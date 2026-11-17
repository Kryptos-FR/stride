// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Engine;

namespace Stride.Editor.Tests.PlayMode;

/// <summary>
/// A game's component, as the editor has it.
/// </summary>
[DataContract]
public sealed class PlayTestComponent : EntityComponent
{
    public int Health { get; set; }

    public string? Label { get; set; }

    public Entity? Target { get; set; }

    public TransformComponent? TargetTransform { get; set; }

    public PlayTestComponent? Other { get; set; }

    public PlayTestComponent? Sibling { get; set; }
}

/// <summary>
/// Another component of the same entity.
/// </summary>
[DataContract]
public sealed class PlayTestSecondComponent : EntityComponent
{
    public float Speed { get; set; }
}
