using ColorRise.Saga.Assets;
using ColorRise.Saga.Content;
using ColorRise.Saga.Editor.ViewModels;
using ColorRise.Saga.Expressions;
using Stride.Core.Assets.Editor.Panels;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Presentation.Collections;
using Stride.Core.Presentation.Commands;

namespace ColorRise.Saga.Editor.Player;

/// <summary>
/// The story player panel: plays a story of the session from its assets as they are in memory, with the story's API by hand
/// (variables, events, functions) and the characters to talk to. It drives the session's <see cref="SagaPlaybackService"/> and
/// shows the run at its cursor, which the timeline moves back and forth.
/// </summary>
/// <remarks>
/// The story is checked when it is picked and when the player is reset; a story with errors shows them and does not start, its
/// warnings show without stopping it. What the story or a dialogue throws while playing goes in the transcript, as do the
/// warnings of the runtime.
/// </remarks>
public sealed class StoryPlayerViewModel : PanelViewModel
{
    /// <summary>
    /// The identifier of the panel.
    /// </summary>
    public new const string PanelId = "ColorRise.Saga.StoryPlayer";

    private readonly ISessionViewModel session;
    private StoryDefinition? shown;
    private string currentAct = string.Empty;

    public StoryPlayerViewModel(PanelContext context)
        : base(context)
    {
        session = context.Session;
        Service = ServiceProvider.Get<SagaPlaybackService>();
        StartCommand = new AnonymousCommand(ServiceProvider, () => Service.Start());
        ResetCommand = new AnonymousCommand(ServiceProvider, Service.Reset);
        ContinueCommand = new AnonymousCommand(ServiceProvider, () => Service.Do(SagaStep.Continue()));
        ShowTimelineCommand = new AnonymousCommand(ServiceProvider, () => ServiceProvider.TryGet<IPanelService>()?.Show(StoryTimelineViewModel.PanelId));
        Service.Changed += OnPlaybackChanged;
        RefreshStories();
        Update();
    }

    /// <summary>
    /// The description of the panel: hidden at first, on the right.
    /// </summary>
    public static PanelDescriptor Panel { get; } = new()
    {
        Id = PanelId,
        Title = () => "Story player",
        DefaultPlacement = PanelPlacement.Right,
        VisibleByDefault = false,
        CreateViewModel = context => context.CreateInstance<StoryPlayerViewModel>(),
    };

    /// <summary>
    /// The playback of the session, which the panel drives.
    /// </summary>
    public SagaPlaybackService Service { get; }

    /// <summary>
    /// The stories of the session.
    /// </summary>
    public ObservableList<StoryViewModel> Stories { get; } = [];

    /// <summary>
    /// The story to play; picking one stops the player and checks the story.
    /// </summary>
    public StoryViewModel? SelectedStory
    {
        get => Service.Story;
        set
        {
            if (value != Service.Story)
                Service.Select(value);
        }
    }

    /// <summary>
    /// Builds the story and starts it, unless it has errors (<see cref="Diagnostics"/>).
    /// </summary>
    public ICommandBase StartCommand { get; }

    /// <summary>
    /// Stops the story, forgets its run, and checks it again.
    /// </summary>
    public ICommandBase ResetCommand { get; }

    /// <summary>
    /// What is wrong with the story or its dialogues, as the compiler would report it: errors, which keep the story from starting,
    /// and warnings.
    /// </summary>
    public ObservableList<SagaBuildDiagnostic> Diagnostics { get; } = [];

    public bool IsPlaying => Service.IsPlaying;

    /// <summary>
    /// The act being played, or a note once the story is over.
    /// </summary>
    public string CurrentAct { get => currentAct; private set => SetValue(ref currentAct, value); }

    public ObservableList<StoryPlayerEntry> Scenarios { get; } = [];

    public ObservableList<StoryPlayerEntry> Quests { get; } = [];

    public ObservableList<PlayerVariableViewModel> Variables { get; } = [];

    public ObservableList<PlayerEventViewModel> Events { get; } = [];

    public ObservableList<ManualFunctionViewModel> Functions { get; } = [];

    public ObservableList<PlayerCharacterViewModel> Characters { get; } = [];

    /// <summary>
    /// The lines of the dialogues up to the cursor, the choices taken, and what went wrong.
    /// </summary>
    public ObservableList<string> Transcript { get; } = [];

    /// <summary>
    /// The choices the dialogue being played shows.
    /// </summary>
    public ObservableList<PlayerChoiceViewModel> Choices { get; } = [];

    /// <summary>
    /// Goes on after a line of the dialogue being played.
    /// </summary>
    public ICommandBase ContinueCommand { get; }

    /// <summary>
    /// Shows the timeline of the run, to go back and forth through its steps.
    /// </summary>
    public ICommandBase ShowTimelineCommand { get; }

    /// <summary>
    /// Plays a story from its start: picks it, checks it, and starts it unless it has errors.
    /// </summary>
    public void Play(StoryViewModel story)
    {
        ArgumentNullException.ThrowIfNull(story);
        RefreshStories();
        SelectedStory = story;
        Service.Start();
    }

    /// <inheritdoc />
    public override void Destroy()
    {
        Service.Changed -= OnPlaybackChanged;
        base.Destroy();
    }

    /// <inheritdoc />
    protected override void OnIsShownChanged()
    {
        if (!IsShown)
            return;
        // The stories may have changed while the panel was hidden; a session with a single story plays that one
        RefreshStories();
        if (SelectedStory is null && Stories.Count == 1)
            SelectedStory = Stories[0];
    }

