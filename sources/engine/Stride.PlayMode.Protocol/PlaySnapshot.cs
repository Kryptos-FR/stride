// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.PlayMode.Protocol;

/// <summary>
/// The formats of a component's payload in a snapshot. The protocol carries the payload as opaque bytes, so that a format can
/// be added without changing it.
/// </summary>
public enum PlaySnapshotFormat : byte
{
    /// <summary>The agent could not write the component; <see cref="PlayComponentSnapshot.Error"/> says why.</summary>
    None = 0,

    /// <summary>
    /// Stride's binary serialization, cut at the entity boundary: objects outside the component's entity travel as descriptors
    /// (the agent's codec, "PlayMirror"). Reading it needs the same build of the component's type as the game's.
    /// </summary>
    Binary = 1,
}

/// <summary>
/// One component in a snapshot.
/// </summary>
/// <param name="Id">The component's id.</param>
/// <param name="TypeName">Its type: the full name, a comma and the assembly's simple name.</param>
/// <param name="Format">The format of <paramref name="Payload"/>.</param>
/// <param name="Payload">The component's state in that format.</param>
/// <param name="Error">Why the agent could not write it, when <paramref name="Format"/> is <see cref="PlaySnapshotFormat.None"/>.</param>
public sealed record PlayComponentSnapshot(Guid Id, string TypeName, PlaySnapshotFormat Format, ReadOnlyMemory<byte> Payload, string? Error)
{
    internal static PlayComponentSnapshot Read(PlayReader reader)
        => new(reader.ReadGuid(), reader.ReadString() ?? string.Empty, (PlaySnapshotFormat)reader.ReadByte(), reader.ReadBytes(), reader.ReadString());

    internal static void Write(PlayWriter writer, PlayComponentSnapshot component)
    {
        writer.Write(component.Id);
        writer.Write(component.TypeName);
        writer.Write((byte)component.Format);
        writer.WriteBytes(component.Payload.Span);
        writer.Write(component.Error);
    }
}

/// <summary>
/// One entity in a snapshot.
/// </summary>
/// <param name="Handle">The agent's handle of the entity.</param>
/// <param name="Id">The entity's id.</param>
/// <param name="Name">Its name.</param>
/// <param name="Components">Its components, in order.</param>
public sealed record PlayEntitySnapshot(int Handle, Guid Id, string? Name, IReadOnlyList<PlayComponentSnapshot> Components)
{
    internal static PlayEntitySnapshot Read(PlayReader reader)
        => new(reader.ReadInt32(), reader.ReadGuid(), reader.ReadString(), reader.ReadList(PlayComponentSnapshot.Read, 23));

    internal static void Write(PlayWriter writer, PlayEntitySnapshot entity)
    {
        writer.Write(entity.Handle);
        writer.Write(entity.Id);
        writer.Write(entity.Name);
        writer.WriteList(entity.Components, PlayComponentSnapshot.Write);
    }
}

/// <summary>
/// The state of a subscriber's entities at the end of a frame. An entity removed from the game is not in it.
/// </summary>
/// <param name="Frame">The game's frame.</param>
/// <param name="GameTime">The game's total time, in seconds.</param>
/// <param name="Entities">The subscribed entities still in the game.</param>
public sealed record PlaySnapshot(long Frame, double GameTime, IReadOnlyList<PlayEntitySnapshot> Entities) : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.Snapshot;

    internal static PlaySnapshot Read(PlayReader reader) => new(reader.ReadInt64(), reader.ReadDouble(), reader.ReadList(PlayEntitySnapshot.Read, 25));

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer)
    {
        writer.Write(Frame);
        writer.Write(GameTime);
        writer.WriteList(Entities, PlayEntitySnapshot.Write);
    }

    /// <summary>
    /// Writes one entity of a snapshot into its own buffer, so that the agent can reuse it for several subscribers in a frame.
    /// </summary>
    public static byte[] EncodeEntity(PlayEntitySnapshot entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var writer = new PlayWriter();
        PlayEntitySnapshot.Write(writer, entity);
        return writer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Writes a snapshot frame from entities already encoded by <see cref="EncodeEntity"/>.
    /// </summary>
    public static byte[] EncodeFrame(long frame, double gameTime, IReadOnlyList<byte[]> encodedEntities)
    {
        ArgumentNullException.ThrowIfNull(encodedEntities);
        var writer = new PlayWriter(64 + encodedEntities.Sum(x => x.Length));
        writer.Write(frame);
        writer.Write(gameTime);
        writer.Write(encodedEntities.Count);
        // Already in the layout of PlayEntitySnapshot.Write
        foreach (var entity in encodedEntities)
            writer.WriteRaw(entity);
        return PlayFraming.Encode(PlayMessageType.Snapshot, writer.WrittenSpan);
    }
}
