// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.PlayMode.Protocol;

/// <summary>
/// A component of a live entity, as the tree shows it.
/// </summary>
/// <param name="Id">The component's id.</param>
/// <param name="TypeName">Its type: the full name, a comma and the assembly's simple name.</param>
public sealed record PlayComponentInfo(Guid Id, string TypeName)
{
    internal static PlayComponentInfo Read(PlayReader reader) => new(reader.ReadGuid(), reader.ReadString() ?? string.Empty);

    internal static void Write(PlayWriter writer, PlayComponentInfo component)
    {
        writer.Write(component.Id);
        writer.Write(component.TypeName);
    }
}

/// <summary>
/// A scene of the running game: the root scene or a child scene.
/// </summary>
/// <param name="Handle">The agent's handle of this scene instance, unique for the life of the game.</param>
/// <param name="Id">The scene's id (the id of its asset).</param>
/// <param name="Name">The scene's name.</param>
/// <param name="Url">The URL the game loaded it from, or <c>null</c> for a scene made at run time.</param>
/// <param name="ParentHandle">The handle of its parent scene, or 0 for the root scene.</param>
public sealed record PlaySceneInfo(int Handle, Guid Id, string? Name, string? Url, int ParentHandle)
{
    internal static PlaySceneInfo Read(PlayReader reader) => new(reader.ReadInt32(), reader.ReadGuid(), reader.ReadString(), reader.ReadString(), reader.ReadInt32());

    internal static void Write(PlayWriter writer, PlaySceneInfo scene)
    {
        writer.Write(scene.Handle);
        writer.Write(scene.Id);
        writer.Write(scene.Name);
        writer.Write(scene.Url);
        writer.Write(scene.ParentHandle);
    }
}

/// <summary>
/// An entity of the running game.
/// </summary>
/// <param name="Handle">The agent's handle of this entity instance, unique for the life of the game. Entities instantiated
/// from a prefab keep the prefab's ids, so the id is not unique; the handle is.</param>
/// <param name="Id">The entity's id: the id in its scene or prefab asset, shared by the instances of a prefab.</param>
/// <param name="Name">The entity's name.</param>
/// <param name="Enabled">Whether all its components that can be disabled are enabled.</param>
/// <param name="ParentHandle">The handle of its parent entity, or 0 for an entity at the root of its scene.</param>
/// <param name="SceneHandle">The handle of its scene.</param>
/// <param name="Components">Its components, in order.</param>
public sealed record PlayEntityInfo(int Handle, Guid Id, string? Name, bool Enabled, int ParentHandle, int SceneHandle, IReadOnlyList<PlayComponentInfo> Components)
{
    internal static PlayEntityInfo Read(PlayReader reader)
        => new(reader.ReadInt32(), reader.ReadGuid(), reader.ReadString(), reader.ReadBoolean(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadList(PlayComponentInfo.Read, 17));

    internal static void Write(PlayWriter writer, PlayEntityInfo entity)
    {
        writer.Write(entity.Handle);
        writer.Write(entity.Id);
        writer.Write(entity.Name);
        writer.Write(entity.Enabled);
        writer.Write(entity.ParentHandle);
        writer.Write(entity.SceneHandle);
        writer.WriteList(entity.Components, PlayComponentInfo.Write);
    }
}

/// <summary>
/// The whole tree of the running scenes. It is sent when a subscriber connects and when the game changes its root scene;
/// it replaces whatever tree the subscriber had.
/// </summary>
/// <param name="Frame">The game's frame.</param>
/// <param name="Scenes">The scenes, each after its parent.</param>
/// <param name="Entities">The entities, each after its parent, children in the order of their parent's children.</param>
public sealed record PlayTree(long Frame, IReadOnlyList<PlaySceneInfo> Scenes, IReadOnlyList<PlayEntityInfo> Entities) : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.Tree;

    internal static PlayTree Read(PlayReader reader) => new(reader.ReadInt64(), reader.ReadList(PlaySceneInfo.Read, 26), reader.ReadList(PlayEntityInfo.Read, 34));

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer)
    {
        writer.Write(Frame);
        writer.WriteList(Scenes, PlaySceneInfo.Write);
        writer.WriteList(Entities, PlayEntityInfo.Write);
    }
}

