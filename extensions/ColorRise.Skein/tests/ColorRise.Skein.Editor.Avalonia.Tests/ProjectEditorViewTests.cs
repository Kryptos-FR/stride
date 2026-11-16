using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ColorRise.Skein.Assets;
using ColorRise.Skein.Editor.Avalonia.Views;
using ColorRise.Skein.Editor.Tests;
using ColorRise.Skein.Editor.ViewModels;

namespace ColorRise.Skein.Editor.Avalonia.Tests;

/// <summary>
/// The view of the overview of a project shows what the overview has.
/// </summary>
public sealed class ProjectEditorViewTests
{
    private const string Act1 = "title: Arrival\n---\n/// The purse\n<<declare $gold = 5 as number>>\nNarrator: Here. #line:here\nNarrator: No id.\n<<jump Nowhere>>\n===\n";

    [AvaloniaFact]
    public void The_view_shows_the_scripts_the_variables_the_languages_and_the_problems()
    {
        // The session is opened and read beside the UI thread, and nothing is compiled again while the view is there
        var (session, editor) = Task.Run(async () =>
        {
            var project = new YarnProjectAssetFactory().New();
            project.Localisations.Add(new YarnLocalisation { Language = "fr" });
            var opened = await SkeinSession.OpenAsync(("Dialogue/Story", project), SkeinSession.Script("Dialogue/Act1", Act1));
            var made = opened.EditProject("Dialogue/Story");
            await made.WhenRefreshed();
            opened.Analysis.Delay = TimeSpan.FromDays(1);
            return (opened, made);
        }).GetAwaiter().GetResult();

        YarnProjectEditorView? view = null;
        Window? window = null;
        try
        {
            session.Session.Dispatcher.Invoke(() =>
            {
                view = new YarnProjectEditorView { DataContext = editor };
                window = new Window { Width = 1000, Height = 800, Content = view };
                window.Show();
                Dispatcher.UIThread.RunJobs();
            });

            var texts = view!.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(TextOf).ToList();
            Assert.Contains("Story", texts);
            Assert.Contains("1 script  ·  1 node  ·  2 lines  ·  1 variable", texts);
            Assert.Contains("Dialogue/Act1.yarn", texts);
            Assert.Contains("$gold", texts);
            Assert.Contains("The purse", texts);
            Assert.Contains("fr", texts);
            Assert.Contains("(base)", texts);
            Assert.Contains("Jump to undefined node: 'Nowhere'", texts);
            Assert.Contains(texts, t => t.Contains("1 line has no line id", StringComparison.Ordinal));
            Assert.Contains("1 line has no id and cannot be translated.", texts);

            // Both languages can be exported, and the buttons run the editor's commands
            var buttons = view!.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).ToList();
            Assert.Equal(2, buttons.Count(b => Equals(b.Content, "Export strings")));
            Assert.Same(editor.AddLineIdsCommand, view!.FindControl<Button>("AddLineIdsButton")!.Command);
            Assert.Same(editor.UpdateStringsCommand, view!.FindControl<Button>("UpdateStringsButton")!.Command);
        }
        finally
        {
            session.Session.Dispatcher.Invoke(() =>
            {
                view?.DataContext = null;
                window?.Close();
                Dispatcher.UIThread.RunJobs();
            });
            Task.Run(async () => await session.DisposeAsync()).GetAwaiter().GetResult();
        }
    }

    // The text of a block, with the text of its runs
    private static string TextOf(TextBlock block)
        => block.Inlines is { Count: > 0 } inlines ? string.Concat(inlines.OfType<Run>().Select(r => r.Text)) : block.Text ?? "";
}
