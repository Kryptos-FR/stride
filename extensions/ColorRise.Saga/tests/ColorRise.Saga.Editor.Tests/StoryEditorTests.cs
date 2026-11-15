using ColorRise.Saga.Assets;
using ColorRise.Saga.Content;
using ColorRise.Saga.Editor.Tests.Helpers;
using ColorRise.Saga.Editor.ViewModels;
using Stride.Core.Assets;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;

namespace ColorRise.Saga.Editor.Tests;

/// <summary>
/// The story editor: the plot, an act, a scenario or quest; the breadcrumb; the commands that change the story, each one step of
/// the undo history.
/// </summary>
public sealed class StoryEditorTests
{
    [Fact]
    public Task PlotShowsActsAndTheirLinks() => RunAsync((_, editor) =>
    {
        Assert.Equal(StoryGraphDepth.Plot, editor.CurrentLevel.Depth);
        Assert.Equal(["Main"], editor.Breadcrumb.Select(x => x.Title));
        Assert.Equal(["the_mine", "with_the_guild", "with_the_crown"], editor.Nodes.Select(x => x.Id));
        Assert.All(editor.Nodes, x => Assert.Equal(SagaGraphNodeKind.Act, x.Kind));
        Assert.Equal(new Vector2(10, 20), Node(editor, "the_mine").Position);

        var link = Assert.Single(editor.Links);
        Assert.Same(Node(editor, "the_mine").Output, link.Source);
        Assert.Same(Node(editor, "with_the_guild").Input, link.Target);
        Assert.Equal("with_the_guild", Assert.IsType<ActLinkCard>(link.Card).TargetActId);
        return Task.CompletedTask;
    });

    [Fact]
    public Task OpeningAnActShowsItsScenariosAndQuests() => RunAsync((_, editor) =>
    {
        editor.OpenCommand.Execute(Node(editor, "the_mine"));

        Assert.Equal(StoryGraphDepth.Act, editor.CurrentLevel.Depth);
        Assert.Equal("the_mine", editor.CurrentLevel.ActId);
        Assert.Equal(["Main", "The mine"], editor.Breadcrumb.Select(x => x.Title));
        Assert.Equal([(SagaGraphNodeKind.Scenario, "cave_in"), (SagaGraphNodeKind.Quest, "rescue"), (SagaGraphNodeKind.Quest, "side_job")],
            editor.Nodes.Select(x => (x.Kind, x.Id)));

        // A quest of a scenario is linked from it
        var link = Assert.Single(editor.Links);
        Assert.Same(Node(editor, "cave_in").Output, link.Source);
        Assert.Same(Node(editor, "rescue").Input, link.Target);
        return Task.CompletedTask;
    });

    [Fact]
    public Task BreadcrumbGoesBack() => RunAsync((fixture, editor) =>
    {
        editor.OpenCommand.Execute(Node(editor, "the_mine"));
        editor.OpenCommand.Execute(Node(editor, "cave_in"));

        Assert.Equal(StoryGraphDepth.Scenario, editor.CurrentLevel.Depth);
        Assert.Equal(("the_mine", "cave_in"), (editor.CurrentLevel.ActId, editor.CurrentLevel.OwnerId));
        Assert.Equal(["Main", "The mine", "cave_in"], editor.Breadcrumb.Select(x => x.Title));
        var dialogue = Assert.Single(editor.Nodes);
        Assert.Equal((SagaGraphNodeKind.Dialogue, "Guards"), (dialogue.Kind, dialogue.Id));

        editor.GoToLevelCommand.Execute(editor.Breadcrumb[0]);

        Assert.Equal(StoryGraphDepth.Plot, editor.CurrentLevel.Depth);
        Assert.Single(editor.Breadcrumb);
        Assert.Equal(["the_mine", "with_the_guild", "with_the_crown"], editor.Nodes.Select(x => x.Id));
        return Task.CompletedTask;
    }, ("Dialogues/Guards", new DialogueAsset()));

    [Fact]
    public Task OpeningADialogueOpensItsEditor() => RunAsync((fixture, editor) =>
    {
        editor.OpenCommand.Execute(Node(editor, "the_mine"));
        editor.OpenCommand.Execute(Node(editor, "cave_in"));

        editor.OpenCommand.Execute(Node(editor, "Guards"));

        var opened = Assert.IsType<DialogueEditorViewModel>(Assert.Single(fixture.Session.EditorCollection.Editors));
        Assert.Same(fixture.Asset<DialogueViewModel>("Dialogues/Guards"), opened.Asset);
        Assert.Equal(StoryGraphDepth.Scenario, editor.CurrentLevel.Depth);
        return Task.CompletedTask;
    }, ("Dialogues/Guards", new DialogueAsset()));