/// <summary>
/// The kinds of <see cref="PlayTreeOperation"/>.
/// </summary>
public enum PlayTreeOperationKind : byte
{
    /// <summary>A scene was added (<see cref="PlaySceneAdded"/>).</summary>
    SceneAdded = 1,

    /// <summary>A scene was removed, with its entities (<see cref="PlaySceneRemoved"/>).</summary>
    SceneRemoved = 2,

    /// <summary>An entity was added (<see cref="PlayEntityAdded"/>).</summary>
    EntityAdded = 3,

    /// <summary>An entity was removed (<see cref="PlayEntityRemoved"/>).</summary>
    EntityRemoved = 4,

    /// <summary>An entity changed parent (<see cref="PlayEntityMoved"/>).</summary>
    EntityMoved = 5,

    /// <summary>An entity's components changed (<see cref="PlayEntityComponentsChanged"/>).</summary>
    ComponentsChanged = 6,

    /// <summary>An entity's name or enabled state changed (<see cref="PlayEntityStateChanged"/>).</summary>
    StateChanged = 7,
}

/// <summary>
/// A change of the tree.
/// </summary>
public abstract record PlayTreeOperation
{
    /// <summary>
    /// The kind of the change, written before its fields.
    /// </summary>
    public abstract PlayTreeOperationKind Kind { get; }

    internal static PlayTreeOperation Read(PlayReader reader)
    {
        var kind = (PlayTreeOperationKind)reader.ReadByte();
        return kind switch
        {
            PlayTreeOperationKind.SceneAdded => new PlaySceneAdded(PlaySceneInfo.Read(reader)),
            PlayTreeOperationKind.SceneRemoved => new PlaySceneRemoved(reader.ReadInt32()),
            PlayTreeOperationKind.EntityAdded => new PlayEntityAdded(PlayEntityInfo.Read(reader), reader.ReadInt32()),
            PlayTreeOperationKind.EntityRemoved => new PlayEntityRemoved(reader.ReadInt32()),
            PlayTreeOperationKind.EntityMoved => new PlayEntityMoved(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()),
            PlayTreeOperationKind.ComponentsChanged => new PlayEntityComponentsChanged(reader.ReadInt32(), reader.ReadList(PlayComponentInfo.Read, 17)),
            PlayTreeOperationKind.StateChanged => new PlayEntityStateChanged(reader.ReadInt32(), reader.ReadString(), reader.ReadBoolean()),
            _ => throw new PlayProtocolException($"Unknown tree operation {(byte)kind}."),
        };
    }

    internal static void Write(PlayWriter writer, PlayTreeOperation operation)
    {
        writer.Write((byte)operation.Kind);
        operation.WriteFields(writer);
    }

    /// <summary>
    /// Writes the fields of the change.
    /// </summary>
    private protected abstract void WriteFields(PlayWriter writer);
}

/// <summary>A scene was added to the running scenes; its entities follow as <see cref="PlayEntityAdded"/>.</summary>
/// <param name="Scene">The scene.</param>
public sealed record PlaySceneAdded(PlaySceneInfo Scene) : PlayTreeOperation
{
    /// <inheritdoc/>
    public override PlayTreeOperationKind Kind => PlayTreeOperationKind.SceneAdded;

    private protected override void WriteFields(PlayWriter writer) => PlaySceneInfo.Write(writer, Scene);
}

/// <summary>A scene was removed; its entities were removed before it, in the same batch.</summary>
/// <param name="Handle">The scene's handle.</param>
public sealed record PlaySceneRemoved(int Handle) : PlayTreeOperation
{
    /// <inheritdoc/>
    public override PlayTreeOperationKind Kind => PlayTreeOperationKind.SceneRemoved;

    private protected override void WriteFields(PlayWriter writer) => writer.Write(Handle);
}

