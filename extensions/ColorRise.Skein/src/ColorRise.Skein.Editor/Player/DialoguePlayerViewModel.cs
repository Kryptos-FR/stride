using System.Collections.Specialized;
using ColorRise.Skein.Editor.Services;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Core.Assets.Editor.Panels;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Presentation.Collections;
using Stride.Core.Presentation.Commands;

namespace ColorRise.Skein.Editor.Player;

/// <summary>
/// The dialogue player panel: plays a node of a Yarn project of the session from its scripts as they are in memory, with
/// the variables to set and the functions of the game to answer by hand. It drives the session's
/// <see cref="DialoguePlayerService"/> and shows the run at its cursor, which the timeline moves back and forth.
/// </summary>
public sealed class DialoguePlayerViewModel : PanelViewModel
{
    /// <summary>
    /// The identifier of the panel.
    /// </summary>
    public new const string PanelId = "ColorRise.Skein.DialoguePlayer";

    private readonly ISessionViewModel session;
    private YarnProjectViewModel? selectedProject;
    private ObservableList<string>? projectNodes;
    private string? selectedNode;
    private bool nodeIsChosen;
    private ManualFunctionViewModel? question;
    private string status = "";
    private PlayerBuildKey shown;

    /// <param name="context">The context of the panel.</param>
    public DialoguePlayerViewModel(PanelContext context)
        : base(context)
    {
        session = context.Session;
        Service = DialoguePlayerService.Of(session);
        StartCommand = new AnonymousTaskCommand(ServiceProvider, Start);
        ResetCommand = new AnonymousCommand(ServiceProvider, Service.Reset);
        ContinueCommand = new AnonymousTaskCommand(ServiceProvider, Continue);
        ReplayCommand = new AnonymousTaskCommand(ServiceProvider, Replay);
        ShowTimelineCommand = new AnonymousCommand(ServiceProvider, () => ServiceProvider.TryGet<IPanelService>()?.Show(DialogueTimelineViewModel.PanelId));
        Service.Changed += Update;
        RefreshProjects();
        Update();
    }

    /// <summary>
    /// The description of the panel: hidden at first, on the right.
    /// </summary>
    public static PanelDescriptor Panel { get; } = new()
    {
        Id = PanelId,
        Title = () => "Dialogue player",
        DefaultPlacement = PanelPlacement.Right,
        VisibleByDefault = false,
        CreateViewModel = context => new DialoguePlayerViewModel(context),
    };

    /// <summary>
    /// The player of the session, which the panel drives.
    /// </summary>
    public DialoguePlayerService Service { get; }

    /// <summary>
    /// The Yarn projects of the session.
    /// </summary>
    public ObservableList<YarnProjectViewModel> Projects { get; } = [];

    /// <summary>
    /// The project to play.
    /// </summary>
    public YarnProjectViewModel? SelectedProject
    {
        get => selectedProject;
        set
        {
            if (!SetValue(ref selectedProject, value))
                return;

            WatchNodes(value);
            UpdateCommands();
        }
    }

    /// <summary>
    /// The titles of the nodes of <see cref="SelectedProject"/> a dialogue can start at, kept current.
    /// </summary>
    public ObservableList<string> Nodes { get; } = [];

    /// <summary>
    /// The node to start at.
    /// </summary>
    public string? SelectedNode
    {
        get => selectedNode;
        set
        {
            nodeIsChosen = value is not null;
            if (SetValue(ref selectedNode, value))
                UpdateCommands();
        }
    }

    /// <summary>
    /// The languages of the project being played, the base language first.
    /// </summary>
    public ObservableList<string> Languages { get; } = [];

    /// <summary>
    /// The language the lines show in.
    /// </summary>
    public string Language
    {
        get => Service.Language;
        set
        {
            if (!string.IsNullOrEmpty(value) && value != Service.Language)
                Service.Language = value;
        }
    }

    /// <summary>
    /// Whether <c>wait</c> takes its time.
    /// </summary>
    public bool HonourWait
    {
        get => Service.HonourWait;
        set => Service.HonourWait = value;
    }

    /// <summary>
    /// Whether a dialogue is playing.
    /// </summary>
    public bool IsPlaying => Service.IsPlaying;

    /// <summary>
    /// Whether the scripts changed since the run: a replay follows them.
    /// </summary>
    public bool IsStale => Service.IsStale;

    /// <summary>
    /// Why the dialogue did not start, where a replay stopped, or that the scripts changed; empty when all is well.
    /// </summary>
    public string Status { get => status; private set => SetValue(ref status, value); }

