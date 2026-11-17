// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Buffers.Binary;
using System.Text;

namespace Stride.PlayMode.Protocol;

/// <summary>
/// Writes the fields of a message: little-endian numbers, length-prefixed UTF-8 strings that can be null, and byte blocks.
/// </summary>
public sealed class PlayWriter
{
    private readonly MemoryStream stream;
    private readonly BinaryWriter writer;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayWriter"/> class over a new buffer.
    /// </summary>
    /// <param name="capacity">The initial capacity of the buffer.</param>
    public PlayWriter(int capacity = 256)
    {
        stream = new MemoryStream(capacity);
        writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
    }

    /// <summary>
    /// The number of bytes written.
    /// </summary>
    public int Length => (int)stream.Length;

    /// <summary>
    /// The bytes written so far.
    /// </summary>
    public ReadOnlySpan<byte> WrittenSpan
    {
        get
        {
            writer.Flush();
            return stream.GetBuffer().AsSpan(0, (int)stream.Length);
        }
    }

    /// <summary>
    /// Forgets what was written, to write another message in the same buffer.
    /// </summary>
    public void Reset()
    {
        writer.Flush();
        stream.SetLength(0);
    }

    /// <summary>Writes a byte.</summary>
    public void Write(byte value) => writer.Write(value);

    /// <summary>Writes a boolean as one byte.</summary>
    public void Write(bool value) => writer.Write(value);

    /// <summary>Writes a 32-bit integer.</summary>
    public void Write(int value) => writer.Write(value);

    /// <summary>Writes a 64-bit integer.</summary>
    public void Write(long value) => writer.Write(value);

    /// <summary>Writes a double.</summary>
    public void Write(double value) => writer.Write(value);

    /// <summary>Writes a GUID as its 16 bytes.</summary>
    public void Write(Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        value.TryWriteBytes(bytes);
        writer.Write(bytes);
    }

    /// <summary>Writes a string that can be null.</summary>
    public void Write(string? value)
    {
        writer.Write(value is not null);
        if (value is not null)
            writer.Write(value);
    }

    /// <summary>Writes a block of bytes, prefixed by its length.</summary>
    public void WriteBytes(ReadOnlySpan<byte> value)
    {
        writer.Write(value.Length);
        writer.Write(value);
    }

    /// <summary>Writes bytes as they are, with no length (bytes another writer already laid out).</summary>
    public void WriteRaw(ReadOnlySpan<byte> value) => writer.Write(value);

    /// <summary>Writes a count followed by each item.</summary>
    public void WriteList<T>(IReadOnlyCollection<T> items, Action<PlayWriter, T> write)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(write);
        writer.Write(items.Count);
        foreach (var item in items)
            write(this, item);
    }
}

/// <summary>
/// Reads the fields <see cref="PlayWriter"/> writes.
/// </summary>
/// <remarks>
/// A read past the end, a negative count or a count larger than what is left throws <see cref="PlayProtocolException"/>.
/// </remarks>
public sealed class PlayReader
{
    private readonly ReadOnlyMemory<byte> buffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayReader"/> class.
    /// </summary>
    /// <param name="buffer">The encoded message.</param>
    public PlayReader(ReadOnlyMemory<byte> buffer)
    {
        this.buffer = buffer;
    }

    /// <summary>
    /// The position of the next read.
    /// </summary>
    public int Position { get; private set; }

    /// <summary>
    /// The number of bytes not read yet.
    /// </summary>
    public int Remaining => buffer.Length - Position;

    /// <summary>Reads a byte.</summary>
    public byte ReadByte() => Take(1)[0];

    /// <summary>Reads a boolean.</summary>
    public bool ReadBoolean() => ReadByte() != 0;

    /// <summary>Reads a 32-bit integer.</summary>
    public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));

    /// <summary>Reads a 64-bit integer.</summary>
    public long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(Take(8));

    /// <summary>Reads a double.</summary>
    public double ReadDouble() => BinaryPrimitives.ReadDoubleLittleEndian(Take(8));

    /// <summary>Reads a GUID.</summary>
    public Guid ReadGuid() => new(Take(16));

    /// <summary>Reads a string that can be null.</summary>
    public string? ReadString()
    {
        if (!ReadBoolean())
            return null;
        // The length prefix of BinaryWriter: 7 bits per byte
        var length = 0;
        var shift = 0;
        byte part;
        do
        {
            if (shift > 28)
                throw new PlayProtocolException("A string has a malformed length.");
            part = ReadByte();
            length |= (part & 0x7F) << shift;
            shift += 7;
        }
        while ((part & 0x80) != 0);
        return Encoding.UTF8.GetString(Take(length));
    }

    /// <summary>Reads a block of bytes written by <see cref="PlayWriter.WriteBytes"/>, without copying it.</summary>
    public ReadOnlyMemory<byte> ReadBytes()
    {
        var length = ReadCount(1);
        var bytes = buffer.Slice(Position, length);
        Position += length;
        return bytes;
    }

    /// <summary>Reads a list written by <see cref="PlayWriter.WriteList{T}"/>.</summary>
    /// <param name="read">Reads one item.</param>
    /// <param name="minimumItemLength">The fewest bytes an item takes, to refuse a count that cannot fit.</param>
    public IReadOnlyList<T> ReadList<T>(Func<PlayReader, T> read, int minimumItemLength = 1)
    {
        ArgumentNullException.ThrowIfNull(read);
        var count = ReadCount(minimumItemLength);
        var items = new T[count];
        for (var i = 0; i < count; i++)
            items[i] = read(this);
        return items;
    }

    private int ReadCount(int minimumItemLength)
    {
        var count = ReadInt32();
        if (count < 0 || (long)count * Math.Max(minimumItemLength, 0) > Remaining)
            throw new PlayProtocolException($"A count of {count} does not fit in the {Remaining} bytes left of the message.");
        return count;
    }

    private ReadOnlySpan<byte> Take(int length)
    {
        if (length < 0 || length > Remaining)
            throw new PlayProtocolException($"A message ends {length - Remaining} bytes too early.");
        var span = buffer.Span.Slice(Position, length);
        Position += length;
        return span;
    }
}
