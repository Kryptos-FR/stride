// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.IO.Pipes;
using System.Threading.Channels;
using Stride.PlayMode.Protocol;

namespace Stride.PlayMode.Tests;

/// <summary>
/// Tests <see cref="PlayConnection"/> over a named pipe, as the editor and the agent use it.
/// </summary>
public sealed class PlayConnectionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task MessagesGoBothWays()
    {
        var (server, client) = await ConnectAsync();
        await using var editor = new PlayConnection(server);
        await using var agent = new PlayConnection(client);
        var toEditor = Channel.CreateUnbounded<PlayMessage>();
        var toAgent = Channel.CreateUnbounded<PlayMessage>();
        editor.Start(x => toEditor.Writer.TryWrite(x));
        agent.Start(x => toAgent.Writer.TryWrite(x));

        Assert.True(agent.Send(new PlayGoodbye("one")));
        Assert.True(agent.SendFrame(new PlayGoodbye("two").Encode()));
        Assert.True(editor.Send(new PlaySubscribe([4], 5)));

        Assert.Equal("one", Assert.IsType<PlayGoodbye>(await toEditor.Reader.ReadAsync().AsTask().WaitAsync(Timeout)).Reason);
        Assert.Equal("two", Assert.IsType<PlayGoodbye>(await toEditor.Reader.ReadAsync().AsTask().WaitAsync(Timeout)).Reason);
        Assert.Equal([4], Assert.IsType<PlaySubscribe>(await toAgent.Reader.ReadAsync().AsTask().WaitAsync(Timeout)).Handles);
        Assert.True(agent.BytesQueued > 0);
    }

    [Fact]
    public async Task ClosingOneEndCompletesTheOther()
    {
        var (server, client) = await ConnectAsync();
        await using var editor = new PlayConnection(server);
        var agent = new PlayConnection(client);
        editor.Start(_ => { });
        agent.Start(_ => { });

        Assert.True(agent.Send(new PlayGoodbye(null)));
        Assert.True(await agent.FlushAndCloseAsync(Timeout));
        await agent.DisposeAsync();

        // A clean end: no error
        await editor.Completion.WaitAsync(Timeout);
        Assert.False(agent.Send(new PlayQuit()));
    }

    [Fact]
    public async Task AForeignStreamFailsTheCompletion()
    {
        var (server, client) = await ConnectAsync();
        await using var editor = new PlayConnection(server);
        editor.Start(_ => { });
        try
        {
            await client.WriteAsync("HTTP/1.1 200 OK\r\n\r\n"u8.ToArray());
            await client.FlushAsync();

            await Assert.ThrowsAsync<PlayProtocolException>(() => editor.Completion.WaitAsync(Timeout));
        }
        finally
        {
            await client.DisposeAsync();
        }
    }

    [Fact]
    public async Task AFullQueueDropsFrames()
    {
        // Nobody reads the other end: the queue fills up
        var (server, client) = await ConnectAsync();
        await using var agent = new PlayConnection(client, queueCapacity: 2);
        try
        {
            var big = PlayFraming.Encode(PlayMessageType.Log, new byte[1024 * 1024]);
            var sent = 0;
            for (var i = 0; i < 50; i++)
            {
                if (agent.SendFrame(big))
                    sent++;
            }

            Assert.True(sent < 50);
            Assert.Equal(50 - sent, agent.DroppedFrames);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    private static async Task<(NamedPipeServerStream Server, NamedPipeClientStream Client)> ConnectAsync()
    {
        var name = $"stride-play-test-{Guid.NewGuid():N}";
        var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await Task.WhenAll(server.WaitForConnectionAsync(), client.ConnectAsync()).WaitAsync(Timeout);
        return (server, client);
    }
}
