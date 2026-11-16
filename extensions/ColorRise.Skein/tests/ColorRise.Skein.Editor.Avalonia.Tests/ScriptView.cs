using Avalonia.Controls;
using Avalonia.Threading;
using ColorRise.Skein.Editor.Avalonia.Views;
using ColorRise.Skein.Editor.Tests;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Core.Assets;

namespace ColorRise.Skein.Editor.Avalonia.Tests;

/// <summary>
/// The view of the editor of a script, in a window that is not shown, over a session of the editor test kit.
/// </summary>
/// <remarks>
/// The kit's session has a dispatcher of its own, which runs its work on the threads of the pool, and a view lives on
/// Avalonia's UI thread. A test is on the UI thread and does what it does to the editor inside <see cref="Do"/>, which
/// makes that thread the session's dispatcher for the time of the call. What the session does later in the background
/// would reach the view from another thread: the scripts are compiled before the view is made, and not again.
/// </remarks>
internal sealed class ScriptView : IDisposable
{
    private readonly SkeinSession session;
    private readonly Window window;

    private ScriptView(SkeinSession session, YarnScriptEditorViewModel editor)
    {
        this.session = session;
        Editor = editor;
        YarnScriptEditorView? view = null;
        Window? made = null;
        session.Session.Dispatcher.Invoke(() =>
        {
            view = new YarnScriptEditorView { DataContext = editor };
            made = new Window { Width = 1200, Height = 700, Content = view };
            made.Show();
            Dispatcher.UIThread.RunJobs();
        });
        View = view!;
        window = made!;
    }

    public SkeinSession Session => session;

    public YarnScriptEditorViewModel Editor { get; }

    public YarnScriptEditorView View { get; }

    public Window Window => window;

    /// <summary>
    /// Opens a session with assets, compiles its scripts once, and shows the editor of one of them.
    /// </summary>
    public static ScriptView Open(string location, params (string Location, Asset Asset)[] assets)
    {
        var (session, editor) = Task.Run(async () =>
        {
            var opened = await SkeinSession.OpenAsync(assets);
            var made = opened.Edit(location);
            await opened.Analysis.WhenIdle();
            // Nothing is compiled again while the view is there
            opened.Analysis.Delay = TimeSpan.FromDays(1);
            return (opened, made);
        }).GetAwaiter().GetResult();
        return new ScriptView(session, editor);
    }

    /// <summary>
    /// Does something to the editor or to its view, as the session's dispatcher, then lets the view catch up.
    /// </summary>
    public void Do(Action action)
    {
        session.Session.Dispatcher.Invoke(() =>
        {
            action();
            Dispatcher.UIThread.RunJobs();
        });
    }

    public void Dispose()
    {
        // The view lets go of the editor before the session closes it on another thread
        session.Session.Dispatcher.Invoke(() =>
        {
            View.DataContext = null;
            window.Close();
            Dispatcher.UIThread.RunJobs();
        });
        Task.Run(async () => await session.DisposeAsync()).GetAwaiter().GetResult();
    }
}
