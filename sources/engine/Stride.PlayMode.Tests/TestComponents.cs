// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering;

namespace Stride.PlayMode.Tests;

/// <summary>
/// A component with values, references to entities and components, and content.
/// </summary>
[DataContract]
public sealed class TestComponent : EntityComponent
{
    public float Speed { get; set; }

    public Vector3 Offset { get; set; }

    public string? Label { get; set; }

    public List<int> Values { get; set; } = [];

    public Entity? Target { get; set; }

    public TransformComponent? TargetTransform { get; set; }

    public EntityComponent? Sibling { get; set; }

    public Model? Model { get; set; }
}

/// <summary>
/// A component whose serialization throws.
/// </summary>
[DataContract]
public sealed class ThrowingComponent : EntityComponent
{
    public int Value
    {
        get => throw new InvalidOperationException("Boom.");
        set { }
    }
}
