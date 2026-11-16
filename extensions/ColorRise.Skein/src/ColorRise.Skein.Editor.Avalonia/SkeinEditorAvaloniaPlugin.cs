using ColorRise.Skein.Editor.Avalonia.Views;
using ColorRise.Skein.Editor.Player;
using Stride.Core.Assets.Editor.Services;
using Stride.Editor.Avalonia;

namespace ColorRise.Skein.Editor.Avalonia;

/// <summary>
/// Skein's entry point into the cross-platform Game Studio: the views of its editors and panels. The editor views are found by
/// their <c>AssetEditorView</c> attribute; this plugin registers the templates of the property grid, the views of the panels and the look they share.
/// </summary>
public sealed class SkeinEditorAvaloniaPlugin : StrideAssetsAvaloniaPlugin
{
    /// <inheritdoc />
    public override void Initialize(IEditorPluginContext context)
    {
        base.Initialize(context);

        // The picker of a Yarn node joins the property grid
        RegisterResourceDictionary(context, new YarnPropertyTemplates());

        context.ViewTypes.Register(typeof(DialoguePlayerViewModel), typeof(DialoguePlayerView));
        context.ViewTypes.Register(typeof(DialogueTimelineViewModel), typeof(DialogueTimelineView));
    }
}
