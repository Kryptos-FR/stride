// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Buffers.Binary;

namespace Stride.PlayMode.Protocol;

/// <summary>
/// One message on the wire: its type and its payload, as <see cref="PlayFraming"/> reads and writes them.
/// </summary>
/// <param name="Type">The type of the message.</param>
/// <param name="Payload">The encoded message, which <see cref="PlayMessage.Decode"/> reads.</param>
public readonly record struct PlayFrame(PlayMessageType Type, ReadOnlyMemory<byte> Payload);

/// <summary>
/// An error in a play stream: a truncated frame, a frame too long, or a frame of another version of the protocol.
/// </summary>
public sealed class PlayProtocolException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlayProtocolException"/> class.
    /// </summary>
    /// <param name="message">What is wrong with the stream.</param>
    /// <param name="version">The version a frame carried, when it is not <see cref="PlayProtocol.Version"/>.</param>
    public PlayProtocolException(string message, byte? version = null)
        : base(message)
    {
        Version = version;
    }

    /// <summary>
    /// The version of the protocol the other end speaks, when the error is a version mismatch.
    /// </summary>
    public byte? Version { get; }
}

/// <summary>
/// Frames messages on a stream: the length (4 bytes, little-endian, counting what follows it), the protocol version, the
/// message type, then the payload.
/// </summary>
public static class PlayFraming
{
    /// <summary>
    /// Writes a frame into a new array, ready to be sent on any stream.
    /// </summary>
    /// <param name="type">The type of the message.</param>
    /// <param name="payload">The encoded message.</param>
    /// <returns>The header followed by the payload.</returns>
    public static byte[] Encode(PlayMessageType type, ReadOnlySpan<byte> payload)
    {
        var bytes = new byte[PlayProtocol.HeaderLength + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, payload.Length + 2);
        bytes[4] = PlayProtocol.Version;
        bytes[5] = (byte)type;
        payload.CopyTo(bytes.AsSpan(PlayProtocol.HeaderLength));
        return bytes;
    }

    /// <summary>
    /// Reads the next frame.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The frame, or <c>null</c> when the stream ends between two frames.</returns>
    /// <exception cref="PlayProtocolException">The stream ends inside a frame, a length is out of bounds, or a frame has
    /// another version of the protocol.</exception>
    public static async ValueTask<PlayFrame?> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var header = new byte[PlayProtocol.HeaderLength];
        var read = await ReadAtMostAsync(stream, header, cancellationToken).ConfigureAwait(false);
        if (read == 0)
            return null;
        if (read < header.Length)
            throw new PlayProtocolException($"The stream ended inside the header of a frame ({read} of {header.Length} bytes).");

        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length < 2 || length > PlayProtocol.MaxFrameLength)
            throw new PlayProtocolException($"A frame announces {length} bytes, outside of 2 to {PlayProtocol.MaxFrameLength}.");
        if (header[4] != PlayProtocol.Version)
            throw new PlayProtocolException($"A frame has version {header[4]} of the protocol; this side speaks version {PlayProtocol.Version}.", header[4]);

        var payload = new byte[length - 2];
        read = await ReadAtMostAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        if (read < payload.Length)
            throw new PlayProtocolException($"The stream ended inside a frame ({read} of {payload.Length} bytes of payload).");
        return new PlayFrame((PlayMessageType)header[5], payload);
    }

    private static async ValueTask<int> ReadAtMostAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken).ConfigureAwait(false);
            if (count == 0)
                break;
            read += count;
        }
        return read;
    }
}
