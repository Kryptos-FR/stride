// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Buffers.Binary;
using Stride.PlayMode.Protocol;

namespace Stride.PlayMode.Tests;

/// <summary>
/// Tests the frames and messages of the play protocol (<see cref="PlayFraming"/>, <see cref="PlayMessage"/>).
/// </summary>
public sealed class PlayProtocolTests
{
    public static TheoryData<string> MessageNames => new(Messages().Keys);

    [Theory]
    [MemberData(nameof(MessageNames))]
    public async Task EveryMessageReadsBackAsWritten(string name)
    {
        var message = Messages()[name];
        var frame = message.Encode();

        var read = await RoundTripAsync(frame);

        Assert.Equal(message.GetType(), read.GetType());
        Assert.Equal(message.Type, read.Type);
        // Equal bytes once encoded again: every field read back
        Assert.Equal(frame, read.Encode());
    }

    [Fact]
    public async Task HelloKeepsItsFields()
    {
        var mvid = Guid.NewGuid();
        var shape = Guid.NewGuid();
        var hello = new PlayHello(42, "4.5.4-dev", "4.5.4-dev+abc", "MyGame.MyGame", [new PlayAssemblyInfo("MyGame", "1.0.0.0", mvid, shape), new PlayAssemblyInfo("Other", null, Guid.Empty)]);

        var read = Assert.IsType<PlayHello>(await RoundTripAsync(hello.Encode()));

        Assert.Equal(42, read.ProcessId);
        Assert.Equal("4.5.4-dev", read.StrideVersion);
        Assert.Equal("4.5.4-dev+abc", read.AgentVersion);
        Assert.Equal("MyGame.MyGame", read.GameType);
        Assert.Equal(2, read.Assemblies.Count);
        Assert.Equal(new PlayAssemblyInfo("MyGame", "1.0.0.0", mvid, shape), read.Assemblies[0]);
        Assert.Null(read.Assemblies[1].Version);
    }

    [Fact]
    public async Task SnapshotKeepsItsPayloadsAndErrors()
    {
        var id = Guid.NewGuid();
        var snapshot = new PlaySnapshot(7, 1.5, [
            new PlayEntitySnapshot(3, id, "Player", [
                new PlayComponentSnapshot(Guid.NewGuid(), "A.B, A", PlaySnapshotFormat.Binary, new byte[] { 1, 2, 3 }, null),
                new PlayComponentSnapshot(Guid.NewGuid(), "A.C, A", PlaySnapshotFormat.None, ReadOnlyMemory<byte>.Empty, "It failed."),
            ]),
        ]);

        var read = Assert.IsType<PlaySnapshot>(await RoundTripAsync(snapshot.Encode()));

        Assert.Equal(7, read.Frame);
        Assert.Equal(1.5, read.GameTime);
        var entity = Assert.Single(read.Entities);
        Assert.Equal((3, id, "Player"), (entity.Handle, entity.Id, entity.Name));
        Assert.Equal(new byte[] { 1, 2, 3 }, entity.Components[0].Payload.ToArray());
        Assert.Equal(PlaySnapshotFormat.None, entity.Components[1].Format);
        Assert.Equal("It failed.", entity.Components[1].Error);
    }

    [Fact]
    public async Task EncodedEntitiesMakeTheSameFrameAsTheMessage()
    {
        var entities = new[]
        {
            new PlayEntitySnapshot(1, Guid.NewGuid(), "A", [new PlayComponentSnapshot(Guid.NewGuid(), "T, A", PlaySnapshotFormat.Binary, new byte[] { 9 }, null)]),
            new PlayEntitySnapshot(2, Guid.NewGuid(), null, []),
        };

        // What the agent does to share an entity's bytes between subscribers
        var frame = PlaySnapshot.EncodeFrame(5, 2.0, entities.Select(PlaySnapshot.EncodeEntity).ToList());

        Assert.Equal(new PlaySnapshot(5, 2.0, entities).Encode(), frame);
        var read = Assert.IsType<PlaySnapshot>(await RoundTripAsync(frame));
        Assert.Equal(2, read.Entities.Count);
    }

    [Fact]
    public async Task SeveralFramesReadInOrderThenTheEnd()
    {
        using var stream = new MemoryStream();
        stream.Write(new PlayQuit().Encode());
        stream.Write(new PlayGoodbye("bye").Encode());
        stream.Position = 0;

        Assert.IsType<PlayQuit>(PlayMessage.Decode((await PlayFraming.ReadAsync(stream))!.Value));
        Assert.Equal("bye", Assert.IsType<PlayGoodbye>(PlayMessage.Decode((await PlayFraming.ReadAsync(stream))!.Value)).Reason);
        Assert.Null(await PlayFraming.ReadAsync(stream));
    }

