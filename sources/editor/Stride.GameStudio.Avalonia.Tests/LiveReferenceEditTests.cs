// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Stride.Assets.Editor.Avalonia.Views;
using Stride.Assets.Editor.PlayMode;
using Stride.Assets.Editor.Quantum.NodePresenters.Commands;
using Stride.Assets.Editor.Quantum.NodePresenters.Keys;
using Stride.Assets.Materials;
using Stride.Assets.Models;
using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Quantum.NodePresenters.Commands;
using Stride.Core.Assets.Editor.Quantum.NodePresenters.Keys;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Presentation.Quantum.Presenters;
using Stride.Core.Presentation.Quantum.ViewModels;
using Stride.Core.Presentation.ViewModels;
using Stride.Core.Serialization;
using Stride.Editor.PlayMode;
using Stride.Engine;
using Stride.GameStudio.Avalonia.Tests.Helpers;
using Stride.PlayMode.Agent;
using Stride.PlayMode.Protocol;
using Stride.PlayMode.Snapshots;
using Stride.Rendering;

namespace Stride.GameStudio.Avalonia.Tests;

/// <summary>
/// Live references set from the property grid: content picked among the assets of the game's build, entities and components
/// picked among the live entities, each sent to the fake game of <see cref="LiveEditTests"/>.
/// </summary>
public sealed partial class LiveEditTests
{
    private const string SphereLocation = "Models/Sphere";
    private const string UnusedLocation = "Models/Unused";
    private const string MaterialLocation = "Materials/Red";
    private const int SceneHandle = 10;

    private readonly LiveReferenceTestScript references = new();

