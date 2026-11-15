using Stride.Core.Assets.Editor.Services;
using Stride.Editor.Avalonia;

namespace ColorRise.Skein.Editor.Avalonia;

/// <summary>
/// Skein's entry point into the cross-platform Game Studio: the views of its editors and panels. The editor views are found by
/// their <c>AssetEditorView</c> attribute; this plugin registers the views of the panels and the look they share.
/// </summary>
public sealed class SkeinEditorAvaloniaPlugin : StrideAssetsAvaloniaPlugin
{
    /// <inheritdoc />
    public override void Initialize(IEditorPluginContext context)
    {
        base.Initialize(context);
    }
}
