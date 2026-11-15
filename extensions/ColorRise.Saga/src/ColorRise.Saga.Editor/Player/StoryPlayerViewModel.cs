using ColorRise.Saga.Assets;
using ColorRise.Saga.Content;
using ColorRise.Saga.Editor.ViewModels;
using ColorRise.Saga.Expressions;
using Stride.Core.Assets.Editor.Panels;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Diagnostics;
using Stride.Core.Presentation.Collections;
using Stride.Core.Presentation.Commands;
using Stride.Core.Serialization;

namespace ColorRise.Saga.Editor.Player;

/// <summary>
/// The story player panel: plays a story of the session from its assets as they are in memory, with the story's API by hand
/// (variables, events, functions) and the characters to talk to.
/// </summary>
/// <remarks>
/// The story is checked when it is picked and when the player is reset; a story with problems shows them and does not start.
/// What the story or a dialogue throws while playing goes in the transcript, as do the warnings of the runtime.
/// </remarks>
public sealed class StoryPlayerViewModel : PanelViewModel
{
    /// <summary>
    /// The identifier of the panel.
    /// </summary>
    public const string PanelId = "ColorRise.Saga.StoryPlayer";

    private readonly ISessionViewModel session;
    private readonly LoggerResult log = new("Saga player");
    private StoryViewModel? selectedStory;
    private SagaPlayback? playback;
    private DialogueSession? dialogue;
    private Dictionary<string, string> speakers = [];
    private string currentAct = string.Empty;

