using ColorRise.Saga.Assets;
using ColorRise.Saga.Editor.Tests.Helpers;
using ColorRise.Saga.Editor.ViewModels;

namespace ColorRise.Saga.Editor.Tests;

/// <summary>
/// The dialogue editor: lines, choices, branches and ends with a slot per way out; linking, adding, deleting; each change one step
/// of the undo history.
/// </summary>
public sealed class DialogueEditorTests
{
    [Fact]
    public Task GraphShowsNodesTheirWaysOutAndLinks() => RunAsync((_, editor) =>
    {
        Assert.Equal([(SagaGraphNodeKind.Line, "hello"), (SagaGraphNodeKind.Choices, "hub"), (SagaGraphNodeKind.Line, "mine"), (SagaGraphNodeKind.Branch, "check"), (SagaGraphNodeKind.End, "end")],
            editor.Nodes.Select(x => (x.Kind, x.Id)));
        Assert.Equal(["Ask about the mine", "Leave"], Node(editor, "hub").Outputs.Select(x => x.Name));
        Assert.Single(Node(editor, "check").Outputs);
        Assert.Empty(Node(editor, "end").Outputs);
        Assert.True(Node(editor, "hello").IsEntry);
        Assert.False(Node(editor, "hub").IsEntry);

        Assert.Equal(["hello.Output -> hub.Input", "hub.Ask about the mine -> mine.Input", "hub.Leave -> end.Input", "mine.Output -> hub.Input"],
            editor.Links.Select(x => x.ToString()));
        Assert.IsType<DialogueChoiceCard>(editor.Links[1].Card);
        return Task.CompletedTask;
    });

    [Fact]
    public Task LinkingAWayOutSetsItsTarget() => RunAsync((fixture, editor) =>
    {
        var names = RecordUndoNames(fixture);
        var leave = Node(editor, "hub").Outputs[1];

        Assert.False(editor.CanLink(leave, Node(editor, "end").Input));
        editor.LinkCommand.Execute(new SagaLinkRequest(leave, Node(editor, "hello").Input));
        editor.LinkCommand.Execute(new SagaLinkRequest(Node(editor, "check").Outputs[0], Node(editor, "end").Input));

        Assert.Equal(["Link nodes", "Link nodes"], names);
        var nodes = editor.Asset.Asset.Nodes;
        Assert.Equal("hello", ((ChoicesNodeCard)nodes[1]).Choices[1].TargetId);
        Assert.Equal("end", ((BranchNodeCard)nodes[3]).Links[0].TargetId);
        Assert.Contains(editor.Links, x => x.ToString() == "hub.Leave -> hello.Input");
        Assert.DoesNotContain(editor.Links, x => x.ToString() == "hub.Leave -> end.Input");

        editor.UnlinkCommand.Execute(editor.Links.Single(x => x.ToString() == "hello.Output -> hub.Input"));
        Assert.Equal("", ((LineNodeCard)nodes[0]).NextId);
        Assert.Equal("Unlink nodes", names[^1]);
        return Task.CompletedTask;
    });

    [Fact]
    public Task MovingALinkRetargetsItsWayOut() => RunAsync((fixture, editor) =>
    {
        var names = RecordUndoNames(fixture);
        var link = editor.Links.Single(x => x.ToString() == "hub.Leave -> end.Input");

        editor.LinkCommand.Execute(new SagaLinkRequest(link.Source, Node(editor, "check").Input, link));

        Assert.Equal(["Move link"], names);
        Assert.Equal("check", ((ChoicesNodeCard)editor.Asset.Asset.Nodes[1]).Choices[1].TargetId);
        Assert.Contains(editor.Links, x => x.ToString() == "hub.Leave -> check.Input");
        Assert.DoesNotContain(editor.Links, x => x.ToString() == "hub.Leave -> end.Input");
        return Task.CompletedTask;
    });

    [Fact]
    public Task AddingNodesAndWaysOutAreUndoSteps() => RunAsync((fixture, editor) =>
    {
        var names = RecordUndoNames(fixture);

        editor.AddLineCommand.Execute(null);
        editor.AddChoicesCommand.Execute(null);
        editor.AddBranchCommand.Execute(null);
        editor.AddEndCommand.Execute(null);
        editor.AddWayOutCommand.Execute(Node(editor, "hub"));

        Assert.Equal(["Add line", "Add choices", "Add branch", "Add end", "Add choice"], names);
        Assert.Equal(["hello", "hub", "mine", "check", "end", "line_3", "choices_2", "branch_2", "end_2"], editor.Nodes.Select(x => x.Id));
        Assert.Single(Node(editor, "choices_2").Outputs);
        Assert.Equal(3, Node(editor, "hub").Outputs.Count);

        fixture.Session.UndoRedoService.Undo();
        Assert.Equal(2, Node(editor, "hub").Outputs.Count);
        return Task.CompletedTask;
    });

