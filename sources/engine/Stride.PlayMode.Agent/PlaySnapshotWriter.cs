// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Engine;
using Stride.PlayMode.Protocol;
using Stride.PlayMode.Snapshots;

namespace Stride.PlayMode.Agent;

/// <summary>
/// Writes the snapshot of a live entity, one component at a time, so that a component that cannot be written fails alone.
/// </summary>
public static class PlaySnapshotWriter
{
    /// <summary>
    /// The largest payload of one component; a larger one is replaced by an error, so that a component holding a big list
    /// does not flood the subscribers five times a second.
    /// </summary>
    public const int MaxComponentLength = 1024 * 1024;

    /// <summary>
    /// Writes an entity's components.
    /// </summary>
    /// <param name="entity">The entity, on the game thread.</param>
    /// <param name="handleOf">Gives the agent's handle of an entity.</param>
    public static PlayEntitySnapshot Write(Entity entity, Func<Entity, int> handleOf)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(handleOf);
        var components = new List<PlayComponentSnapshot>(entity.Components.Count);
        foreach (var component in entity.Components)
        {
            var typeName = PlayMirrorSerializer.TypeNameOf(component.GetType());
            try
            {
                var payload = PlayMirrorSerializer.Write(component, handleOf);
                components.Add(payload.Length <= MaxComponentLength
                    ? new PlayComponentSnapshot(component.Id, typeName, PlaySnapshotFormat.Binary, payload, null)
                    : new PlayComponentSnapshot(component.Id, typeName, PlaySnapshotFormat.None, ReadOnlyMemory<byte>.Empty, $"The component takes {payload.Length / 1024} KB, more than the {MaxComponentLength / 1024} KB a snapshot carries."));
            }
            catch (Exception e)
            {
                // Content made at run time (no URL), a type with no serializer, a getter that throws...
                components.Add(new PlayComponentSnapshot(component.Id, typeName, PlaySnapshotFormat.None, ReadOnlyMemory<byte>.Empty, $"{e.GetType().Name}: {e.Message}"));
            }
        }
        return new PlayEntitySnapshot(handleOf(entity), entity.Id, entity.Name, components);
    }
}
