using Avalonia.Controls;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// The view of the dialogue timeline panel: the buttons to go back and forth and to replay, the steps and the lanes of
/// the run, and what the step at the cursor did.
/// </summary>
public sealed partial class DialogueTimelineView : UserControl
{
    /// <summary>
    /// Makes the view of the dialogue timeline.
    /// </summary>
    public DialogueTimelineView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The steps and the lanes, drawn.
    /// </summary>
    public DialogueTimelineControl Timeline => TimelineControl;
}
