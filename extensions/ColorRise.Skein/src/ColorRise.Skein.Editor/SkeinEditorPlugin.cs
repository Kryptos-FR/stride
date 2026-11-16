using ColorRise.Skein.Editor.Player;
using ColorRise.Skein.Editor.PropertyGrid;
using ColorRise.Skein.Editor.Services;
using ColorRise.Skein.Editor.Templates;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Assets.Templates;
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
        context.Panels.Register(DialoguePlayerViewModel.Panel);
        context.Panels.Register(DialogueTimelineViewModel.Panel);
        foreach (var command in SkeinCommands.Create())
        {
            context.Commands.Register(command);
        }
    }

    /// <inheritdoc />
    public override void InitializeSession(ISessionPluginContext context)
    {
        base.InitializeSession(context);

        var analysis = new YarnAnalysisService(context.Session);
        context.Services.Register(analysis);
        context.OnClose(analysis.Close);

        var externalChanges = new YarnExternalChangeService(context.Session);
        context.Services.Register(externalChanges);
        context.OnClose(externalChanges.Close);

        context.Services.Register(new YarnLocalisationService(context.Session));

        var player = DialoguePlayerService.Of(context.Session);
        context.Services.Register(player);
        context.OnClose(player.Close);

        context.NodePresenterUpdaters.Register(new YarnNodeUpdater(context.Session));

        // The template manager looks at the last registered generator first: this one goes before Stride's own for the
        // "Yarn project" template
        var projectTemplate = new YarnProjectTemplateGenerator();
        TemplateManager.Register(projectTemplate);
        context.OnClose(() => TemplateManager.Unregister(projectTemplate));
    }
}