    [Fact]
    public async Task AnotherVersionIsRefused()
    {
        var frame = new PlayQuit().Encode();
        frame[4] = PlayProtocol.Version + 1;

        var exception = await Assert.ThrowsAsync<PlayProtocolException>(() => RoundTripAsync(frame));

        Assert.Equal((byte)(PlayProtocol.Version + 1), exception.Version);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public async Task ATruncatedHeaderIsRefused(int length)
    {
        var frame = new PlayGoodbye("bye").Encode();

        var exception = await Assert.ThrowsAsync<PlayProtocolException>(() => RoundTripAsync(frame[..length]));

        Assert.Null(exception.Version);
    }

    [Fact]
    public async Task ATruncatedPayloadIsRefused()
    {
        var frame = new PlayGoodbye("a reason").Encode();

        await Assert.ThrowsAsync<PlayProtocolException>(() => RoundTripAsync(frame[..^2]));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(PlayProtocol.MaxFrameLength + 1)]
    [InlineData(-1)]
    public async Task ALengthOutOfBoundsIsRefused(int length)
    {
        var frame = new PlayQuit().Encode();
        BinaryPrimitives.WriteInt32LittleEndian(frame, length);

        await Assert.ThrowsAsync<PlayProtocolException>(() => RoundTripAsync(frame));
    }

    [Fact]
    public async Task APayloadShorterThanItsFieldsIsRefused()
    {
        // A count larger than the bytes left: refused before allocating
        var writer = new PlayWriter();
        writer.Write(int.MaxValue);
        var frame = PlayFraming.Encode(PlayMessageType.Subscribe, writer.WrittenSpan);

        await Assert.ThrowsAsync<PlayProtocolException>(() => RoundTripAsync(frame));
    }

    [Fact]
    public async Task AnUnknownTypeIsKeptAsIs()
    {
        var frame = PlayFraming.Encode((PlayMessageType)200, new byte[] { 1, 2 });

        var read = Assert.IsType<PlayUnknownMessage>(await RoundTripAsync(frame));

        Assert.Equal((PlayMessageType)200, read.Type);
        Assert.Equal(new byte[] { 1, 2 }, read.Payload.ToArray());
    }

    private static async Task<PlayMessage> RoundTripAsync(byte[] frame)
    {
        using var stream = new MemoryStream(frame);
        var read = await PlayFraming.ReadAsync(stream);
        Assert.NotNull(read);
        return PlayMessage.Decode(read.Value);
    }

    private static Dictionary<string, PlayMessage> Messages()
    {
        var components = new[] { new PlayComponentInfo(Guid.NewGuid(), "Stride.Engine.TransformComponent, Stride.Engine") };
        var entity = new PlayEntityInfo(4, Guid.NewGuid(), "Entity", true, 3, 1, components);
        var scene = new PlaySceneInfo(1, Guid.NewGuid(), "Main", "MainScene", 0);
        return new()
        {
            ["Hello"] = new PlayHello(1, "4.5.4", "4.5.4", null, []),
            ["Assemblies"] = new PlayAssemblies([new PlayAssemblyInfo("A", "1.0", Guid.NewGuid())]),
            ["Tree"] = new PlayTree(10, [scene, scene with { Handle = 2, ParentHandle = 1, Url = null }], [entity, entity with { Handle = 5, Name = null, Enabled = false }]),
            ["TreeBatch"] = new PlayTreeBatch(11, [
                new PlaySceneAdded(scene),
                new PlayEntityAdded(entity),
                new PlayEntityMoved(4, 0, 1),
                new PlayEntityComponentsChanged(4, components),
                new PlayEntityStateChanged(4, "Renamed", false),
                new PlayEntityRemoved(4),
                new PlaySceneRemoved(1),
            ]),
            ["Snapshot"] = new PlaySnapshot(12, 0.25, [new PlayEntitySnapshot(4, Guid.NewGuid(), "E", [new PlayComponentSnapshot(Guid.NewGuid(), "T, A", PlaySnapshotFormat.Binary, new byte[] { 1, 2, 3, 4 }, null)])]),
            ["Log"] = new PlayLog(13, PlayLogLevel.Warning, "Module", "Text", "System.Exception: boom"),
            ["Heartbeat"] = new PlayHeartbeat(14, 3.5, 60, new PlayAgentStats(60, 12.5, 40, 5, 200, 300, 1024, 2)),
            ["Goodbye"] = new PlayGoodbye(null),
            ["Subscribe"] = new PlaySubscribe([1, 2, 3], 5),
            ["Quit"] = new PlayQuit(),
        };
    }
}