    [Fact]
    public Task DeletingANodeClearsTheWaysThatLedToIt() => RunAsync(async (fixture, editor) =>
    {
        var names = RecordUndoNames(fixture);
        editor.SelectedNodes.Add(Node(editor, "mine"));
        editor.SelectedNodes.Add(Node(editor, "hello"));
        await fixture.WaitForPropertiesAsync();

        editor.DeleteSelectionCommand.Execute(null);
        await fixture.WaitForPropertiesAsync();

        Assert.Equal(["Delete selection"], names);
        var dialogue = editor.Asset.Asset;
        Assert.Equal(["hub", "check", "end"], dialogue.Nodes.Select(x => x.Id));
        Assert.Equal("", ((ChoicesNodeCard)dialogue.Nodes[0]).Choices[0].TargetId);
        Assert.Equal("", dialogue.EntryNodeId);
        Assert.Equal(["hub.Leave -> end.Input"], editor.Links.Select(x => x.ToString()));

        fixture.Session.UndoRedoService.Undo();
        Assert.Equal(5, dialogue.Nodes.Count);
        Assert.Equal("hello", dialogue.EntryNodeId);
        Assert.Equal(4, editor.Links.Count);
    });

    [Fact]
    public Task SetEntryMakesANodeTheFirst() => RunAsync((fixture, editor) =>
    {
        var names = RecordUndoNames(fixture);

        Assert.False(editor.SetEntryCommand.CanExecute(null));
        editor.SetEntryCommand.Execute(Node(editor, "hub"));

        Assert.Equal(["Set entry node"], names);
        Assert.Equal("hub", editor.Asset.Asset.EntryNodeId);
        Assert.True(Node(editor, "hub").IsEntry);
        Assert.False(Node(editor, "hello").IsEntry);
        return Task.CompletedTask;
    });

    [Fact]
    public Task RenamingANodeRenamesTheWaysToIt() => RunAsync((fixture, editor) =>
    {
        var dialogue = editor.Asset.Asset;
        var hello = (LineNodeCard)dialogue.Nodes[0];
        var hub = (ChoicesNodeCard)dialogue.Nodes[1];
        var mine = (LineNodeCard)dialogue.Nodes[2];

        fixture.Edit(hub, nameof(DialogueNodeCard.Id), "questions");
        fixture.Edit(hello, nameof(DialogueNodeCard.Id), "greeting");
        fixture.Edit(mine, nameof(DialogueNodeCard.Id), "collapse");

        Assert.Equal(("questions", "questions", "greeting"), (hello.NextId, mine.NextId, dialogue.EntryNodeId));
        Assert.Equal(["collapse", "end"], hub.Choices.Select(x => x.TargetId));
        Assert.True(Node(editor, "greeting").IsEntry);

        fixture.Session.UndoRedoService.Undo();
        fixture.Session.UndoRedoService.Undo();
        fixture.Session.UndoRedoService.Undo();
        Assert.Equal(("hub", "hub", "hello"), (hello.NextId, mine.NextId, dialogue.EntryNodeId));
        Assert.Equal(["mine", "end"], hub.Choices.Select(x => x.TargetId));
        return Task.CompletedTask;
    });

    private static SagaGraphNodeViewModel Node(DialogueEditorViewModel editor, string id) => editor.Nodes.Single(x => x.Id == id);

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
    /// Opens the editor of a dialogue: a greeting, a hub of two choices (one to a line that loops back), a branch and an end.
    /// </summary>
    private static Task RunAsync(Func<SagaSessionFixture, DialogueEditorViewModel, Task> test)
    {
        var dialogue = new DialogueAsset
        {
            CharacterId = "mara",
            EntryNodeId = "hello",
            Nodes =
            {
                new LineNodeCard { Id = "hello", SpeakerId = "mara", Text = "You came back.", NextId = "hub" },
                new ChoicesNodeCard
                {
                    Id = "hub",
                    Choices = { new DialogueChoiceCard { Text = "Ask about the mine", TargetId = "mine" }, new DialogueChoiceCard { Text = "Leave", TargetId = "end" } },
                },
                new LineNodeCard { Id = "mine", SpeakerId = "mara", Text = "It collapsed.", NextId = "hub" },
                new BranchNodeCard { Id = "check", Links = { new DialogueLinkCard { When = "day > 3" } } },
                new EndNodeCard { Id = "end" },
            },
        };
        return SagaSessionFixture.RunAsync(async fixture =>
        {
            Assert.False(fixture.Result.HasErrors, fixture.Log);
            var editor = new DialogueEditorViewModel(fixture.Asset<DialogueViewModel>("Dialogues/Mara"));
            Assert.True(await editor.Initialize());
            try
            {
                await test(fixture, editor);
            }
            finally
            {
                editor.Destroy();
            }
        }, ("Dialogues/Mara", dialogue));
    }
}
