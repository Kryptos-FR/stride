// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.PlayMode.Protocol;

/// <summary>
/// A message of the play mode's protocol. The messages do not depend on the channel they travel on (a named pipe now, a
/// socket later): <see cref="Encode"/> gives the bytes of a whole frame, <see cref="Decode"/> reads a frame back.
/// </summary>
public abstract record PlayMessage
{
    /// <summary>
    /// The type of the message, written in the frame's header.
    /// </summary>
    public abstract PlayMessageType Type { get; }

    /// <summary>
    /// Encodes the message as a whole frame (header and payload).
    /// </summary>
    public byte[] Encode()
    {
        var writer = new PlayWriter();
        WritePayload(writer);
        return PlayFraming.Encode(Type, writer.WrittenSpan);
    }

    /// <summary>
    /// Decodes the message of a frame.
    /// </summary>
    /// <param name="frame">A frame read by <see cref="PlayFraming.ReadAsync"/>.</param>
    /// <returns>The message, or a <see cref="PlayUnknownMessage"/> for a type this side does not know.</returns>
    /// <exception cref="PlayProtocolException">The payload is shorter than its fields, or a count does not fit.</exception>
    public static PlayMessage Decode(PlayFrame frame)
    {
        var reader = new PlayReader(frame.Payload);
        return frame.Type switch
        {
            PlayMessageType.Hello => PlayHello.Read(reader),
            PlayMessageType.Assemblies => PlayAssemblies.Read(reader),
            PlayMessageType.Tree => PlayTree.Read(reader),
            PlayMessageType.TreeBatch => PlayTreeBatch.Read(reader),
            PlayMessageType.Snapshot => PlaySnapshot.Read(reader),
            PlayMessageType.Log => PlayLog.Read(reader),
            PlayMessageType.Heartbeat => PlayHeartbeat.Read(reader),
            PlayMessageType.Goodbye => PlayGoodbye.Read(reader),
            PlayMessageType.Subscribe => PlaySubscribe.Read(reader),
            PlayMessageType.Quit => new PlayQuit(),
            PlayMessageType.State => PlayGameState.Read(reader),
            PlayMessageType.ScriptException => PlayScriptException.Read(reader),
            PlayMessageType.Pause => new PlayPause(),
            PlayMessageType.Resume => new PlayResume(),
            PlayMessageType.Step => PlayStep.Read(reader),
            PlayMessageType.TimeScale => PlayTimeScale.Read(reader),
            PlayMessageType.Edit => PlayEdit.Read(reader),
            PlayMessageType.EditResult => PlayEditResult.Read(reader),
            PlayMessageType.WindowOnTop => PlayWindowOnTop.Read(reader),
            PlayMessageType.EditorActive => PlayEditorActive.Read(reader),
            PlayMessageType.ContentRequest => new PlayContentRequest(),
            PlayMessageType.ContentIndex => PlayContentIndex.Read(reader),
            _ => new PlayUnknownMessage(frame.Type, frame.Payload),
        };
    }

    /// <summary>
    /// Writes the fields of the message.
    /// </summary>
    protected abstract void WritePayload(PlayWriter writer);
}

/// <summary>
/// A message of a type this side does not know (sent by a newer agent or subscriber of the same protocol version). It is
/// ignored.
/// </summary>
/// <param name="MessageType">The type in the frame's header.</param>
/// <param name="Payload">The payload, not decoded.</param>
public sealed record PlayUnknownMessage(PlayMessageType MessageType, ReadOnlyMemory<byte> Payload) : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => MessageType;

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer) => writer.WriteBytes(Payload.Span);
}

