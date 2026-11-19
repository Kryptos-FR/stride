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
    public async Task TheGameStateAndTheControlsKeepTheirFields()
    {
        var state = Assert.IsType<PlayGameState>(await RoundTripAsync(new PlayGameState(120, 80, true, 0.25, 0.5, 29.5, 304, 1).Encode()));
        Assert.Equal(new PlayGameState(120, 80, true, 0.25, 0.5, 29.5, 304, 1), state);

        Assert.Equal(4, Assert.IsType<PlayStep>(await RoundTripAsync(new PlayStep(4).Encode())).Frames);
        Assert.Equal(1, new PlayStep().Frames);
        Assert.Equal(0.1, Assert.IsType<PlayTimeScale>(await RoundTripAsync(new PlayTimeScale(0.1).Encode())).Scale);
        Assert.IsType<PlayPause>(await RoundTripAsync(new PlayPause().Encode()));
        Assert.IsType<PlayResume>(await RoundTripAsync(new PlayResume().Encode()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheWindowMessagesKeepTheirValue(bool value)
    {
        Assert.Equal(new PlayWindowOnTop(value), await RoundTripAsync(new PlayWindowOnTop(value).Encode()));
        Assert.Equal(new PlayEditorActive(value), await RoundTripAsync(new PlayEditorActive(value).Encode()));
        Assert.Equal(PlayMessageType.WindowOnTop, new PlayWindowOnTop(value).Type);
        Assert.Equal(PlayMessageType.EditorActive, new PlayEditorActive(value).Type);
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
    public async Task TheStateSaysWhyTheGameIsPausedAndAStateWithoutItReadsAsPlaying()
    {
        var state = new PlayGameState(15, 9, true, 1, 1, 60, 304, 0, PlayPauseReason.ScriptException);

        Assert.Equal(state, await RoundTripAsync(state.Encode()));

        // A state written before the reason: its payload ends after the steps
        var writer = new PlayWriter();
        writer.Write(15L);
        writer.Write(9L);
        writer.Write(true);
        writer.Write(1.0);
        writer.Write(1.0);
        writer.Write(60.0);
        writer.Write(304);
        writer.Write(0);
        var old = Assert.IsType<PlayGameState>(await RoundTripAsync(PlayFraming.Encode(PlayMessageType.State, writer.WrittenSpan)));
        Assert.True(old.Paused);
        Assert.Equal(PlayPauseReason.None, old.PauseReason);
    }

    [Fact]
    public async Task AScriptExceptionKeepsItsFields()
    {
        var id = Guid.NewGuid();
        var exception = new PlayScriptException(16, PlayScriptExceptionKind.Update, "MyGame.Player.PlayerController", 7, id, "Player", "System.InvalidOperationException", "Boom.", "details", true);

        var read = Assert.IsType<PlayScriptException>(await RoundTripAsync(exception.Encode()));

        Assert.Equal(exception, read);
        Assert.Equal("PlayerController", read.ScriptName);
        Assert.Null((exception with { ScriptType = null }).ScriptName);
        Assert.Equal("Script", (exception with { ScriptType = "Script" }).ScriptName);
    }

    [Theory]
    [InlineData("F7", "F7", PlayHotkeyModifiers.None)]
    [InlineData("shift+f7", "f7", PlayHotkeyModifiers.Shift)]
    [InlineData(" Ctrl + Alt + Pause ", "Pause", PlayHotkeyModifiers.Ctrl | PlayHotkeyModifiers.Alt)]
    [InlineData("Control+Shift+P", "P", PlayHotkeyModifiers.Ctrl | PlayHotkeyModifiers.Shift)]
    public void AHotkeyReadsItsKeyAndModifiers(string text, string key, PlayHotkeyModifiers modifiers)
    {
        Assert.True(PlayHotkey.TryParse(text, out var hotkey));

        Assert.Equal(new PlayHotkey(key, modifiers), hotkey);
        Assert.True(PlayHotkey.TryParse(hotkey.ToString(), out var again));
        Assert.Equal(hotkey, again);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("None")]
    [InlineData("none")]
    [InlineData("Shift+")]
    [InlineData("Win+F7")]
    public void NoKeyOrAnUnknownModifierIsNoHotkey(string? text) => Assert.False(PlayHotkey.TryParse(text, out _));

    [Fact]
    public void TheLaunchOptionsGoThroughTheGameVariables()
    {
        var options = new PlayLaunchOptions(StartPaused: true, PauseOnScriptException: true, Hotkey: new PlayHotkey("F7", PlayHotkeyModifiers.Shift), Scene: "/MyGame/Scenes/Level2", WindowOnTop: true);

        var variables = options.ToVariables();

        Assert.Equal("1", variables[PlayProtocol.StartPausedVariable]);
        Assert.Equal("1", variables[PlayProtocol.PauseOnScriptExceptionVariable]);
        Assert.Equal("Shift+F7", variables[PlayProtocol.HotkeyVariable]);
        Assert.Equal("/MyGame/Scenes/Level2", variables[PlayProtocol.SceneVariable]);
        Assert.Equal("1", variables[PlayProtocol.WindowOnTopVariable]);
        Assert.All(variables.Keys, x => Assert.StartsWith(PlayProtocol.VariablePrefix, x));
        Assert.All(variables.Keys, x => Assert.Contains(x, PlayProtocol.GameVariables));
        Assert.Equal(options, PlayLaunchOptions.FromVariables(variables));
    }

    [Fact]
    public void NoVariableIsNoOption()
    {
        Assert.Empty(PlayLaunchOptions.None.ToVariables());
        Assert.Equal(PlayLaunchOptions.None, PlayLaunchOptions.FromVariables(new Dictionary<string, string>()));
        Assert.Equal(PlayLaunchOptions.None, PlayLaunchOptions.FromVariables(new Dictionary<string, string>
        {
            [PlayProtocol.StartPausedVariable] = "0",
            [PlayProtocol.HotkeyVariable] = "None",
            [PlayProtocol.SceneVariable] = " ",
        }));
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

    [Fact]
    public async Task AnEditKeepsItsPathValueAndTarget()
    {
        var component = Guid.NewGuid();
        var edit = new PlayEdit(3, 9, component, [PlayPathSegment.Member("Range"), PlayPathSegment.Member("Span"), PlayPathSegment.Member("X")], PlayEditOperation.Set, PlayEditValue.Of(new byte[] { 1, 2, 3 }));
        var move = new PlayEdit(4, 9, component, [PlayPathSegment.Member("Values"), PlayPathSegment.Item(2)], PlayEditOperation.Move, PlayEditValue.Null, PlayPathSegment.Item(0));

        var read = Assert.IsType<PlayEdit>(await RoundTripAsync(edit.Encode()));
        var readMove = Assert.IsType<PlayEdit>(await RoundTripAsync(move.Encode()));

        Assert.Equal(edit.Encode(), read.Encode());
        Assert.Equal(edit.Path, read.Path);
        Assert.Equal("Range.Span.X", read.PathText);
        Assert.Equal(component, read.ComponentId);
        Assert.Equal([1, 2, 3], read.Value.Payload.ToArray());
        Assert.Equal(move.Path, readMove.Path);
        Assert.Equal(move.Target, readMove.Target);
        Assert.Equal("Values[2]", readMove.PathText);
        Assert.Equal(0, readMove.Target!.Index);
        Assert.Equal("Table[Fast]", PlayPathSegment.Format([PlayPathSegment.Member("Table"), PlayPathSegment.ForKey(new byte[] { 1 }, "Fast")]));
    }

    [Fact]
    public async Task AnEditResultKeepsItsStatusReadBackAndError()
    {
        var result = new PlayEditResult(5, 200, PlayEditStatus.Failed, PlayEditValue.Of(new byte[] { 9 }), "Boom.");

        var read = Assert.IsType<PlayEditResult>(await RoundTripAsync(result.Encode()));

        Assert.Equal(result, read);
        Assert.Equal(PlayEditValueKind.Value, read.ReadBack.Kind);
        Assert.Equal("Boom.", read.Error);
    }

    [Fact]
    public async Task TheContentIndexKeepsItsUrlsAndTheirOrder()
    {
        var index = new PlayContentIndex(["Materials/Ground", "Models/Box", "Prefabs/Crate", "Scenes/Level 2"]);

        var read = Assert.IsType<PlayContentIndex>(await RoundTripAsync(index.Encode()));

        Assert.Equal(index.Urls, read.Urls);
        Assert.Equal(index, read);
        Assert.IsType<PlayContentRequest>(await RoundTripAsync(new PlayContentRequest().Encode()));
        Assert.Empty(Assert.IsType<PlayContentIndex>(await RoundTripAsync(new PlayContentIndex([]).Encode())).Urls);
    }

    private static async Task<PlayMessage> RoundTripAsync(byte[] frame)
    {
        using var stream = new MemoryStream(frame);
        var read = await PlayFraming.ReadAsync(stream);
        Assert.NotNull(read);
        return PlayMessage.Decode(read.Value);
    }

    [Fact]
    public async Task AStructureCommandKeepsItsFields()
    {
        var component = Guid.NewGuid();
        var edit = new PlayStructureEdit(7, PlayStructureOperation.MoveEntity, 3, component, 2, 5, 1, "Game.Type, Game", "Prefabs/Crate", "Name", true);

        var read = Assert.IsType<PlayStructureEdit>(await RoundTripAsync(edit.Encode()));
        var result = Assert.IsType<PlayStructureResult>(await RoundTripAsync(new PlayStructureResult(7, 99, PlayEditStatus.Applied, [4, 8], component, null).Encode()));

        Assert.Equal(edit, read);
        Assert.Equal(new PlayStructureResult(7, 99, PlayEditStatus.Applied, [4, 8], component, null), result);
        Assert.True((int)PlayMessageType.StructureEdit >= 32);
        Assert.True((int)PlayMessageType.StructureResult < 32);
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
            ["State"] = new PlayGameState(15, 9, true, 0.25, 0.5, 58.5, 304, 2, PlayPauseReason.Hotkey),
            ["ScriptException"] = new PlayScriptException(16, PlayScriptExceptionKind.Async, "MyGame.Player.PlayerController", 7, Guid.NewGuid(), "Player", "System.InvalidOperationException", "Boom.", "System.InvalidOperationException: Boom.\n   at ...", true),
            ["Pause"] = new PlayPause(),
            ["Resume"] = new PlayResume(),
            ["Step"] = new PlayStep(3),
            ["TimeScale"] = new PlayTimeScale(0.25),
            ["Edit"] = new PlayEdit(17, 4, Guid.NewGuid(), [PlayPathSegment.Member("Offset"), PlayPathSegment.Member("X")], PlayEditOperation.Set, PlayEditValue.Of(new byte[] { 5, 6 })),
            ["EditMove"] = new PlayEdit(18, 4, Guid.NewGuid(), [PlayPathSegment.Member("Table"), PlayPathSegment.ForKey(new byte[] { 1 }, "Fast")], PlayEditOperation.Move, PlayEditValue.Null, PlayPathSegment.ForKey(new byte[] { 2 }, "Slow")),
            ["EditNewInstance"] = new PlayEdit(19, 4, Guid.NewGuid(), [PlayPathSegment.Member("Shapes"), PlayPathSegment.Item(1)], PlayEditOperation.Insert, PlayEditValue.NewInstanceOf("Game.Box, Game")),
            ["EditResult"] = new PlayEditResult(17, 120, PlayEditStatus.Applied, PlayEditValue.Of(new byte[] { 7 }), null),
            ["EditResultFailed"] = new PlayEditResult(18, 121, PlayEditStatus.Failed, PlayEditValue.Null, "The setter threw."),
            ["WindowOnTop"] = new PlayWindowOnTop(true),
            ["EditorActive"] = new PlayEditorActive(true),
            ["ContentRequest"] = new PlayContentRequest(),
            ["ContentIndex"] = new PlayContentIndex(["MainScene", "Models/Box", "Materials/Ground"]),
            ["StructureAddComponent"] = new PlayStructureEdit(20, PlayStructureOperation.AddComponent, EntityHandle: 4, Index: 2, TypeName: "Stride.Engine.LightComponent, Stride.Engine"),
            ["StructureMove"] = new PlayStructureEdit(21, PlayStructureOperation.MoveEntity, EntityHandle: 4, SceneHandle: 2, ParentHandle: 9, KeepWorldTransform: true),
            ["StructureSpawn"] = new PlayStructureEdit(22, PlayStructureOperation.SpawnPrefab, SceneHandle: 1, Url: "Prefabs/Crate", Name: "Crate"),
            ["StructureRemove"] = new PlayStructureEdit(23, PlayStructureOperation.RemoveComponent, EntityHandle: 4, ComponentId: Guid.NewGuid()),
            ["StructureResult"] = new PlayStructureResult(22, 130, PlayEditStatus.Applied, [12, 13, 14], Guid.Empty, null),
            ["StructureResultComponent"] = new PlayStructureResult(20, 131, PlayEditStatus.Applied, [], Guid.NewGuid(), null),
            ["StructureResultFailed"] = new PlayStructureResult(24, 132, PlayEditStatus.Gone, [], Guid.Empty, "The entity is no longer in the game."),
        };
    }
}
