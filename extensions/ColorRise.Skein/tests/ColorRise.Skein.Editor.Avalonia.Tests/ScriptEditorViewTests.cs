using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ColorRise.Skein.Assets.Text;
using ColorRise.Skein.Editor.Avalonia.Views;
using ColorRise.Skein.Editor.Graph;
using ColorRise.Skein.Editor.Tests;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Core.Presentation.Avalonia.Controls;

namespace ColorRise.Skein.Editor.Avalonia.Tests;

/// <summary>
/// The view of the script editor shows what the editor has, and what the user does in it goes to the editor.
/// </summary>
public sealed class ScriptEditorViewTests
{
    private const string Act1 =
        "title: Start\nposition: 0,0\n---\nOdile: The lamp's out again.\n<<jump Pier>>\n===\n"
        + "title: Pier\nposition: 340,0\ncluster: Shore\n---\nFisher: Mind the nets.\n-> Ask about the oil\n    Fisher: None left.\n-> Leave\n===\n"
        + "title: Lamp\ntags: quest light\nquest: keeper\n---\nKeeper: It wants oil.\n<<jump Harbour>>\n===\n";

    private const string Act2 = "title: Harbour\n---\nThe boats are in.\n===\n";

    private static ScriptView Open(string text = Act1)
        => ScriptView.Open("Dialogue/Act1", SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", text), SkeinSession.Script("Dialogue/Act2", Act2));

    private static int LineOf(string text, string part) => text[..text.IndexOf(part, StringComparison.Ordinal)].Count(c => c == '\n') + 1;

    [AvaloniaFact]
    public void The_view_shows_the_cards_at_their_places_the_links_and_the_text()
    {
        using var view = Open();
        var (editor, graph, pane) = (view.Editor, view.View.NodeGraph, view.View.Text);

        // Three cards and the stub of the node of the other script, each at the place the editor gives
        Assert.Equal(4, editor.Nodes.Count);
        foreach (var node in editor.Nodes.Cast<IYarnGraphNode>())
        {
            var container = graph.ContainerFromNode(node);
            Assert.NotNull(container);
            Assert.True(container.Bounds.Width > 0, node.Title);
            Assert.Equal(new Point(node.Position.X, node.Position.Y), graph.GetNodePosition(node));
        }

        Assert.Contains("stub", graph.ContainerFromNode(editor.Stubs[0])!.Classes);
        Assert.DoesNotContain("stub", graph.ContainerFromNode(editor.Cards[0])!.Classes);

        // The links end on connectors the cards show
        Assert.Equal(2, editor.Links.Count);
        foreach (var link in editor.Links)
        {
            Assert.NotNull(graph.ConnectorFromSlot(link.SourceSlot, NodeGraphSlotDirection.Output));
            Assert.NotNull(graph.ConnectorFromSlot(link.TargetSlot, NodeGraphSlotDirection.Input));
        }

        // The graph showed all its nodes when it opened
        Assert.True(graph.Zoom <= 1);
        var first = graph.ContainerFromNode(editor.Cards[0])!;
        Assert.True(first.TranslatePoint(default, graph) is { X: >= 0, Y: >= 0 });

        // The text is the script's, in the colours of what Yarn reads
        Assert.Equal(Act1, pane.Document.Text);
        Assert.Equal(YarnTokenKind.Keyword, pane.TokenKindAt(Act1.IndexOf("jump Pier", StringComparison.Ordinal)));
        Assert.Equal(YarnTokenKind.NodeName, pane.TokenKindAt(Act1.IndexOf("Pier>>", StringComparison.Ordinal)));
        Assert.Equal(YarnTokenKind.Speaker, pane.TokenKindAt(Act1.IndexOf("Odile", StringComparison.Ordinal)));
        Assert.Null(pane.TokenKindAt(Act1.IndexOf("The lamp", StringComparison.Ordinal)));
        Assert.Null(pane.MessageAt(0));
    }

    [AvaloniaFact]
    public void What_is_typed_in_the_pane_changes_the_script_and_an_undo_shows_in_the_pane()
    {
        using var view = Open();
        var pane = view.View.Text;
        var script = view.Session.ScriptAt("Dialogue/Act1");
        var at = Act1.IndexOf("Odile:", StringComparison.Ordinal);

        view.Do(() => pane.Document.Insert(at, "Odile: Again?\n"));

        Assert.Equal(Act1.Insert(at, "Odile: Again?\n"), script.TextBuffer.Text);
        Assert.True(script.IsDirty);
        Assert.Equal("Odile: Again?\nOdile: The lamp's out again.", view.Editor.Cards[0].Preview);

        view.Do(() => view.Session.UndoRedo.Undo());

        Assert.Equal(Act1, pane.Document.Text);
        Assert.Equal(Act1, script.TextBuffer.Text);
    }

    [AvaloniaFact]
    public void The_pane_shows_the_selected_nodes_or_the_whole_file()
    {
        using var view = Open();
        var (editor, pane) = (view.Editor, view.View.Text);
        var lines = Act1.Count(c => c == '\n');
        var (pierFirst, pierLast) = (LineOf(Act1, "title: Pier"), LineOf(Act1, "title: Lamp") - 1);

        // Nothing selected: the whole file
        Assert.All(Enumerable.Range(1, lines), line => Assert.True(pane.IsLineShown(line)));

        view.Do(() => editor.SelectedCards.Add(editor.Cards[1]));
        Assert.All(Enumerable.Range(1, lines), line => Assert.Equal(line >= pierFirst && line <= pierLast, pane.IsLineShown(line)));
        Assert.InRange(pane.TextArea.Caret.Line, pierFirst, pierLast);

        // Two nodes: the lines of both
        view.Do(() => editor.SelectedCards.Add(editor.Cards[2]));
        Assert.All(Enumerable.Range(1, lines), line => Assert.Equal(line >= pierFirst, pane.IsLineShown(line)));

        // What the graph does to another node keeps the lines of the selected ones, where they now are
        view.Do(() =>
        {
            editor.SelectedCards.Remove(editor.Cards[2]);
            editor.Move([new YarnCardMove(editor.Cards[0], new YarnGraphPoint(10, 20))]);
        });
        Assert.Equal(editor.TextBuffer.Text, pane.Document.Text);
        var moved = editor.TextBuffer.Text;
        Assert.True(pane.IsLineShown(LineOf(moved, "title: Pier")));
        Assert.True(pane.IsLineShown(LineOf(moved, "-> Leave") + 1));
        Assert.False(pane.IsLineShown(LineOf(moved, "title: Pier") - 1));
        Assert.False(pane.IsLineShown(LineOf(moved, "title: Lamp")));

        view.Do(() => editor.ShowWholeFile = true);
        Assert.All(Enumerable.Range(1, lines), line => Assert.True(pane.IsLineShown(line)));

        view.Do(() => editor.ShowWholeFile = false);
        Assert.False(pane.IsLineShown(1));

        // The name of a node in a jump leads to the node
        view.Do(() => editor.ShowWholeFile = true);
        var jump = moved.IndexOf("Pier>>", StringComparison.Ordinal);
        Assert.False(pane.FollowNodeNameAt(moved.IndexOf("Odile", StringComparison.Ordinal)));
        view.Do(() => Assert.True(pane.FollowNodeNameAt(jump)));
        Assert.Equal([editor.Cards[1]], editor.SelectedCards);
    }

    [AvaloniaFact]
    public void The_messages_of_the_compiler_are_underlined_and_listed()
    {
        var broken = Act1.Replace("<<jump Pier>>", "<<jump Nowhere>>");
        using var view = Open(broken);
        var (editor, pane) = (view.Editor, view.View.Text);

        var message = Assert.Single(editor.ScriptDiagnostics);
        var at = broken.IndexOf("Nowhere", StringComparison.Ordinal);
        Assert.Equal(message.Message, pane.MessageAt(at));
        Assert.Null(pane.MessageAt(broken.IndexOf("Odile", StringComparison.Ordinal)));

        // The mark follows the text that is typed before it
        view.Do(() => pane.Document.Insert(0, "// A comment\n"));
        Assert.False(editor.IsAnalysisCurrent);
        Assert.Equal(message.Message, pane.MessageAt(at + "// A comment\n".Length));
        Assert.Null(pane.MessageAt(at - 20));

        var problems = view.View.FindControl<ListBox>("Problems")!;
        Assert.True(problems.IsVisible);
        Assert.Equal(1, problems.ItemCount);

        // The stub of the node that is nowhere is marked as broken
        Assert.Contains("broken", view.View.NodeGraph.ContainerFromNode(editor.Stubs.Single(s => s.IsBroken))!.Classes);

        // A place asked of the editor is where the caret goes, in a node that the pane then shows
        view.Do(() => editor.SelectedCards.Add(editor.Cards[2]));
        Assert.False(pane.IsLineShown(1));
        view.Do(() => editor.RequestPlace(new YarnTextPlace(5, 3)));
        Assert.Equal((5, 3), (pane.TextArea.Caret.Line, pane.TextArea.Caret.Column));
        Assert.True(pane.IsLineShown(5));
        Assert.Equal([editor.Cards[0]], editor.SelectedCards);
    }

    [AvaloniaFact]
    public void A_card_dragged_on_the_graph_gets_its_place_in_the_text()
    {
        using var view = Open();
        var (editor, graph) = (view.Editor, view.View.NodeGraph);
        var start = editor.Cards[0];
        var container = graph.ContainerFromNode(start)!;
        var from = container.TranslatePoint(new Point(120, 8), view.Window)!.Value;

        view.Do(() =>
        {
            view.Window.MouseDown(from, MouseButton.Left);
            view.Window.MouseMove(from + new Vector(30, 20));
            view.Window.MouseMove(from + new Vector(60, 40));
            view.Window.MouseUp(from + new Vector(60, 40), MouseButton.Left);
        });

        var expected = new YarnGraphPoint((int)Math.Round(60 / graph.Zoom), (int)Math.Round(40 / graph.Zoom));
        Assert.Equal(expected, start.Position);
        Assert.Contains($"title: Start\nposition: {expected.X},{expected.Y}\n", editor.TextBuffer.Text);
        Assert.Equal("Move node", view.Session.Steps[^1]);
        Assert.Equal(new Point(expected.X, expected.Y), graph.GetNodePosition(start));
        Assert.Equal([start], editor.SelectedCards);

        // The card that had no place of its own keeps the one it was shown at
        Assert.True(editor.Cards[2].HasPosition);
    }

    [AvaloniaFact]
    public void A_card_is_renamed_in_a_field_at_the_card()
    {
        using var view = Open();
        var editor = view.Editor;
        var start = editor.Cards[0];

        void Enter(string title) => view.Do(() =>
        {
            var box = view.View.BeginRename(start)!;
            box.Text = title;
            box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        });

        Enter("Pier");
        Assert.Equal("Start", start.Title);
        Assert.Equal("A node named 'Pier' already exists.", editor.Problem);

        Enter("Dawn");
        Assert.Equal("Dawn", start.Title);
        Assert.Null(editor.Problem);
        Assert.StartsWith("title: Dawn\n", editor.TextBuffer.Text);
    }

    [AvaloniaFact]
    public void Cards_that_go_together_are_framed()
    {
        using var view = Open();
        var editor = view.Editor;

        // The cluster of a node is a frame
        var frame = Assert.Single(YarnGraphDecorations.Frames(editor));
        Assert.Equal("Shore", frame.Label);
        Assert.Equal([editor.Cards[1]], frame.Cards);

        // So are the nodes of one value of the header the cards are grouped by, around the others
        view.Do(() => editor.GroupBy = "quest");
        var frames = YarnGraphDecorations.Frames(editor);
        Assert.Equal(["quest: keeper", "Shore"], frames.Select(f => f.Label));
        Assert.Equal([editor.Cards[2]], frames[0].Cards);
        Assert.True(frames[0].Padding > frames[1].Padding);
        Assert.Equal("quest", view.View.FindControl<ComboBox>("GroupByBox")!.SelectedItem);

        // And the nodes of a node group
        view.Do(() => editor.AddToGroup(editor.Cards[0]));
        frames = YarnGraphDecorations.Frames(editor);
        Assert.Equal(["quest: keeper", "Shore", "Start"], frames.Select(f => f.Label));
        Assert.Equal(2, frames[2].Cards.Count);

        // A card that the search leaves out is dimmed
        view.Do(() => editor.Search = "fisher");
        Assert.Contains("filtered", view.View.NodeGraph.ContainerFromNode(editor.Cards[0])!.Classes);
        Assert.DoesNotContain("filtered", view.View.NodeGraph.ContainerFromNode(editor.Cards[1])!.Classes);
    }
}