    [Fact]
    public Task SelectingANodeShowsItsCardInThePropertyGrid() => RunAsync(async (fixture, editor) =>
    {
        editor.SelectedNodes.Add(Node(editor, "the_mine"));
        await fixture.WaitForPropertiesAsync();

        var properties = fixture.Session.AssetViewProperties;
        Assert.Same(Node(editor, "the_mine"), Assert.Single(properties.Selection!));
        var members = properties.ViewModel!.RootNode.Children.Select(x => x.Name).ToList();
        Assert.Contains(nameof(ActCard.EndWhen), members);
        Assert.DoesNotContain(nameof(StoryAsset.Acts), members);

        editor.SelectedLinks.Add(Assert.Single(editor.Links));
        editor.SelectedNodes.Clear();
        await fixture.WaitForPropertiesAsync();
        Assert.Contains(nameof(ActLinkCard.When), properties.ViewModel!.RootNode.Children.Select(x => x.Name));
    });

    [Fact]
    public Task AddActIsOneUndoStep() => RunAsync((fixture, editor) =>
    {
        var names = RecordUndoNames(fixture);

        editor.AddActCommand.Execute(null);

        Assert.Equal(["Add act"], names);
        var story = editor.Asset.Asset;
        Assert.Equal(4, story.Acts.Count);
        Assert.Equal("act_4", story.Acts[3].Id);
        Assert.Equal(["the_mine", "with_the_guild", "with_the_crown", "act_4"], editor.Nodes.Select(x => x.Id));
        Assert.True(editor.Asset.IsDirty);

        fixture.Session.UndoRedoService.Undo();
        Assert.Equal(3, story.Acts.Count);
        Assert.Equal(["the_mine", "with_the_guild", "with_the_crown"], editor.Nodes.Select(x => x.Id));
        return Task.CompletedTask;
    });

    [Fact]
    public Task AddScenarioAndQuestGoToTheOpenAct() => RunAsync((fixture, editor) =>
    {
        editor.OpenCommand.Execute(Node(editor, "with_the_guild"));
        var names = RecordUndoNames(fixture);

        editor.AddScenarioCommand.Execute(null);
        editor.AddQuestCommand.Execute(null);

        Assert.Equal(["Add scenario", "Add quest"], names);
        var act = editor.Asset.Asset.Acts[1];
        // Numbered after the scenarios and quests of the whole story
        Assert.Equal("scenario_2", Assert.Single(act.Scenarios).Id);
        Assert.Equal("quest_3", Assert.Single(act.Quests).Id);
        Assert.Equal(["scenario_2", "quest_3"], editor.Nodes.Select(x => x.Id));
        return Task.CompletedTask;
    });

    [Fact]
    public Task LinkingActsAddsANextLink() => RunAsync((fixture, editor) =>
    {
        var names = RecordUndoNames(fixture);
        var guild = Node(editor, "with_the_guild");
        var crown = Node(editor, "with_the_crown");

        Assert.True(editor.CanLink(guild.Output, crown.Input));
        Assert.False(editor.CanLink(guild.Output, guild.Input));
        Assert.False(editor.CanLink(Node(editor, "the_mine").Output, guild.Input));
        editor.LinkCommand.Execute(new SagaLinkRequest(guild.Output, crown.Input));

        Assert.Equal(["Link acts"], names);
        Assert.Equal("with_the_crown", Assert.Single(editor.Asset.Asset.Acts[1].Next).TargetActId);
        Assert.Contains(editor.Links, x => x.Source == guild.Output && x.Target == crown.Input);

        fixture.Session.UndoRedoService.Undo();
        Assert.Empty(editor.Asset.Asset.Acts[1].Next);
        Assert.Single(editor.Links);
        return Task.CompletedTask;
    });

    [Fact]
    public Task LinkingAScenarioToAQuestMakesItTheQuestsScenario() => RunAsync((fixture, editor) =>
    {
        editor.OpenCommand.Execute(Node(editor, "the_mine"));
        var names = RecordUndoNames(fixture);

        editor.LinkCommand.Execute(new SagaLinkRequest(Node(editor, "cave_in").Output, Node(editor, "side_job").Input));
        Assert.Equal("cave_in", editor.Asset.Asset.Acts[0].Quests[1].ScenarioId);
        Assert.Equal(2, editor.Links.Count);

        editor.UnlinkCommand.Execute(editor.Links.Single(x => x.Target == Node(editor, "rescue").Input));
        Assert.Equal("", editor.Asset.Asset.Acts[0].Quests[0].ScenarioId);
        Assert.Single(editor.Links);
        Assert.Equal(["Attach quest to scenario", "Detach quest from scenario"], names);
        return Task.CompletedTask;
    });