    private void RefreshStories()
    {
        var stories = session.AllAssets.OfType<StoryViewModel>().OrderBy(x => x.Url.ToString(), StringComparer.Ordinal).ToList();
        if (!stories.SequenceEqual(Stories))
        {
            Stories.Clear();
            Stories.AddRange(stories);
        }
        if (SelectedStory is not null && !Stories.Contains(SelectedStory))
            SelectedStory = null;
    }

    private void OnPlaybackChanged(object? sender, EventArgs e) => Update();

    /// <summary>
    /// Shows the run at its cursor: the story's problems, its API, where it is, the transcript and the choices.
    /// </summary>
    private void Update()
    {
        if (!Diagnostics.SequenceEqual(Service.Diagnostics))
        {
            Diagnostics.Clear();
            Diagnostics.AddRange(Service.Diagnostics);
        }
        if (Service.Definition != shown)
            ShowDeclarations(Service.Definition);
        if (Service.Run is { } run)
            ShowRun(run);
        else
            ShowStopped();
        OnPropertyChanged(nameof(SelectedStory), nameof(IsPlaying));
        StartCommand.IsEnabled = SelectedStory is not null && !Service.IsPlaying && !Service.HasErrors;
        ResetCommand.IsEnabled = SelectedStory is not null;
        ContinueCommand.IsEnabled = Service.Run?.CanApply(SagaStep.Continue()) is null && Service.IsPlaying;
    }

    // The story's API by hand, from the build the run plays
    private void ShowDeclarations(StoryDefinition? definition)
    {
        shown = definition;
        Variables.Clear();
        Events.Clear();
        Functions.Clear();
        Characters.Clear();
        if (definition is null)
            return;
        var declarations = definition.Declarations;
        foreach (var declaration in declarations.Functions)
            Functions.Add(new ManualFunctionViewModel(ServiceProvider, declaration, value => Service.Do(SagaStep.Answer(declaration.Name, value.ToString()))));
        foreach (var declaration in declarations.Variables)
            Variables.Add(new PlayerVariableViewModel(ServiceProvider, declaration, SagaLiterals.DefaultOf(declaration.Type), value => Service.Do(SagaStep.Set(declaration.Name, value.ToString()))));
        foreach (var declaration in declarations.Events)
            Events.Add(new PlayerEventViewModel(ServiceProvider, declaration.Name, () => Service.Do(SagaStep.Raise(declaration.Name))));
        foreach (var declaration in declarations.Characters)
            Characters.Add(new PlayerCharacterViewModel(ServiceProvider, declaration, () => Service.Do(SagaStep.Talk(declaration.Id))));
    }

    private void ShowRun(SagaRun run)
    {
        var runtime = run.Runtime;
        var state = runtime.State;
        CurrentAct = state.CurrentActId ?? (runtime.IsOver ? "(the story is over)" : string.Empty);
        var acts = runtime.Story.Plot.Acts;
        Replace(Scenarios, acts.SelectMany(x => x.Scenarios).Where(x => state.Scenarios.ContainsKey(x.Id)).Select(x =>
        {
            var progress = state.Scenarios[x.Id];
            return new StoryPlayerEntry(x.Id, SagaSymbolSets.ScenarioStateNames[(int)progress.State], progress.Outcome ?? string.Empty);
        }));
        Replace(Quests, acts.SelectMany(x => x.Quests).Where(x => state.Quests.ContainsKey(x.Id)).Select(x =>
        {
            var progress = state.Quests[x.Id];
            var stage = progress.State == QuestState.Active && progress.Stage < x.Stages.Count ? x.Stages[progress.Stage].Id : string.Empty;
            return new StoryPlayerEntry(x.Id, SagaSymbolSets.QuestStateNames[(int)progress.State], stage);
        }));
        foreach (var variable in Variables)
            variable.Update(runtime.Get(variable.Name));
        foreach (var function in Functions)
            function.Update(run.Answer(function.Name));
        foreach (var raised in Events)
            raised.Count = state.Events.GetValueOrDefault(raised.Name);

        var transcript = Service.Steps.Take(Service.Cursor + 1).SelectMany(x => x.Transcript).ToList();
        if (!transcript.SequenceEqual(Transcript))
        {
            // Lines added at the end are added, so that the view follows them
            if (transcript.Count > Transcript.Count && transcript.Take(Transcript.Count).SequenceEqual(Transcript))
            {
                Transcript.AddRange(transcript.Skip(Transcript.Count));
            }
            else
            {
                Transcript.Clear();
                Transcript.AddRange(transcript);
            }
        }

        Choices.Clear();
        if (run.Dialogue is { } dialogue)
        {
            foreach (var choice in dialogue.Choices)
                Choices.Add(new PlayerChoiceViewModel(ServiceProvider, choice.Text, choice.IsEnabled, () => Service.Do(SagaStep.Choose(choice.Index, choice.Text))));
        }
    }

    private void ShowStopped()
    {
        CurrentAct = string.Empty;
        foreach (var list in new System.Collections.IList[] { Scenarios, Quests, Transcript, Choices })
            list.Clear();
    }

    private static void Replace(ObservableList<StoryPlayerEntry> list, IEnumerable<StoryPlayerEntry> entries)
    {
        var items = entries.ToList();
        if (items.SequenceEqual(list))
            return;
        list.Clear();
        list.AddRange(items);
    }
}
