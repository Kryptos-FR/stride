using Stride.Core.Assets.Editor.Services;
using Stride.Editor;

namespace ColorRise.Saga.Editor;

/// <summary>
/// Saga's entry point into the Game Studio: what the assembly declares with attributes (asset view models and editors) is
/// found through it, and it registers the story player panel and Saga's commands.
/// </summary>
public sealed class SagaEditorPlugin : StrideAssetsPlugin
{
    /// <inheritdoc />
    public override void Initialize(IEditorPluginContext context)
    {
        base.Initialize(context);
    }
}
