using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using ColorRise.Saga.Editor.Player;

namespace ColorRise.Saga.Editor.Avalonia.Views;

/// <summary>
/// The view of the story player panel: the story to play, where it is, its variables, events and functions by hand, the
/// characters to talk to, and the transcript with the choices.
/// </summary>
public sealed partial class StoryPlayerView : UserControl
{
    private StoryPlayerViewModel? player;

    public StoryPlayerView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (player is not null)
            player.Transcript.CollectionChanged -= OnTranscriptChanged;
        player = DataContext as StoryPlayerViewModel;
        if (player is not null)
            player.Transcript.CollectionChanged += OnTranscriptChanged;
    }

    private void OnTranscriptChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // The last line in view, once it is laid out
        if (e.Action == NotifyCollectionChangedAction.Add)
            Dispatcher.UIThread.Post(TranscriptScroll.ScrollToEnd, DispatcherPriority.Background);
    }
}
