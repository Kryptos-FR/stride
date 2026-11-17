using System.Reflection;
using ColorRise.Skein.Assets;
using ColorRise.Skein.Assets.Localisation;
using ColorRise.Skein.Audio;
using ColorRise.Skein.Editor.EntityFactories;
using ColorRise.Skein.Editor.Player;
using ColorRise.Skein.Editor.ViewModels;
using ColorRise.Skein.UI;
using Stride.Assets.Editor.ViewModels;
using Stride.Assets.Entities;
using Stride.Assets.Presentation.ViewModels;
using Stride.Assets.Textures;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Commands;
using Stride.Core.Assets.Editor.Components.TemplateDescriptions;
using Stride.Core.Assets.Editor.Panels;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Assets.Templates;
using Stride.Core.IO;
using Stride.Core.Serialization;
using Stride.Core.Yaml;

namespace ColorRise.Skein.Editor.Tests;

/// <summary>
/// Skein's commands, its "Yarn project" template and its "Dialogue runner" entity: what they are enabled on, and what
/// they make.
/// </summary>
public sealed class SkeinCommandsTests
{
    private const string Act1 = """
        title: Reminder
        style: note
        ---
        Ask about the wick.
        ===
        title: Pier
        ---
        Odile: Mind the oil. #line:oil
        Odile: No id yet.
        ===
        title: Start
        ---
        Odile: The tide is out. #line:tide
        <<jump Pier>>
        ===

        """;

    private const string Alone = "title: Alone\n---\nKye: Nobody.\n===\n";

    private static async DispatcherTask<SkeinSession> OpenAsync(RecordingPanelService? panels = null, params string[] languages)
    {
        var project = new YarnProjectAssetFactory().New();
        project.Localisations.AddRange(languages.Select(l => new YarnLocalisation { Language = l }));
        var (dialogs, _) = ExternalChangeTests.Dialogs.Answering(1);
        return await SkeinSession.OpenAsync(
            [dialogs, DispatchProxy.Create<IProgressWindowService, ProjectEditorTests.Silent>(), panels ?? new RecordingPanelService()],
            ("Dialogue/Story", project),
            SkeinSession.Script("Dialogue/Act1", Act1),
            SkeinSession.Script("Elsewhere/Alone", Alone),
            ("Textures/Sky", new TextureAsset { Source = new UFile("sky.dds") }));
    }

    private static CommandDescriptor Command(string id) => SkeinCommands.Create().Single(c => c.Id == id);

    private static CommandContext FromAssetView(SkeinSession session, params object[] selection)
        => new() { Session = session.Session, Location = CommandLocations.AssetViewContext, Selection = selection };

    [Fact]
    public void The_commands_have_their_ids_their_category_and_their_places()
    {
        var commands = SkeinCommands.Create().ToList();

        Assert.Equal(
            ["ColorRise.Skein.PlayFromHere", "ColorRise.Skein.PlayProject", "ColorRise.Skein.OpenDialoguePlayer", "ColorRise.Skein.AddLineIds", "ColorRise.Skein.ExportStrings", "ColorRise.Skein.UpdateStrings", "ColorRise.Skein.ExportRecordingScript"],
            commands.Select(c => c.Id));
        Assert.All(commands, c => Assert.Equal(("Dialogue", true), (c.Category, c.ShowInPalette)));
        Assert.Equal(
            ["Play from here", "Play dialogue", "Open dialogue player", "Add line ids", "Export strings", "Update strings files", "Export recording script"],
            commands.Select(c => c.Title()));

        // The asset view's menu shows what acts on a project; the graph's own menu binds "Play from here" by its id
        Assert.Equal(
            [SkeinCommands.PlayProject, SkeinCommands.AddLineIds, SkeinCommands.ExportStrings, SkeinCommands.UpdateStrings, SkeinCommands.ExportRecordingScript],
            commands.Where(c => c.GetPlacement(CommandLocations.AssetViewContext) is not null).Select(c => c.Id));
        var playFromHere = commands[0];
        Assert.Empty(playFromHere.Placements);
        Assert.Equal(("Ctrl+F5", typeof(YarnScriptEditorViewModel)), (playFromHere.DefaultGesture, playFromHere.GestureScope));
    }

