using Stride.Core.Assets.Editor.Panels;
using Stride.Core.Presentation.Collections;
using Stride.Core.Presentation.Commands;

namespace ColorRise.Saga.Editor.Player;

/// <summary>
/// A step of the run as the timeline shows it.
/// </summary>
/// <param name="Glyph">The kind of step, in one character.</param>
/// <param name="Tip">What the step did, in words.</param>
/// <param name="IsFuture">Whether the step comes after the cursor: recorded, not played.</param>
/// <param name="IsDiverged">Whether the step no longer applies since the last replay.</param>
/// <param name="PlaysDifferently">Whether the last replay made the step do something else than before.</param>
public sealed record TimelineStep(int Index, string Glyph, string Tip, bool IsFuture, bool IsDiverged, bool PlaysDifferently);

/// <summary>
/// The story timeline panel: the steps of the run of the session's <see cref="SagaPlaybackService"/> and what was going on across
/// them, lane by lane. Clicking a step moves the run there; after an edit of the story, replaying the run follows the change.
/// </summary>
public sealed class StoryTimelineViewModel : PanelViewModel
{
    /// <summary>
    /// The identifier of the panel.
    /// </summary>
    public new const string PanelId = "ColorRise.Saga.StoryTimeline";

    private IReadOnlyList<TimelineStep> steps = [];
    private IReadOnlyList<TimelineLane> lanes = [];
    private int cursor = -1;
    private string status = string.Empty;

    public StoryTimelineViewModel(PanelContext context)
        : base(context)
    {
        Service = ServiceProvider.Get<SagaPlaybackService>();
        GoToStepCommand = new AnonymousCommand<int>(ServiceProvider, Service.GoTo);
        BackCommand = new AnonymousCommand(ServiceProvider, () => Service.GoTo(Service.Cursor - 1));
        ForwardCommand = new AnonymousCommand(ServiceProvider, () => Service.GoTo(Service.Cursor + 1));
        ReplayCommand = new AnonymousCommand(ServiceProvider, () => Service.Replay());
        Service.Changed += OnPlaybackChanged;
        Update();
    }

    /// <summary>
    /// The description of the panel: hidden at first, at the bottom.
    /// </summary>
    public static PanelDescriptor Panel { get; } = new()
    {
        Id = PanelId,
        Title = () => "Story timeline",
        DefaultPlacement = PanelPlacement.Bottom,
        VisibleByDefault = false,
        CreateViewModel = context => context.CreateInstance<StoryTimelineViewModel>(),
    };

    /// <summary>
    /// The playback of the session, which the panel follows and moves.
    /// </summary>
    public SagaPlaybackService Service { get; }

    /// <summary>
    /// The steps of the run, those after the cursor included.
    /// </summary>
    public IReadOnlyList<TimelineStep> Steps { get => steps; private set => SetValue(ref steps, value); }

    /// <summary>
    /// What was going on across the steps: the acts, the scenarios and quests, the dialogues, what the game did, the signals.
    /// </summary>
    public IReadOnlyList<TimelineLane> Lanes { get => lanes; private set => SetValue(ref lanes, value); }

    /// <summary>
    /// The index of the step the run is at, or -1 when the story isn't playing.
    /// </summary>
    public int Cursor { get => cursor; private set => SetValue(ref cursor, value); }

    /// <summary>
    /// Where the run is, whether the story changed since, and where a replay stopped.
    /// </summary>
    public string Status { get => status; private set => SetValue(ref status, value); }

    /// <summary>
    /// What the step at the cursor did, in words.
    /// </summary>
    public ObservableList<string> Details { get; } = [];

    /// <summary>
    /// Moves the run to the step at an index.
    /// </summary>
    public ICommandBase GoToStepCommand { get; }

    public ICommandBase BackCommand { get; }

    public ICommandBase ForwardCommand { get; }

    /// <summary>
    /// Builds the story again and plays the run on it, enabled once the story or one of its dialogues changed.
    /// </summary>
    public ICommandBase ReplayCommand { get; }

    /// <inheritdoc />
    public override void Destroy()
    {
        Service.Changed -= OnPlaybackChanged;
        base.Destroy();
    }

    private void OnPlaybackChanged(object? sender, EventArgs e) => Update();

    private void Update()
    {
        var recorded = Service.Steps;
        Steps = [.. recorded.Select((x, i) => new TimelineStep(i, Glyph(x.Step.Kind), string.Join("\n", SagaTimeline.Describe(x, i)), i > Service.Cursor,
            i == Service.DivergedAt, x.PlaysDifferently))];
        Lanes = Service.Definition is { } definition ? SagaTimeline.Build(recorded, definition) : [];
        Cursor = Service.Cursor;
        Status = StatusText();
        var details = Service.CurrentStep is { } current ? SagaTimeline.Describe(current, Service.Cursor) : [];
        if (!details.SequenceEqual(Details))
        {
            Details.Clear();
            Details.AddRange(details);
        }
        GoToStepCommand.IsEnabled = Service.IsPlaying;
        BackCommand.IsEnabled = Service.Cursor > 0;
        ForwardCommand.IsEnabled = Service.IsPlaying && Service.Cursor < recorded.Count - 1 && Service.DivergedAt != Service.Cursor + 1;
        ReplayCommand.IsEnabled = Service.IsPlaying && Service.IsStale;
    }

    private string StatusText()
    {
        if (!Service.IsPlaying)
            return "The story is not playing.";
        var text = $"Step {Service.Cursor} of {Service.Steps.Count - 1}.";
        if (Service.IsStale)
            text += " The story changed since this run: replay it to follow the changes.";
        if (Service.DivergedAt is { } diverged)
            text += $" Step {diverged} no longer applies: {Service.Divergence}";
        return text;
    }

    private static string Glyph(SagaStepKind kind) => kind switch
    {
        SagaStepKind.Start => "▶",
        SagaStepKind.Raise => "!",
        SagaStepKind.Set => "=",
        SagaStepKind.Answer => "ƒ",
        SagaStepKind.Talk => "@",
        SagaStepKind.Continue => "…",
        SagaStepKind.Choose => ">",
        _ => "?",
    };
}
