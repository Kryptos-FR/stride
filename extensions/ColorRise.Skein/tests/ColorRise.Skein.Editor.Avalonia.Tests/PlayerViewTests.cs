using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ColorRise.Skein.Editor.Avalonia.Views;
using ColorRise.Skein.Editor.Player;
using ColorRise.Skein.Editor.Tests;

namespace ColorRise.Skein.Editor.Avalonia.Tests;

/// <summary>
/// The views of the dialogue player and of its timeline show the run of the session, the script editor marks where the
/// run is, and what the user does in them drives the run.
/// </summary>
public sealed class PlayerViewTests
{
    private const string Lamp =
        "title: Start\nposition: 0,0\n---\n<<declare $gold = 5 as number>>\nOdile: The tide is out.\n"
        + "<<if has_item(\"oil\")>>\n    Odile: Your lamp is full.\n<<endif>>\n"
        + "-> Take the lamp\n    <<jump Pier>>\n-> Leave it <<if $gold > 100>>\n    Odile: As you like.\n===\n"
        + "title: Pier\nposition: 340,0\n---\n=> Odile: Mind the oil.\n=> Odile: Mind the wick. <<if $gold > 10>>\nNarrator: She leaves.\n===\n";

    private static ScriptView Open() => ScriptView.Open("Dialogue/Act1", SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Lamp));

    private static int LineOf(string part) => Lamp[..Lamp.IndexOf(part, StringComparison.Ordinal)].Count(c => c == '\n') + 1;

    // An input of the player, given as the views give theirs: in a job of the UI thread. The player plays with no wait,
    // so the input is over once the UI thread ran its jobs
    private static void Play(ScriptView view, Func<Task> input)
    {
        view.Do(() =>
        {
            Task? played = null;
            Dispatcher.UIThread.Post(() => played = input());
            Dispatcher.UIThread.RunJobs();
            Assert.True(played?.IsCompletedSuccessfully, $"{played?.Status} {played?.Exception}");
        });
    }

    private static bool IsShown(Control control) => control.IsAttachedToVisualTree() && control.IsEffectivelyVisible;

    private static List<T> Shown<T>(Control within)
        where T : Control
        => [.. within.GetVisualDescendants().OfType<T>().Where(IsShown)];

    // A click of the mouse in the middle of a control
    private static void Click(ScriptView view, Control control)
    {
        view.Do(() =>
        {
            var window = Assert.IsType<Window>(TopLevel.GetTopLevel(control));
            var place = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
            window.MouseDown(place, MouseButton.Left);
            window.MouseUp(place, MouseButton.Left);
        });
    }

    private static List<string> Texts(Control within) => [.. Shown<TextBlock>(within).Select(t => t.Text ?? string.Concat(t.Inlines?.Select(i => (i as Run)?.Text) ?? [])).Where(t => t.Length > 0)];

    [AvaloniaFact]
    public void The_player_view_shows_the_run_and_drives_it()
    {
        using var script = Open();
        using var player = new PanelView<DialoguePlayerViewModel, DialoguePlayerView>(script.Session, DialoguePlayerViewModel.Panel, 360, 800);
        var (panel, view) = (player.Panel, player.View);
        var transcript = view.FindControl<ItemsControl>("TranscriptList")!;
        var options = view.FindControl<ItemsControl>("OptionsList")!;
        var question = view.FindControl<ContentControl>("QuestionHost")!;
        var go = view.FindControl<Button>("ContinueButton")!;

        // Nothing plays: the project and its first node are picked, and only Play is offered
        Assert.Equal("Start", view.FindControl<ComboBox>("NodeBox")!.SelectedItem);
        Assert.True(view.FindControl<Button>("StartButton")!.IsEffectivelyEnabled);
        Assert.False(IsShown(go));
        Assert.False(IsShown(transcript));

        Click(script, view.FindControl<Button>("StartButton")!);

        // The node, then the line with its speaker; the run waits for the user to go on
        Assert.True(panel.IsPlaying);
        Assert.Equal(["▸", "Start", "Odile", "The tide is out."], Texts(transcript));
        Assert.True(IsShown(go));
        Assert.Empty(Shown<Button>(options));

        // The scripts ask the game: the question shows with its two answers
        Click(script, go);
        Assert.NotNull(panel.Question);
        Assert.Contains(Texts(question), t => t.EndsWith("has_item(\"oil\")", StringComparison.Ordinal));
        var answers = Shown<Button>(question);
        Assert.Equal(["true", "false"], answers.Select(b => b.Content));
        Assert.False(IsShown(go));

        // Answered, the dialogue goes on to its options: the one whose condition fails cannot be chosen
        Click(script, answers[0]);
        Assert.Null(panel.Question);
        Assert.Empty(Shown<Button>(question));
        Click(script, go);
        var choices = Shown<Button>(options);
        Assert.Equal(2, choices.Count);
        Assert.Equal(["Take the lamp", "Leave it"], Texts(options));
        Assert.True(choices[0].IsEffectivelyEnabled);
        Assert.False(choices[1].IsEffectivelyEnabled);
        Assert.Contains("has_item(\"oil\")", Texts(view.FindControl<ItemsControl>("AnswersList")!));
        Assert.Contains("$gold", Texts(view.FindControl<ItemsControl>("VariablesList")!));

        // Chosen: the line group picks, and the transcript tells why
        Click(script, choices[0]);
        var said = Texts(transcript);
        Assert.Contains("> Take the lamp", said);
        Assert.Contains("Mind the oil.", said);
        Assert.Contains(said, t => t.Contains("Mind the wick.", StringComparison.Ordinal) && t.Contains("fails", StringComparison.Ordinal));
        Assert.Empty(Shown<Button>(options));
    }

    [AvaloniaFact]
    public void The_timeline_view_shows_the_steps_and_a_click_moves_the_run()
    {
        using var script = Open();
        using var timeline = new PanelView<DialogueTimelineViewModel, DialogueTimelineView>(script.Session, DialogueTimelineViewModel.Panel, 1200, 240);
        var (panel, control) = (timeline.Panel, timeline.View.Timeline);
        var service = script.Session.Player;
        var back = timeline.View.FindControl<Button>("BackButton")!;
        Assert.False(back.IsEffectivelyEnabled);

        Play(script, () => service.Start(script.Session.ProjectAt("Dialogue/Story"), "Start"));
        Play(script, () => service.Continue());
        Play(script, () => service.Answer("has_item", false));
        Play(script, () => service.Choose(0));

        // A column per step and a row per lane, the cursor on the last step
        Assert.Equal(service.Steps.Count, control.Steps!.Count);
        Assert.Equal(["Nodes", "Lines", "Choices", "Game", "Variables"], control.Lanes!.Select(l => l.Title));
        Assert.Equal(service.Steps.Count - 1, control.CurrentStep);
        Assert.Equal(DialogueTimelineControl.TitleWidth + service.Steps.Count * DialogueTimelineControl.StepWidth + 8, control.Bounds.Width);
        Assert.True(back.IsEffectivelyEnabled);

        // The tooltip of the pick tells why each candidate was or was not chosen
        var pick = service.Steps.ToList().FindIndex(s => s.Kind == PlayerStepKind.Saliency);
        var tip = control.TipAt(control.ColumnCentre(pick));
        Assert.Contains("✓ Odile: Mind the oil.", tip);
        Assert.Contains("✗ Odile: Mind the wick.", tip);

        // A click on the column of a step moves the run there, and the player goes back with it
        var line = service.Steps.ToList().FindIndex(s => s.Kind == PlayerStepKind.Line);
        script.Do(() =>
        {
            var place = control.TranslatePoint(control.ColumnCentre(line), timeline.Window)!.Value;
            timeline.Window.MouseDown(place, MouseButton.Left);
            timeline.Window.MouseUp(place, MouseButton.Left);
        });
        Assert.Equal(line, service.Cursor);
        Assert.Equal(line, control.CurrentStep);
        Assert.Contains(timeline.View.FindControl<ItemsControl>("DetailsList")!.GetVisualDescendants().OfType<SelectableTextBlock>(), t => t.Text == $"Step {line}: Odile: The tide is out.");

        // The arrows of the keyboard and the buttons step through the run
        script.Do(() => control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right }));
        Assert.Equal(line + 1, service.Cursor);
        Click(script, back);
        Assert.Equal(line, service.Cursor);
    }

    [AvaloniaFact]
    public void The_script_view_marks_the_nodes_and_the_line_of_the_run()
    {
        using var script = Open();
        var (editor, graph, pane) = (script.Editor, script.View.NodeGraph, script.View.Text);
        var service = script.Session.Player;
        Assert.Null(pane.PlayLine);

        // Play, from the toolbar: the first node of the script
        Click(script, script.View.FindControl<Button>("PlayButton")!);
        Assert.True(service.IsPlaying);
        Assert.Equal("Start", service.StartNode);
        Assert.Contains("current", graph.ContainerFromNode(editor.Cards[0])!.Classes);
        Assert.DoesNotContain("current", graph.ContainerFromNode(editor.Cards[1])!.Classes);
        Assert.Equal(LineOf("Odile: The tide is out."), pane.PlayLine);

        // The run goes on to the other node
        Play(script, () => service.Continue());
        Play(script, () => service.Answer("has_item", false));
        Play(script, () => service.Choose(0));
        Assert.Contains("visited", graph.ContainerFromNode(editor.Cards[0])!.Classes);
        Assert.Contains("current", graph.ContainerFromNode(editor.Cards[1])!.Classes);
        Assert.Equal(LineOf("=> Odile: Mind the oil."), pane.PlayLine);

        script.Do(service.Reset);
        Assert.DoesNotContain("visited", graph.ContainerFromNode(editor.Cards[0])!.Classes);
        Assert.DoesNotContain("current", graph.ContainerFromNode(editor.Cards[1])!.Classes);
        Assert.Null(pane.PlayLine);
    }
}
