// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Stride.PlayMode.Tests;

/// <summary>
/// A component whose members the edit tests change: a clamping and a throwing setter, a struct, lists, a dictionary, an
/// abstract member.
/// </summary>
[DataContract]
public sealed class EditableComponent : EntityComponent
{
    public float Speed { get; set; }

    /// <summary>Kept within [0, 1] by its setter.</summary>
    public float Ratio { get => field; set => field = Math.Clamp(value, 0, 1); }

    /// <summary>Refuses negative values.</summary>
    public int Count
    {
        get => field;
        set => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "The count cannot be negative.");
    }

    public Vector3 Offset { get; set; }

    public Color3 Tint { get; set; }

    public EditMode Mode { get; set; }

    public string? Label { get; set; }

    public List<int> Values { get; } = [];

    public List<EditShape> Shapes { get; } = [];

    public Dictionary<string, float> Table { get; } = [];

    public EditShape? Shape { get; set; }

    /// <summary>A struct whose members are fields, inside a struct.</summary>
    public EditRange Range { get; set; }
}

/// <summary>
/// The modes of <see cref="EditableComponent.Mode"/>.
/// </summary>
public enum EditMode
{
    Walk,
    Run,
}

/// <summary>
/// A struct with a nested struct.
/// </summary>
[DataContract]
public struct EditRange
{
    public float Min;

    public Vector2 Span;
}

/// <summary>
/// An abstract type, for null and new instances.
/// </summary>
[DataContract]
public abstract class EditShape
{
    public float Size { get; set; } = 1;
}

/// <summary>
/// A shape.
/// </summary>
[DataContract]
public sealed class EditBox : EditShape
{
    public Vector3 Extent { get; set; } = Vector3.One;
}

/// <summary>
/// Another shape.
/// </summary>
[DataContract]
public sealed class EditSphere : EditShape
{
    public float Radius { get; set; } = 0.5f;
}
