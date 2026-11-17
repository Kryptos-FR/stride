// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.PlayMode.Protocol;

namespace Stride.PlayMode.Snapshots;

/// <summary>
/// The kinds of <see cref="PlayMirrorReference"/>.
/// </summary>
public enum PlayMirrorReferenceKind : byte
{
    /// <summary>An entity outside the snapshot's component.</summary>
    Entity = 1,

    /// <summary>A component outside the snapshot's component: another component of its entity, or of another entity.</summary>
    Component = 2,
}

/// <summary>
/// An object a component refers to that is not part of it: it travels as this descriptor, and the reader decides what it
/// becomes (the mirror's own entity and components, or a stand-in).
/// </summary>
/// <remarks>
/// Entities are named by the agent's runtime handle: the instances of a prefab share their ids, the handles are unique. The
/// ids travel too, for an entity the agent does not track (handle 0) and for the editor to find the entity's asset.
/// </remarks>
/// <param name="Kind">An entity or a component.</param>
/// <param name="EntityHandle">The agent's handle of the entity, or of the component's entity; 0 when the agent does not
/// track it (not in a running scene).</param>
/// <param name="EntityId">The id of the entity, or of the component's entity.</param>
/// <param name="EntityName">The name of the entity, or of the component's entity.</param>
/// <param name="ComponentId">The component's id, for <see cref="PlayMirrorReferenceKind.Component"/>.</param>
/// <param name="ComponentTypeName">The component's type (full name, a comma and the assembly's simple name).</param>
public readonly record struct PlayMirrorReference(PlayMirrorReferenceKind Kind, int EntityHandle, Guid EntityId, string? EntityName, Guid ComponentId, string? ComponentTypeName)
{
    internal static PlayMirrorReference Read(PlayReader reader)
        => new((PlayMirrorReferenceKind)reader.ReadByte(), reader.ReadInt32(), reader.ReadGuid(), reader.ReadString(), reader.ReadGuid(), reader.ReadString());

    internal static void Write(PlayWriter writer, PlayMirrorReference reference)
    {
        writer.Write((byte)reference.Kind);
        writer.Write(reference.EntityHandle);
        writer.Write(reference.EntityId);
        writer.Write(reference.EntityName);
        writer.Write(reference.ComponentId);
        writer.Write(reference.ComponentTypeName);
    }
}
