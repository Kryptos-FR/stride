using Stride.Core.Assets.Editor.Services;
using Stride.Editor;

namespace ColorRise.Skein.Editor;

/// <summary>
/// Skein's entry point into Game Studio: what the assembly declares with attributes (asset view models and editors) is found
/// through it, and it registers Skein's panels, commands and session services. The views come from the toolkit companion
/// (<c>ColorRise.Skein.Editor.Avalonia</c>).
/// </summary>
public sealed class SkeinEditorPlugin : StrideAssetsPlugin
{
    /// <inheritdoc />
    public override void Initialize(IEditorPluginContext context)
    {
        base.Initialize(context);
    }

    /// <inheritdoc />
    public override void InitializeSession(ISessionPluginContext context)
    {
        base.InitializeSession(context);
    }
}
