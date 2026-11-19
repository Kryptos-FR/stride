// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Concurrent;
using System.IO.Pipes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Stride.Assets.Editor.PlayMode;
using Stride.Assets.Editor.Quantum.NodePresenters.Commands;
using Stride.Assets.Editor.Quantum.NodePresenters.Keys;
using Stride.Assets.Models;
using Stride.Core;
using Stride.Core.Assets.Editor.Quantum.NodePresenters.Commands;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.Avalonia.Controls;
using Stride.Core.Presentation.Quantum.Presenters;
using Stride.Core.Presentation.Quantum.ViewModels;
using Stride.Core.Quantum;
using Stride.Editor.PlayMode;
using Stride.Engine;
using Stride.GameStudio.Avalonia.Tests.Helpers;
using Stride.GameStudio.Avalonia.Views;
using Stride.PlayMode.Agent;
using Stride.PlayMode.Protocol;
using Stride.PlayMode.Snapshots;

namespace Stride.GameStudio.Avalonia.Tests;

/// <summary>
/// Live edits from the property grid: the grid of a play session whose game, a fake on the session's real pipe, answers the
/// edits as the test says (the agent's own tests apply them to a real game).
/// </summary>
public sealed partial class LiveEditTests : IAsyncLifetime
{
    private const string Version = "1.0.0-test";
    private const string ModelLocation = "Models/Box";
    private const int PlayerHandle = 1;
    private const int OtherHandle = 3;

    private readonly string agentDirectory = Path.Combine(Path.GetTempPath(), $"stride-live-edit-tests-{Guid.NewGuid():N}");
    private readonly Entity target = new("Target");
    private readonly Entity player;
    private readonly Entity other;
    private readonly LiveEditTestScript script = new();
    private readonly LiveEditTestScript otherScript = new();
    private readonly Dictionary<Entity, int> handles;
    private PlaySession session = null!;
    private EditGame? game;
    private long frame;

    public LiveEditTests()
    {
        script.Target = target;
        player = new Entity("Player") { script };
        other = new Entity("Other") { otherScript };
        handles = new(ReferenceEqualityComparer.Instance) { [player] = PlayerHandle, [target] = 2, [other] = OtherHandle };
    }

