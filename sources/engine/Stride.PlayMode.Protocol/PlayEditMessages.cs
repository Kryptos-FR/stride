// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;

namespace Stride.PlayMode.Protocol;

/// <summary>
/// The kinds of <see cref="PlayPathSegment"/>.
/// </summary>
public enum PlayPathSegmentKind : byte
{
    /// <summary>A member (a property or a field) of an object, by its name in the code.</summary>
    Member = 1,

    /// <summary>An item of a list or an array, by its position.</summary>
    Index = 2,

    /// <summary>An item of a dictionary, by its key, written with the snapshots' codec.</summary>
    Key = 3,
}

/// <summary>
/// One step of the path of an edit, from a component down to the value it changes.
/// </summary>
/// <param name="Kind">A member, an index or a key.</param>
/// <param name="Name">The member's name in the code (not the name it is serialized under), for <see cref="PlayPathSegmentKind.Member"/>.</param>
/// <param name="Index">The position, for <see cref="PlayPathSegmentKind.Index"/>.</param>
/// <param name="Key">The key in the snapshots' codec, for <see cref="PlayPathSegmentKind.Key"/>.</param>
/// <param name="KeyText">The key as text, for messages (the error of an edit, the log); not used to find the item.</param>
public sealed record PlayPathSegment(PlayPathSegmentKind Kind, string? Name, int Index, ReadOnlyMemory<byte> Key, string? KeyText = null)
{
    internal const int MinimumLength = 11;

    /// <summary>A member of an object.</summary>
    public static PlayPathSegment Member(string name) => new(PlayPathSegmentKind.Member, name ?? throw new ArgumentNullException(nameof(name)), 0, ReadOnlyMemory<byte>.Empty);

    /// <summary>An item of a list or an array.</summary>
    public static PlayPathSegment Item(int index) => new(PlayPathSegmentKind.Index, null, index, ReadOnlyMemory<byte>.Empty);

    /// <summary>An item of a dictionary.</summary>
    public static PlayPathSegment ForKey(ReadOnlyMemory<byte> key, string? keyText) => new(PlayPathSegmentKind.Key, null, 0, key, keyText);

    /// <inheritdoc/>
    public bool Equals(PlayPathSegment? other)
        => other is not null && Kind == other.Kind && Name == other.Name && Index == other.Index && Key.Span.SequenceEqual(other.Key.Span);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Kind, Name, Index, Key.Length);

    /// <inheritdoc/>
    public override string ToString() => Kind switch
    {
        PlayPathSegmentKind.Member => Name ?? string.Empty,
        PlayPathSegmentKind.Index => $"[{Index}]",
        _ => $"[{KeyText ?? "key"}]",
    };

    /// <summary>
    /// The path as text: <c>Position.X</c>, <c>Values[2]</c>, <c>Table[Fast]</c>.
    /// </summary>
    public static string Format(IEnumerable<PlayPathSegment> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var text = new StringBuilder();
        foreach (var segment in path)
        {
            if (segment.Kind == PlayPathSegmentKind.Member && text.Length > 0)
                text.Append('.');
            text.Append(segment);
        }
        return text.ToString();
    }

    internal static PlayPathSegment Read(PlayReader reader)
        => new((PlayPathSegmentKind)reader.ReadByte(), reader.ReadString(), reader.ReadInt32(), reader.ReadBytes(), reader.ReadString());

    internal static void Write(PlayWriter writer, PlayPathSegment segment)
    {
        writer.Write((byte)segment.Kind);
        writer.Write(segment.Name);
        writer.Write(segment.Index);
        writer.WriteBytes(segment.Key.Span);
        writer.Write(segment.KeyText);
    }
}

/// <summary>
/// The kinds of <see cref="PlayEditValue"/>.
/// </summary>
public enum PlayEditValueKind : byte
{
    /// <summary>No value: <c>null</c>.</summary>
    Null = 0,

    /// <summary>A value written with the snapshots' codec (<see cref="PlayEditValue.Payload"/>).</summary>
    Value = 1,

    /// <summary>A new instance of a type, which the game makes by its name with the type's own defaults
    /// (<see cref="PlayEditValue.TypeName"/>).</summary>
    NewInstance = 2,
}

/// <summary>
/// The value an edit sets or adds.
/// </summary>
/// <param name="Kind">Null, a value, or a new instance.</param>
/// <param name="TypeName">For <see cref="PlayEditValueKind.NewInstance"/>: the type, its full name, a comma and its assembly's
/// simple name.</param>
/// <param name="Payload">For <see cref="PlayEditValueKind.Value"/>: the value in the snapshots' codec.</param>
public sealed record PlayEditValue(PlayEditValueKind Kind, string? TypeName, ReadOnlyMemory<byte> Payload)
{
    internal const int MinimumLength = 6;

    /// <summary>No value.</summary>
    public static PlayEditValue Null { get; } = new(PlayEditValueKind.Null, null, ReadOnlyMemory<byte>.Empty);

    /// <summary>A value written with the snapshots' codec.</summary>
    public static PlayEditValue Of(ReadOnlyMemory<byte> payload) => new(PlayEditValueKind.Value, null, payload);

    /// <summary>A new instance of a type, made by the game.</summary>
    public static PlayEditValue NewInstanceOf(string typeName) => new(PlayEditValueKind.NewInstance, typeName ?? throw new ArgumentNullException(nameof(typeName)), ReadOnlyMemory<byte>.Empty);