/// <summary>An entity was added, with its parent and its place among its siblings at the end of the frame.</summary>
/// <param name="Entity">The entity.</param>
/// <param name="AfterHandle">The sibling it comes right after (the previous entity under its parent, or at the root of its
/// scene): its handle, 0 when it is the first, or -1 when the place is not known and it goes last.</param>
public sealed record PlayEntityAdded(PlayEntityInfo Entity, int AfterHandle = -1) : PlayTreeOperation
{
    /// <inheritdoc/>
    public override PlayTreeOperationKind Kind => PlayTreeOperationKind.EntityAdded;

    private protected override void WriteFields(PlayWriter writer)
    {
        PlayEntityInfo.Write(writer, Entity);
        writer.Write(AfterHandle);
    }
}

/// <summary>An entity was removed; its handle is not used again.</summary>
/// <param name="Handle">The entity's handle.</param>
public sealed record PlayEntityRemoved(int Handle) : PlayTreeOperation
{
    /// <inheritdoc/>
    public override PlayTreeOperationKind Kind => PlayTreeOperationKind.EntityRemoved;

    private protected override void WriteFields(PlayWriter writer) => writer.Write(Handle);
}

/// <summary>An entity changed parent or scene.</summary>
/// <param name="Handle">The entity's handle.</param>
/// <param name="ParentHandle">Its new parent, or 0 at the root of its scene.</param>
/// <param name="SceneHandle">Its scene.</param>
/// <param name="AfterHandle">The sibling it now comes right after: its handle, 0 when it is the first, or -1 when the place is
/// not known and it goes last.</param>
public sealed record PlayEntityMoved(int Handle, int ParentHandle, int SceneHandle, int AfterHandle = -1) : PlayTreeOperation
{
    /// <inheritdoc/>
    public override PlayTreeOperationKind Kind => PlayTreeOperationKind.EntityMoved;

    private protected override void WriteFields(PlayWriter writer)
    {
        writer.Write(Handle);
        writer.Write(ParentHandle);
        writer.Write(SceneHandle);
        writer.Write(AfterHandle);
    }
}

/// <summary>An entity's components changed (added, removed or replaced).</summary>
/// <param name="Handle">The entity's handle.</param>
/// <param name="Components">All its components now, in order.</param>
public sealed record PlayEntityComponentsChanged(int Handle, IReadOnlyList<PlayComponentInfo> Components) : PlayTreeOperation
{
    /// <inheritdoc/>
    public override PlayTreeOperationKind Kind => PlayTreeOperationKind.ComponentsChanged;

    private protected override void WriteFields(PlayWriter writer)
    {
        writer.Write(Handle);
        writer.WriteList(Components, PlayComponentInfo.Write);
    }
}

/// <summary>An entity's name or enabled state changed. The agent polls them, so the change can be a fraction of a second late.</summary>
/// <param name="Handle">The entity's handle.</param>
/// <param name="Name">Its name.</param>
/// <param name="Enabled">Whether all its components that can be disabled are enabled.</param>
public sealed record PlayEntityStateChanged(int Handle, string? Name, bool Enabled) : PlayTreeOperation
{
    /// <inheritdoc/>
    public override PlayTreeOperationKind Kind => PlayTreeOperationKind.StateChanged;

    private protected override void WriteFields(PlayWriter writer)
    {
        writer.Write(Handle);
        writer.Write(Name);
        writer.Write(Enabled);
    }
}

/// <summary>
/// The changes of the tree during one frame, coalesced: an entity added and removed in the frame is not in it, and a change of
/// an entity removed in the frame is not either. Scenes added come first, then entities added (parents before children),
/// and scenes removed last.
/// </summary>
/// <param name="Frame">The game's frame.</param>
/// <param name="Operations">The changes, in order.</param>
public sealed record PlayTreeBatch(long Frame, IReadOnlyList<PlayTreeOperation> Operations) : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.TreeBatch;

    internal static PlayTreeBatch Read(PlayReader reader) => new(reader.ReadInt64(), reader.ReadList(PlayTreeOperation.Read, 5));

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer)
    {
        writer.Write(Frame);
        writer.WriteList(Operations, PlayTreeOperation.Write);
    }
}
