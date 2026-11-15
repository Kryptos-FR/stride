using Avalonia.Controls;

namespace ColorRise.Saga.Editor.Avalonia.Views;

/// <summary>
/// The view of the story timeline panel: the buttons to go back and forth and to replay, the steps and lanes of the run, and what
/// the step at the cursor did.
/// </summary>
public sealed partial class StoryTimelineView : UserControl
{
    public StoryTimelineView()
    {
        InitializeComponent();
    }
}
