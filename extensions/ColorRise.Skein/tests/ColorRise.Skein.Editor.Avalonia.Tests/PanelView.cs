using Avalonia.Controls;
using Avalonia.Threading;
using ColorRise.Skein.Editor.Tests;
using Stride.Core.Assets.Editor.Panels;

namespace ColorRise.Skein.Editor.Avalonia.Tests;

/// <summary>
/// The view of a panel over its view model, in a window that is not shown, in the session of a <see cref="ScriptView"/>.
/// </summary>
internal sealed class PanelView<TViewModel, TView> : IDisposable
    where TViewModel : PanelViewModel
    where TView : Control, new()
{
    private readonly SkeinSession session;

    public PanelView(SkeinSession session, PanelDescriptor descriptor, double width, double height)
    {
        this.session = session;
        TViewModel? panel = null;
        TView? view = null;
        Window? window = null;
        session.Session.Dispatcher.Invoke(() =>
        {
            panel = (TViewModel)descriptor.CreateViewModel(new PanelContext { Descriptor = descriptor, Session = session.Session, ServiceProvider = session.Session.ServiceProvider });
            panel.IsShown = true;
            view = new TView { DataContext = panel };
            window = new Window { Width = width, Height = height, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();
        });
        Panel = panel!;
        View = view!;
        Window = window!;
    }

    public TViewModel Panel { get; }

    public TView View { get; }

    public Window Window { get; }

    public void Dispose()
    {
        session.Session.Dispatcher.Invoke(() =>
        {
            View.DataContext = null;
            Window.Close();
            Panel.Destroy();
            Dispatcher.UIThread.RunJobs();
        });
    }
}