/// <summary>
/// An assembly loaded in the game, with the identifier of its build.
/// </summary>
/// <param name="Name">The simple name of the assembly.</param>
/// <param name="Version">The assembly version.</param>
/// <param name="Mvid">The module version identifier of the build: two builds of the same code differ.</param>
/// <param name="Shape">The shape of the data its types serialize (<see cref="PlayAssemblyShape"/>), the same for two builds of
/// the same code; <see cref="Guid.Empty"/> when not computed (Stride's own assemblies and those that do not use Stride).</param>
public sealed record PlayAssemblyInfo(string Name, string? Version, Guid Mvid, Guid Shape = default)
{
    internal const int MinimumLength = 34;

    internal static PlayAssemblyInfo Read(PlayReader reader) => new(reader.ReadString() ?? string.Empty, reader.ReadString(), reader.ReadGuid(), reader.ReadGuid());

    internal static void Write(PlayWriter writer, PlayAssemblyInfo assembly)
    {
        writer.Write(assembly.Name);
        writer.Write(assembly.Version);
        writer.Write(assembly.Mvid);
        writer.Write(assembly.Shape);
    }
}

/// <summary>
/// The game's first message to a subscriber.
/// </summary>
/// <param name="ProcessId">The game's process.</param>
/// <param name="StrideVersion">The informational version of the game's Stride engine.</param>
/// <param name="AgentVersion">The informational version of the agent's build, which must be the engine's.</param>
/// <param name="GameType">The type of the game (its <c>Game</c> class).</param>
/// <param name="Assemblies">The assemblies the game had loaded, apart from the .NET runtime's.</param>
public sealed record PlayHello(int ProcessId, string StrideVersion, string AgentVersion, string? GameType, IReadOnlyList<PlayAssemblyInfo> Assemblies) : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.Hello;

    internal static PlayHello Read(PlayReader reader)
        => new(reader.ReadInt32(), reader.ReadString() ?? string.Empty, reader.ReadString() ?? string.Empty, reader.ReadString(), reader.ReadList(PlayAssemblyInfo.Read, PlayAssemblyInfo.MinimumLength));

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer)
    {
        writer.Write(ProcessId);
        writer.Write(StrideVersion);
        writer.Write(AgentVersion);
        writer.Write(GameType);
        writer.WriteList(Assemblies, PlayAssemblyInfo.Write);
    }
}

/// <summary>
/// Assemblies the game loaded after its hello (game scripts are often loaded with the first scene).
/// </summary>
/// <param name="Assemblies">The new assemblies.</param>
public sealed record PlayAssemblies(IReadOnlyList<PlayAssemblyInfo> Assemblies) : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.Assemblies;

    internal static PlayAssemblies Read(PlayReader reader) => new(reader.ReadList(PlayAssemblyInfo.Read, PlayAssemblyInfo.MinimumLength));

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer) => writer.WriteList(Assemblies, PlayAssemblyInfo.Write);
}

/// <summary>
/// The levels of <see cref="PlayLog"/>, those of Stride's log.
/// </summary>
public enum PlayLogLevel : byte
{
    /// <summary>Debug.</summary>
    Debug = 0,

    /// <summary>Verbose.</summary>
    Verbose = 1,

    /// <summary>Info.</summary>
    Info = 2,

    /// <summary>Warning.</summary>
    Warning = 3,

    /// <summary>Error.</summary>
    Error = 4,

    /// <summary>Fatal.</summary>
    Fatal = 5,
}

/// <summary>
/// A message of the game's log.
/// </summary>
/// <param name="Frame">The game's frame when the message was logged (on any thread).</param>
/// <param name="Level">Its level.</param>
/// <param name="Module">The logger's module.</param>
/// <param name="Text">The message.</param>
/// <param name="Exception">The exception logged with it, as text.</param>
public sealed record PlayLog(long Frame, PlayLogLevel Level, string? Module, string Text, string? Exception) : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.Log;

    internal static PlayLog Read(PlayReader reader)
        => new(reader.ReadInt64(), (PlayLogLevel)reader.ReadByte(), reader.ReadString(), reader.ReadString() ?? string.Empty, reader.ReadString());

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer)
    {
        writer.Write(Frame);
        writer.Write((byte)Level);
        writer.Write(Module);
        writer.Write(Text);
        writer.Write(Exception);
    }
}

