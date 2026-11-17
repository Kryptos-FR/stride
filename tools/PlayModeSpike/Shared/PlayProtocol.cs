// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Stride.PlayMode.Spike;

/// <summary>
/// The kinds of message that travel over the play pipe.
/// </summary>
public enum PlayMessageType : byte
{
    // Game to editor
    Hello = 1,
    Tree = 2,
    TreeBatch = 3,
    Snapshot = 4,
    Stats = 5,
    Log = 6,

    // Editor to game
    Select = 20,
    ResetStats = 21,
    RequestStats = 22,
    Quit = 23,
}

/// <summary>
/// The snapshot formats the agent can write.
/// </summary>
public enum SnapshotFormat : byte
{
    Binary = 1,
    Yaml = 2,
}

/// <summary>
/// Frames messages on a stream: a 4-byte little-endian length, the type byte, then the payload.
/// </summary>
public static class PlayFraming
{
    /// <summary>The environment variable that carries the pipe name to the game.</summary>
    public const string PipeVariable = "STRIDE_PLAY_PIPE";

    /// <summary>The environment variable that selects a mode of the agent (empty: connect; "meter": frame times only).</summary>
    public const string ModeVariable = "STRIDE_PLAY_AGENT_MODE";

    /// <summary>The environment variable that gives the file the meter mode writes its frame times to.</summary>
    public const string MeterOutputVariable = "STRIDE_PLAY_METER_OUTPUT";

    /// <summary>Writes one message.</summary>
    public static void Write(Stream stream, PlayMessageType type, ReadOnlySpan<byte> payload)
    {
        Span<byte> header = stackalloc byte[5];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length + 1);
        header[4] = (byte)type;
        stream.Write(header);
        stream.Write(payload);
    }

    /// <summary>Reads one message, or returns null at the end of the stream.</summary>
    public static async Task<(PlayMessageType Type, byte[] Payload)?> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[5];
        if (!await ReadExactlyOrEndAsync(stream, header, cancellationToken))
            return null;
        var length = BinaryPrimitives.ReadInt32LittleEndian(header) - 1;
        var payload = new byte[length];
        if (length > 0 && !await ReadExactlyOrEndAsync(stream, payload, cancellationToken))
            return null;
        return ((PlayMessageType)header[4], payload);
    }

    private static async Task<bool> ReadExactlyOrEndAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken);
            if (count == 0)
                return false;
            read += count;
        }
        return true;
    }
}