    [AvaloniaFact]
    public async Task ThePickerOffersTheBuildsAssetsOfTheTypeAndThePickGoesToTheGameByUrl()
    {
        using var fixture = await OpenReferencesAsync();
        var dialogs = fixture.ReplaceDialogService();
        var properties = await ShowReferencesAsync(fixture, PlayerHandle);
        try
        {
            var model = ReferenceNode(properties, nameof(LiveReferenceTestScript.Model));
            Assert.False(model.IsReadOnly);
            dialogs.PickAsset = picker => picker.FilteredAssets.Single(x => x.Url.EndsWith(SphereLocation, StringComparison.Ordinal));

            await PickAsync(model, PickupAssetCommand.CommandName, typeof(Model));

            // The models are listed, those of the build first; the material is not
            var picker = dialogs.AssetPickers.Single();
            Assert.Equal(["Box", "Sphere", "Unused"], picker.FilteredAssets.Select(x => x.Name));
            Assert.All(picker.FilteredAssets.Take(2), x => Assert.Null(picker.GetDisabledReason(x)));
            Assert.Equal(LiveReferenceContext.NotInBuildReason, picker.GetDisabledReason(picker.FilteredAssets[2]));
            picker.SelectedAsset = picker.FilteredAssets[2];
            Assert.False(picker.CanPick);

            // The game gets the URL of its build
            var edit = await Game.NextEditAsync();
            Assert.Equal("Model", edit.PathText);
            Assert.Equal(SphereLocation, AttachedReferenceManager.GetUrl(Read(edit.Value)!));
            Assert.Equal(LiveEditData.Pending, State(model));

            // It reads back the content it loaded, which the grid shows as the session's asset
            Game.Send(ReferenceAnswer(edit, PlayEditStatus.Applied, Proxy<Model>(SphereLocation)));
            await WindowTestHelper.WaitUntilAsync(() => State(model) is null, "the answer");
            var shown = AttachedReferenceManager.GetAttachedReference(model.NodeValue)!;
            Assert.Equal(SphereLocation, shown.Url);
            Assert.Equal(fixture.Asset(SphereLocation).Id, shown.Id);

            // The snapshot after the edit shows the same content: nothing is written again
            references.Model = Proxy<Model>(SphereLocation);
            var applied = await properties.ApplyAsync(Snapshot(frame + 2));
            Assert.False(applied.Rebuilt);
            Assert.Equal(0, applied.Entities.Single(x => x.Handle == PlayerHandle).Result.WrittenValues);
            Assert.False(fixture.Session.ActionHistory.CanUndo);
            Assert.False(fixture.Session.HasUnsavedChanges);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task AnAssetDroppedOnAReferenceIsAcceptedOnlyFromTheBuildAndWithItsType()
    {
        using var fixture = await OpenReferencesAsync();
        var properties = await ShowReferencesAsync(fixture, PlayerHandle);
        try
        {
            var material = ReferenceNode(properties, nameof(LiveReferenceTestScript.Material));
            var drop = Assert.IsType<LiveContentReferenceViewModel>(material.NodePresenters.Single().AttachedProperties.Get(ReferenceData.Key));
            drop.SetTargetNode(material);

            Assert.False(drop.CanAddChildren([fixture.Asset(SphereLocation)], AddChildModifiers.None, out var wrongType));
            Assert.Equal("Incompatible asset", wrongType);
            Assert.True(drop.CanAddChildren([fixture.Asset(MaterialLocation)], AddChildModifiers.None, out _));
            var model = ReferenceNode(properties, nameof(LiveReferenceTestScript.Model));
            var modelDrop = (LiveContentReferenceViewModel)model.NodePresenters.Single().AttachedProperties.Get(ReferenceData.Key);
            modelDrop.SetTargetNode(model);
            Assert.False(modelDrop.CanAddChildren([fixture.Asset(UnusedLocation)], AddChildModifiers.None, out var notBuilt));
            Assert.Equal(LiveReferenceContext.NotInBuildReason, notBuilt);

            drop.AddChildren([fixture.Asset(MaterialLocation)], AddChildModifiers.None);

            var edit = await Game.NextEditAsync();
            Assert.Equal(MaterialLocation, AttachedReferenceManager.GetUrl(Read(edit.Value)!));
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task AReferenceToContentIsClearedAndAnItemOfAListOfContentIsSet()
    {
        using var fixture = await OpenReferencesAsync();
        var dialogs = fixture.ReplaceDialogService();
        var properties = await ShowReferencesAsync(fixture, PlayerHandle);
        try
        {
            // Cleared: the null reference goes
            var model = ReferenceNode(properties, nameof(LiveReferenceTestScript.Model));
            model.NodeValue = null;
            var cleared = await Game.NextEditAsync();
            Assert.Equal(PlayEditValueKind.Null, cleared.Value.Kind);
            Game.Send(ReferenceAnswer(cleared, PlayEditStatus.Applied, null));
            await WindowTestHelper.WaitUntilAsync(() => State(model) is null, "the answer");
            Assert.Null(model.NodeValue);

            // An item of a list: picked as any reference
            var item = ReferenceNode(properties, nameof(LiveReferenceTestScript.Models)).Children.Single();
            dialogs.PickAsset = picker => picker.FilteredAssets.Single(x => x.Name == "Sphere");
            await PickAsync(item, PickupAssetCommand.CommandName, typeof(Model));
            var set = await Game.NextEditAsync();
            Assert.Equal(("Models[0]", PlayEditOperation.Set), (set.PathText, set.Operation));
            Assert.Equal(SphereLocation, AttachedReferenceManager.GetUrl(Read(set.Value)!));
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task AnItemAddedToAListOfEntitiesIsEmptyUntilItIsPicked()
    {
        using var fixture = await OpenReferencesAsync();
        var properties = await ShowReferencesAsync(fixture, PlayerHandle);
        try
        {
            // The add command makes a new entity, which no running scene holds: the item goes empty
            ReferenceNode(properties, nameof(LiveReferenceTestScript.Watched)).NodePresenters.Single().AddItem(new Entity("New"));
            var add = await Game.NextEditAsync();
            Assert.Equal((PlayEditOperation.Add, "Watched", PlayEditValueKind.Null), (add.Operation, add.PathText, add.Value.Kind));
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task ContentTheGameMadeAtRunTimeCannotBeSentAndSaysWhy()
    {
        using var fixture = await OpenReferencesAsync();
        var properties = await ShowReferencesAsync(fixture, PlayerHandle);
        try
        {
            var model = ReferenceNode(properties, nameof(LiveReferenceTestScript.Model));
            var sent = properties.SentEdits;

            model.NodeValue = new Model();

            Assert.Equal(sent, properties.SentEdits);
            Assert.Equal(1, properties.RefusedWrites);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task AnEntityIsPickedFromTheLiveTreeAndAComponentThroughItsEntity()
    {
        using var fixture = await OpenReferencesAsync();
        var pickers = ReplaceLivePicker(fixture, (picker, _) =>
        {
            picker.SelectedRow = picker.Rows.Single(x => x.Entity?.Handle == OtherHandle);
            return Task.FromResult(picker.Pick);
        });
        var properties = await ShowReferencesAsync(fixture, PlayerHandle);
        try
        {
            // An entity: by its handle
            var targetNode = ReferenceNode(properties, nameof(LiveReferenceTestScript.Target));
            await PickAsync(targetNode, PickupEntityCommand.CommandName, Tuple.Create<object?, object?>(null, null));
            var picker = pickers.Pickers.Single();
            Assert.Null(picker.ComponentType);
            Assert.Equal(["Main", "Player", "Target", "Other"], picker.Rows.Select(x => x.Name));
            Assert.False(picker.Rows[0].IsPickable);
            var edit = await Game.NextEditAsync();
            var sent = Assert.Single(PlayMirrorSerializer.ReadReferences(edit.Value.Payload));
            Assert.Equal((PlayMirrorReferenceKind.Entity, OtherHandle), (sent.Kind, sent.EntityHandle));
            Assert.Equal("Other", ((Entity)targetNode.NodeValue!).Name);

            Game.Send(ReferenceAnswer(edit, PlayEditStatus.Applied, other));
            await WindowTestHelper.WaitUntilAsync(() => State(targetNode) is null, "the answer");
            Assert.Equal("Other", ((Entity)targetNode.NodeValue!).Name);

            // The snapshot that follows the edit: the same live entity, nothing written again
            references.Target = other;
            var applied = await properties.ApplyAsync(Snapshot(frame + 2));
            Assert.False(applied.Rebuilt);
            Assert.Equal(0, applied.Entities.Single().Result.WrittenValues);

            // A component: the entity, then its component of the reference's type (the only one is picked with it)
            var pointer = ReferenceNode(properties, nameof(LiveReferenceTestScript.Pointer));
            await PickAsync(pointer, PickupEntityComponentCommand.CommandName, Tuple.Create<object?, object?>(null, typeof(TransformComponent)));
            Assert.Equal(typeof(TransformComponent), pickers.Pickers[1].ComponentType);
            var componentEdit = await Game.NextEditAsync();
            var component = Assert.Single(PlayMirrorSerializer.ReadReferences(componentEdit.Value.Payload));
            Assert.Equal((PlayMirrorReferenceKind.Component, OtherHandle, other.Transform.Id), (component.Kind, component.EntityHandle, component.ComponentId));
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task ThePickerOfAComponentListsTheEntitiesWithoutOneDisabled()
    {
        using var fixture = await OpenReferencesAsync();
        var properties = await ShowReferencesAsync(fixture, PlayerHandle);
        try
        {
            var picker = new LiveEntityPickerViewModel(fixture.Session.ServiceProvider, properties.Tree!, typeof(LiveEditTestScript));

            // Player and Other have the script; Target has only its transform
            Assert.Equal("Target has no LiveEditTestScript component.", picker.Rows.Single(x => x.Name == "Target").DisabledReason);
            picker.SelectedRow = picker.Rows.Single(x => x.Name == "Target");
            Assert.Null(picker.SelectedRow);
            picker.SelectedRow = picker.Rows.Single(x => x.Name == "Other");
            Assert.True(picker.CanPick);
            Assert.Equal(otherScript.Id, picker.Pick!.Component!.Id);

            // The search narrows the entities, under their scene
            picker.SearchText = "play";
            Assert.Equal(["Main", "Player"], picker.Rows.Select(x => x.Name));
            Assert.Null(picker.SelectedRow);

            // An entity that leaves the game leaves the picker
            picker.SearchText = string.Empty;
            picker.SelectedRow = picker.Rows.Single(x => x.Name == "Other");
            properties.Tree!.Apply(new PlayTreeBatch(frame + 1, [new PlayEntityRemoved(OtherHandle)]));
            Assert.DoesNotContain(picker.Rows, x => x.Name == "Other");
            Assert.Null(picker.SelectedRow);
            Assert.False(picker.CanPick);
            picker.Destroy();
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task ThePickerWindowDisablesTheScenesAndClosesOnAPick()
    {
        using var fixture = await OpenReferencesAsync();
        var properties = await ShowReferencesAsync(fixture, PlayerHandle);
        var owner = new Window();
        owner.Show();
        try
        {
            var picker = new LiveEntityPickerViewModel(fixture.Session.ServiceProvider, properties.Tree!, typeof(TransformComponent), initialHandle: OtherHandle);
            var window = new LiveEntityPickerWindow { DataContext = picker };
            var closed = window.ShowDialog<bool>(owner);
            await WindowTestHelper.PumpAsync();

            var items = window.GetVisualDescendants().OfType<ListBoxItem>().Where(x => x.DataContext is LivePickerRow).ToList();
            Assert.Equal(["Main", "Player", "Target", "Other"], items.Select(x => ((LivePickerRow)x.DataContext!).Name));
            Assert.False(items[0].IsEnabled);
            Assert.True(items[3].IsEnabled);
            // The current target is selected, with its only transform
            Assert.Equal(OtherHandle, picker.SelectedRow?.Entity?.Handle);
            Assert.True(picker.CanPick);

            window.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "OkButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(await closed);
            Assert.True(window.Picked);
            Assert.Equal(other.Transform.Id, picker.Pick!.Component!.Id);
            picker.Destroy();
        }
        finally
        {
            owner.Close();
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task AReferenceToAnEntityThatLeftTheGameIsAnsweredGoneAndComesBack()
    {
        using var fixture = await OpenReferencesAsync();
        ReplaceLivePicker(fixture, (picker, _) =>
        {
            picker.SelectedRow = picker.Rows.Single(x => x.Entity?.Handle == OtherHandle);
            return Task.FromResult(picker.Pick);
        });
        var properties = await ShowReferencesAsync(fixture, PlayerHandle);
        try
        {
            var targetNode = ReferenceNode(properties, nameof(LiveReferenceTestScript.Target));
            Assert.Equal("Target", ((Entity)targetNode.NodeValue!).Name);
            await PickAsync(targetNode, PickupEntityCommand.CommandName, Tuple.Create<object?, object?>(null, null));
            var edit = await Game.NextEditAsync();

            Game.Send(new PlayEditResult(edit.Id, frame + 1, PlayEditStatus.Gone, PlayEditValue.Null, "Other is no longer in the game."));

            await WindowTestHelper.WaitUntilAsync(() => Equals(State(targetNode), LiveEditData.Refused), "the answer");
            Assert.Equal("Target", ((Entity)targetNode.NodeValue!).Name);
            Assert.Contains("Other is no longer in the game.", (string)targetNode[LiveEditData.Tip]!);
            Assert.Equal(1, properties.RefusedEdits);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task SeveralEntitiesSelectedGetAReferenceEach()
    {
        using var fixture = await OpenReferencesAsync();
        otherScript.Target = player;
        var properties = await ShowReferencesAsync(fixture, PlayerHandle, OtherHandle);
        try
        {
            // The combined property of the two scripts
            var targetNode = Node(properties, nameof(LiveEditTestScript.Target));
            ReplaceLivePicker(fixture, (picker, _) =>
            {
                picker.SelectedRow = picker.Rows.Single(x => x.Entity?.Handle == 2);
                return Task.FromResult(picker.Pick);
            });

            await PickAsync(targetNode, PickupEntityCommand.CommandName, Tuple.Create<object?, object?>(null, null));

            var edits = new[] { await Game.NextEditAsync(), await Game.NextEditAsync() };
            Assert.Equal([PlayerHandle, OtherHandle], edits.Select(x => x.EntityHandle).Order());
            Assert.All(edits, x => Assert.Equal(2, PlayMirrorSerializer.ReadReferences(x.Value.Payload).Single().EntityHandle));
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task AStopDuringAPickClosesThePickerAndWritesNothing()
    {
        using var fixture = await OpenReferencesAsync();
        var dialogs = fixture.ReplaceDialogService();
        var cancelled = new TaskCompletionSource();
        dialogs.PickAssetAsync = async (picker, token) =>
        {
            // The picker stays open until the game stops, then closes without a pick
            await Task.Delay(Timeout.Infinite, token).ContinueWith(_ => { }, TaskScheduler.Default);
            cancelled.TrySetResult();
            return picker.FilteredAssets.First();
        };
        var properties = await ShowReferencesAsync(fixture, PlayerHandle);
        try
        {
            var model = ReferenceNode(properties, nameof(LiveReferenceTestScript.Model));
            var pick = PickAsync(model, PickupAssetCommand.CommandName, typeof(Model));
            await WindowTestHelper.WaitUntilAsync(() => dialogs.AssetPickers.Count == 1, "the picker");
            Game.ExitOnQuit = true;

            await session.StopAsync();
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await pick.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(0, properties.SentEdits);
            Assert.Equal(0, properties.RefusedWrites);
            Assert.Empty(Game.Edits);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    private static Task<ShellSessionFixture> OpenReferencesAsync()
        => ShellSessionFixture.OpenAsync((ModelLocation, new ModelAsset()), (SphereLocation, new ModelAsset()), (UnusedLocation, new ModelAsset()), (MaterialLocation, new MaterialAsset()));

    private async Task<LivePropertiesViewModel> ShowReferencesAsync(ShellSessionFixture fixture, params int[] shown)
    {
        // The model of the box, a list of one, the entity of the test script
        references.Model = Proxy<Model>(ModelLocation);
        references.Models.Add(Proxy<Model>(ModelLocation));
        references.Target = target;
        if (!player.Components.Contains(references))
            player.Add(references);
        // The game's build: all but the unused model
        Game.Send(new PlayContentIndex([ModelLocation, SphereLocation, MaterialLocation, "Scenes/Main"]));
        await WindowTestHelper.WaitUntilAsync(() => session.BuildContent is not null, "the content of the build");
        var properties = await ShowAsync(fixture, shown);
        var tree = new LiveTree();
        tree.Reset(new PlayTree(frame, [new PlaySceneInfo(SceneHandle, Guid.NewGuid(), "Main", "Scenes/Main", 0)], [Info(player, PlayerHandle), Info(target, 2), Info(other, OtherHandle)]));
        properties.Tree = tree;
        return properties;
    }

    private static PlayEntityInfo Info(Entity entity, int handle)
        => new(handle, entity.Id, entity.Name, true, 0, SceneHandle, [.. entity.Components.Select(x => new PlayComponentInfo(x.Id, PlayMirrorSerializer.TypeNameOf(x.GetType())))]);

    private static T Proxy<T>(string url) where T : class, new() => AttachedReferenceManager.CreateProxyObject<T>(AssetId.Empty, url);

    // The game reads the reference back, with the handles of its entities
    private PlayEditResult ReferenceAnswer(PlayEdit edit, PlayEditStatus status, object? readBack)
        => new(edit.Id, frame + 1, status, readBack is null ? PlayEditValue.Null : PlayEditValue.Of(PlayMirrorSerializer.WriteValue(readBack, x => handles.GetValueOrDefault(x))), null);

    // As the view runs a command: picked once, then set on each entity of the selection
    private static async Task PickAsync(NodeViewModel node, string command, object? parameter)
    {
        var presenters = node.NodePresenters;
        var picker = presenters.First().Commands.Single(x => x.Name == command);
        Assert.All(presenters, x => Assert.Contains(picker, x.Commands));
        var picked = await picker.PreExecute(presenters, parameter);
        foreach (var presenter in presenters)
            await picker.Execute(presenter, parameter, picked);
        await picker.PostExecute(presenters, parameter);
        await WindowTestHelper.PumpAsync();
    }

    private static FakeLivePicker ReplaceLivePicker(ShellSessionFixture fixture, Func<LiveEntityPickerViewModel, CancellationToken, Task<LiveEntityPick?>> pick)
    {
        var services = (ViewModelServiceProvider)fixture.Session.ServiceProvider;
        while (services.TryGet<ILiveEntityPickerService>() is { } existing)
        {
            services.UnregisterService(existing);
            if (services.TryGet<ILiveEntityPickerService>() == existing)
                break;
        }
        var fake = new FakeLivePicker(pick);
        services.RegisterService(fake);
        return fake;
    }

    private static NodeViewModel ReferenceNode(LivePropertiesViewModel properties, string member)
    {
        var viewModel = properties.ViewModel ?? throw new InvalidOperationException("The grid is being built.");
        var components = viewModel.RootNode.Children.Single(x => x.Name == nameof(Entity.Components));
        return components.Children.First(x => x.NodeValue is LiveReferenceTestScript).Children.Single(x => x.Name == member);
    }

    private sealed class FakeLivePicker(Func<LiveEntityPickerViewModel, CancellationToken, Task<LiveEntityPick?>> pick) : ILiveEntityPickerService
    {
        public List<LiveEntityPickerViewModel> Pickers { get; } = [];

        public Task<LiveEntityPick?> PickAsync(LiveEntityPickerViewModel picker, CancellationToken cancellation)
        {
            Pickers.Add(picker);
            return pick(picker, cancellation);
        }
    }
}

/// <summary>
/// A game's script whose references the edit tests change.
/// </summary>
[DataContract]
public sealed class LiveReferenceTestScript : SyncScript
{
    public Model? Model { get; set; }

    public Material? Material { get; set; }

    public List<Model> Models { get; } = [];

    public Entity? Target { get; set; }

    public List<Entity?> Watched { get; } = [];

    public TransformComponent? Pointer { get; set; }

    public override void Update()
    {
    }
}