    /// <summary>
    /// The steps of the run up to the cursor: the lines with their speakers, the choices taken, the commands, the picks.
    /// </summary>
    public ObservableList<PlayerTranscriptItem> Transcript { get; } = [];

    /// <summary>
    /// The options the run waits at.
    /// </summary>
    public ObservableList<PlayerOptionViewModel> Options { get; } = [];

    /// <summary>
    /// The call of a function of the game the run waits for the answer of, or null.
    /// </summary>
    public ManualFunctionViewModel? Question { get => question; private set => SetValue(ref question, value); }

    /// <summary>
    /// The answers given for the functions of the game, as they hold at the cursor.
    /// </summary>
    public ObservableList<ManualFunctionViewModel> Answers { get; } = [];

    /// <summary>
    /// The variables of the scripts, by name, with their values at the cursor.
    /// </summary>
    public ObservableList<PlayerVariableViewModel> Variables { get; } = [];

    /// <summary>
    /// Starts a new run of <see cref="SelectedProject"/> at <see cref="SelectedNode"/>.
    /// </summary>
    public ICommandBase StartCommand { get; }

    /// <summary>
    /// Stops the run and forgets it.
    /// </summary>
    public ICommandBase ResetCommand { get; }

    /// <summary>
    /// Goes on after a line.
    /// </summary>
    public ICommandBase ContinueCommand { get; }

    /// <summary>
    /// Plays the run again on the scripts as they are now.
    /// </summary>
    public ICommandBase ReplayCommand { get; }

    /// <summary>
    /// Shows the timeline of the run.
    /// </summary>
    public ICommandBase ShowTimelineCommand { get; }

    /// <summary>
    /// Starts a new run of the selected project at the selected node.
    /// </summary>
    public Task Start() => SelectedProject is { } project && SelectedNode is { } node ? Service.Start(project, node) : Task.CompletedTask;

