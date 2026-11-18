// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.IO.Pipes;
using System.Threading.Channels;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.PlayMode.Agent;
using Stride.PlayMode.Protocol;
using Stride.PlayMode.Snapshots;

namespace Stride.PlayMode.Tests;

/// <summary>
/// Tests the edits of the play agent (<see cref="PlayEditApplier"/>, <see cref="PlayAgentSystem.ApplyEdits"/>) on a game that
/// is not run (no graphics): its scene instance holds real entities, and the agent's frames run by hand.
/// </summary>
public sealed class PlayEditTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly Game game = new();
    private readonly Scene scene = new() { Name = "Root" };
    private readonly SceneInstance instance;
    private readonly PlayTreeTracker tracker = new();
    private readonly PlayEditApplier applier = new();
    private readonly Entity player = new("Player");
    private readonly EditableComponent editable = new();
    private readonly List<IDisposable> disposables = [];
    private int nextId;

    public PlayEditTests()
    {
        player.Add(editable);
        scene.Entities.Add(player);
        instance = new SceneInstance(game.Services, scene);
        tracker.Update(instance);
    }

    public void Dispose()
    {
        foreach (var disposable in disposables)
            disposable.Dispose();
        tracker.Dispose();
        instance.Dispose();
        game.Dispose();
    }

    [Fact]
    public void ALeafSetGoesThroughTheSetterAndIsReadBack()
    {
        var result = Apply(Set(editable, 2.5f, "Speed"));

        Assert.Equal(PlayEditStatus.Applied, result.Status);
        Assert.Equal(2.5f, editable.Speed);
        Assert.Equal(2.5f, ReadBack(result));
        Assert.Null(result.Error);
    }

    [Fact]
    public void AStructPathIsReadChangedAndSetBackThroughItsOwner()
    {
        editable.Offset = new Vector3(1, 2, 3);
        editable.Range = new EditRange { Min = 1, Span = new Vector2(4, 5) };

        var x = Apply(Set(editable, 9f, "Offset", "X"));
        var nested = Apply(Set(editable, 7f, "Range", "Span", "Y"));
        var transform = Apply(Set(player.Transform, new Vector3(0, 1, 2), "Position"));

        Assert.Equal(new Vector3(9, 2, 3), editable.Offset);
        Assert.Equal(9f, ReadBack(x));
        Assert.Equal(new Vector2(4, 7), editable.Range.Span);
        Assert.Equal(1, editable.Range.Min);
        Assert.Equal(7f, ReadBack(nested));
        Assert.Equal(new Vector3(0, 1, 2), player.Transform.Position);
        Assert.Equal(new Vector3(0, 1, 2), ReadBack(transform));
    }

    [Fact]
    public void ValuesOfEveryKindAreWrittenAndReadBack()
    {
        Assert.Equal(PlayEditStatus.Applied, Apply(Set(editable, new Color3(1, 0.5f, 0.25f), "Tint")).Status);
        Assert.Equal(PlayEditStatus.Applied, Apply(Set(editable, EditMode.Run, "Mode")).Status);
        Assert.Equal(PlayEditStatus.Applied, Apply(Set(editable, "Hello", "Label")).Status);
        var nullLabel = Apply(Edit(editable, PlayEditOperation.Set, PlayEditValue.Null, "Label"));

        Assert.Equal(new Color3(1, 0.5f, 0.25f), editable.Tint);
        Assert.Equal(EditMode.Run, editable.Mode);
        Assert.Null(editable.Label);
        Assert.Equal(PlayEditValueKind.Null, nullLabel.ReadBack.Kind);
    }

    [Fact]
    public void ASetterThatClampsShowsItsClampedValue()
    {
        var result = Apply(Set(editable, 4f, "Ratio"));

        Assert.Equal(PlayEditStatus.Applied, result.Status);
        Assert.Equal(1f, editable.Ratio);
        Assert.Equal(1f, ReadBack(result));
    }

    [Fact]
    public void ASetterThatThrowsIsAnsweredWithItsErrorAndTheValueAsItStayed()
    {
        editable.Count = 3;

        var result = Apply(Set(editable, -1, "Count"));

        Assert.Equal(PlayEditStatus.Failed, result.Status);
        Assert.Contains("ArgumentOutOfRangeException", result.Error);
        Assert.Contains("The count cannot be negative.", result.Error);
        Assert.Equal(3, ReadBack(result));
        Assert.Equal(3, editable.Count);
    }

    [Fact]
    public void AMemberTheGameDoesNotHaveIsRefused()
    {
        var result = Apply(Set(editable, 1f, "Missing"));
        var readOnly = Apply(Set(editable, new List<int>(), "Values"));

        Assert.Equal(PlayEditStatus.Failed, result.Status);
        Assert.Contains("has no member Missing", result.Error);
        Assert.Equal(PlayEditStatus.Failed, readOnly.Status);
        Assert.Contains("no setter", readOnly.Error);
    }

    [Fact]
    public void TwoSetsOfOneMemberInAFrameApplyTheLastAndAnswerEach()
    {
        var first = Set(editable, 1f, "Speed");
        var other = Set(editable, 0.5f, "Ratio");
        var last = Set(editable, 3f, "Speed");

        var results = applier.Apply([(0, first), (0, other), (0, last)], 7, Find, tracker.GetHandle);

        Assert.Equal([first.Id, other.Id, last.Id], results.Select(x => x.Result.Id));
        Assert.Equal(PlayEditStatus.Coalesced, results[0].Result.Status);
        Assert.Equal(PlayEditStatus.Applied, results[1].Result.Status);
        Assert.Equal(PlayEditStatus.Applied, results[2].Result.Status);
        // Both get the value the game kept
        Assert.Equal(3f, ReadBack(results[0].Result));
        Assert.Equal(3f, ReadBack(results[2].Result));
        Assert.All(results, x => Assert.Equal(7, x.Result.Frame));
        Assert.Equal(3f, editable.Speed);
    }

    [Fact]
    public void ACollectionOperationBetweenTwoSetsKeepsBoth()
    {
        editable.Values.AddRange([1, 2]);
        var first = Set(editable, 10, "Values", 0);
        var insert = Edit(editable, PlayEditOperation.Insert, Value(5), "Values", 0);
        var second = Set(editable, 20, "Values", 0);

        var results = applier.Apply([(0, first), (0, insert), (0, second)], 1, Find, tracker.GetHandle);

        Assert.All(results, x => Assert.Equal(PlayEditStatus.Applied, x.Result.Status));
        Assert.Equal([20, 10, 2], editable.Values);
    }

    [Fact]
    public void ListItemsAreAddedInsertedRemovedAndMoved()
    {
        Assert.Equal(PlayEditStatus.Applied, Apply(Edit(editable, PlayEditOperation.Add, Value(1), "Values")).Status);
        Apply(Edit(editable, PlayEditOperation.Add, Value(2), "Values"));
        Apply(Edit(editable, PlayEditOperation.Insert, Value(0), "Values", 0));
        Assert.Equal([0, 1, 2], editable.Values);

        Apply(Move(editable, 0, 2, "Values"));
        Assert.Equal([1, 2, 0], editable.Values);

        Apply(Edit(editable, PlayEditOperation.Remove, PlayEditValue.Null, "Values", 1));
        Assert.Equal([1, 0], editable.Values);

        var outOfRange = Apply(Edit(editable, PlayEditOperation.Remove, PlayEditValue.Null, "Values", 5));
        Assert.Equal(PlayEditStatus.Failed, outOfRange.Status);
        Assert.Contains("The list has 2 items", outOfRange.Error);
    }

    [Fact]
    public void AMovedObjectKeepsItsState()
    {
        var box = new EditBox { Size = 3 };
        var sphere = new EditSphere();
        editable.Shapes.AddRange([box, sphere]);

        Apply(Move(editable, 0, 1, "Shapes"));

        Assert.Same(sphere, editable.Shapes[0]);
        Assert.Same(box, editable.Shapes[1]);
    }

    [Fact]
    public void DictionaryKeysAreAddedRemovedAndRenamed()
    {
        var add = Apply(Edit(editable, PlayEditOperation.Insert, Value(1.5f), "Table", Key("Fast")));
        Apply(Edit(editable, PlayEditOperation.Insert, Value(0.5f), "Table", Key("Slow")));
        var set = Apply(Set(editable, 2f, "Table", Key("Fast")));
        Assert.Equal(PlayEditStatus.Applied, add.Status);
        Assert.Equal(2f, ReadBack(set));
        Assert.Equal(2f, editable.Table["Fast"]);

        var rename = Apply(Edit(editable, PlayEditOperation.Move, PlayEditValue.Null, "Table", Key("Fast")) with { Target = KeySegment("Faster") });
        Assert.Equal(PlayEditStatus.Applied, rename.Status);
        Assert.Equal(2f, editable.Table["Faster"]);
        Assert.False(editable.Table.ContainsKey("Fast"));

        Apply(Edit(editable, PlayEditOperation.Remove, PlayEditValue.Null, "Table", Key("Slow")));
        Assert.Equal(["Faster"], editable.Table.Keys);

        var duplicate = Apply(Edit(editable, PlayEditOperation.Insert, Value(1f), "Table", Key("Faster")));
        Assert.Equal(PlayEditStatus.Failed, duplicate.Status);
        Assert.Contains("already has the key [Faster]", duplicate.Error);
    }

    [Fact]
    public void NullThenANewInstanceOfAChosenTypeMadeByTheGame()
    {
        editable.Shape = new EditBox();

        Apply(Edit(editable, PlayEditOperation.Set, PlayEditValue.Null, "Shape"));
        Assert.Null(editable.Shape);

        var created = Apply(Edit(editable, PlayEditOperation.Set, PlayEditValue.NewInstanceOf(PlayMirrorSerializer.TypeNameOf(typeof(EditSphere))), "Shape"));
        Assert.Equal(PlayEditStatus.Applied, created.Status);
        var sphere = Assert.IsType<EditSphere>(editable.Shape);
        // The game's own defaults
        Assert.Equal(0.5f, sphere.Radius);

        var inserted = Apply(Edit(editable, PlayEditOperation.Insert, PlayEditValue.NewInstanceOf(PlayMirrorSerializer.TypeNameOf(typeof(EditBox))), "Shapes", 0));
        Assert.Equal(PlayEditStatus.Applied, inserted.Status);
        Assert.IsType<EditBox>(Assert.Single(editable.Shapes));
    }

    [Fact]
    public void AnUnknownTypeIsRefused()
    {
        var result = Apply(Edit(editable, PlayEditOperation.Set, PlayEditValue.NewInstanceOf("MyGame.Gone.Shape, MyGame"), "Shape"));
        var wrongType = Apply(Edit(editable, PlayEditOperation.Set, PlayEditValue.NewInstanceOf(PlayMirrorSerializer.TypeNameOf(typeof(EditMode))), "Shape"));

        Assert.Equal(PlayEditStatus.Failed, result.Status);
        Assert.Contains("The game has no type MyGame.Gone.Shape", result.Error);
        Assert.Equal(PlayEditStatus.Failed, wrongType.Status);
        Assert.Null(editable.Shape);
    }

    [Fact]
    public void AnEditWithoutComponentChangesTheEntityItself()
    {
        var edit = new PlayEdit(++nextId, tracker.GetHandle(player), Guid.Empty, [PlayPathSegment.Member(nameof(Entity.Name))], PlayEditOperation.Set, Value("Hero"));

        var result = Apply(edit);

        Assert.Equal(PlayEditStatus.Applied, result.Status);
        Assert.Equal("Hero", player.Name);
        Assert.Equal("Hero", ReadBack(result));
    }

    [Fact]
    public void AnEditOfAnEntityOrComponentNoLongerInTheGameSaysSo()
    {
        var other = new Entity("Other") { new EditableComponent() };
        scene.Entities.Add(other);
        tracker.Update(instance);
        var edit = Set((EditableComponent)other.Components[1], 1f, "Speed");
        var removedComponent = Set(editable, 1f, "Speed");
        scene.Entities.Remove(other);
        player.Remove(editable);

        Assert.Equal(PlayEditStatus.Gone, Apply(edit).Status);
        var result = Apply(removedComponent);
        Assert.Equal(PlayEditStatus.Gone, result.Status);
        Assert.Contains("no longer has this component", result.Error);
    }

    [Fact]
    public async Task TheAgentAppliesEditsAtTheStartOfTheFrameAndSendsTheResultsThenASnapshot()
    {
        var agent = new PlayAgentSystem(game);
        disposables.Add(agent);
        game.SceneSystem.SceneInstance = instance;
        var editor = await ConnectAsync(agent);
        agent.Update(new GameTime());
        var handle = agent.Tracker.GetHandle(player);
        // Snapshots once a second: the edit's snapshot comes before its time
        editor.Send(new PlaySubscribe([handle], 1));
        await FrameUntilAsync(agent, editor.TryRead<PlaySnapshot>);

        var first = new PlayEdit(++nextId, handle, editable.Id, [PlayPathSegment.Member("Speed")], PlayEditOperation.Set, Value(1f));
        var second = first with { Id = ++nextId, Value = Value(4f) };
        editor.Send(first);
        editor.Send(second);
        await WaitAsync(() => agent.PendingEdits == 2);
        agent.EditSystem.Update(new GameTime());
        Assert.Equal(4f, editable.Speed);
        agent.Update(new GameTime());

        var results = new[] { await editor.ReadAsync<PlayEditResult>(), await editor.ReadAsync<PlayEditResult>() };
        Assert.Equal([PlayEditStatus.Coalesced, PlayEditStatus.Applied], results.Select(x => x.Status));
        Assert.Equal([first.Id, second.Id], results.Select(x => x.Id));
        var snapshot = await editor.ReadAsync<PlaySnapshot>();
        Assert.Equal(results[1].Frame, snapshot.Frame);
        Assert.Equal((2, 0), agent.EditCounts);
        Assert.Equal(int.MinValue, agent.EditSystem.UpdateOrder);
    }

    private PlayEditResult Apply(PlayEdit edit) => applier.Apply([(0, edit)], 1, Find, tracker.GetHandle)[0].Result;

    private Entity? Find(int handle) => tracker.Entities.TryGetValue(handle, out var entity) && entity.EntityManager is not null ? entity : null;

    private PlayEdit Set(EntityComponent component, object? value, params object[] path) => Edit(component, PlayEditOperation.Set, Value(value), path);

    private PlayEdit Move(EntityComponent component, int from, int to, string member)
        => Edit(component, PlayEditOperation.Move, PlayEditValue.Null, member, from) with { Target = PlayPathSegment.Item(to) };

    private PlayEdit Edit(EntityComponent component, PlayEditOperation operation, PlayEditValue value, params object[] path)
    {
        var segments = path.Select(x => x switch
        {
            string name => PlayPathSegment.Member(name),
            int index => PlayPathSegment.Item(index),
            PlayPathSegment segment => segment,
            _ => throw new ArgumentException($"A path step cannot be a {x.GetType().Name}."),
        }).ToList();
        return new PlayEdit(++nextId, tracker.GetHandle(component.Entity), component.Id, segments, operation, value);
    }

    private static PlayEditValue Value(object? value) => value is null ? PlayEditValue.Null : PlayEditValue.Of(PlayMirrorSerializer.WriteValue(value, _ => 0));

    private static PlayPathSegment Key(string key) => KeySegment(key);

    private static PlayPathSegment KeySegment(string key) => PlayPathSegment.ForKey(PlayMirrorSerializer.WriteValue(key, _ => 0), key);

    private static object? ReadBack(PlayEditResult result) => PlayMirrorSerializer.ReadValue(result.ReadBack.Payload, _ => null);

    private static async Task FrameUntilAsync(PlayAgentSystem agent, Func<bool> received)
    {
        var start = DateTime.UtcNow;
        do
        {
            if (DateTime.UtcNow - start > Timeout)
                throw new TimeoutException("The agent did not answer.");
            agent.Update(new GameTime());
            await Task.Delay(10);
        }
        while (!received());
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        var start = DateTime.UtcNow;
        while (!condition())
        {
            if (DateTime.UtcNow - start > Timeout)
                throw new TimeoutException("The agent did not receive the edits.");
            await Task.Delay(5);
        }
    }

    private async Task<EditorEnd> ConnectAsync(PlayAgentSystem agent)
    {
        var name = $"stride-play-edit-test-{Guid.NewGuid():N}";
        var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await Task.WhenAll(server.WaitForConnectionAsync(), client.ConnectAsync()).WaitAsync(Timeout);
        var editor = new EditorEnd(server);
        disposables.Add(editor);
        agent.AddSubscriber(client);
        return editor;
    }

    /// <summary>
    /// The editor's end of a connection.
    /// </summary>
    private sealed class EditorEnd : IDisposable
    {
        private readonly PlayConnection connection;
        private readonly Channel<PlayMessage> received = Channel.CreateUnbounded<PlayMessage>();

        public EditorEnd(Stream stream)
        {
            connection = new PlayConnection(stream);
            connection.Start(x => received.Writer.TryWrite(x));
        }

        public void Send(PlayMessage message) => Assert.True(connection.Send(message));

        public bool TryRead<T>()
            where T : PlayMessage
        {
            while (received.Reader.TryRead(out var message))
            {
                if (message is T)
                    return true;
            }
            return false;
        }

        public async Task<T> ReadAsync<T>()
            where T : PlayMessage
        {
            using var timeout = new CancellationTokenSource(Timeout);
            while (true)
            {
                if (await received.Reader.ReadAsync(timeout.Token) is T typed)
                    return typed;
            }
        }

        public void Dispose() => connection.DisposeAsync().AsTask().Wait(Timeout);
    }
}
