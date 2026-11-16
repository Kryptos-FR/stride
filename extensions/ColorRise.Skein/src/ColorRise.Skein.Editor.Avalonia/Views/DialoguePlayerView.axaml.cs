using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Threading;
using ColorRise.Skein.Editor.Player;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// The view of the dialogue player panel: the node to play, what the dialogue said, the options and the questions the
/// run waits at, and the variables and the answers of the game by hand.
/// </summary>
public sealed partial class DialoguePlayerView : UserControl
{
    /// <summary>
    /// The sign of a step of the transcript that is not said: a node entered, a pick, an answer, a variable set, a failure.
    /// </summary>
    public static readonly IValueConverter StepSign = new FuncValueConverter<PlayerStepKind, string>(kind => kind switch
    {
        PlayerStepKind.NodeEnter => "▸",
        PlayerStepKind.Saliency => "≈",
        PlayerStepKind.Function => "ƒ",
        PlayerStepKind.SetVariable => "=",
        PlayerStepKind.Error => "!",
        _ => "",
    });

    private DialoguePlayerViewModel? player;

    /// <summary>
    /// Makes the view of the dialogue player.
    /// </summary>
    public DialoguePlayerView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (player is not null)
            player.Transcript.CollectionChanged -= OnTranscriptChanged;

        player = DataContext as DialoguePlayerViewModel;
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
