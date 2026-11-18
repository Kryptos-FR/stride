// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.IO.Pipes;
using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.IO;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using Stride.Core.Storage;
using Stride.Engine;
using Stride.Games;
using Stride.PlayMode.Agent;
using Stride.PlayMode.Protocol;
using Stride.PlayMode.Snapshots;

namespace Stride.PlayMode.Tests;

/// <summary>
/// Tests the edits of references of the play agent: content set by URL and loaded by the game's content manager, the loads given
/// back, entities and components set by the agent's handles and ids. The game is not run; its content is a database of its own,
/// written by the test.
/// </summary>
public sealed class PlayReferenceEditTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly string folder = $"{nameof(PlayReferenceEditTests)}/{Guid.NewGuid():N}";
    private readonly ObjectDatabase database;
    private readonly ContentManager content;
    private readonly PlayContentLoads loads;
    private readonly PlayEditApplier applier;
    private readonly Game game = new();
    private readonly Scene scene = new() { Name = "Root" };
    private readonly SceneInstance instance;
    private readonly PlayTreeTracker tracker = new();
    private readonly Entity player = new("Player");
    private readonly Entity camera = new("Camera");
    private readonly ReferenceHolderComponent holder = new();
    private readonly EditableComponent cameraPart = new();
    private readonly List<IDisposable> disposables = [];
    private int nextId;

    public PlayReferenceEditTests()
    {
        database = new ObjectDatabase($"/tmp/{folder}", "index", loadDefaultBundle: false);
        var provider = new DatabaseFileProviderService(new DatabaseFileProvider(database));
        // Written by a content manager of its own: the game's starts with nothing loaded
        var writer = new ContentManager(provider);
        writer.Save("Tests/A", new TestContent { Text = "A" });
        writer.Save("Tests/B", new TestContent { Text = "B" });
        writer.Save("Tests/Number", new OtherTestContent { Number = 7 });
        content = new ContentManager(provider);
        loads = new PlayContentLoads(new PlayGameContent(() => content));
        applier = new PlayEditApplier(loads);

        player.Add(holder);
        camera.Add(cameraPart);
        scene.Entities.Add(player);
        scene.Entities.Add(camera);
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
        database.Dispose();
        var path = Path.Combine(PlatformFolders.ApplicationTemporaryDirectory, folder);
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    [Fact]
    public void AContentReferenceIsLoadedByUrlAndReadBack()
    {
        var result = Apply(Set(Content<TestContent>("Tests/A"), "Content"));

        Assert.Equal(PlayEditStatus.Applied, result.Status);
        Assert.Equal("A", holder.Content?.Text);
        Assert.True(content.IsLoaded("Tests/A"));
        Assert.Equal("Tests/A", UrlOf(ReadBack(result)));
        Assert.Equal(1, loads.Count);
    }

    [Fact]
    public void NullClearsTheReferenceAndGivesTheLoadBack()
    {
        Apply(Set(Content<TestContent>("Tests/A"), "Content"));

        var result = Apply(Set(null, "Content"));

        Assert.Equal(PlayEditStatus.Applied, result.Status);
        Assert.Null(holder.Content);
        Assert.Equal(PlayEditValueKind.Null, result.ReadBack.Kind);
        Assert.False(content.IsLoaded("Tests/A"));
        Assert.Equal(0, loads.Count);
    }

    [Fact]
    public void AnotherReferenceGivesThePreviousLoadBack()
    {
        Apply(Set(Content<TestContent>("Tests/A"), "Content"));

        Apply(Set(Content<TestContent>("Tests/B"), "Content"));

        Assert.Equal("B", holder.Content?.Text);
        Assert.False(content.IsLoaded("Tests/A"));
        Assert.True(content.IsLoaded("Tests/B"));
        Assert.Equal((2, 1), (loads.Loaded, loads.Unloaded));
    }

    [Fact]
    public void AUrlNotInTheBuildIsRefusedAndTheReferenceStays()
    {
        Apply(Set(Content<TestContent>("Tests/A"), "Content"));

        var result = Apply(Set(Content<TestContent>("Tests/Missing"), "Content"));

        Assert.Equal(PlayEditStatus.Failed, result.Status);
        Assert.Contains("Tests/Missing is not in the game's build", result.Error);
        Assert.Equal("A", holder.Content?.Text);
        Assert.Equal("Tests/A", UrlOf(ReadBack(result)));
        Assert.True(content.IsLoaded("Tests/A"));
    }

    [Fact]
    public void ContentOfAnotherTypeIsRefusedAndNotKeptLoaded()
    {
        // The editor says it is a TestContent: the build has another type at that URL
        var wrongUrl = Apply(Set(Content<TestContent>("Tests/Number"), "Content"));
        // The editor sends the right type for the URL, but the member takes another one
        var wrongMember = Apply(Set(Content<OtherTestContent>("Tests/Number"), "Content"));

        Assert.Equal(PlayEditStatus.Failed, wrongUrl.Status);
        Assert.Contains("Tests/Number is a OtherTestContent in the game's build, not a TestContent", wrongUrl.Error);
        Assert.Equal(PlayEditStatus.Failed, wrongMember.Status);
        Assert.Contains("Content is a TestContent in the game, and the editor sent a OtherTestContent", wrongMember.Error);
        Assert.Null(holder.Content);
        Assert.False(content.IsLoaded("Tests/Number"));
        Assert.Equal(0, loads.Count);
    }

    [Fact]
    public void ContentMadeAtRunTimeCannotBeSent()
    {
        var made = (TestContent)AttachedReferenceManager.CreateProxyObject(typeof(TestContent), AssetId.Empty, null!);

        var result = Apply(Set(made, "Content"));

        Assert.Equal(PlayEditStatus.Failed, result.Status);
        Assert.Contains("has no URL: content the game made at run time cannot be sent", result.Error);
    }

    [Fact]
    public void ContentTheGameLoadedItselfStaysLoadedWhenTheAgentGivesItsLoadsBack()
    {
        // The game's own load, set by the game
        holder.Content = content.Load<TestContent>("Tests/A");

        Apply(Set(Content<TestContent>("Tests/A"), "Second"));
        Apply(Set(Content<TestContent>("Tests/B"), "Content"));
        Assert.Same(holder.Second, content.Get<TestContent>("Tests/A"));
        var unloaded = loads.ReleaseAll();

        Assert.Equal(2, unloaded);
        Assert.True(content.IsLoaded("Tests/A"));
        Assert.False(content.IsLoaded("Tests/B"));
        // The game's own pair still works
        content.Unload(content.Get<TestContent>("Tests/A")!);
        Assert.False(content.IsLoaded("Tests/A"));
    }

    [Fact]
    public void TwoReferencesToTheSameContentKeepItLoadedUntilBothChange()
    {
        Apply(Set(Content<TestContent>("Tests/A"), "Content"));
        Apply(Set(Content<TestContent>("Tests/A"), "Second"));
        Assert.Same(holder.Content, holder.Second);

        Apply(Set(null, "Content"));
        Assert.True(content.IsLoaded("Tests/A"));

        Apply(Set(null, "Second"));
        Assert.False(content.IsLoaded("Tests/A"));
    }

    [Fact]
    public void ItemsOfCollectionsOfContentAreAddedSetAndRemoved()
    {
        Apply(Edit(holder, PlayEditOperation.Add, Value(Content<TestContent>("Tests/A")), "Items"));
        Apply(Edit(holder, PlayEditOperation.Add, Value(Content<TestContent>("Tests/A")), "Items"));
        Apply(Set(Content<TestContent>("Tests/B"), "Items", 1));
        Apply(Edit(holder, PlayEditOperation.Insert, Value(Content<TestContent>("Tests/B")), "Table", Key("Slow")));
        Assert.Equal(["A", "B"], holder.Items.Select(x => x?.Text));
        Assert.Equal("B", holder.Table["Slow"]?.Text);
        Assert.Equal(3, loads.Count);

        Apply(Edit(holder, PlayEditOperation.Remove, PlayEditValue.Null, "Items", 0));
        Assert.False(content.IsLoaded("Tests/A"));
        Apply(Edit(holder, PlayEditOperation.Remove, PlayEditValue.Null, "Items", 0));
        // Still in the dictionary
        Assert.True(content.IsLoaded("Tests/B"));
        Apply(Edit(holder, PlayEditOperation.Remove, PlayEditValue.Null, "Table", Key("Slow")));
        Assert.False(content.IsLoaded("Tests/B"));
        Assert.Equal(0, loads.Count);
    }

    [Fact]
    public void ASlotOfAnIndexingDictionaryOfContentIsSetAndGivenBackLikeTheMaterialsOfAModel()
    {
        Assert.Equal(PlayEditStatus.Applied, Apply(Edit(holder, PlayEditOperation.Insert, Value(Content<TestContent>("Tests/A")), "Slots", IntKey(0))).Status);
        var set = Apply(Set(Content<TestContent>("Tests/B"), "Slots", IntKey(0)));
        Assert.Equal(PlayEditStatus.Applied, set.Status);
        Assert.Equal("Tests/B", UrlOf(ReadBack(set)));
        Assert.Equal("B", holder.Slots[0]?.Text);
        Assert.False(content.IsLoaded("Tests/A"));
        Assert.Equal(1, loads.Count);

        Apply(Edit(holder, PlayEditOperation.Remove, PlayEditValue.Null, "Slots", IntKey(0)));
        Assert.False(content.IsLoaded("Tests/B"));
        Assert.Equal(0, loads.Count);
    }

    [Fact]
    public void WhenTheEditorLeavesOnlyTheLoadsNoLongerUsedAreGivenBack()
    {
        Apply(Set(Content<TestContent>("Tests/A"), "Content"));
        Apply(Set(Content<TestContent>("Tests/B"), "Second"));
        // The game itself drops one of them
        holder.Second = null;

        var (unloaded, kept) = loads.ReleaseUnused((slot, path) => (true, PlayEditPath.Get(holder, path, _ => null)));

        Assert.Equal((1, 1), (unloaded, kept));
        Assert.True(content.IsLoaded("Tests/A"));
        Assert.False(content.IsLoaded("Tests/B"));
        Assert.Equal(1, loads.ReleaseAll());
        Assert.False(content.IsLoaded("Tests/A"));
    }

    [Fact]
    public void AnEntityReferenceIsSetByHandleAndAComponentReferenceById()
    {
        var entity = Apply(Set(Reference(camera), "Target"));
        var component = Apply(Set(Reference(cameraPart), "Pointer"));

        Assert.Equal(PlayEditStatus.Applied, entity.Status);
        Assert.Same(camera, holder.Target);
        Assert.Equal(PlayEditStatus.Applied, component.Status);
        Assert.Same(cameraPart, holder.Pointer);
        // The read-back names the live objects by the same handle and id
        var references = PlayMirrorSerializer.ReadReferences(component.ReadBack.Payload);
        Assert.Equal(tracker.GetHandle(camera), Assert.Single(references).EntityHandle);
        Assert.Equal(cameraPart.Id, references[0].ComponentId);
    }

    [Fact]
    public void AReferenceToAnEntityOrComponentNoLongerInTheGameIsGone()
    {
        holder.Target = player;
        var handle = tracker.GetHandle(camera);
        // Sent while the camera was in the game, applied once it left
        var setEntity = Set(Reference(camera), "Target");
        var setComponent = Set(Reference(cameraPart), "Pointer");
        scene.Entities.Remove(camera);
        tracker.Update(instance);

        var entity = Apply(setEntity);
        var component = Apply(setComponent);

        Assert.NotEqual(0, handle);
        Assert.Equal(PlayEditStatus.Gone, entity.Status);
        Assert.Contains("Camera is no longer in the game", entity.Error);
        Assert.Same(player, holder.Target);
        Assert.Equal(PlayEditStatus.Gone, component.Status);
    }

    [Fact]
    public void AnEntityTheAgentDoesNotTrackCannotBeReferenced()
    {
        var outside = new Entity("Outside");

        var result = Apply(Edit(holder, PlayEditOperation.Set, PlayEditValue.Of(PlayMirrorSerializer.WriteValue(outside, _ => 0)), "Target"));

        Assert.Equal(PlayEditStatus.Failed, result.Status);
        Assert.Contains("Outside is not in a running scene of the game", result.Error);
        Assert.Null(holder.Target);
    }

    [Fact]
    public async Task TheAgentListsItsBuildOnRequestAndGivesItsLoadsBackWhenTheGameExits()
    {
        var agent = new PlayAgentSystem(game, null, null, null, new PlayGameContent(() => content));
        disposables.Add(agent);
        game.SceneSystem.SceneInstance = instance;
        var editor = await ConnectAsync(agent);
        agent.Update(new GameTime());
        var handle = agent.Tracker.GetHandle(player);

        editor.Send(new PlayContentRequest());
        await FrameUntilAsync(agent, () => editor.Index is not null);
        Assert.Contains("Tests/A", editor.Index!.Urls);
        Assert.Contains("Tests/Number", editor.Index.Urls);

        editor.Send(new PlayEdit(++nextId, handle, holder.Id, [PlayPathSegment.Member("Content")], PlayEditOperation.Set, Value(Content<TestContent>("Tests/A"))));
        await WaitAsync(() => agent.PendingEdits == 1);
        agent.EditSystem.Update(new GameTime());
        Assert.Equal("A", holder.Content?.Text);
        Assert.Equal(1, agent.ContentLoads.Count);

        // Stop: the game exits, and the agent gives its load back first
        agent.Dispose();
        Assert.Equal(0, agent.ContentLoads.Count);
        Assert.False(content.IsLoaded("Tests/A"));
    }

    private PlayEditResult Apply(PlayEdit edit) => applier.Apply([(0, edit)], 1, Find, tracker.GetHandle)[0].Result;

    private Entity? Find(int handle) => tracker.Entities.TryGetValue(handle, out var entity) && entity.EntityManager is not null ? entity : null;

    private PlayEdit Set(object? value, params object[] path) => Edit(holder, PlayEditOperation.Set, Value(value), path);

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

    // What the editor sends for a picked asset: a proxy of the content type with the asset's URL
    private static T Content<T>(string url) => (T)AttachedReferenceManager.CreateProxyObject(typeof(T), AssetId.Empty, url);

    // What the editor sends for a picked entity or component: written with the agent's handles
    private ReferenceValue Reference(object live) => new(live);

    private PlayEditValue Value(object? value)
    {
        return value switch
        {
            null => PlayEditValue.Null,
            ReferenceValue reference => PlayEditValue.Of(PlayMirrorSerializer.WriteValue(reference.Live, tracker.GetHandle)),
            _ => PlayEditValue.Of(PlayMirrorSerializer.WriteValue(value, _ => 0)),
        };
    }

    private static PlayPathSegment Key(string key) => PlayPathSegment.ForKey(PlayMirrorSerializer.WriteValue(key, _ => 0), key);

    private static PlayPathSegment IntKey(int key) => PlayPathSegment.ForKey(PlayMirrorSerializer.WriteValue(key, _ => 0), key.ToString());

    private static object? ReadBack(PlayEditResult result) => result.ReadBack.Kind == PlayEditValueKind.Null ? null : PlayMirrorSerializer.ReadValue(result.ReadBack.Payload, _ => null);

    private static string? UrlOf(object? value) => value is null ? null : AttachedReferenceManager.GetUrl(value);

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
        var name = $"stride-play-reference-test-{Guid.NewGuid():N}";
        var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await Task.WhenAll(server.WaitForConnectionAsync(), client.ConnectAsync()).WaitAsync(Timeout);
        var editor = new EditorEnd(server);
        disposables.Add(editor);
        agent.AddSubscriber(client);
        return editor;
    }

    private sealed record ReferenceValue(object Live);

    /// <summary>
    /// The editor's end of a connection: keeps the content index it receives.
    /// </summary>
    private sealed class EditorEnd : IDisposable
    {
        private readonly PlayConnection connection;

        public EditorEnd(Stream stream)
        {
            connection = new PlayConnection(stream);
            connection.Start(x =>
            {
                if (x is PlayContentIndex index)
                    Index = index;
            });
        }

        public PlayContentIndex? Index { get; private set; }

        public void Send(PlayMessage message) => Assert.True(connection.Send(message));

        public void Dispose() => connection.DisposeAsync().AsTask().Wait(Timeout);
    }
}