    [Fact]
    public Task MovingNodesWritesTheirPositionsInOneStep() => RunAsync((fixture, editor) =>
    {
        var names = RecordUndoNames(fixture);

        editor.MoveNodes([(Node(editor, "the_mine"), new Vector2(100, 50)), (Node(editor, "with_the_guild"), new Vector2(300, 50))]);

        Assert.Equal(["Move cards"], names);
        Assert.Equal(new Vector2(100, 50), editor.Asset.Asset.Acts[0].Position);
        Assert.Equal(new Vector2(300, 50), Node(editor, "with_the_guild").Position);
        return Task.CompletedTask;
    });

    [Fact]
    public Task DeletingAnActRemovesTheLinksToIt() => RunAsync(async (fixture, editor) =>
    {
        var names = RecordUndoNames(fixture);
        editor.SelectedNodes.Add(Node(editor, "with_the_guild"));
        await fixture.WaitForPropertiesAsync();

        editor.DeleteSelectionCommand.Execute(null);
        await fixture.WaitForPropertiesAsync();

        Assert.Equal(["Delete selection"], names);
        var story = editor.Asset.Asset;
        Assert.Equal(["the_mine", "with_the_crown"], story.Acts.Select(x => x.Id));
        Assert.Empty(story.Acts[0].Next);
        Assert.Empty(editor.Links);
        Assert.Empty(editor.SelectedNodes);

        fixture.Session.UndoRedoService.Undo();
        Assert.Equal(3, story.Acts.Count);
        Assert.Single(editor.Links);
    });

    [Fact]
    public Task AddDialogueCreatesTheAssetAndReferencesIt() => RunAsync((fixture, editor) =>
    {
        editor.OpenCommand.Execute(Node(editor, "the_mine"));
        editor.OpenCommand.Execute(Node(editor, "rescue"));
        var names = RecordUndoNames(fixture);

        editor.AddDialogueCommand.Execute(null);

        Assert.Equal(["Add dialogue"], names);
        var dialogue = fixture.Asset<DialogueViewModel>("Story/rescue_dialogue");
        Assert.Equal(editor.Asset.Id, dialogue.Asset.Story?.Id);
        var reference = Assert.Single(editor.Asset.Asset.Acts[0].Quests[0].Dialogues);
        Assert.Equal(dialogue.Id, AttachedReferenceManager.GetAttachedReference(reference)?.Id);
        Assert.Equal("rescue_dialogue", Assert.Single(editor.Nodes).Id);

        fixture.Session.UndoRedoService.Undo();
        Assert.Empty(editor.Asset.Asset.Acts[0].Quests[0].Dialogues);
        Assert.Empty(editor.Nodes);
        return Task.CompletedTask;
    });

    private static SagaGraphNodeViewModel Node(StoryEditorViewModel editor, string id) => editor.Nodes.Single(x => x.Id == id);

    private static List<string?> RecordUndoNames(SagaSessionFixture fixture)
    {
        var undoRedo = fixture.Session.UndoRedoService;
        var names = new List<string?>();
        undoRedo.Done += (_, e) =>
        {
            if (e.Transaction.Operations.Any(x => x.HasEffect))
                names.Add(undoRedo.GetName(e.Transaction));
        };
        return names;
    }

    /// <summary>
    /// Opens the editor of a story of three acts: the mine (a scenario, a quest of that scenario, a quest of the act; a link to
    /// the guild act) and two acts after it. A dialogue given with the assets is the scenario's.
    /// </summary>
    private static Task RunAsync(Func<SagaSessionFixture, StoryEditorViewModel, Task> test, params (string Location, Asset Asset)[] assets)
    {
        var story = new StoryAsset
        {
            FirstActId = "the_mine",
            Acts =
            {
                new ActCard
                {
                    Id = "the_mine",
                    Title = "The mine",
                    Position = new Vector2(10, 20),
                    Next = { new ActLinkCard { TargetActId = "with_the_guild" } },
                    Scenarios = { new ScenarioCard { Id = "cave_in" } },
                    Quests = { new QuestCard { Id = "rescue", ScenarioId = "cave_in" }, new QuestCard { Id = "side_job" } },
                },
                new ActCard { Id = "with_the_guild" },
                new ActCard { Id = "with_the_crown" },
            },
        };
        var all = new List<(string, Asset)> { ("Story/Main", story) };
        foreach (var (location, asset) in assets)
        {
            all.Add((location, asset));
            if (asset is DialogueAsset)
                story.Acts[0].Scenarios[0].Dialogues.Add(AttachedReferenceManager.CreateProxyObject<DialogueDefinition>(asset.Id, location));
        }

        return SagaSessionFixture.RunAsync(async fixture =>
        {
            Assert.False(fixture.Result.HasErrors, fixture.Log);
            var editor = new StoryEditorViewModel(fixture.Asset<StoryViewModel>("Story/Main"));
            Assert.True(await editor.Initialize());
            try
            {
                await test(fixture, editor);
            }
            finally
            {
                editor.Destroy();
            }
        }, [.. all]);
    }
}