    /// <inheritdoc/>
    public bool Equals(PlayEditValue? other)
        => other is not null && Kind == other.Kind && TypeName == other.TypeName && Payload.Span.SequenceEqual(other.Payload.Span);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Kind, TypeName, Payload.Length);

    internal static PlayEditValue Read(PlayReader reader) => new((PlayEditValueKind)reader.ReadByte(), reader.ReadString(), reader.ReadBytes());

    internal static void Write(PlayWriter writer, PlayEditValue value)
    {
        writer.Write((byte)value.Kind);
        writer.Write(value.TypeName);
        writer.WriteBytes(value.Payload.Span);
    }
}

/// <summary>
/// What an edit does (<see cref="PlayEdit.Operation"/>).
/// </summary>
public enum PlayEditOperation : byte
{
    /// <summary>Sets the value at the path through its setter (or the item at an index or a key).</summary>
    Set = 1,

    /// <summary>Adds the value at the end of the list at the path.</summary>
    Add = 2,

    /// <summary>Inserts the value in a list at the path's last index, or adds it to a dictionary under the path's last key.</summary>
    Insert = 3,

    /// <summary>Removes the item of a list at the path's last index, or of a dictionary under its last key.</summary>
    Remove = 4,

    /// <summary>Moves the item of a list at the path's last index to <see cref="PlayEdit.Target"/>'s index, or gives the item of a
    /// dictionary at the path's last key the key of <see cref="PlayEdit.Target"/>. The item itself moves: an object keeps its
    /// state.</summary>
    Move = 5,
}

/// <summary>
/// A subscriber changes a value of a live entity. The agent applies it at the start of the next frame, on the game thread,
/// through the setters, and answers with a <see cref="PlayEditResult"/> of the same <see cref="Id"/>.
/// </summary>
/// <param name="Id">The subscriber's number of the edit, which the result carries.</param>
/// <param name="EntityHandle">The agent's handle of the entity (<see cref="PlayEntityInfo.Handle"/>).</param>
/// <param name="ComponentId">The id of the entity's component the path starts from.</param>
/// <param name="Path">From the component down to the value; empty only for <see cref="PlayEditOperation.Add"/> on a component
/// that is a list (none is).</param>
/// <param name="Operation">What the edit does.</param>
/// <param name="Value">The value to set, add or insert; <see cref="PlayEditValue.Null"/> for a remove or a move.</param>
/// <param name="Target">The index or key an item moves to, for <see cref="PlayEditOperation.Move"/>.</param>
public sealed record PlayEdit(int Id, int EntityHandle, Guid ComponentId, IReadOnlyList<PlayPathSegment> Path, PlayEditOperation Operation, PlayEditValue Value, PlayPathSegment? Target = null) : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.Edit;

    /// <summary>The path as text (<see cref="PlayPathSegment.Format"/>).</summary>
    public string PathText => PlayPathSegment.Format(Path);

    internal static PlayEdit Read(PlayReader reader)
        => new(reader.ReadInt32(), reader.ReadInt32(), reader.ReadGuid(), reader.ReadList(PlayPathSegment.Read, PlayPathSegment.MinimumLength), (PlayEditOperation)reader.ReadByte(),
            PlayEditValue.Read(reader), reader.ReadBoolean() ? PlayPathSegment.Read(reader) : null);

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer)
    {
        writer.Write(Id);
        writer.Write(EntityHandle);
        writer.Write(ComponentId);
        writer.WriteList(Path, PlayPathSegment.Write);
        writer.Write((byte)Operation);
        PlayEditValue.Write(writer, Value);
        writer.Write(Target is not null);
        if (Target is not null)
            PlayPathSegment.Write(writer, Target);
    }
}

/// <summary>
/// What became of an edit (<see cref="PlayEditResult.Status"/>).
/// </summary>
public enum PlayEditStatus : byte
{
    /// <summary>The edit was applied; <see cref="PlayEditResult.ReadBack"/> has the value the game reads after it (a setter
    /// may have changed it).</summary>
    Applied = 1,

    /// <summary>A later edit of the same member in the same frame replaced it, and was applied; the read-back is the later
    /// one's.</summary>
    Coalesced = 2,

    /// <summary>The game refused it (<see cref="PlayEditResult.Error"/>): a setter threw, the path or the type is not in the
    /// game, the value could not be read. The read-back has the value as it stayed, when it can be read.</summary>
    Failed = 3,

    /// <summary>The entity, or its component, is no longer in the game.</summary>
    Gone = 4,
}

/// <summary>
/// The game's answer to a <see cref="PlayEdit"/>, sent at the frame it was applied. The next snapshot of the entity is taken
/// at the end of that frame.
/// </summary>
/// <param name="Id">The edit's <see cref="PlayEdit.Id"/>.</param>
/// <param name="Frame">The agent's frame the edit was applied in.</param>
/// <param name="Status">What became of it.</param>
/// <param name="ReadBack">The value at the path once the edit applied, in the snapshots' codec; <see cref="PlayEditValue.Null"/>
/// for a value that is <c>null</c>, and for a collection operation (the snapshot that follows has the collection).</param>
/// <param name="Error">Why the game refused it.</param>
public sealed record PlayEditResult(int Id, long Frame, PlayEditStatus Status, PlayEditValue ReadBack, string? Error) : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.EditResult;

    internal static PlayEditResult Read(PlayReader reader)
        => new(reader.ReadInt32(), reader.ReadInt64(), (PlayEditStatus)reader.ReadByte(), PlayEditValue.Read(reader), reader.ReadString());

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer)
    {
        writer.Write(Id);
        writer.Write(Frame);
        writer.Write((byte)Status);
        PlayEditValue.Write(writer, ReadBack);
        writer.Write(Error);
    }
}