    [Fact]
    public async Task The_plugin_registers_the_commands()
    {
        await using var session = await OpenAsync();
        var host = Assert.IsAssignableFrom<EditorPluginHost>(session.Session.ServiceProvider.Get<IAssetsPluginService>());

        Assert.Equal(SkeinCommands.Create().Select(c => c.Id), host.Commands.Items.Select(c => c.Id).Where(id => id.StartsWith("ColorRise.Skein.", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Play_dialogue_is_for_one_project_or_one_script_of_a_project()
    {
        await using var session = await OpenAsync();
        var play = Command(SkeinCommands.PlayProject);
        var (project, script) = (session.ProjectAt("Dialogue/Story"), session.ScriptAt("Dialogue/Act1"));

        bool CanPlay(params object[] selection) => play.CanExecute!.Invoke(FromAssetView(session, selection));

        Assert.True(CanPlay(project));
        Assert.True(CanPlay(script));
        Assert.False(CanPlay());
        Assert.False(CanPlay(session.Session.AllAssets.Single(a => a.Name == "Sky")));
        Assert.False(CanPlay(project, script));
        // A script in no project has nothing the player can play
        Assert.False(CanPlay(session.ScriptAt("Elsewhere/Alone")));
    }

    [Fact]
    public async Task Play_dialogue_shows_the_player_and_starts_at_the_first_node()
    {
        var panels = new RecordingPanelService();
        await using var session = await OpenAsync(panels);
        var play = Command(SkeinCommands.PlayProject);
        var (project, script) = (session.ProjectAt("Dialogue/Story"), session.ScriptAt("Dialogue/Act1"));
        var player = session.Player;

        // A project starts at its node "Start"
        await play.ExecuteAsync(FromAssetView(session, project));

        Assert.Equal([(DialoguePlayerViewModel.PanelId, true)], panels.Shown);
        Assert.True(player.IsPlaying);
        Assert.Equal((project, "Start"), (player.Project, player.StartNode));
        Assert.Equal("Odile: The tide is out.", player.Steps[^1].Text);

        // A script starts at its first node, which a note is not
        await play.ExecuteAsync(FromAssetView(session, script));

        Assert.Equal((project, "Pier"), (player.Project, player.StartNode));
        Assert.Equal(2, panels.Shown.Count);

        // Without a node "Start", the first node of the project by name
        var buffer = script.TextBuffer;
        buffer.Replace(buffer.Text.IndexOf("title: Start", StringComparison.Ordinal), "title: Start".Length, "title: Tide", Text.YarnTextBuffer.TypingActionName);
        buffer.Replace(buffer.Text.IndexOf("<<jump Pier>>\n", StringComparison.Ordinal), 0, "<<jump Tide>>\n", Text.YarnTextBuffer.TypingActionName);
        await play.ExecuteAsync(FromAssetView(session, project));

        Assert.Equal("Pier", player.StartNode);
        Assert.True(player.IsPlaying);
    }

    [Fact]
    public async Task Play_from_here_plays_a_node_of_the_script_editor()
    {
        var panels = new RecordingPanelService();
        await using var session = await OpenAsync(panels);
        var play = Command(SkeinCommands.PlayFromHere);
        var editor = session.Edit("Dialogue/Act1");
        await session.Analysis.WhenIdle();
        var player = session.Player;

        CommandContext FromGraph(params object[] selection)
            => new() { Session = session.Session, Location = SkeinCommands.GraphContext, Selection = selection, Editor = editor };

        // The menu of the graph gives the card it opened on
        var pier = editor.Cards.Single(c => c.Title == "Pier");
        Assert.True(play.CanExecute!.Invoke(FromGraph(pier)));
        Assert.False(play.CanExecute!.Invoke(FromGraph(editor.Cards.Single(c => c.IsNote))));
        await play.ExecuteAsync(FromGraph(pier));
        Assert.Equal((session.ProjectAt("Dialogue/Story"), "Pier"), (player.Project, player.StartNode));
        Assert.Equal([(DialoguePlayerViewModel.PanelId, true)], panels.Shown);

        // The shortcut and the palette give the editor: its selected node, else its first one
        Assert.True(editor.Select("Start"));
        var palette = CommandContext.ForPalette(session.Session, editor: editor);
        Assert.True(play.CanExecute!.Invoke(palette));
        await play.ExecuteAsync(palette);
        Assert.Equal("Start", player.StartNode);

        // Outside a script editor there is no node to play
        Assert.False(play.CanExecute!.Invoke(CommandContext.ForPalette(session.Session)));
        Assert.False(play.CanExecute!.Invoke(FromAssetView(session, session.ScriptAt("Dialogue/Act1"))));
    }

    [Fact]
    public async Task Open_dialogue_player_shows_the_panel()
    {
        var panels = new RecordingPanelService();
        await using var session = await OpenAsync(panels);
        var open = Command(SkeinCommands.OpenDialoguePlayer);

        Assert.True(open.CanExecute?.Invoke(CommandContext.ForPalette(session.Session)) ?? true);
        await open.ExecuteAsync(CommandContext.ForPalette(session.Session));

        Assert.Equal([(DialoguePlayerViewModel.PanelId, true)], panels.Shown);
        Assert.False(session.Player.IsPlaying);
    }

    [Fact]
    public async Task The_line_id_and_strings_commands_run_in_the_overview_of_the_project()
    {
        await using var session = await OpenAsync(null, "fr");
        var project = session.ProjectAt("Dialogue/Story");
        var (addLineIds, export, update) = (Command(SkeinCommands.AddLineIds), Command(SkeinCommands.ExportStrings), Command(SkeinCommands.UpdateStrings));

        // On one project, of the asset view or of the overview that has the focus
        foreach (var command in (CommandDescriptor[])[addLineIds, export, update])
        {
            Assert.True(command.CanExecute!.Invoke(FromAssetView(session, project)));
            Assert.False(command.CanExecute!.Invoke(FromAssetView(session, session.ScriptAt("Dialogue/Act1"))));
            Assert.False(command.CanExecute!.Invoke(FromAssetView(session)));
        }

        // The command opens the overview, which shows what was done
        await addLineIds.ExecuteAsync(FromAssetView(session, project));

        Assert.True(session.Session.EditorCollection.TryGetAssetEditor<YarnProjectEditorViewModel>(project, out var editor));
        // The line of the note too: a note is a node for Yarn Spinner
        Assert.Equal("Added 2 line ids.", editor.Message);
        Assert.Contains("Odile: No id yet. #line:", session.ScriptAt("Dialogue/Act1").TextBuffer.Text);
        Assert.Equal(["Add line ids"], session.Steps);

        // The strings of each language that has no strings file
        await export.ExecuteAsync(FromAssetView(session, project));

        Assert.Equal("Exported 4 lines to Strings/Story.fr.csv, from the folder of the project.", editor.Message);
        var file = Path.Combine(Path.GetDirectoryName(session.FileOf("Dialogue/Story", ".sdyarn"))!, "Strings", "Story.fr.csv");
        Assert.Equal(4, YarnStringsCsv.Read(File.ReadAllText(file)).Count);
        await export.ExecuteAsync(FromAssetView(session, project));
        Assert.Equal("Every language has its strings file: \"Update strings files\" brings them up to date.", editor.Message);

        // From the palette, with the focus in the overview
        var palette = CommandContext.ForPalette(session.Session, selection: [], editor: editor);
        Assert.True(update.CanExecute!.Invoke(palette));
        await update.ExecuteAsync(palette);
        Assert.Equal("fr: up to date.", editor.Message);
    }

    [Fact]
    public async Task The_yarn_project_template_creates_the_project_and_a_first_script()
    {
        await using var session = await SkeinSession.OpenAsync(
            [new RecordingPanelService(), DispatchProxy.Create<IProgressWindowService, ProjectEditorTests.Silent>()],
            SkeinSession.Script("Elsewhere/Alone", Alone),
            ("Chapters/Sky", new TextureAsset { Source = new UFile("sky.dds") }));
        var template = ProjectTemplate();
        Assert.Equal(("Yarn project", "Dialogue", "YarnProject"), (template.Name, template.Group, template.DefaultOutputName));

        // In a folder without a script: the project, and a script with a node "Start" that the project includes
        var created = await RunAsync(session, template, "Chapters");

        Assert.Equal(["/TestGame/Chapters/YarnProject", "/TestGame/Chapters/Start"], created.Select(a => a.Url));
        var project = Assert.IsType<YarnProjectViewModel>(created[0]);
        var script = Assert.IsType<YarnScriptViewModel>(created[1]);
        Assert.Equal(YarnScriptAssetFactory.StarterText, script.TextBuffer.Text);
        Assert.Equal([project], session.Analysis.ProjectsOf(script));
        await Command(SkeinCommands.PlayProject).ExecuteAsync(FromAssetView(session, project));
        Assert.Equal("Start", session.Player.StartNode);
        Assert.Equal("Narrator: Write your first line here.", session.Player.Steps[^1].Text);
        session.Player.Reset();

        // One undo takes both back
        session.UndoRedo.Undo();
        Assert.True(project.IsDeleted);
        Assert.True(script.IsDeleted);
        session.UndoRedo.Redo();

        // In a folder that has scripts, the project alone: it includes them
        var second = Assert.Single(await RunAsync(session, template, "Chapters"));
        Assert.Equal("/TestGame/Chapters/YarnProject_2", second.Url);
        var third = Assert.Single(await RunAsync(session, template, "Elsewhere"));
        Assert.Equal([third], session.Analysis.ProjectsOf(session.ScriptAt("Elsewhere/Alone")));
    }

    [Fact]
    public void The_dialogue_runner_entity_has_the_presenters_of_the_UI_and_audio_packages_when_the_game_has_them()
    {
        var bare = DialogueRunnerFactory.Create("Runner", withPresenters: false);

        Assert.Equal("Runner", bare.Name);
        var runner = Assert.Single(bare.Components.OfType<DialogueRunnerComponent>());
        Assert.Empty(runner.Presenters);
        Assert.Empty(bare.Components.OfType<DialoguePresenter>());
        Assert.Equal("Start", runner.StartNode);

        var full = DialogueRunnerFactory.Create("Runner", withPresenters: true);

        runner = Assert.Single(full.Components.OfType<DialogueRunnerComponent>());
        Assert.Equal([typeof(LinePresenter), typeof(OptionsPresenter), typeof(LineAdvancer)], runner.Presenters.Select(p => p.GetType()));
        Assert.Equal(runner.Presenters, full.Components.OfType<DialoguePresenter>());

        var voiced = DialogueRunnerFactory.Create("Runner", withPresenters: true, withVoice: true);
        runner = Assert.Single(voiced.Components.OfType<DialogueRunnerComponent>());
        Assert.Equal([typeof(LinePresenter), typeof(OptionsPresenter), typeof(LineAdvancer), typeof(VoiceOverPresenter)], runner.Presenters.Select(p => p.GetType()));
    }

    [Fact]
    public async Task Add_entity_makes_a_dialogue_runner_in_the_scene()
    {
        await using var session = await SkeinSession.OpenAsync(
            SkeinSession.Project("Dialogue/Story"),
            SkeinSession.Script("Dialogue/Act1", Act1),
            ("MainScene", new SceneAsset()));
        var scene = Assert.IsType<SceneViewModel>(session.Session.AllAssets.Single(a => a.Name == "MainScene"));
        var editor = new SceneEditorViewModel(scene);
        try
        {
            Assert.True(await editor.Initialize());

            editor.CreateEntityInRootCommand.Execute(new DialogueRunnerFactory());
            editor.CreateEntityInRootCommand.Execute(new DialogueRunnerFactory());

            var entities = editor.HierarchyRoot.Children.OfType<EntityViewModel>().ToList();
            Assert.Equal(["Dialogue runner", "Dialogue runner"], entities.Select(e => e.Name));
            var runner = entities[0].AssetSideEntity.Get<DialogueRunnerComponent>();
            Assert.NotNull(runner);
            // The test's session loaded the UI and audio packages with the plugin: the game can use them
            Assert.Equal([typeof(LinePresenter), typeof(OptionsPresenter), typeof(LineAdvancer), typeof(VoiceOverPresenter)], runner.Presenters.Select(p => p.GetType()));
            Assert.Equal(runner.Presenters, entities[0].AssetSideEntity.Components.OfType<DialoguePresenter>());
            // The only Yarn project of the game is the one the runner plays
            Assert.Equal(session.ProjectAt("Dialogue/Story").Id, AttachedReferenceManager.GetAttachedReference(runner.Project)?.Id);
        }
        finally
        {
            editor.Destroy();
        }
    }

    private static TemplateDescription ProjectTemplate()
    {
        const string RelativePath = "src/ColorRise.Skein.Assets/Templates/Assets/YarnProject.sdtpl";
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
            {
                var description = YamlSerializer.Load<TemplateDescription>(path);
                description.FullPath = path;
                return description;
            }
        }

        throw new FileNotFoundException(RelativePath);
    }

    /// <summary>
    /// Runs a template as "Add asset" does, in a folder of the package.
    /// </summary>
    private static async Task<List<AssetViewModel>> RunAsync(SkeinSession session, TemplateDescription template, string folder)
    {
        var assets = session.Session.AssetCollection;
        var transactions = session.Session.ActionHistory.Transactions.Count;
        var created = await session.Session.Dispatcher.InvokeTask(() =>
        {
            assets.SelectedLocations.Clear();
            assets.SelectedLocations.Add(session.Session.LocalPackages.Single().AssetMountPoint.GetDirectory(folder));
            return assets.RunAssetTemplateAsync(new TemplateDescriptionViewModel(session.Session.ServiceProvider, template), null);
        });

        // The history records the creation once the session heard of the new assets: the test goes on after that
        if (created.Count > 0)
        {
            await ExclusiveDispatcherService.WaitUntilAsync(() => session.Session.ActionHistory.Transactions.Count > transactions, "the creation to be recorded");
        }

        return created;
    }

    /// <summary>
    /// A shell without panels that records the panels shown, and whether they were brought forward.
    /// </summary>
    private sealed class RecordingPanelService : IPanelService
    {
        public List<(string PanelId, bool Activate)> Shown { get; } = [];

        public PanelRegistry Registry { get; } = new();

        public event EventHandler<PanelChangedEventArgs>? PanelChanged { add { } remove { } }

        public bool IsVisible(string panelId) => Shown.Any(x => x.PanelId == panelId);

        public void Show(string panelId, bool activate = true) => Shown.Add((panelId, activate));

        public void Hide(string panelId)
        {
        }

        public IPanelViewModel? GetPanel(string panelId) => null;

        public IReadOnlyList<IPanelViewModel> GetInstances(string panelId) => [];

        public Task<IPanelViewModel> OpenInstanceAsync(string panelId) => throw new NotSupportedException();

        public void Activate(IPanelViewModel instance)
        {
        }

        public Task<bool> CloseInstanceAsync(IPanelViewModel instance) => Task.FromResult(false);

        public Task ResetLayoutAsync() => Task.CompletedTask;
    }
}
