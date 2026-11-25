using ColorRise.Tessera.Editor.Avalonia.Views;
using Stride.Core.Assets.Editor.Services;
using Stride.Editor.Avalonia;

namespace ColorRise.Tessera.Editor.Avalonia;

/// <summary>
/// Tessera's entry point into the cross-platform Game Studio: the views of its editors, found by their
/// <c>AssetEditorView</c> attribute, and the templates it gives the property grid.
/// </summary>
public sealed class TesseraEditorAvaloniaPlugin : StrideAssetsAvaloniaPlugin
{
    /// <inheritdoc/>
    public override void Initialize(IEditorPluginContext context)
    {
        base.Initialize(context);
        // Game Studio's own editor of flags shows boxes that write nothing: Tessera's flags have an editor of their own
        RegisterResourceDictionary(context, new TesseraPropertyTemplateProviders());
    }
}
