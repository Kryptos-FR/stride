// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Stride.Assets.Editor.Avalonia.Views;
using Stride.Assets.Editor.PlayMode;
using Stride.Assets.Editor.Quantum.NodePresenters.Keys;
using Stride.Assets.Entities;
using Stride.Assets.Models;
using Stride.Core.Assets;
using Stride.Core.Assets.Quantum;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Presentation.Quantum.ViewModels;
using Stride.Core.Presentation.ViewModels;
using Stride.Core.Quantum;
using Stride.Core.Serialization;
using Stride.Editor.PlayMode;
using Stride.Engine;
using Stride.GameStudio.Avalonia.Tests.Helpers;
using Stride.PlayMode.Protocol;
using Stride.PlayMode.Snapshots;
using Stride.Rendering.Lights;

namespace Stride.GameStudio.Avalonia.Tests;

/// <summary>
/// Apply to asset: the values of the running game copied into the scene asset whose entities have the same ids (the scene the
/// game started from), from the live grid and from the Live tab.
/// </summary>
public sealed partial class LiveEditTests
{
    private const string SceneLocation = "Scenes/Main";

    [AvaloniaFact]
    public async Task ApplyToAssetSetsTheSceneAssetValueInOneUndoStepAndSendsNothingToTheGame()
    {
        using var fixture = await OpenWithSceneAsync(player, target);
        var properties = await ShowWithTreeAsync(fixture, PlayerHandle);
        try
        {
            // The game changed a value itself
            script.Speed = 4;
            await properties.ApplyAsync(Snapshot());
            await WindowTestHelper.PumpAsync();
            var speed = Node(properties, nameof(LiveEditTestScript.Speed));
            var lives = Node(properties, nameof(LiveEditTestScript.Lives));
            Assert.True(speed.AssociatedData.ContainsKey(LiveEditData.ApplyToAsset));

            // The same value as the asset's: nothing to apply, and the menu says why
            var same = properties.GetApplyState(lives.NodePresenters);
            Assert.False(same.CanApply);
            Assert.Equal(LiveAssetApplier.SameAsAssetReason, same.Reason);
            var state = properties.GetApplyState(speed.NodePresenters);
            Assert.True(state.CanApply);
            Assert.Null(state.Reason);
            var scene = fixture.Asset(SceneLocation);
            Assert.Equal(1, properties.ApplyToAsset(speed.NodePresenters));

            var asset = AssetScript(fixture, player);
            Assert.Equal(4, asset.Speed);
            Assert.True(scene.IsDirty);
            var undoRedo = fixture.Session.UndoRedoService!;
            Assert.Equal("Apply to asset: Speed of Player", undoRedo.GetName(undoRedo.RetrieveAllTransactions().Last()));
            // Applied: the same now
            Assert.Equal(LiveAssetApplier.SameAsAssetReason, properties.GetApplyState(speed.NodePresenters).Reason);

            undoRedo.Undo();
            Assert.Equal(1, asset.Speed);
            undoRedo.Redo();
            Assert.Equal(4, asset.Speed);
            // The game keeps its value: nothing was sent
            await WindowTestHelper.PumpAsync();
            Assert.Empty(Game.Edits);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [Fact]
    public void TheListNamesThePropertiesAsTheGridDoes()
    {
        // The display attribute of each member, split as the grid splits names, walked on the live instance of an abstract member
        var light = new LightComponent { Type = new LightDirectional() };
        Assert.Equal("Light", LiveAssetApplier.ComponentDisplayName(typeof(LightComponent)));
        Assert.Equal("Live Edit Test Script", LiveAssetApplier.ComponentDisplayName(typeof(LiveEditTestScript)));
        Assert.Equal("Light > Color", LiveAssetApplier.DisplayText(light, [LiveApplyStep.Of(nameof(LightComponent.Type)), LiveApplyStep.Of(nameof(LightDirectional.Color))]));
        Assert.Equal("Values[1]", LiveAssetApplier.DisplayText(new LiveEditTestScript(), [LiveApplyStep.Of(nameof(LiveEditTestScript.Values)), LiveApplyStep.At(new NodeIndex(1))]));
        Assert.Equal("Table[Slow]", LiveAssetApplier.DisplayText(new LiveEditTestScript(), [LiveApplyStep.Of(nameof(LiveEditTestScript.Table)), LiveApplyStep.At(new NodeIndex("Slow"))]));
        Assert.Equal(string.Empty, LiveAssetApplier.DisplayText(light, []));
    }

    [AvaloniaFact]
    public async Task AnEntityReferenceGoesToTheCounterpartInTheSameSceneAsset()
    {
        using var fixture = await OpenWithSceneAsync(player, target);
        var properties = await ShowWithTreeAsync(fixture, PlayerHandle);
        try
        {
            var targetNode = Node(properties, nameof(LiveEditTestScript.Target));
            Assert.Null(AssetScript(fixture, player).Target);

            Assert.Equal(1, properties.ApplyToAsset(targetNode.NodePresenters));

            // The asset's own entity of the same id, not the game's copy
            var assetTarget = SceneAsset(fixture).Hierarchy.Parts[target.Id].Entity;
            Assert.Same(assetTarget, AssetScript(fixture, player).Target);

            // A reference to an entity added while the game runs has nothing to go to in the asset: off, with the reason
            AssetScript(fixture, player).Target = null;
            properties.Tree!.IsAddedWhilePlaying = handle => handle == 2;
            properties.Tree.Reset(TreeOf(fixture));
            var state = properties.GetApplyState(targetNode.NodePresenters);
            Assert.False(state.CanApply);
            Assert.Contains("was added while the game runs", state.Reason, StringComparison.Ordinal);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task ContentGoesBackByUrlAndAListThatChangedLengthIsAppliedWhole()
    {
        player.Add(references);
        using var fixture = await OpenWithSceneAsync(player, target);
        var properties = await ShowReferencesAsync(fixture, PlayerHandle);
        try
        {
            properties.Tree = TreeWithCounterparts(fixture);
            var asset = SceneAsset(fixture).Hierarchy.Parts[player.Id].Entity.Get<LiveReferenceTestScript>()!;

            // The model: the reference of the session's asset of the same URL
            Assert.Equal(1, properties.ApplyToAsset(ReferenceNode(properties, nameof(LiveReferenceTestScript.Model)).NodePresenters));
            Assert.Equal(fixture.Asset(ModelLocation).Url, AttachedReferenceManager.GetUrl(asset.Model!));
            Assert.Equal(fixture.Asset(ModelLocation).Id, AttachedReferenceManager.GetAttachedReference(asset.Model!)!.Id);

            // One model in the game, none in the asset: the items cannot be matched, the list goes whole
            var models = ReferenceNode(properties, nameof(LiveReferenceTestScript.Models));
            var changes = properties.AssetApplier.ListChanges(properties.CreateApplySource(properties.Mirrors.Single()), references.Id, [LiveApplyStep.Of(nameof(LiveReferenceTestScript.Models))], out var reason);
            Assert.Null(reason);
            Assert.Equal(LiveApplyKind.Collection, Assert.Single(changes).Kind);
            Assert.Equal(1, properties.ApplyToAsset(models.NodePresenters));
            Assert.Equal(fixture.Asset(ModelLocation).Url, AttachedReferenceManager.GetUrl(Assert.Single(asset.Models)));
        }
        finally
        {
            properties.ViewModel?.Destroy();
            player.Remove(references);
        }
    }

    [AvaloniaFact]
    public async Task OnAPrefabInstanceTheValueAppliedIsAnOverrideEvenWhenItEqualsThePrefabs()
    {
        using var fixture = await ShellSessionFixture.OpenAsync((ModelLocation, new ModelAsset()), (SceneLocation, new SceneAsset()), ("Prefabs/Player", new PrefabAsset()));
        var undoRedo = fixture.Session.UndoRedoService!;
        var prefab = fixture.Asset("Prefabs/Player");
        var prefabGraph = (AssetCompositeHierarchyPropertyGraph<EntityDesign, Entity>)prefab.PropertyGraph!;
        var sceneGraph = (AssetCompositeHierarchyPropertyGraph<EntityDesign, Entity>)fixture.Asset(SceneLocation).PropertyGraph!;
        using (undoRedo.CreateTransaction())
        {
            var design = new EntityDesign(new Entity("Player") { new LiveEditTestScript() });
            prefabGraph.AddPartToAsset(new AssetPartCollection<EntityDesign, Entity> { design }, design, null, 0);
        }
        // An instance in the scene, as the scene editor adds it; the game runs it with the same ids
        var instance = ((PrefabAsset)prefab.Asset).CreatePrefabInstance(prefab.Url);
        var root = instance.Parts[instance.RootParts.Single().Id];
        using (undoRedo.CreateTransaction())
            sceneGraph.AddPartToAsset(instance.Parts, root, null, 0);
        player.Id = root.Entity.Id;
        player.Transform.Id = root.Entity.Transform.Id;
        script.Id = root.Entity.Get<LiveEditTestScript>()!.Id;
        var assetScript = AssetScript(fixture, player);
        var speedNode = (IAssetMemberNode)fixture.Session.AssetNodeContainer.GetNode(assetScript)![nameof(LiveEditTestScript.Speed)];
        Assert.False(speedNode.IsContentOverridden());

        var properties = await ShowWithTreeAsync(fixture, PlayerHandle);
        try
        {
            script.Speed = 4;
            await properties.ApplyAsync(Snapshot());
            var speed = Node(properties, nameof(LiveEditTestScript.Speed));
            Assert.Equal(1, properties.ApplyToAsset(speed.NodePresenters));
            Assert.Equal(4, assetScript.Speed);
            Assert.True(speedNode.IsContentOverridden());

            // Back to the prefab's value in the game: applied as an override of the same value, as the grid sets it
            script.Speed = 1;
            await properties.ApplyAsync(Snapshot());
            Assert.Equal(1, properties.ApplyToAsset(speed.NodePresenters));
            Assert.Equal(1, assetScript.Speed);
            Assert.True(speedNode.IsContentOverridden());
            undoRedo.Undo();
            Assert.Equal(4, assetScript.Speed);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task AnEntityWithoutACounterpartCannotBeAppliedAndSaysWhy()
    {
        // Other is not in the scene asset: the game made it
        using var fixture = await OpenWithSceneAsync(player, target);
        var properties = await ShowWithTreeAsync(fixture, OtherHandle);
        try
        {
            otherScript.Speed = 3;
            await properties.ApplyAsync(Snapshot());
            var state = properties.GetApplyState(Node(properties, nameof(LiveEditTestScript.Speed)).NodePresenters);
            Assert.False(state.CanApply);
            Assert.Equal(LiveAssetApplier.MadeByGameReason, state.Reason);

            // Added from the Live tab: it says so
            properties.Tree!.IsAddedWhilePlaying = handle => handle == OtherHandle;
            properties.Tree.Reset(TreeOf(fixture));
            state = properties.GetApplyState(Node(properties, nameof(LiveEditTestScript.Speed)).NodePresenters);
            Assert.Equal(LiveAssetApplier.AddedWhilePlayingReason, state.Reason);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task ApplyToAssetOnSeveralEntitiesSetsThoseWithACounterpartInOneUndoStep()
    {
        using var fixture = await OpenWithSceneAsync(player, target);
        var properties = await ShowWithTreeAsync(fixture, PlayerHandle, OtherHandle);
        try
        {
            script.Speed = 2;
            otherScript.Speed = 2;
            await properties.ApplyAsync(Snapshot());
            var speed = Node(properties, nameof(LiveEditTestScript.Speed));
            Assert.Equal(2, speed.NodePresenters.Count);

            // One of the two can be applied: the menu names the count
            var state = properties.GetApplyState(speed.NodePresenters);
            Assert.Equal(new LiveApplyState(1, 2, null), state);
            var transactions = fixture.Session.UndoRedoService!.RetrieveAllTransactions().Count();
            Assert.Equal(1, properties.ApplyToAsset(speed.NodePresenters));
            Assert.Equal(2, AssetScript(fixture, player).Speed);
            Assert.Equal(transactions + 1, fixture.Session.UndoRedoService!.RetrieveAllTransactions().Count());
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task ApplyComponentListsTheValuesEditedLiveCheckedAndThoseTheGameChangedUnchecked()
    {
        using var fixture = await OpenWithSceneAsync(player, target);
        var lists = ReplaceApplyList(fixture, _ => false);
        var properties = await ShowWithTreeAsync(fixture, PlayerHandle);
        try
        {
            // Edited live: the game applied it
            var stamina = Node(properties, nameof(LiveEditTestScript.Stamina));
            stamina.NodeValue = 0.5f;
            var edit = await Game.NextEditAsync();
            script.Stamina = 0.5f;
            Game.Send(Answer(edit, PlayEditStatus.Applied, script.Stamina));
            await WindowTestHelper.WaitUntilAsync(() => State(stamina) is null, "the answer");
            Assert.True(session.EditRecord.IsEdited(PlayerHandle, script.Id, nameof(LiveEditTestScript.Stamina)));
            // Changed by the game
            script.Count = 7;
            await properties.ApplyAsync(Snapshot());
            await WindowTestHelper.PumpAsync();

            var header = Components(properties).Children.First(x => x.NodeValue is LiveEditTestScript);
            Assert.True(header.AssociatedData.ContainsKey(LiveEditData.ApplyComponent));
            Assert.True(properties.GetApplyState(header.NodePresenters, wholeComponent: true).CanApply);

            // Cancel writes nothing
            var undoRedo = fixture.Session.UndoRedoService!;
            var transactions = undoRedo.RetrieveAllTransactions().Count();
            Assert.Equal(0, await properties.ApplyComponentToAssetAsync(header.NodePresenters));
            var list = Assert.Single(lists.Shown);
            var asset = AssetScript(fixture, player);
            Assert.Equal(1, asset.Stamina);
            Assert.Equal(3, asset.Count);
            Assert.Equal(transactions, undoRedo.RetrieveAllTransactions().Count());

            var staminaRow = list.Items.Single(x => x.Path == "Live Edit Test Script > Stamina");
            Assert.True(staminaRow.IsChecked);
            Assert.Equal("Edited live", staminaRow.Origin);
            Assert.Equal("1", staminaRow.AssetValue);
            Assert.Equal("0.5", staminaRow.LiveValue);
            var countRow = list.Items.Single(x => x.Path == "Live Edit Test Script > Count");
            Assert.False(countRow.IsChecked);
            Assert.Equal("Changed by the game", countRow.Origin);
            Assert.Equal(1, list.CheckedCount);

            // The window: a cell whose text is cut shows it whole in its tooltip, one that fits has none
            var window = new LiveApplyListWindow { DataContext = list, Width = 480 };
            try
            {
                window.Show();
                await WindowTestHelper.PumpAsync();
                var cells = window.GetVisualDescendants().OfType<TextBlock>().ToList();
                var path = cells.First(x => x.Name == "PathText" && x.Text == staminaRow.Path);
                Assert.True(LiveApplyListWindow.IsCut(path));
                Assert.Equal(staminaRow.Path, ToolTip.GetTip(path));
                var value = cells.First(x => x.Name == "LiveText" && x.Text == staminaRow.LiveValue);
                Assert.False(LiveApplyListWindow.IsCut(value));
                Assert.Null(ToolTip.GetTip(value));
            }
            finally
            {
                window.Close();
            }

            // OK applies the rows checked, in one undo step
            lists.Answer = _ => true;
            Assert.Equal(1, await properties.ApplyComponentToAssetAsync(header.NodePresenters));
            Assert.Equal(0.5f, asset.Stamina);
            Assert.Equal(3, asset.Count);
            Assert.Equal("Apply to asset: Stamina of Player", undoRedo.GetName(undoRedo.RetrieveAllTransactions().Last()));

            // Every row checked: one step for all
            lists.Answer = shown =>
            {
                shown.ToggleAll();
                return true;
            };
            Assert.True(await properties.ApplyComponentToAssetAsync(header.NodePresenters) >= 2);
            Assert.Equal(7, asset.Count);
            Assert.StartsWith("Apply to asset: ", undoRedo.GetName(undoRedo.RetrieveAllTransactions().Last()), StringComparison.Ordinal);
            undoRedo.Undo();
            Assert.Equal(3, asset.Count);
            Assert.Equal(0.5f, asset.Stamina);
        }
        finally
        {
            properties.ViewModel?.Destroy();
        }
    }

    [AvaloniaFact]
    public async Task AComponentAddedWhilePlayingIsAddedToTheAssetWithItsValues()
    {
        using var fixture = await OpenWithSceneAsync(player, target);
        var lists = ReplaceApplyList(fixture, _ => true);
        var light = new LiveEditTestLight { Range = 9 };
        player.Add(light);
        session.EditRecord.Add(new PlayStructureEdit(100, PlayStructureOperation.AddComponent, PlayerHandle, TypeName: PlayMirrorSerializer.TypeNameOf(typeof(LiveEditTestLight))),
            new PlayStructureResult(100, 1, PlayEditStatus.Applied, [], light.Id, null));
        var properties = await ShowWithTreeAsync(fixture, PlayerHandle);
        try
        {
            var header = Components(properties).Children.First(x => x.NodeValue is LiveEditTestLight);
            Assert.True(properties.GetApplyState(header.NodePresenters, wholeComponent: true).CanApply);

            Assert.Equal(1, await properties.ApplyComponentToAssetAsync(header.NodePresenters));
            var row = Assert.Single(Assert.Single(lists.Shown).Items);
            Assert.Equal(LiveApplyKind.Component, row.Change.Kind);
            Assert.Equal("Edited live", row.Origin);

            var assetEntity = SceneAsset(fixture).Hierarchy.Parts[player.Id].Entity;
            var added = Assert.Single(assetEntity.Components.OfType<LiveEditTestLight>());
            Assert.Equal(light.Id, added.Id);
            Assert.Equal(9, added.Range);
            fixture.Session.UndoRedoService!.Undo();
            Assert.Empty(assetEntity.Components.OfType<LiveEditTestLight>());
        }
        finally
        {
            properties.ViewModel?.Destroy();
            player.Remove(light);
        }
    }

    [AvaloniaFact]
    public async Task TheLiveTabAppliesAnEntityAfterTheGameStoppedWithItsLastValues()
    {
        using var fixture = await OpenWithSceneAsync(player, target);
        var lists = ReplaceApplyList(fixture, list =>
        {
            list.ToggleAll();
            return true;
        });
        var tree = TreeWithCounterparts(fixture);
        using var grid = new LivePropertyGrid(fixture.Session);
        var selection = new LiveSelection(session, [PlayerHandle]) { Names = ["Player"], Tree = tree };
        var selectOther = new LiveSelection(session, [OtherHandle]) { Names = ["Other"], Tree = tree };

        // Not seen yet: it says what it waits for
        Assert.NotNull(grid.GetApplyToAssetDisabledReason(selection));
        grid.Show(selection);
        await Game.NextSubscribeAsync();
        await WindowTestHelper.PumpAsync();
        script.Speed = 6;
        await grid.Properties!.ApplyAsync(Snapshot());
        await WindowTestHelper.PumpAsync();
        Assert.Null(grid.GetApplyToAssetDisabledReason(selection));

        // The game stops: the grid goes, the values stay
        Game.ExitOnQuit = true;
        await session.StopAsync();
        grid.Show(null);
        Assert.Null(grid.Properties);
        var stopped = selection with { };
        Assert.True(stopped.IsStopped);
        Assert.Null(grid.GetApplyToAssetDisabledReason(stopped));
        Assert.Contains("stopped before", grid.GetApplyToAssetDisabledReason(selectOther), StringComparison.Ordinal);

        Assert.True(await grid.ApplyToAssetAsync(stopped) >= 1);
        Assert.Equal("Apply to asset: Player", Assert.Single(lists.Shown).Title);
        Assert.Equal(6, AssetScript(fixture, player).Speed);
    }

    private static async Task<ShellSessionFixture> OpenWithSceneAsync(params Entity[] entities)
    {
        var fixture = await ShellSessionFixture.OpenAsync((ModelLocation, new ModelAsset()), (SceneLocation, new SceneAsset()));
        // The asset's copies: the same ids, the default values; added once the scene is loaded, through its graph
        var graph = (AssetCompositeHierarchyPropertyGraph<EntityDesign, Entity>)fixture.Asset(SceneLocation).PropertyGraph!;
        using (fixture.Session.UndoRedoService!.CreateTransaction())
        {
            foreach (var entity in entities)
            {
                var copy = new Entity(entity.Name) { Id = entity.Id };
                copy.Transform.Id = entity.Transform.Id;
                foreach (var component in entity.Components.Where(x => x is not TransformComponent))
                {
                    var empty = (EntityComponent)Activator.CreateInstance(component.GetType())!;
                    empty.Id = component.Id;
                    copy.Add(empty);
                }
                var design = new EntityDesign(copy);
                graph.AddPartToAsset(new AssetPartCollection<EntityDesign, Entity> { design }, design, null, graph.Asset.Hierarchy.RootParts.Count);
            }
        }
        return fixture;
    }

    private async Task<LivePropertiesViewModel> ShowWithTreeAsync(ShellSessionFixture fixture, params int[] shown)
    {
        var properties = await ShowAsync(fixture, shown);
        properties.Tree = TreeWithCounterparts(fixture);
        return properties;
    }

    // The tree of the Live tab, whose entities have their counterparts in the scene asset
    private LiveTree TreeWithCounterparts(ShellSessionFixture fixture)
    {
        var tree = new LiveTree(info => info.Url == SceneLocation ? SceneAsset(fixture).Hierarchy.Parts.Keys.ToHashSet() : null);
        tree.Reset(TreeOf(fixture));
        return tree;
    }

    private PlayTree TreeOf(ShellSessionFixture fixture)
        => new(frame, [new PlaySceneInfo(SceneHandle, (Guid)fixture.Asset(SceneLocation).Id, "Main", SceneLocation, 0)], [Info(player, PlayerHandle), Info(target, 2), Info(other, OtherHandle)]);

    private static SceneAsset SceneAsset(ShellSessionFixture fixture) => (SceneAsset)fixture.Asset(SceneLocation).Asset;

    private static LiveEditTestScript AssetScript(ShellSessionFixture fixture, Entity live)
        => SceneAsset(fixture).Hierarchy.Parts[live.Id].Entity.Get<LiveEditTestScript>()!;

    private static NodeViewModel Components(LivePropertiesViewModel properties)
        => (properties.ViewModel ?? throw new InvalidOperationException("The grid is being built.")).RootNode.Children.Single(x => x.Name == nameof(Entity.Components));

    private static FakeApplyList ReplaceApplyList(ShellSessionFixture fixture, Func<LiveApplyListViewModel, bool> answer)
    {
        var services = (ViewModelServiceProvider)fixture.Session.ServiceProvider;
        while (services.TryGet<ILiveApplyListService>() is { } existing)
        {
            services.UnregisterService(existing);
            if (services.TryGet<ILiveApplyListService>() == existing)
                break;
        }
        var fake = new FakeApplyList { Answer = answer };
        services.RegisterService(fake);
        return fake;
    }

    /// <summary>
    /// Answers the list of the values to apply as the test says, and keeps the lists shown.
    /// </summary>
    private sealed class FakeApplyList : ILiveApplyListService
    {
        public required Func<LiveApplyListViewModel, bool> Answer { get; set; }

        public List<LiveApplyListViewModel> Shown { get; } = [];

        public Task<bool> ShowAsync(LiveApplyListViewModel list)
        {
            Shown.Add(list);
            return Task.FromResult(Answer(list));
        }
    }
}

/// <summary>
/// A component the tests add to an entity while the game runs.
/// </summary>
[Stride.Core.DataContract]
public sealed class LiveEditTestLight : EntityComponent
{
    public float Range { get; set; } = 1;
}