    private EditGame Game => game ?? throw new InvalidOperationException("The game did not start.");

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(agentDirectory);
        foreach (var file in PlayAgentFiles.FileNames)
            File.WriteAllBytes(Path.Combine(agentDirectory, file), []);
        session = new PlaySession(new PipePlayTransport(), new PlayAgentFiles(agentDirectory, Version), new PlaySessionOptions { StopGracePeriod = TimeSpan.FromSeconds(2) });
        var start = session.StartAsync(environment => game = new EditGame(environment));
        await Game.ConnectAsync();
        await start.WaitAsync(TimeSpan.FromSeconds(10));
        Game.Send(new PlayHello(4321, Version, Version, "Test.Game", []));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (session.State != PlaySessionState.Connected)
            await Task.Delay(10, timeout.Token);
    }

    public async ValueTask DisposeAsync()
    {
        await session.DisposeAsync();
        if (game is not null)
            await game.DisposeAsync();
        Directory.Delete(agentDirectory, recursive: true);
    }

    [AvaloniaFact]
    public async Task AValueThatTheSetterClampsShowsClampedOnceTheGameAnswers()
    {
        using var fixture = await OpenAsync();
        var properties = await ShowAsync(fixture, PlayerHandle);
        try
        {
            var stamina = Node(properties, nameof(LiveEditTestScript.Stamina));
            var shown = new List<object?>();
            stamina.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(NodeViewModel.NodeValue))
                    shown.Add(stamina.NodeValue);
            };

            stamina.NodeValue = 5f;

            // The game gets the value typed, and its setter decides
            var edit = await Game.NextEditAsync();
            Assert.Equal(5f, Read(edit.Value));
            script.Stamina = 5;
            Game.Send(Answer(edit, PlayEditStatus.Applied, script.Stamina));
            await WindowTestHelper.WaitUntilAsync(() => State(stamina) is null, "the answer");

            // The grid shows the game's value, and the view was told
            Assert.Equal(1f, stamina.NodeValue);
            Assert.Equal(1f, shown[^1]);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task AValueThatTheSetterRefusesGoesToTheGameWhichSaysWhy()
    {
        using var fixture = await OpenAsync();
        var properties = await ShowAsync(fixture, PlayerHandle);
        try
        {
            var lives = Node(properties, nameof(LiveEditTestScript.Lives));
            var notified = 0;
            lives.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(NodeViewModel.NodeValue))
                    ++notified;
            };

            // The grid's copy runs the same setter: it keeps its value, and the game decides
            lives.NodeValue = -1;
            var edit = await Game.NextEditAsync();
            Assert.Equal(-1, Read(edit.Value));
            Assert.Equal(3, lives.NodeValue);
            Assert.Equal(LiveEditData.Pending, State(lives));

            notified = 0;
            Game.Send(Answer(edit, PlayEditStatus.Failed, 3, "Lives cannot be negative."));
            await WindowTestHelper.WaitUntilAsync(() => State(lives) is LiveEditData.Refused, "the refusal");

            // The view hears of the refusal, and its tip says why
            Assert.True(notified > 0);
            Assert.Equal(3, lives.NodeValue);
            Assert.Contains("Lives cannot be negative.", (string)lives.NodePresenters.Single().AttachedProperties[LiveEditData.TipKey]);
            Assert.Equal(1, properties.RefusedEdits);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task AValueTypedGoesToTheGameAndTheGridShowsWhatTheGameReadsBack()
    {
        using var fixture = await OpenAsync();
        var properties = await ShowAsync(fixture, PlayerHandle);
        try
        {
            var speed = Node(properties, nameof(LiveEditTestScript.Speed));
            Assert.False(speed.IsReadOnly);

            speed.NodeValue = 12f;

            // The value shows at once, pending, and goes to the game
            var edit = await Game.NextEditAsync();
            Assert.Equal(PlayerHandle, edit.EntityHandle);
            Assert.Equal(script.Id, edit.ComponentId);
            Assert.Equal("Speed", edit.PathText);
            Assert.Equal(PlayEditOperation.Set, edit.Operation);
            Assert.Equal(12f, Read(edit.Value));
            Assert.Equal(12f, speed.NodeValue);
            Assert.Equal(LiveEditData.Pending, State(speed));
            Assert.Equal(1, properties.PendingEdits);

            // A snapshot of a frame before the edit does not undo it in the grid
            await properties.ApplyAsync(Snapshot());
            Assert.Equal(12f, speed.NodeValue);

            // The game's setter clamps it: the grid shows the game's value
            script.Speed = 10;
            Game.Send(Answer(edit, PlayEditStatus.Applied, 10f));
            await WindowTestHelper.WaitUntilAsync(() => State(speed) is null, "the answer");
            Assert.Equal(10f, speed.NodeValue);
            Assert.Equal(0, properties.PendingEdits);
            Assert.Equal(1, properties.SentEdits);
            // and the next snapshot agrees with it
            await properties.ApplyAsync(Snapshot());
            Assert.Equal(10f, speed.NodeValue);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task AValueTheGameRefusesShowsItsErrorOnTheProperty()
    {
        using var fixture = await OpenAsync();
        var properties = await ShowAsync(fixture, PlayerHandle);
        var window = new Window { Width = 500, Height = 1400, Content = new PropertyGridView { DataContext = properties } };
        await window.ShowAsync();
        try
        {
            (await ItemAsync(window, x => x.Name == "Components")).IsExpanded = true;
            (await ItemAsync(window, x => x.NodeValue is LiveEditTestScript)).IsExpanded = true;
            var count = Node(properties, nameof(LiveEditTestScript.Count));
            var answered = new List<LiveEditEventArgs>();
            properties.EditAnswered += (_, e) => answered.Add(e);

            count.NodeValue = -1;
            var edit = await Game.NextEditAsync();
            const string error = "The setter threw ArgumentOutOfRangeException: the count cannot be negative.";
            Game.Send(Answer(edit, PlayEditStatus.Failed, 3, error));
            await WindowTestHelper.WaitUntilAsync(() => State(count) is LiveEditData.Refused, "the refusal");

            // The value the game kept, a red frame, and the game's error in the tooltip
            Assert.Equal(3, count.NodeValue);
            Assert.Equal(1, properties.RefusedEdits);
            var item = await ItemAsync(window, x => x.Name == nameof(LiveEditTestScript.Count));
            var editor = item.GetVisualDescendants().OfType<Border>().First(x => x.Name == "PART_Editor");
            await WindowTestHelper.WaitUntilAsync(() => editor.Classes.Contains("liveRefused"), "the red frame");
            Assert.Equal($"The game refused this value: {error}", ToolTip.GetTip(editor));
            Assert.Equal(PlayEditStatus.Failed, Assert.Single(answered).Result.Status);

            // A value the game takes clears it
            count.NodeValue = 4;
            edit = await Game.NextEditAsync();
            Game.Send(Answer(edit, PlayEditStatus.Applied, 4));
            await WindowTestHelper.WaitUntilAsync(() => State(count) is null, "the second answer");
            await WindowTestHelper.WaitUntilAsync(() => !editor.Classes.Contains("liveRefused"), "the frame cleared");
        }
        finally
        {
            await window.CloseAsync();
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task ASliderDraggedSendsOneEditAtATimeAndItsLastValue()
    {
        using var fixture = await OpenAsync();
        var properties = await ShowAsync(fixture, PlayerHandle);
        try
        {
            var speed = Node(properties, nameof(LiveEditTestScript.Speed));
            // A drag: five values before the game answers the first
            for (var i = 2; i <= 6; i++)
                speed.NodeValue = (float)i;

            var first = await Game.NextEditAsync();
            Assert.Equal(2f, Read(first.Value));
            await Task.Delay(100);
            Assert.True(Game.Edits.IsEmpty, "a second edit went before the game answered the first");
            Assert.Equal(6f, speed.NodeValue);

            Game.Send(Answer(first, PlayEditStatus.Applied, 2f));
            var last = await Game.NextEditAsync();
            Assert.Equal(6f, Read(last.Value));
            // The answer of the first, older than the last value, did not show
            Assert.Equal(6f, speed.NodeValue);
            Assert.Equal(LiveEditData.Pending, State(speed));
            Game.Send(Answer(last, PlayEditStatus.Applied, 6f));
            await WindowTestHelper.WaitUntilAsync(() => State(speed) is null, "the last answer");

            Assert.Equal(6f, speed.NodeValue);
            Assert.Equal(2, properties.SentEdits);
            Assert.Equal(3, properties.CoalescedWrites);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task ItemsAreAddedRemovedMovedAndRenamedInTheGame()
    {
        using var fixture = await OpenAsync();
        var properties = await ShowAsync(fixture, PlayerHandle);
        try
        {
            var values = Node(properties, nameof(LiveEditTestScript.Values));
            var table = Node(properties, nameof(LiveEditTestScript.Table));
            var shape = Node(properties, nameof(LiveEditTestScript.Shape));
            var commands = values.NodePresenters.Single().Children.SelectMany(x => x.Commands).Select(x => x.GetType()).ToHashSet();
            Assert.Contains(typeof(LiveMoveItemCommand), commands);

            values.NodePresenters.Single().AddItem(0);
            var add = await Game.NextEditAsync();
            Assert.Equal((PlayEditOperation.Add, "Values"), (add.Operation, add.PathText));
            Assert.Equal(0, Read(add.Value));

            values.NodePresenters.Single().RemoveItem(2, new NodeIndex(1));
            var remove = await Game.NextEditAsync();
            Assert.Equal((PlayEditOperation.Remove, "Values[1]"), (remove.Operation, remove.PathText));

            var first = values.NodePresenters.Single().Children.First();
            await first.Commands.Single(x => x.Name == MoveItemCommand.CommandName).Execute(first, Tuple.Create(0, 2), null);
            var move = await Game.NextEditAsync();
            Assert.Equal((PlayEditOperation.Move, "Values[0]"), (move.Operation, move.PathText));
            Assert.Equal(PlayPathSegment.Item(2), move.Target);

            var key = table.NodePresenters.Single().Children.Single();
            await key.Commands.Single(x => x.Name == RenameStringKeyCommand.CommandName).Execute(key, "Fast", null);
            var rename = await Game.NextEditAsync();
            Assert.Equal((PlayEditOperation.Move, "Table[Slow]"), (rename.Operation, rename.PathText));
            Assert.Equal("Fast", rename.Target!.KeyText);

            // A new instance is made by the game, of the type chosen
            shape.NodeValue = new LiveEditBox();
            var created = await Game.NextEditAsync();
            Assert.Equal(PlayEditValueKind.NewInstance, created.Value.Kind);
            Assert.Equal(typeof(LiveEditBox).FullName, created.Value.TypeName?.Split(',')[0]);

            // The grid changes nothing itself: the snapshot after the edits shows the collections
            Assert.Equal(new List<int> { 1, 2, 3 }, (List<int>)values.NodeValue!);
            script.Values.Add(0);
            await WindowTestHelper.WaitUntilAsync(() => properties.PendingEdits == 5, "the edits on their way");
            foreach (var edit in new[] { add, remove, move, rename })
                Game.Send(Answer(edit, PlayEditStatus.Applied, null));
            await WindowTestHelper.WaitUntilAsync(() => properties.PendingEdits == 1, "the answers");
            await properties.ApplyAsync(Snapshot(frame + 2));
            Assert.Equal(new List<int> { 1, 2, 3, 0 }, (List<int>)Node(properties, nameof(LiveEditTestScript.Values)).NodeValue!);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task ComponentsAndPlaceholdersStayReadOnlyAndSayWhyAndReferencesCanChange()
    {
        using var fixture = await OpenAsync();
        var properties = await ShowAsync(fixture, PlayerHandle);
        try
        {
            // A reference changes, with the live picker
            var reference = Node(properties, nameof(LiveEditTestScript.Target));
            Assert.False(reference.IsReadOnly);
            Assert.Contains(reference.NodePresenters.Single().Commands, x => x is LivePickupEntityCommand);
            var components = properties.ViewModel!.RootNode.Children.Single(x => x.Name == nameof(Entity.Components));
            Assert.True(components.IsReadOnly);
            Assert.Equal(LiveEditNodeUpdater.ComponentsReason, components[LiveEditData.Tip]);
            Assert.All(components.Children, x => Assert.True(x.IsReadOnly));
            // The transform's values change
            var position = components.Children.Single(x => x.NodeValue is TransformComponent).Children.Single(x => x.Name == nameof(TransformComponent.Position));
            Assert.False(position.IsReadOnly);
            // No command of an asset (paste, replace, the asset pickers) nor of the components: the pickers are the live ones
            var allowed = new HashSet<string>
            {
                CopyPropertyCommand.CommandName, FetchAssetCommand.CommandName, AddNewItemCommand.CommandName, RemoveItemCommand.CommandName,
                AddPrimitiveKeyCommand.CommandName, CreateNewInstanceCommand.CommandName, MoveItemCommand.CommandName, RenameStringKeyCommand.CommandName,
                FlagEnumSelectAllCommand.CommandName, FlagEnumSelectNoneCommand.CommandName, FlagEnumSelectInvertCommand.CommandName,
                PickupAssetCommand.CommandName, PickupEntityCommand.CommandName, PickupEntityComponentCommand.CommandName,
            };
            var names = Descendants(properties.ViewModel.RootNode).SelectMany(x => x.NodePresenters).SelectMany(x => x.Commands).Select(x => x.Name).ToHashSet();
            Assert.Subset(allowed, names);
            var pickers = Descendants(properties.ViewModel.RootNode).SelectMany(x => x.NodePresenters).SelectMany(x => x.Commands)
                .Where(x => x.Name is PickupAssetCommand.CommandName or PickupEntityCommand.CommandName or PickupEntityComponentCommand.CommandName);
            Assert.All(pickers, x => Assert.True(x is LivePickupAssetCommand or LivePickupEntityCommand, x.GetType().Name));
            // The components are added and removed by the game: the transform cannot be removed
            Assert.IsType<LiveAddComponentCommand>(components.NodePresenters.Single().Commands.Single(x => x.Name == AddNewItemCommand.CommandName));
            Assert.DoesNotContain(components.Children.Single(x => x.NodeValue is TransformComponent).NodePresenters.Single().Commands, x => x.Name == RemoveItemCommand.CommandName);
            Assert.IsType<LiveRemoveComponentCommand>(components.Children.Single(x => x.NodeValue is LiveEditTestScript).NodePresenters.Single().Commands.Single(x => x.Name == RemoveItemCommand.CommandName));

            // A member the engine reads once
            Assert.Null(LiveMemberSupport.GetReadOnlyReason(typeof(LiveEditTestScript), nameof(LiveEditTestScript.Once)));
            LiveMemberSupport.AddReadOnly(typeof(LiveEditTestScript).FullName!, nameof(LiveEditTestScript.Once), "Read when the script starts.");
            properties.EditsEnabled = false;
            properties.EditsEnabled = true;
            await WindowTestHelper.WaitUntilAsync(() => properties.ViewModel is not null && Node(properties, nameof(LiveEditTestScript.Once)).IsReadOnly, "the grid built again");
            Assert.Equal("Read when the script starts.", Node(properties, nameof(LiveEditTestScript.Once))[LiveEditData.Tip]);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task ComponentsAreAddedAndRemovedByTheGame()
    {
        using var fixture = await OpenAsync();
        var properties = await ShowAsync(fixture, PlayerHandle);
        try
        {
            var components = properties.ViewModel!.RootNode.Children.Single(x => x.Name == nameof(Entity.Components)).NodePresenters.Single();
            // The list of the Add component button: every type the entity can take, but a second transform
            Assert.True(components.AttachedProperties.TryGetValue(EntityHierarchyData.EntityComponentAvailableTypesKey, out var types));
            Assert.Contains(types, x => x.Type == typeof(ModelComponent));
            Assert.DoesNotContain(types, x => x.Type == typeof(TransformComponent));
            Assert.True(components.AttachedProperties.ContainsKey(EntityHierarchyData.EntityComponentAvailableTypeGroupsKey));

            await components.Commands.Single(x => x.Name == AddNewItemCommand.CommandName).Execute(components, new AbstractNodeType(typeof(ModelComponent)), null);
            var add = await Game.NextStructureEditAsync();
            Assert.Equal((PlayStructureOperation.AddComponent, PlayerHandle, "Stride.Engine.ModelComponent, Stride.Engine"), (add.Operation, add.EntityHandle, add.TypeName));
            Game.Send(new PlayStructureResult(add.Id, frame + 1, PlayEditStatus.Applied, [], Guid.NewGuid(), null));
            await WindowTestHelper.WaitUntilAsync(() => properties.PendingEdits == 0, "the add answered");

            // The game refuses to remove the script: the Output pane says why (PlayModeService)
            var scriptItem = properties.ViewModel!.RootNode.Children.Single(x => x.Name == nameof(Entity.Components)).Children.Single(x => x.NodeValue is LiveEditTestScript).NodePresenters.Single();
            await scriptItem.Commands.Single(x => x.Name == RemoveItemCommand.CommandName).Execute(scriptItem, null, null);
            var remove = await Game.NextStructureEditAsync();
            Assert.Equal((PlayStructureOperation.RemoveComponent, script.Id), (remove.Operation, remove.ComponentId));
            Game.Send(new PlayStructureResult(remove.Id, frame + 1, PlayEditStatus.Failed, [], Guid.Empty, "Another component requires it."));
            await WindowTestHelper.WaitUntilAsync(() => properties.PendingEdits == 0, "the remove answered");
            Assert.Equal(1, properties.RefusedEdits);
            // The rows of the component still change the game
            Node(properties, nameof(LiveEditTestScript.Speed)).NodeValue = 5f;
            Assert.Equal(5f, Read((await Game.NextEditAsync()).Value));
            // Nothing in the undo history, nothing to save
            Assert.False(fixture.Session.ActionHistory.CanUndo);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task WithEditsOffComponentsCannotBeAddedOrRemoved()
    {
        using var fixture = await OpenAsync();
        var properties = await ShowAsync(fixture, PlayerHandle);
        try
        {
            properties.EditsEnabled = false;
            await WindowTestHelper.WaitUntilAsync(() => properties.ViewModel is not null && Node(properties, nameof(LiveEditTestScript.Speed)).IsReadOnly, "the read-only grid");

            var components = properties.ViewModel!.RootNode.Children.Single(x => x.Name == nameof(Entity.Components));
            Assert.DoesNotContain(components.NodePresenters.Single().Commands, x => x.Name == AddNewItemCommand.CommandName);
            Assert.False(components.NodePresenters.Single().AttachedProperties.ContainsKey(EntityHierarchyData.EntityComponentAvailableTypesKey));
            Assert.All(components.Children, x => Assert.DoesNotContain(x.NodePresenters.Single().Commands, c => c.Name == RemoveItemCommand.CommandName));
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task SeveralEntitiesSelectedGetAnEditEach()
    {
        using var fixture = await OpenAsync();
        var properties = await ShowAsync(fixture, PlayerHandle, OtherHandle);
        try
        {
            Node(properties, nameof(LiveEditTestScript.Speed)).NodeValue = 7f;

            var edits = new[] { await Game.NextEditAsync(), await Game.NextEditAsync() };
            Assert.Equal([PlayerHandle, OtherHandle], edits.Select(x => x.EntityHandle).Order());
            Assert.Equal([script.Id, otherScript.Id], edits.OrderBy(x => x.EntityHandle).Select(x => x.ComponentId));
            Assert.All(edits, x => Assert.Equal(7f, Read(x.Value)));
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task WithEditsOffNothingGoesToTheGame()
    {
        using var fixture = await OpenAsync();
        var properties = await ShowAsync(fixture, PlayerHandle);
        try
        {
            properties.EditsEnabled = false;
            await WindowTestHelper.WaitUntilAsync(() => properties.ViewModel is not null && Node(properties, nameof(LiveEditTestScript.Speed)).IsReadOnly, "the read-only grid");
            Assert.Equal("Edits are off: the values are read-only.", properties.LiveNotice);

            Node(properties, nameof(LiveEditTestScript.Speed)).NodePresenters.Single().UpdateValue(3f);
            await Task.Delay(100);

            Assert.True(Game.Edits.IsEmpty);
            Assert.Equal(1, properties.RefusedWrites);
            Assert.Equal(1f, Node(properties, nameof(LiveEditTestScript.Speed)).NodeValue);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task AStopDuringAnEditWritesNothingAndRefusesNothing()
    {
        using var fixture = await OpenAsync();
        var properties = await ShowAsync(fixture, PlayerHandle);
        try
        {
            var speed = Node(properties, nameof(LiveEditTestScript.Speed));
            speed.NodeValue = 9f;
            await Game.NextEditAsync();
            Game.ExitOnQuit = true;

            await session.StopAsync();

            await WindowTestHelper.WaitUntilAsync(() => properties.PendingEdits == 0, "the end of the edit");
            Assert.Null(State(speed));
            Assert.Equal(0, properties.RefusedEdits);
            // Nothing more for the game, which is gone, and nothing for the assets
            speed.NodeValue = 10f;
            Assert.Equal(1, properties.SentEdits);
            Assert.False(fixture.Session.ActionHistory.CanUndo);
            Assert.False(fixture.Session.HasUnsavedChanges);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task ALiveEditIsNotInTheUndoHistoryAndUndoDoesNotReachTheGame()
    {
        using var fixture = await OpenAsync();
        var model = fixture.Asset(ModelLocation);
        var history = fixture.Session.ActionHistory;
        // A change of an asset first, that Undo reverts
        using (fixture.Session.UndoRedoService.CreateTransaction())
            model.PropertyGraph!.RootNode[nameof(ModelAsset.Source)].Update(new UFile("box.fbx"));
        await WindowTestHelper.WaitUntilAsync(() => history.Transactions.Count == 1, "the asset's history entry");
        var properties = await ShowAsync(fixture, PlayerHandle);
        try
        {
            var speed = Node(properties, nameof(LiveEditTestScript.Speed));
            speed.NodeValue = 6f;
            var edit = await Game.NextEditAsync();
            Game.Send(Answer(edit, PlayEditStatus.Applied, 6f));
            await WindowTestHelper.WaitUntilAsync(() => State(speed) is null, "the answer");
            await Task.Delay(100);
            await WindowTestHelper.PumpAsync();

            // The live edit added nothing to the history, made no asset dirty
            Assert.Single(history.Transactions);
            Assert.DoesNotContain(fixture.Session.AllAssets, x => x != model && x.IsDirty);

            // Undo reverts the asset's change, not the game's value, and sends nothing to the game; Redo the same
            history.UndoCommand.Execute();
            Assert.NotEqual("box.fbx", ((ModelAsset)model.Asset).Source?.ToString());
            history.RedoCommand.Execute();
            Assert.Equal("box.fbx", ((ModelAsset)model.Asset).Source?.ToString());
            await Task.Delay(100);
            Assert.True(Game.Edits.IsEmpty);
            Assert.Equal(6f, speed.NodeValue);
            Assert.Equal(1, properties.SentEdits);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    private static Task<ShellSessionFixture> OpenAsync() => ShellSessionFixture.OpenAsync((ModelLocation, new ModelAsset()));

    private async Task<LivePropertiesViewModel> ShowAsync(ShellSessionFixture fixture, params int[] shown)
    {
        var properties = new LivePropertiesViewModel(fixture.Session) { Throttle = new LiveRefreshThrottle { Budget = TimeSpan.FromSeconds(10) } };
        properties.Show(session, shown);
        await Game.NextSubscribeAsync();
        await WindowTestHelper.PumpAsync();
        await properties.ApplyAsync(Snapshot());
        await WindowTestHelper.PumpAsync();
        return properties;
    }

    private LiveSnapshot Snapshot(long? at = null)
    {
        frame = at ?? frame + 1;
        var snapshot = new PlaySnapshot(frame, frame / 60.0, [PlaySnapshotWriter.Write(player, x => handles.GetValueOrDefault(x)), PlaySnapshotWriter.Write(other, x => handles.GetValueOrDefault(x))]);
        return new LiveMirrorReader(new PlayAssemblyCheck()).Read(snapshot);
    }

    // The game answers at its next frame
    private PlayEditResult Answer(PlayEdit edit, PlayEditStatus status, object? readBack, string? error = null)
        => new(edit.Id, frame + 1, status, readBack is null ? PlayEditValue.Null : PlayEditValue.Of(PlayMirrorSerializer.WriteValue(readBack, _ => 0)), error);

    private static object? Read(PlayEditValue value) => PlayMirrorSerializer.ReadValue(value.Payload, _ => null);

    private static object? State(NodeViewModel node)
        => node.NodePresenters.Single().AttachedProperties.TryGetValue(LiveEditData.StateKey, out var state) ? state : null;

    private static NodeViewModel Node(LivePropertiesViewModel properties, string member)
    {
        var viewModel = properties.ViewModel ?? throw new InvalidOperationException("The grid is being built.");
        var components = viewModel.RootNode.Children.Single(x => x.Name == nameof(Entity.Components));
        return components.Children.First(x => x.NodeValue is LiveEditTestScript).Children.Single(x => x.Name == member);
    }

    private static IEnumerable<NodeViewModel> Descendants(NodeViewModel node) => node.Children.SelectMany(x => Descendants(x).Prepend(x));

    private static async Task<PropertyViewItem> ItemAsync(Window window, Func<NodeViewModel, bool> match)
    {
        IEnumerable<PropertyViewItem> Items() => window.GetVisualDescendants().OfType<PropertyViewItem>();
        await WindowTestHelper.WaitUntilAsync(() => Items().Any(x => x.DataContext is NodeViewModel node && match(node)), "the property");
        return Items().First(x => x.DataContext is NodeViewModel node && match(node));
    }

    /// <summary>
    /// A game's process that the test drives: it connects to the session's pipe as the agent does, keeps the edits it receives,
    /// and answers what the test sends.
    /// </summary>
    private sealed class EditGame(IReadOnlyDictionary<string, string> environment) : IPlayGameProcess, IAsyncDisposable
    {
        private readonly TaskCompletionSource<int> exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentQueue<PlaySubscribe> subscribes = new();
        private PlayConnection? connection;

        public int Id => 4321;

        public IReadOnlyDictionary<string, string> Environment => environment;

        public Task<int> Exited => exited.Task;

        public bool ExitOnQuit { get; set; }

        public ConcurrentQueue<PlayEdit> Edits { get; } = new();

        public ConcurrentQueue<PlayStructureEdit> StructureEdits { get; } = new();

        public async Task<PlayStructureEdit> NextStructureEditAsync()
        {
            PlayStructureEdit? next = null;
            await WindowTestHelper.WaitUntilAsync(() => StructureEdits.TryDequeue(out next), "a structure command");
            return next!;
        }

        public async Task ConnectAsync()
        {
            var pipe = new NamedPipeClientStream(".", environment[PlayProtocol.PipeVariable], PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            connection = new PlayConnection(pipe);
            connection.Start(message =>
            {
                switch (message)
                {
                    case PlaySubscribe subscribe:
                        subscribes.Enqueue(subscribe);
                        break;
                    case PlayEdit edit:
                        Edits.Enqueue(edit);
                        break;
                    case PlayStructureEdit command:
                        StructureEdits.Enqueue(command);
                        break;
                    case PlayQuit when ExitOnQuit:
                        Exit(0);
                        break;
                }
            });
        }

        public void Send(PlayMessage message) => Assert.True(connection?.Send(message));

        public async Task<PlayEdit> NextEditAsync()
        {
            PlayEdit? next = null;
            await WindowTestHelper.WaitUntilAsync(() => Edits.TryDequeue(out next), "an edit");
            return next!;
        }

        public async Task<PlaySubscribe> NextSubscribeAsync()
        {
            PlaySubscribe? next = null;
            await WindowTestHelper.WaitUntilAsync(() => subscribes.TryDequeue(out next), "a request for snapshots");
            return next!;
        }

        public void Exit(int exitCode) => exited.TrySetResult(exitCode);

        public void Kill() => Exit(-1);

        public async ValueTask DisposeAsync()
        {
            Exit(-1);
            if (connection is not null)
                await connection.DisposeAsync();
        }
    }
}

/// <summary>
/// A game's script whose values the edit tests change.
/// </summary>
[DataContract]
public sealed class LiveEditTestScript : SyncScript
{
    public float Speed { get; set; } = 1;

    public float Stamina { get; set => field = Math.Clamp(value, 0, 1); } = 1;

    public int Lives { get; set => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "Lives cannot be negative."); } = 3;

    public int Count { get; set; } = 3;

    public int Once { get; set; }

    public List<int> Values { get; } = [1, 2, 3];

    public Dictionary<string, float> Table { get; } = new() { ["Slow"] = 1 };

    public LiveEditShape? Shape { get; set; }

    public Entity? Target { get; set; }

    public override void Update()
    {
    }
}

/// <summary>
/// An abstract type, for new instances.
/// </summary>
[DataContract]
public abstract class LiveEditShape
{
    public float Size { get; set; } = 1;
}

/// <summary>
/// A shape.
/// </summary>
[DataContract]
public sealed class LiveEditBox : LiveEditShape
{
    public Vector3 Extent { get; set; } = Vector3.One;
}