    public StoryPlayerViewModel(PanelContext context)
        : base(context)
    {
        session = context.Session;
        StartCommand = new AnonymousCommand(ServiceProvider, Start);
        ResetCommand = new AnonymousCommand(ServiceProvider, Reset);
        ContinueCommand = new AnonymousCommand(ServiceProvider, () => Play(() => dialogue?.Continue()));
        RefreshStories();
        UpdateCommands();
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
    /// The stories of the session.
    /// </summary>
    public ObservableList<StoryViewModel> Stories { get; } = [];

    /// <summary>
    /// The story to play; picking one resets the player and checks the story.
    /// </summary>
    public StoryViewModel? SelectedStory { get => selectedStory; set => SetValue(ref selectedStory, value, Reset); }

    /// <summary>
    /// Builds the story and starts it, unless it has errors (<see cref="Diagnostics"/>).
    /// </summary>
    public ICommandBase StartCommand { get; }

    /// <summary>
    /// Stops the story, forgets its state, and checks it again.
    /// </summary>
    public ICommandBase ResetCommand { get; }

    /// <summary>
    /// What is wrong with the story or its dialogues, as the compiler would report it: errors, which keep the story from starting,
    /// and warnings.
    /// </summary>
    public ObservableList<SagaBuildDiagnostic> Diagnostics { get; } = [];

    private bool HasErrors => Diagnostics.Any(x => x.Severity == SagaDiagnosticSeverity.Error);

    /// <summary>
    /// The story being played, if any.
    /// </summary>
    public SagaPlayback? Playback => playback;

    public bool IsPlaying => playback is not null;

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
    /// The lines of the dialogues, the choices taken, and what went wrong.
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
    /// Plays a story from its start: picks it, checks it, and starts it unless it has problems.
    /// </summary>
    public void Play(StoryViewModel story)
    {
        ArgumentNullException.ThrowIfNull(story);
        RefreshStories();
        SelectedStory = story;
        Start();
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

    private void Reset()
    {
        Stop();
        Diagnostics.Clear();
        if (SelectedStory is not null)
            Build(new LoggerResult());
        UpdateCommands();
    }

    private void Start()
    {
        Reset();
        if (SelectedStory is null || HasErrors)
            return;
        // Built again for the runtime, with its logger: the problems are the same
        Diagnostics.Clear();
        if (Build(log) is not { } built)
            return;

        playback = built;
        var runtime = built.Runtime;
        var declarations = runtime.Story.Declarations;
        foreach (var declaration in declarations.Functions)
        {
            var function = new ManualFunctionViewModel(ServiceProvider, declaration);
            runtime.RegisterFunction(declaration.Name, _ => function.Value);
            Functions.Add(function);
        }
        foreach (var declaration in declarations.Variables)
            Variables.Add(new PlayerVariableViewModel(ServiceProvider, declaration, runtime.Get(declaration.Name), value => SetVariable(declaration.Name, value)));
        foreach (var declaration in declarations.Events)
            Events.Add(new PlayerEventViewModel(ServiceProvider, declaration.Name, () => Play(() => runtime.Raise(declaration.Name))));
        foreach (var declaration in declarations.Characters)
        {
            var character = new PlayerCharacterViewModel(ServiceProvider, declaration, () => Talk(declaration.Id));
            Characters.Add(character);
        }
        speakers = Characters.ToDictionary(x => x.Id, x => x.DisplayName);
        speakers.TryAdd(SagaContentBuilder.PlayerId, "Player");
        OnPropertyChanged(nameof(IsPlaying), nameof(Playback));
        Play(runtime.Start);
    }

    /// <summary>
    /// Builds the selected story, filling <see cref="Diagnostics"/> with its problems.
    /// </summary>
    private SagaPlayback? Build(ILogger logger)
    {
        var story = SelectedStory!;
        var diagnostics = new List<SagaBuildDiagnostic>();
        var built = SagaPlayback.Create(story.Asset, story.Url, ResolveDialogue, diagnostics, logger);
        Diagnostics.AddRange(diagnostics);
        return built;
    }

    private (DialogueAsset Asset, string Url)? ResolveDialogue(DialogueDefinition reference)
        => AttachedReferenceManager.GetAttachedReference(reference) is { } target && session.GetAssetById(target.Id) is DialogueViewModel dialogueAsset
            ? (dialogueAsset.Asset, dialogueAsset.Url)
            : null;

    private void Stop()
    {
        var wasPlaying = playback is not null;
        playback = null;
        dialogue = null;
        log.Clear();
        CurrentAct = string.Empty;
        foreach (var list in new System.Collections.IList[] { Scenarios, Quests, Variables, Events, Functions, Characters, Transcript, Choices })
            list.Clear();
        if (wasPlaying)
            OnPropertyChanged(nameof(IsPlaying), nameof(Playback));
    }

    private string? SetVariable(string name, SagaValue value)
    {
        if (playback is null)
            return "The story is not playing.";
        string? error = null;
        Play(() =>
        {
            try
            {
                playback.Runtime.Set(name, value);
            }
            catch (ArgumentException e)
            {
                error = e.Message;
            }
        });
        return error;
    }

    private void Talk(string characterId)
    {
        Play(() =>
        {
            Transcript.Add($"Talking to {speakers.GetValueOrDefault(characterId, characterId)}");
            dialogue = playback!.Runtime.Talk(characterId);
            if (dialogue is null)
                Transcript.Add("(nothing to say now)");
        });
    }

    private void Choose(DialogueChoiceView choice)
    {
        Play(() =>
        {
            Transcript.Add($"> {choice.Text}");
            dialogue?.Choose(choice.Index);
        });
    }

    /// <summary>
    /// Runs a step of the story, then shows where the story and the dialogue are; what goes wrong goes in the transcript.
    /// </summary>
    private void Play(Action step)
    {
        if (playback is null)
            return;
        var shownLine = dialogue?.CurrentLine;
        try
        {
            step();
        }
        catch (Exception e) when (e is SagaException or ArgumentException or InvalidOperationException)
        {
            Transcript.Add($"Error: {e.Message}");
        }
        foreach (var message in log.Messages)
            Transcript.Add($"{message.Type}: {message.Text}");
        log.Clear();
        ShowDialogue(shownLine);
        ShowStory();
        UpdateCommands();
    }

    private void ShowDialogue(DialogueLine? shownLine)
    {
        Choices.Clear();
        if (dialogue is null)
            return;
        if (dialogue.CurrentLine is { } line && !ReferenceEquals(line, shownLine))
            Transcript.Add($"{speakers.GetValueOrDefault(line.SpeakerId, line.SpeakerId)}: {line.Text}");
        if (dialogue.IsEnded)
        {
            Transcript.Add("(the dialogue ends)");
            dialogue = null;
            return;
        }
        foreach (var choice in dialogue.Choices)
            Choices.Add(new PlayerChoiceViewModel(ServiceProvider, choice.Text, choice.IsEnabled, () => Choose(choice)));
    }

    private void ShowStory()
    {
        var runtime = playback!.Runtime;
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
        foreach (var raised in Events)
            raised.Count = state.Events.GetValueOrDefault(raised.Name);
    }

    private static void Replace(ObservableList<StoryPlayerEntry> list, IEnumerable<StoryPlayerEntry> entries)
    {
        var items = entries.ToList();
        if (items.SequenceEqual(list))
            return;
        list.Clear();
        list.AddRange(items);
    }

    private void UpdateCommands()
    {
        StartCommand.IsEnabled = SelectedStory is not null && playback is null && !HasErrors;
        ResetCommand.IsEnabled = SelectedStory is not null;
        ContinueCommand.IsEnabled = dialogue is { IsEnded: false, CurrentLine: not null } && dialogue.Choices.Count == 0;
    }
}
