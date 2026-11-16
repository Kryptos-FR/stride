using Stride.Core.Assets.Editor.Panels;
using Stride.Core.Presentation.Collections;
using Stride.Core.Presentation.Commands;

namespace ColorRise.Skein.Editor.Player;

/// <summary>
/// A step of the run as the timeline shows it.
/// </summary>
/// <param name="Index">The index of the step in the run.</param>
/// <param name="Kind">What the step is.</param>
/// <param name="Glyph">The kind of step, in one character.</param>
/// <param name="Tip">What the step did, in words.</param>
/// <param name="IsFuture">Whether the step comes after the cursor: recorded, not shown as played.</param>
/// <param name="Changed">Whether the last replay made the step or made it differ.</param>
public sealed record TimelineStep(int Index, PlayerStepKind Kind, string Glyph, string Tip, bool IsFuture, bool Changed);

/// <summary>
/// The dialogue timeline panel: the steps of the run of the session's <see cref="DialoguePlayerService"/> and what went
/// on across them, lane by lane. Clicking a step moves the run there; after an edit of the scripts, replaying the run
/// follows the change.
/// </summary>
public sealed class DialogueTimelineViewModel : PanelViewModel
{
    /// <summary>
    /// The identifier of the panel.
    /// </summary>
    public new const string PanelId = "ColorRise.Skein.DialogueTimeline";

    private IReadOnlyList<TimelineStep> steps = [];
    private IReadOnlyList<TimelineLane> lanes = [];
    private int cursor = -1;
    private string status = "";

    /// <param name="context">The context of the panel.</param>
    public DialogueTimelineViewModel(PanelContext context)
        : base(context)
    {
        Service = DialoguePlayerService.Of(context.Session);
        GoToStepCommand = new AnonymousCommand<int>(ServiceProvider, step => Service.GoTo(step));
        BackCommand = new AnonymousCommand(ServiceProvider, () => Service.GoTo(Service.Cursor - 1));
        ForwardCommand = new AnonymousCommand(ServiceProvider, () => Service.GoTo(Service.Cursor + 1));
        ReplayCommand = new AnonymousTaskCommand(ServiceProvider, Replay);
        Service.Changed += Update;
        Update();
    }

    /// <summary>
    /// The description of the panel: hidden at first, at the bottom.
    /// </summary>
    public static PanelDescriptor Panel { get; } = new()
    {
        Id = PanelId,
        Title = () => "Dialogue timeline",
        DefaultPlacement = PanelPlacement.Bottom,
        VisibleByDefault = false,
        CreateViewModel = context => new DialogueTimelineViewModel(context),
    };

    /// <summary>
    /// The player of the session, which the panel follows and moves.
    /// </summary>
    public DialoguePlayerService Service { get; }

    /// <summary>
    /// The steps of the run, those after the cursor included.
    /// </summary>
    public IReadOnlyList<TimelineStep> Steps { get => steps; private set => SetValue(ref steps, value); }

    /// <summary>
    /// What went on across the steps: the nodes, the lines, the choices, what the game did, the variables.
    /// </summary>
    public IReadOnlyList<TimelineLane> Lanes { get => lanes; private set => SetValue(ref lanes, value); }

    /// <summary>
    /// The index of the step the run is shown at, or -1 when nothing plays.
    /// </summary>
    public int Cursor { get => cursor; private set => SetValue(ref cursor, value); }

    /// <summary>
    /// Where the run is, whether the scripts changed since, and where a replay stopped.
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

    /// <summary>
    /// Moves the run one step back.
    /// </summary>
    public ICommandBase BackCommand { get; }

    /// <summary>
    /// Moves the run one recorded step forward.
    /// </summary>
    public ICommandBase ForwardCommand { get; }

    /// <summary>
    /// Plays the run again on the scripts as they are now; enabled once they changed.
    /// </summary>
    public ICommandBase ReplayCommand { get; }

    /// <summary>
    /// Plays the run again on the scripts as they are now.
    /// </summary>
    public Task Replay() => Service.Replay();

    /// <inheritdoc />
    public override void Destroy()
    {
        Service.Changed -= Update;
        base.Destroy();
    }

    private void Update()
    {
        var recorded = Service.Steps;
        Steps = [.. recorded.Select((step, i) => new TimelineStep(i, step.Kind, Glyph(step.Kind), string.Join("\n", DialogueTimeline.Describe(step, i)), i > Service.Cursor, step.Changed))];
        Lanes = DialogueTimeline.Build(recorded);
        Cursor = Service.Cursor;
        Status = StatusText();
        var details = Service.CurrentStep is { } current ? DialogueTimeline.Describe(current, Service.Cursor) : [];
        if (!details.SequenceEqual(Details))
        {
            Details.Clear();
            Details.AddRange(details);
        }

        GoToStepCommand.IsEnabled = Service.IsPlaying;
        BackCommand.IsEnabled = Service.Cursor > 0;
        ForwardCommand.IsEnabled = Service.IsPlaying && Service.Cursor < recorded.Count - 1;
        ReplayCommand.IsEnabled = Service.IsPlaying && Service.IsStale && !Service.IsBusy;
    }

    private string StatusText()
    {
        if (!Service.IsPlaying)
            return Service.Problem ?? "No dialogue is playing.";

        var text = $"Step {Service.Cursor} of {Service.Steps.Count - 1}.";
        if (Service.IsStale)
            text += " The scripts changed since this run: replay it to follow the changes.";
        if (Service.Divergence is { } divergence)
            text += $" The replay stopped. {divergence}";
        if (Service.Problem is { } problem)
            text += $" {problem}";
        return text;
    }

    private static string Glyph(PlayerStepKind kind) => kind switch
    {
        PlayerStepKind.Start => "▶",
        PlayerStepKind.NodeEnter => "□",
        PlayerStepKind.Line => "…",
        PlayerStepKind.Options => "?",
        PlayerStepKind.Choose => ">",
        PlayerStepKind.Command => "«",
        PlayerStepKind.Function => "ƒ",
        PlayerStepKind.SetVariable => "=",
        PlayerStepKind.Saliency => "≈",
        PlayerStepKind.End => "■",
        _ => "!",
    };
}