/// <summary>
/// The agent's costs in the game since the previous heartbeat.
/// </summary>
/// <param name="Frames">The frames in the period.</param>
/// <param name="AgentMeanMicroseconds">The agent's mean time per frame on the game thread (tree, polling, snapshots).</param>
/// <param name="AgentMaxMicroseconds">The agent's longest frame.</param>
/// <param name="Snapshots">The snapshots taken in the period.</param>
/// <param name="SnapshotMeanMicroseconds">The mean time of a snapshot (all its entities).</param>
/// <param name="SnapshotMaxMicroseconds">The longest snapshot.</param>
/// <param name="BytesSent">The bytes queued for the subscribers.</param>
/// <param name="DroppedMessages">The messages dropped because a subscriber's queue was full, since the start.</param>
public sealed record PlayAgentStats(int Frames, double AgentMeanMicroseconds, double AgentMaxMicroseconds, int Snapshots, double SnapshotMeanMicroseconds, double SnapshotMaxMicroseconds, long BytesSent, long DroppedMessages)
{
    internal static PlayAgentStats Read(PlayReader reader)
        => new(reader.ReadInt32(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadInt32(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadInt64(), reader.ReadInt64());

    internal void Write(PlayWriter writer)
    {
        writer.Write(Frames);
        writer.Write(AgentMeanMicroseconds);
        writer.Write(AgentMaxMicroseconds);
        writer.Write(Snapshots);
        writer.Write(SnapshotMeanMicroseconds);
        writer.Write(SnapshotMaxMicroseconds);
        writer.Write(BytesSent);
        writer.Write(DroppedMessages);
    }
}

/// <summary>
/// A sign of life from the game thread, about once a second. When the game thread hangs, the heartbeats stop.
/// </summary>
/// <param name="Frame">The game's frame.</param>
/// <param name="GameTime">The game's total time, in seconds.</param>
/// <param name="FramesPerSecond">The frame rate over the period.</param>
/// <param name="Stats">The agent's costs over the period.</param>
public sealed record PlayHeartbeat(long Frame, double GameTime, double FramesPerSecond, PlayAgentStats Stats) : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.Heartbeat;

    internal static PlayHeartbeat Read(PlayReader reader) => new(reader.ReadInt64(), reader.ReadDouble(), reader.ReadDouble(), PlayAgentStats.Read(reader));

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer)
    {
        writer.Write(Frame);
        writer.Write(GameTime);
        writer.Write(FramesPerSecond);
        Stats.Write(writer);
    }
}

/// <summary>
/// The game is ending: no message follows.
/// </summary>
/// <param name="Reason">Why, when known.</param>
public sealed record PlayGoodbye(string? Reason) : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.Goodbye;

    internal static PlayGoodbye Read(PlayReader reader) => new(reader.ReadString());

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer) => writer.Write(Reason);
}

/// <summary>
/// A subscriber's choice of entities to receive snapshots of, replacing its previous one.
/// </summary>
/// <param name="Handles">The agent's handles of the entities (see <see cref="PlayEntityInfo.Handle"/>); empty to stop.</param>
/// <param name="RateHz">How many snapshots a second, at most. The agent takes one at the end of a frame.</param>
public sealed record PlaySubscribe(IReadOnlyList<int> Handles, double RateHz) : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.Subscribe;

    internal static PlaySubscribe Read(PlayReader reader) => new(reader.ReadList(x => x.ReadInt32(), 4), reader.ReadDouble());

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer)
    {
        writer.WriteList(Handles, (w, x) => w.Write(x));
        writer.Write(RateHz);
    }
}

/// <summary>
/// A subscriber asks the game to exit, as closing its window does.
/// </summary>
public sealed record PlayQuit : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.Quit;

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer)
    {
    }
}
