using Avalonia.Markup.Xaml.Styling;
using ColorRise.Saga.Editor.Avalonia.Views;
using ColorRise.Saga.Editor.Player;
using Stride.Core.Assets.Editor.Services;
using Stride.Editor.Avalonia;

namespace ColorRise.Saga.Editor.Avalonia;

/// <summary>
/// Saga's entry point into the cross-platform Game Studio: the views of its editors, of the story player and of the story
/// timeline. The editor views are found by their <c>AssetEditorView</c> attribute; this plugin adds the look they share to the
/// application.
/// </summary>
public sealed class SagaEditorAvaloniaPlugin : StrideAssetsAvaloniaPlugin
{
    /// <summary>
    /// The dictionary of the look of Saga's views: the colors and the template of the graph nodes, the colors of the
    /// timeline.
    /// </summary>
    public static readonly Uri StylesUri = new("avares://ColorRise.Saga.Editor.Avalonia/Themes/SagaStyles.axaml");

    /// <inheritdoc />
    public override void Initialize(IEditorPluginContext context)
    {
        base.Initialize(context);
        RegisterControlThemes(context.Logger, new ResourceInclude(StylesUri) { Source = StylesUri });
        context.ViewTypes.Register(typeof(StoryPlayerViewModel), typeof(StoryPlayerView));
        context.ViewTypes.Register(typeof(StoryTimelineViewModel), typeof(StoryTimelineView));
    }
}
