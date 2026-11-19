// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.PlayMode.Protocol;

/// <summary>
/// What a <see cref="PlayStructureEdit"/> changes in the running game.
/// </summary>
public enum PlayStructureOperation : byte
{
    /// <summary>Adds a new component of <see cref="PlayStructureEdit.TypeName"/> to the entity, at
    /// <see cref="PlayStructureEdit.Index"/> (at the end when negative).</summary>
    AddComponent = 1,

    /// <summary>Removes the component <see cref="PlayStructureEdit.ComponentId"/> of the entity.</summary>
    RemoveComponent = 2,

    /// <summary>Creates an empty entity named <see cref="PlayStructureEdit.Name"/>, under <see cref="PlayStructureEdit.ParentHandle"/>,
    /// or at the root of <see cref="PlayStructureEdit.SceneHandle"/>.</summary>
    CreateEntity = 3,

    /// <summary>Removes the entity, with its children, from the game.</summary>
    DeleteEntity = 4,

    /// <summary>Clones the entity with its children, and puts the copy right after it, under the same parent.</summary>
    DuplicateEntity = 5,

    /// <summary>Moves the entity under <see cref="PlayStructureEdit.ParentHandle"/>, or to the root of
    /// <see cref="PlayStructureEdit.SceneHandle"/>, at <see cref="PlayStructureEdit.Index"/> among its new siblings (the last
    /// when negative).</summary>
    MoveEntity = 6,

    /// <summary>Instantiates the prefab of <see cref="PlayStructureEdit.Url"/>, from the game's build, under
    /// <see cref="PlayStructureEdit.ParentHandle"/>, or at the root of <see cref="PlayStructureEdit.SceneHandle"/>.</summary>
    SpawnPrefab = 7,
}

/// <summary>
/// A subscriber changes the structure of the running game: a component added or removed, an entity created, deleted,
/// duplicated or moved, a prefab spawned. The agent applies it at the start of the next frame, with the edits, in the order
/// they came, and answers with a <see cref="PlayStructureResult"/> of the same <see cref="Id"/>; the tree batch of that frame
/// shows the change.
/// </summary>
/// <param name="Id">The subscriber's number of the command, which the result carries; the edits and the commands of a
/// subscriber share their numbers.</param>
/// <param name="Operation">What the command does.</param>
/// <param name="EntityHandle">The entity it works on (<see cref="PlayEntityInfo.Handle"/>); 0 for a create and a spawn.</param>
/// <param name="ComponentId">The component it removes.</param>
/// <param name="SceneHandle">The scene of a create, a move or a spawn at the root of a scene (<see cref="PlaySceneInfo.Handle"/>).</param>
/// <param name="ParentHandle">The entity a create, a move or a spawn goes under; 0 for the root of <see cref="SceneHandle"/>.</param>
/// <param name="Index">Where a component is added, or where a moved entity goes among its new siblings; negative for the end.</param>
/// <param name="TypeName">The type of the component added: its full name, a comma and its assembly's simple name.</param>
/// <param name="Url">The URL of the prefab spawned, in the game's build.</param>
/// <param name="Name">The name of the entity created.</param>
/// <param name="KeepWorldTransform">For a move: whether the entity keeps its place in the world (its local transform is
/// changed); otherwise it keeps its local transform, relative to its new parent.</param>
public sealed record PlayStructureEdit(
    int Id,
    PlayStructureOperation Operation,
    int EntityHandle = 0,
    Guid ComponentId = default,
    int SceneHandle = 0,
    int ParentHandle = 0,
    int Index = -1,
    string? TypeName = null,
    string? Url = null,
    string? Name = null,
    bool KeepWorldTransform = false) : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.StructureEdit;

    internal static PlayStructureEdit Read(PlayReader reader)
        => new(reader.ReadInt32(), (PlayStructureOperation)reader.ReadByte(), reader.ReadInt32(), reader.ReadGuid(), reader.ReadInt32(), reader.ReadInt32(),
            reader.ReadInt32(), reader.ReadString(), reader.ReadString(), reader.ReadString(), reader.ReadBoolean());

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer)
    {
        writer.Write(Id);
        writer.Write((byte)Operation);
        writer.Write(EntityHandle);
        writer.Write(ComponentId);
        writer.Write(SceneHandle);
        writer.Write(ParentHandle);
        writer.Write(Index);
        writer.Write(TypeName);
        writer.Write(Url);
        writer.Write(Name);
        writer.Write(KeepWorldTransform);
    }
}

/// <summary>
/// The game's answer to a <see cref="PlayStructureEdit"/>, sent at the start of the frame it was applied in, before the tree
/// batch of that frame.
/// </summary>
/// <param name="Id">The command's <see cref="PlayStructureEdit.Id"/>.</param>
/// <param name="Frame">The agent's frame the command was applied in.</param>
/// <param name="Status"><see cref="PlayEditStatus.Applied"/>, <see cref="PlayEditStatus.Failed"/> (with
/// <see cref="Error"/>), or <see cref="PlayEditStatus.Gone"/> when the entity, its component, the parent or the scene is no
/// longer in the game.</param>
/// <param name="Entities">The handles of the entities the command added to the game (a create, a duplicate, a spawn): the
/// first one (or the roots of a prefab) first, then their descendants, each after its parent. Empty otherwise.</param>
/// <param name="ComponentId">The id of the component added.</param>
/// <param name="Error">Why the game refused it.</param>
public sealed record PlayStructureResult(int Id, long Frame, PlayEditStatus Status, IReadOnlyList<int> Entities, Guid ComponentId, string? Error) : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.StructureResult;

    internal static PlayStructureResult Read(PlayReader reader)
        => new(reader.ReadInt32(), reader.ReadInt64(), (PlayEditStatus)reader.ReadByte(), reader.ReadList(x => x.ReadInt32(), 4), reader.ReadGuid(), reader.ReadString());

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer)
    {
        writer.Write(Id);
        writer.Write(Frame);
        writer.Write((byte)Status);
        writer.WriteList(Entities, static (w, x) => w.Write(x));
        writer.Write(ComponentId);
        writer.Write(Error);
    }

    /// <inheritdoc/>
    public bool Equals(PlayStructureResult? other)
        => other is not null && Id == other.Id && Frame == other.Frame && Status == other.Status && Entities.SequenceEqual(other.Entities) && ComponentId == other.ComponentId && Error == other.Error;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Id, Frame, Status, Entities.Count, ComponentId, Error);
}
