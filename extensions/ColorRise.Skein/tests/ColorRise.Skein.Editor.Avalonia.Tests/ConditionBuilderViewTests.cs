using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ColorRise.Skein.Assets.Text;
using ColorRise.Skein.Editor.Avalonia.Views;
using ColorRise.Skein.Editor.Conditions;
using ColorRise.Skein.Editor.Graph;
using ColorRise.Skein.Editor.Tests;

namespace ColorRise.Skein.Editor.Avalonia.Tests;

/// <summary>
/// The condition and set builders open in a popover from the cards and the property grid, and write what they make.
/// </summary>
public sealed class ConditionBuilderViewTests
{
    private const string Act1 =
        "title: Vars\nposition: 0,0\n---\n<<declare $met = false>>\n<<declare $coins = 0>>\n===\n"
        + "title: Start\nposition: 340,0\n---\n"
        + "Odile: Hello.\n"
        + "-> Ask <<if $met>>\n"
        + "    Odile: Sure.\n"
        + "<<set $coins to 1>>\n"
        + "===\n";

    private static ScriptView Open() => ScriptView.Open("Dialogue/Act1", SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1));

    private static TView Popover<TView>(Control anchor) where TView : Control
        => Assert.IsType<TView>(YarnBuilderPopups.GetOpenBuilder(anchor));

    private static void Click(Control control, string content)
        => control.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == content).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void A_click_on_the_condition_of_an_option_opens_the_builder_and_ok_writes_it()
    {
        using var view = Open();
        var (editor, graph, window) = (view.Editor, view.View.NodeGraph, view.Window);
        var start = editor.Cards.Single(c => c.Title == "Start");
        var container = graph.ContainerFromNode(start)!;
        Border Chip(YarnCardRowViewModel row, string kind) => container.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains(kind) && ReferenceEquals(b.DataContext, row));
        Point Middle(Control control) => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

        // The option shows its condition as a chip; a line without one has "+ if" instead
        var ask = start.Rows.Single(r => r.Display == "Ask");
        var hello = start.Rows[0];
        var chip = Chip(ask, "condition");
        Assert.True(chip.IsEffectivelyVisible);
        Assert.False(Chip(ask, "addif").IsVisible);
        Assert.True(Chip(hello, "addif").IsVisible);

        view.Do(() =>
        {
            window.MouseDown(Middle(chip), MouseButton.Left);
            window.MouseUp(Middle(chip), MouseButton.Left);
        });
        var builderView = Popover<YarnConditionBuilderView>(chip);
        var builder = (YarnConditionBuilderViewModel)builderView.DataContext!;
        Assert.Equal("$met", builder.Condition);
        Assert.False(ask.IsEditing);

        // The rows of the builder edit its terms; OK writes, as one step
        var term = Assert.Single(builder.Terms);
        view.Do(() => term.Comparison = YarnComparison.IsFalse);
        Assert.Equal("<<if not $met>>", builderView.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).Single(t => t?.StartsWith("<<", StringComparison.Ordinal) == true));
        view.Do(() => Click(builderView, "OK"));
        Assert.Contains("-> Ask <<if not $met>>\n", editor.TextBuffer.Text);
        Assert.Equal("Edit condition", view.Session.Steps[^1]);
        Assert.Null(YarnBuilderPopups.GetOpenBuilder(chip));
    }

    [AvaloniaFact]
    public void A_line_gets_a_condition_from_its_plus_if_and_cancel_writes_nothing()
    {
        using var view = Open();
        var (editor, graph, window) = (view.Editor, view.View.NodeGraph, view.Window);
        var start = editor.Cards.Single(c => c.Title == "Start");
        var container = graph.ContainerFromNode(start)!;
        var addIf = container.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("addif") && ReferenceEquals(b.DataContext, start.Rows[0]));
        var at = addIf.TranslatePoint(new Point(addIf.Bounds.Width / 2, addIf.Bounds.Height / 2), window)!.Value;

        view.Do(() =>
        {
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
        });
        var builderView = Popover<YarnConditionBuilderView>(addIf);
        var builder = (YarnConditionBuilderViewModel)builderView.DataContext!;
        Assert.Empty(builder.Terms);

        // "+ condition" adds a row; its boxes take what is typed
        view.Do(() => Click(builderView, "+ condition"));
        var term = Assert.Single(builder.Terms);
        view.Do(() =>
        {
            var boxes = builderView.GetVisualDescendants().OfType<YarnValueBox>().Where(b => b.IsEffectivelyVisible).ToList();
            boxes[0].Text = "$coins";
        });
        Assert.Equal("$coins", term.Subject);

        // The comparison box shows the comparison of the row after its list changed with the type
        var comparisonBox = builderView.GetVisualDescendants().OfType<ComboBox>().Single(b => b.Name == "ComparisonBox");
        Assert.Equal(YarnComparison.Equal, comparisonBox.SelectedItem);
        view.Do(() => builderView.GetVisualDescendants().OfType<YarnValueBox>().First(b => b.IsEffectivelyVisible).Text = "$met");
        Assert.Equal(YarnComparison.IsTrue, comparisonBox.SelectedItem);
        view.Do(() => builderView.GetVisualDescendants().OfType<YarnValueBox>().First(b => b.IsEffectivelyVisible).Text = "$coins");
        view.Do(() =>
        {
            term.Comparison = YarnComparison.Greater;
            builderView.GetVisualDescendants().OfType<YarnValueBox>().Single(b => b.Name == "ValueBox" && b.IsEffectivelyVisible).Text = "2";
        });
        Assert.Equal("$coins > 2", builder.Condition);

        // Text that is not rows stays text, and its box stays ticked
        var textCheck = builderView.GetVisualDescendants().OfType<CheckBox>().Single(b => b.Name == "TextCheck");
        void ClickCheck()
        {
            var middle = textCheck.TranslatePoint(new Point(10, textCheck.Bounds.Height / 2), window)!.Value;
            window.MouseDown(middle, MouseButton.Left);
            window.MouseUp(middle, MouseButton.Left);
        }

        view.Do(ClickCheck);
        Assert.True(builder.IsText);
        view.Do(() => builder.Text = "($coins > 2 or $met) and $coins < 9");
        view.Do(ClickCheck);
        Assert.True(builder.IsText);
        Assert.NotNull(builder.Problem);
        Assert.True(textCheck.IsChecked);
        view.Do(() => builder.Text = "$coins > 2");
        view.Do(ClickCheck);
        Assert.False(textCheck.IsChecked);
        Assert.Equal("$coins > 2", builder.Condition);

        // Cancel leaves the text as it is
        view.Do(() => Click(builderView, "Cancel"));
        Assert.Equal(Act1, editor.TextBuffer.Text);
        Assert.Empty(view.Session.Steps);
    }

    [AvaloniaFact]
    public void A_double_click_on_a_set_opens_its_builder()
    {
        using var view = Open();
        var (editor, graph, window) = (view.Editor, view.View.NodeGraph, view.Window);
        var start = editor.Cards.Single(c => c.Title == "Start");
        var set = start.Rows.Single(r => r.IsAssignment);
        var row = graph.ContainerFromNode(start)!.GetVisualDescendants().OfType<Grid>().First(g => ReferenceEquals(g.DataContext, set));
        var at = row.TranslatePoint(new Point(40, row.Bounds.Height / 2), window)!.Value;

        view.Do(() =>
        {
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
        });
        var builderView = Popover<YarnAssignmentBuilderView>(row);
        var builder = (YarnAssignmentBuilderViewModel)builderView.DataContext!;
        Assert.Equal("set $coins to 1", builder.Command);

        // The operator box shows the operator after its list changed with the type
        var operatorBox = builderView.GetVisualDescendants().OfType<ComboBox>().Single(b => b.Name == "OperatorBox");
        view.Do(() => builderView.GetVisualDescendants().OfType<YarnValueBox>().Single(b => b.Name == "VariableBox").Text = "$met");
        Assert.Equal(YarnAssignmentOperator.Set, operatorBox.SelectedItem);
        view.Do(() => builderView.GetVisualDescendants().OfType<YarnValueBox>().Single(b => b.Name == "VariableBox").Text = "$coins");
        Assert.Equal(YarnAssignmentOperator.Set, operatorBox.SelectedItem);

        view.Do(() =>
        {
            builder.Operator = YarnAssignmentOperator.Add;
            builderView.GetVisualDescendants().OfType<YarnValueBox>().Single(b => b.Name == "ValueBox").Text = "3";
        });
        view.Do(() => Click(builderView, "OK"));
        Assert.Contains("<<set $coins += 3>>\n", editor.TextBuffer.Text);
        Assert.Equal("Edit set", view.Session.Steps[^1]);
    }

    [AvaloniaFact]
    public void The_box_of_a_when_header_opens_the_builder_and_takes_what_it_writes()
    {
        var knowledge = new YarnConditionKnowledge(
            [new Assets.Compilation.YarnVariableData { Name = "$met", Type = "bool" }], [], ["Pier"]);
        var box = new YarnConditionBox { Knowledge = knowledge, Text = "once if $met" };
        var window = new Window { Content = box, Width = 400, Height = 200 };
        window.Show();
        try
        {
            Assert.Equal("once if $met", box.Box.Text);

            // Typed text goes on Enter
            box.Box.Text = "$met";
            box.Box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            Assert.Equal("$met", box.Text);

            // The builder writes the header
            box.Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var builderView = Popover<YarnConditionBuilderView>(box);
            var builder = (YarnConditionBuilderViewModel)builderView.DataContext!;
            Assert.True(builder.AllowsAlways);
            builder.IsOnce = true;
            builder.Terms[0].Comparison = YarnComparison.IsFalse;
            Click(builderView, "OK");
            Assert.Equal("once if not $met", box.Text);
            Assert.Equal("once if not $met", box.Box.Text);
        }
        finally
        {
            window.Close();
        }
    }
}
