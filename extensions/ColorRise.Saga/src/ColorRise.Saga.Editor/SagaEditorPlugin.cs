using ColorRise.Saga.Editor.Player;
using Stride.Core.Assets.Editor.Services;
using Stride.Editor;

namespace ColorRise.Saga.Editor;

/// <summary>
/// Saga's entry point into the Game Studio: what the assembly declares with attributes (asset view models and editors) is
/// found through it, and it registers the story player panel and Saga's commands. The views come from the toolkit companion
/// (<c>ColorRise.Saga.Editor.Avalonia</c>).
/// </summary>
public sealed class SagaEditorPlugin : StrideAssetsPlugin
{
    /// <inheritdoc />
    public override void Initialize(IEditorPluginContext context)
    {
        base.Initialize(context);
        context.Panels.Register(StoryPlayerViewModel.Panel);
        foreach (var command in SagaCommands.Create())
            context.Commands.Register(command);
    }
}