    /// <summary>
    /// Plays a node of a project from its start: what "Play from here" does.
    /// </summary>
    public Task Play(YarnProjectViewModel project, string node)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(node);
        RefreshProjects();
        SelectedProject = project;
        SelectedNode = node;
        return Service.Start(project, node);
    }

    /// <summary>
    /// Goes on after a line.
    /// </summary>
    public Task Continue() => Service.Continue();

    /// <summary>
    /// Plays the run again on the scripts as they are now.
    /// </summary>
    public Task Replay() => Service.Replay();

    /// <inheritdoc />
    public override void Destroy()
    {
        Service.Changed -= Update;
        WatchNodes(null);
        base.Destroy();
    }

    /// <inheritdoc />
    protected override void OnIsShownChanged()
    {
        if (!IsShown)
            return;

        // The projects may have changed while the panel was hidden; a session with one project plays that one
        RefreshProjects();
        if (SelectedProject is null && Projects.Count == 1)
            SelectedProject = Projects[0];
    }

    private void RefreshProjects()
    {
        var projects = session.AllAssets.OfType<YarnProjectViewModel>().Where(p => !p.IsDeleted).OrderBy(p => p.Url, StringComparer.Ordinal).ToList();
        if (!projects.SequenceEqual(Projects))
        {
            Projects.Clear();
            Projects.AddRange(projects);
        }

        if (SelectedProject is not null && !Projects.Contains(SelectedProject))
            SelectedProject = null;
    }

    // Follows the nodes of the project, which the analysis keeps current
    private void WatchNodes(YarnProjectViewModel? project)
    {
        if (projectNodes is not null)
            projectNodes.CollectionChanged -= OnNodesChanged;

        projectNodes = project is null ? null : session.ServiceProvider.TryGet<YarnAnalysisService>()?.NodeNamesOf(project);
        if (projectNodes is not null)
            projectNodes.CollectionChanged += OnNodesChanged;

        ShowNodes();
    }

    private void OnNodesChanged(object? sender, NotifyCollectionChangedEventArgs e) => ShowNodes();

    private void ShowNodes()
    {
        var nodes = projectNodes?.ToList() ?? [];
        if (!nodes.SequenceEqual(Nodes))
        {
            // The node keeps its place through the change of the list
            var (node, chosen) = (selectedNode, nodeIsChosen);
            Nodes.Clear();
            Nodes.AddRange(nodes);
            (selectedNode, nodeIsChosen) = (node, chosen);
        }

        // The node the user chose as long as the project has it, else the usual first node, else the first
        if (nodes.Count > 0 && (!nodeIsChosen || selectedNode is null || !nodes.Contains(selectedNode)))
        {
            selectedNode = nodes.Contains(YarnProjectAnalysis.DefaultStartNode) ? YarnProjectAnalysis.DefaultStartNode : nodes[0];
            nodeIsChosen = false;
        }

        OnPropertyChanged(nameof(SelectedNode));
        UpdateCommands();
    }

    /// <summary>
    /// Shows the run at its cursor.
    /// </summary>
    private void Update()
    {
        // A run started elsewhere ("Play from here") shows its project and node
        if (Service.IsPlaying && Service.Project is { } project && (project != SelectedProject || Service.StartNode != SelectedNode))
        {
            RefreshProjects();
            SelectedProject = project;
            SelectedNode = Service.StartNode;
        }

        ShowTranscript();
        ShowOptions();
        ShowDeclarations();
        ShowAnswers();

        var languages = Service.Languages;
        if (!languages.SequenceEqual(Languages))
        {
            Languages.Clear();
            Languages.AddRange(languages);
        }

        Status = Service.Problem
            ?? (Service.Divergence is { } divergence ? $"The replay stopped. {divergence}"
            : Service.IsStale ? "The scripts changed since this run: replay it to follow the changes."
            : "");
        OnPropertyChanged(nameof(IsPlaying), nameof(IsStale), nameof(Language), nameof(HonourWait));
        UpdateCommands();
    }

    private void UpdateCommands()
    {
        StartCommand.IsEnabled = SelectedProject is not null && SelectedNode is not null && !Service.IsBusy;
        ResetCommand.IsEnabled = Service.IsPlaying;
        ContinueCommand.IsEnabled = Service.CanContinue;
        ReplayCommand.IsEnabled = Service.IsPlaying && !Service.IsBusy;
    }

    private void ShowTranscript()
    {
        var items = new List<PlayerTranscriptItem>();
        for (var i = 0; i <= Service.Cursor && i < Service.Steps.Count; i++)
        {
            if (PlayerTranscriptItem.Of(Service.Steps[i], i) is { } item)
                items.Add(item);
        }

        // What is added at the end is added, so that the view follows it
        var same = 0;
        while (same < items.Count && same < Transcript.Count && items[same] == Transcript[same])
        {
            same++;
        }

        if (same < Transcript.Count)
        {
            if (same == 0)
                Transcript.Clear();
            else
                Transcript.RemoveRange(same, Transcript.Count - same);
        }

        Transcript.AddRange(items.Skip(same));
    }

    private void ShowOptions()
    {
        var options = Service.IsBusy ? [] : Service.Options;
        if (options.Select(o => (o.Text, o.IsAvailable)).SequenceEqual(Options.Select(o => (o.Text, o.IsAvailable))))
            return;

        Options.Clear();
        foreach (var option in options)
        {
            Options.Add(new PlayerOptionViewModel(ServiceProvider, option, () => Service.Choose(option.Index)));
        }
    }

    // The variables of the build the run plays, then their values at the cursor
    private void ShowDeclarations()
    {
        var variables = Service.Variables;
        var key = new PlayerBuildKey(Service.IsPlaying ? variables : null);
        if (key != shown)
        {
            shown = key;
            Variables.Clear();
            foreach (var variable in variables.OrderBy(v => v.Name, StringComparer.Ordinal))
            {
                var name = variable.Name;
                Variables.Add(new PlayerVariableViewModel(ServiceProvider, variable, Service.ExpressionOf(variable), value => PlayerValues.Try(() => Service.SetVariable(name, value))));
            }
        }

        if (Service.CurrentStep is not { } step)
            return;

        foreach (var variable in Variables)
        {
            if (step.Variables.TryGetValue(variable.Name, out var value))
                variable.Show(value);
        }
    }

    private void ShowAnswers()
    {
        var pending = Service.Question;
        if (pending?.Call != Question?.Call)
        {
            Question = pending is null
                ? null
                : new ManualFunctionViewModel(ServiceProvider, pending.Call, pending.ReturnType, null, value => PlayerValues.Try(() => Service.Answer(pending.Function, value)));
        }

        var answers = Service.Answers;
        if (answers.Select(a => (a.Call, PlayerValues.Show(a.Value))).SequenceEqual(Answers.Select(a => (a.Call, a.Answer))))
            return;

        Answers.Clear();
        foreach (var answer in answers)
        {
            Answers.Add(new ManualFunctionViewModel(ServiceProvider, answer.Call, answer.ReturnType, answer.Value, value => PlayerValues.Try(() => Service.ChangeAnswer(answer.Call, value))));
        }
    }

    // The variables shown are those of one build: another build, or no run, shows them anew
    private readonly record struct PlayerBuildKey(object? Variables);
}
