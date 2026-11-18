// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Threading.Channels;
using Stride.Editor.PlayMode;
using Stride.PlayMode.Protocol;

namespace Stride.Editor.Tests.PlayMode;

/// <summary>
/// One end of an in-memory duplex stream: what one end writes, the other reads. Disposing an end ends the other's reads.
/// </summary>
internal sealed class MemoryDuplexStream : Stream
{
    private readonly Channel<byte[]> incoming;
    private readonly Channel<byte[]> outgoing;
    private ReadOnlyMemory<byte> current;

    private MemoryDuplexStream(Channel<byte[]> incoming, Channel<byte[]> outgoing)
    {
        this.incoming = incoming;
        this.outgoing = outgoing;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public static (Stream First, Stream Second) CreatePair()
    {
        var one = Channel.CreateUnbounded<byte[]>();
        var two = Channel.CreateUnbounded<byte[]>();
        return (new MemoryDuplexStream(one, two), new MemoryDuplexStream(two, one));
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (current.IsEmpty)
        {
            if (!await incoming.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                return 0;
            if (incoming.Reader.TryRead(out var bytes))
                current = bytes;
        }
        var count = Math.Min(buffer.Length, current.Length);
        current[..count].CopyTo(buffer);
        current = current[count..];
        return count;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!outgoing.Writer.TryWrite(buffer.ToArray()))
            throw new IOException("The other end is closed.");
        return ValueTask.CompletedTask;
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Write(byte[] buffer, int offset, int count) => WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        outgoing.Writer.TryComplete();
        incoming.Writer.TryComplete();
        base.Dispose(disposing);
    }
}

/// <summary>
/// A transport the test connects the agent to by hand.
/// </summary>
internal sealed class FakePlayTransport : IPlayTransport
{
    private readonly TaskCompletionSource<Stream> accepted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IReadOnlyDictionary<string, string> GameEnvironment { get; } = new Dictionary<string, string> { [PlayProtocol.PipeVariable] = "fake-pipe" };

    public bool IsDisposed { get; private set; }

    public Task<Stream> AcceptAsync(CancellationToken cancellationToken) => accepted.Task.WaitAsync(cancellationToken);

    /// <summary>
    /// Connects an agent: the session gets one end, the agent the other.
    /// </summary>
    public FakeAgent Connect()
    {
        var (editorEnd, agentEnd) = MemoryDuplexStream.CreatePair();
        accepted.SetResult(editorEnd);
        return new FakeAgent(agentEnd);
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// The game's end of the connection: sends what the agent sends, and keeps what it receives.
/// </summary>
internal sealed class FakeAgent : IAsyncDisposable
{
    private readonly PlayConnection connection;
    private readonly Channel<PlayMessage> received = Channel.CreateUnbounded<PlayMessage>();

    public FakeAgent(Stream stream)
    {
        connection = new PlayConnection(stream);
        connection.Start(x =>
        {
            received.Writer.TryWrite(x);
            Received?.Invoke(x);
        });
    }

    public event Action<PlayMessage>? Received;

    public void Send(PlayMessage message) => Assert.True(connection.Send(message));

    public async Task<T> ReceiveAsync<T>() where T : PlayMessage
    {
        while (true)
        {
            var message = await received.Reader.ReadAsync().AsTask().WaitAsync(PlaySessionTests.Timeout);
            // The session asks for the build's content when it connects; the tests that want it read it as any message
            if (message is PlayContentRequest && typeof(T) != typeof(PlayContentRequest))
                continue;
            return Assert.IsType<T>(message);
        }
    }

    public async Task CloseAsync()
    {
        await connection.FlushAndCloseAsync(PlaySessionTests.Timeout);
        await connection.DisposeAsync();
    }

    public ValueTask DisposeAsync() => connection.DisposeAsync();
}

/// <summary>
/// A game's process the test ends by hand.
/// </summary>
internal sealed class FakeGame : IPlayGameProcess
{
    private readonly TaskCompletionSource<int> exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public FakeGame(IReadOnlyDictionary<string, string> environment)
    {
        Environment = environment;
    }

    public IReadOnlyDictionary<string, string> Environment { get; }

    public int Id => 1234;

    public Task<int> Exited => exited.Task;

    public bool Killed { get; private set; }

    public void Exit(int exitCode) => exited.TrySetResult(exitCode);

    public void Kill()
    {
        Killed = true;
        Exit(-1);
    }
}
