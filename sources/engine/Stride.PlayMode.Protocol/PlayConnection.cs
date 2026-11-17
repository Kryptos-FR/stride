// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Threading.Channels;

namespace Stride.PlayMode.Protocol;

/// <summary>
/// One end of a play connection over a stream (a named pipe, a socket): it sends frames from a bounded queue on a task of its
/// own, so that the sender never blocks, and reads frames on another task.
/// </summary>
/// <remarks>
/// <see cref="Send"/> and <see cref="SendFrame"/> can be called from any thread. The callback given to <see cref="Start"/>
/// runs on the reading task, one message at a time. <see cref="DisposeAsync"/> ends both tasks and waits for them.
/// </remarks>
public sealed class PlayConnection : IAsyncDisposable
{
    private readonly Stream stream;
    private readonly Channel<byte[]> outgoing;
    private readonly CancellationTokenSource stopping = new();
    private Task? readTask;
    private Task? writeTask;
    private long droppedFrames;
    private long bytesQueued;
    private int disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayConnection"/> class.
    /// </summary>
    /// <param name="stream">A connected stream that can read and write. The connection owns it.</param>
    /// <param name="queueCapacity">How many frames can wait to be sent before <see cref="SendFrame"/> drops them.</param>
    public PlayConnection(Stream stream, int queueCapacity = 1024)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfLessThan(queueCapacity, 1);
        this.stream = stream;
        outgoing = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(queueCapacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
    }

    /// <summary>
    /// Completes when the reading ends: normally when the other end closes the stream or this connection is disposed, with
    /// the error otherwise (a <see cref="PlayProtocolException"/> for a broken or foreign stream).
    /// </summary>
    public Task Completion => readTask ?? throw new InvalidOperationException("The connection is not started.");

    /// <summary>
    /// The frames dropped because the queue was full.
    /// </summary>
    public long DroppedFrames => Interlocked.Read(ref droppedFrames);

    /// <summary>
    /// The bytes queued to be sent since the start.
    /// </summary>
    public long BytesQueued => Interlocked.Read(ref bytesQueued);

    /// <summary>
    /// Starts reading and sending.
    /// </summary>
    /// <param name="received">Called on the reading task for each message read.</param>
    public void Start(Action<PlayMessage> received)
    {
        ArgumentNullException.ThrowIfNull(received);
        if (readTask is not null)
            throw new InvalidOperationException("The connection is already started.");
        writeTask = Task.Run(WriteLoopAsync);
        readTask = Task.Run(() => ReadLoopAsync(received));
    }

    /// <summary>
    /// Queues a message to be sent.
    /// </summary>
    /// <returns><c>false</c> when the queue is full or the connection is closed: the message is dropped.</returns>
    public bool Send(PlayMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return SendFrame(message.Encode());
    }

    /// <summary>
    /// Queues a frame encoded with <see cref="PlayMessage.Encode"/> or <see cref="PlayFraming.Encode"/>; the same array can
    /// be queued on several connections.
    /// </summary>
    /// <returns><c>false</c> when the queue is full or the connection is closed: the frame is dropped.</returns>
    public bool SendFrame(byte[] frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (!outgoing.Writer.TryWrite(frame))
        {
            Interlocked.Increment(ref droppedFrames);
            return false;
        }
        Interlocked.Add(ref bytesQueued, frame.Length);
        return true;
    }

    /// <summary>
    /// Stops queuing and waits for the queued frames to be sent, at most for the given time.
    /// </summary>
    /// <returns><c>true</c> when everything queued was sent.</returns>
    public async Task<bool> FlushAndCloseAsync(TimeSpan timeout)
    {
        outgoing.Writer.TryComplete();
        if (writeTask is null)
            return true;
        var finished = await Task.WhenAny(writeTask, Task.Delay(timeout)).ConfigureAwait(false);
        return finished == writeTask && writeTask.IsCompletedSuccessfully;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        outgoing.Writer.TryComplete();
        await stopping.CancelAsync().ConfigureAwait(false);
        await stream.DisposeAsync().ConfigureAwait(false);
        foreach (var task in new[] { readTask, writeTask })
        {
            if (task is null)
                continue;
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Ending the connection ends the tasks in whatever state they were; Completion keeps the error.
            }
        }
        stopping.Dispose();
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            await foreach (var frame in outgoing.Reader.ReadAllAsync(stopping.Token).ConfigureAwait(false))
            {
                await stream.WriteAsync(frame, stopping.Token).ConfigureAwait(false);
                await stream.FlushAsync(stopping.Token).ConfigureAwait(false);
            }
        }
        catch (Exception e) when (e is OperationCanceledException or IOException or ObjectDisposedException)
        {
            // The connection is closing, or the other end went away: what is queued is dropped.
        }
        finally
        {
            outgoing.Writer.TryComplete();
        }
    }

    private async Task ReadLoopAsync(Action<PlayMessage> received)
    {
        try
        {
            while (await PlayFraming.ReadAsync(stream, stopping.Token).ConfigureAwait(false) is { } frame)
                received(PlayMessage.Decode(frame));
        }
        catch (Exception e) when (stopping.IsCancellationRequested && e is OperationCanceledException or IOException or ObjectDisposedException)
        {
            // Disposed while reading: a normal end.
        }
        catch (IOException)
        {
            // The other end closed a pipe while it was being read (a broken pipe): a normal end too.
        }
        finally
        {
            outgoing.Writer.TryComplete();
        }
    }
}
