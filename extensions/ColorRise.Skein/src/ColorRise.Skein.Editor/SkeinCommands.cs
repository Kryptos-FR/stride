using ColorRise.Skein.Editor.Graph;
using ColorRise.Skein.Editor.Player;
using ColorRise.Skein.Editor.Services;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Core.Assets.Editor.Commands;
using Stride.Core.Assets.Editor.Panels;

namespace ColorRise.Skein.Editor;

/// <summary>
/// Skein's commands: play a dialogue from the script editor or the asset view, show the dialogue player, and the line ids
/// and strings files of a project. All of them are in the command palette.
/// </summary>
public static class SkeinCommands
{
    /// <summary>
    /// Plays a node of the script editor in the dialogue player: the node its menu opened on, else the selected node, else
    /// the first node of the script.
    /// </summary>
    public const string PlayFromHere = "ColorRise.Skein.PlayFromHere";

    /// <summary>
    /// Plays the selected project from its node <c>Start</c>, or the selected script from its first node.
    /// </summary>
    public const string PlayProject = "ColorRise.Skein.PlayProject";

    /// <summary>
    /// Shows the dialogue player.
    /// </summary>
    public const string OpenDialoguePlayer = "ColorRise.Skein.OpenDialoguePlayer";

    /// <summary>
    /// Adds a line id to each line of the selected project that has none.
    /// </summary>
    public const string AddLineIds = "ColorRise.Skein.AddLineIds";

    /// <summary>
    /// Writes the strings file of each language of the selected project that has none.
    /// </summary>
    public const string ExportStrings = "ColorRise.Skein.ExportStrings";

    /// <summary>
    /// "Export recording script" on a Yarn project.
    /// </summary>
    public const string ExportRecordingScript = "ColorRise.Skein.ExportRecordingScript";

    /// <summary>
    /// Brings the strings files of the selected project up to date with its scripts.
    /// </summary>
    public const string UpdateStrings = "ColorRise.Skein.UpdateStrings";

    /// <summary>
    /// The category of Skein's commands in the palette and the shortcut settings, and their group in a menu.
    /// </summary>
    public const string Category = "Dialogue";

    /// <summary>
    /// The location of the menu of the script editor's graph, whose selection is the card the menu opened on.
    /// </summary>
    public const string GraphContext = "ColorRise.Skein/Graph/Context";

    /// <summary>
    /// Creates the descriptors of the commands.
    /// </summary>
    public static IEnumerable<CommandDescriptor> Create()
    {
        yield return new CommandDescriptor
        {
            Id = PlayFromHere,
            Title = () => "Play from here",
            Category = Category,
            Keywords = ["yarn", "dialogue", "node", "player", "run"],
            // F5 starts the game: in a script editor, Ctrl+F5 starts the dialogue
            DefaultGesture = "Ctrl+F5",
            GestureScope = typeof(YarnScriptEditorViewModel),
            CanExecute = context => CardOf(context) is not null,
            ExecuteAsync = context => CardOf(context) is { } card && context.Editor is YarnScriptEditorViewModel editor ? editor.PlayFromHere(card) : Task.CompletedTask,
        };
        yield return new CommandDescriptor
        {
            Id = PlayProject,
            Title = () => "Play dialogue",
            Category = Category,
            Keywords = ["yarn", "story", "player", "run"],
            Placements = [new CommandPlacement(CommandLocations.AssetViewContext, Category)],
            CanExecute = context => PlayableOf(context) is not null,
            ExecuteAsync = PlayAsync,
        };
        yield return new CommandDescriptor
        {
            Id = OpenDialoguePlayer,
            Title = () => "Open dialogue player",
            Category = Category,
            Keywords = ["yarn", "story", "panel"],
            CanExecute = context => context.Session.ServiceProvider.TryGet<IPanelService>() is not null,
            ExecuteAsync = context =>
            {
                context.Session.ServiceProvider.TryGet<IPanelService>()?.Show(DialoguePlayerViewModel.PanelId);
                return Task.CompletedTask;
            },
        };
        yield return OnProject(AddLineIds, "Add line ids", 10, ["yarn", "localisation", "translation", "tag"], editor => editor.AddLineIds());
        yield return OnProject(ExportStrings, "Export strings", 20, ["yarn", "localisation", "translation", "csv"], editor => editor.ExportMissingStrings());
        yield return OnProject(UpdateStrings, "Update strings files", 30, ["yarn", "localisation", "translation", "csv"], editor => editor.UpdateStrings());
        yield return OnProject(ExportRecordingScript, "Export recording script", 40, ["yarn", "voice", "voice-over", "audio", "actor", "csv"], editor => editor.ExportRecordingScripts());
    }

    /// <summary>
    /// A command that runs in the overview of a project, which shows what it did.
    /// </summary>
    private static CommandDescriptor OnProject(string id, string title, int order, string[] keywords, Func<YarnProjectEditorViewModel, Task> run) => new()
    {
        Id = id,
        Title = () => title,
        Category = Category,
        Keywords = keywords,
        Placements = [new CommandPlacement(CommandLocations.AssetViewContext, Category, order)],
        CanExecute = context => ProjectOf(context) is not null,
        ExecuteAsync = async context =>
        {
            if (ProjectOf(context) is { } project && await YarnProjectEditorViewModel.OpenEditor(project) is { } editor)
            {
                await run(editor);
            }
        },
    };

    // The menu of the graph gives the card it opened on. The shortcut and the palette give the editor that has the focus
    private static YarnNodeCardViewModel? CardOf(CommandContext context)
    {
        if (context.Editor is not YarnScriptEditorViewModel editor)
            return null;

        var card = context.Selection is [YarnNodeCardViewModel chosen]
            ? chosen
            : editor.SelectedCards.OfType<YarnNodeCardViewModel>().FirstOrDefault(c => !c.IsNote) ?? editor.Cards.FirstOrDefault(c => !c.IsNote);
        return card is { IsNote: false } ? card : null;
    }

    // The asset view gives its selection, and so does the palette, with the editor that has the focus: the selected
    // project, else the project of the overview
    private static YarnProjectViewModel? ProjectOf(CommandContext context)
        => context.Selection is [YarnProjectViewModel project] ? project
            : context.Editor is YarnProjectEditorViewModel editor ? editor.Asset
            : null;

    /// <summary>
    /// The project the player plays for the selection, and the script to start in when the selection is a script.
    /// </summary>
    private static (YarnProjectViewModel Project, YarnScriptViewModel? Script)? PlayableOf(CommandContext context)
    {
        if (ProjectOf(context) is { } project)
            return (project, null);

        if (context.Selection is [YarnScriptViewModel script]
            && context.Session.ServiceProvider.TryGet<YarnAnalysisService>()?.ProjectsOf(script).FirstOrDefault() is { } owner)
            return (owner, script);

        return null;
    }

    private static async Task PlayAsync(CommandContext context)
    {
        if (PlayableOf(context) is not var (project, script))
            return;

        var session = context.Session;
        var analysis = YarnAnalysisService.Of(session);
        analysis.AnalysisOf(project);
        await analysis.WhenIdle();

        // Scripts that never compiled have no node yet: the player says why they do not start
        var node = analysis.AnalysisOf(project)?.FirstNode(script) ?? YarnProjectAnalysis.DefaultStartNode;
        session.ServiceProvider.TryGet<IPanelService>()?.Show(DialoguePlayerViewModel.PanelId);
        await DialoguePlayerService.Of(session).Start(project, node);
    }
}
