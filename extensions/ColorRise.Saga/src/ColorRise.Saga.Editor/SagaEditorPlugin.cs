using ColorRise.Saga.Editor.Player;
using Stride.Core.Assets.Editor.Services;
using Stride.Editor;

namespace ColorRise.Saga.Editor;

/// <summary>
/// Saga's entry point into the Game Studio: what the assembly declares with attributes (asset view models and editors) is
/// found through it, and it registers the story player panel and Saga's commands, and the playback service of each session
/// (<see cref="SagaPlaybackService"/>). The views come from the toolkit companion (<c>ColorRise.Saga.Editor.Avalonia</c>).
/// </summary>
public sealed class SagaEditorPlugin : StrideAssetsPlugin
{
    /// <inheritdoc />
    public override void Initialize(IEditorPluginContext context)
    {
        base.Initialize(context);
        context.Panels.Register(StoryPlayerViewModel.Panel);
        context.Panels.Register(StoryTimelineViewModel.Panel);
        foreach (var command in SagaCommands.Create())
            context.Commands.Register(command);
    }

    /// <inheritdoc />
    public override void InitializeSession(ISessionPluginContext context)
    {
        base.InitializeSession(context);
        // The story played in the session, which the player drives and the timeline and editors follow
        var playback = SagaPlaybackService.Of(context.Session);
        context.Services.Register(playback);
        context.OnClose(playback.Dispose);
    }
}
