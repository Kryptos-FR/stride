using System.Runtime.CompilerServices;
using ColorRise.Saga.Assets;
using ColorRise.Saga.Content;
using ColorRise.Saga.Editor.ViewModels;
using Stride.Core.Assets;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Serialization;

namespace ColorRise.Saga.Editor.Player;

/// <summary>
/// The story played in the session: a recorded run of a story built from its assets as they are in memory, with a cursor that
/// goes back and forth through the steps. The story player panel drives it; the timeline panel and the story and dialogue editors
/// follow it through <see cref="Changed"/>.
/// </summary>
/// <remarks>
/// Going back plays the steps again on a new runtime of the same build. An edit of the story or of one of its dialogues makes the
/// run stale (<see cref="IsStale"/>): it keeps its build until <see cref="Replay"/> builds the story again and plays the same steps
/// on it. There is one instance per session (<see cref="Of"/>), which Saga's editor plugin registers as a service of the session.
/// </remarks>
public sealed class SagaPlaybackService : IDisposable
{
    private static readonly ConditionalWeakTable<ISessionViewModel, SagaPlaybackService> instances = new();

    private readonly ISessionViewModel session;
    private readonly List<SagaRunStep> steps = [];
    private readonly List<AssetViewModel> watched = [];
    private readonly HashSet<AssetId> dialogues = [];
    private List<SagaBuildDiagnostic> diagnostics = [];
    private StoryDefinition? definition;

    public SagaPlaybackService(ISessionViewModel session)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <summary>
    /// The playback of a session, made on first use. Saga's panels take it from here rather than from the session's services: Game
    /// Studio makes the panels of a plugin that joins an open session before it initializes the plugin for the session.
    /// </summary>
    public static SagaPlaybackService Of(ISessionViewModel session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return instances.GetValue(session, x => new SagaPlaybackService(x));
    }

    /// <summary>
    /// Raised after any change: the story picked, its problems, the run, the cursor, the run going stale.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// The story to play.
    /// </summary>
    public StoryViewModel? Story { get; private set; }

    /// <summary>
    /// What is wrong with the story or its dialogues, as the compiler would report it, from the last check or build.
    /// </summary>
    public IReadOnlyList<SagaBuildDiagnostic> Diagnostics => diagnostics;

    public bool HasErrors => diagnostics.Any(x => x.Severity == SagaDiagnosticSeverity.Error);

    public bool IsPlaying => Run is not null;

    /// <summary>
    /// The story as it was built for the run.
    /// </summary>
    public StoryDefinition? Definition => Run is null ? null : definition;

    /// <summary>
    /// The run at the cursor, or null when the story isn't playing.
    /// </summary>
    public SagaRun? Run { get; private set; }

    /// <summary>
    /// The steps of the run, those after the cursor included.
    /// </summary>
    public IReadOnlyList<SagaRunStep> Steps => steps;

    /// <summary>
    /// The index of the step the run is at, or -1 when the story isn't playing.
    /// </summary>
    public int Cursor { get; private set; } = -1;

    public SagaRunStep? CurrentStep => Cursor >= 0 ? steps[Cursor] : null;

    /// <summary>
    /// Whether the story or one of its dialogues changed since the run's build.
    /// </summary>
    public bool IsStale { get; private set; }

    /// <summary>
    /// The first recorded step that no longer applies on the run's build, as the last replay or step forward found it, or null. Going
    /// back keeps it; a new step drops it with the steps after the cursor.
    /// </summary>
    public int? DivergedAt { get; private set; }

    /// <summary>
    /// Why the step at <see cref="DivergedAt"/> no longer applies, or empty.
    /// </summary>
    public string Divergence { get; private set; } = string.Empty;

    /// <summary>
    /// Picks the story to play: stops the run and checks the story.
    /// </summary>
    public void Select(StoryViewModel? story)
    {
        Stop();
        Story = story;
        Check();
        OnChanged();
    }

    /// <summary>
    /// Stops the run, forgets it, and checks the story again.
    /// </summary>
    public void Reset()
    {
        Stop();
        Check();
        OnChanged();
    }

    /// <summary>
    /// Builds the story and starts a new run, unless it has errors.
    /// </summary>
    /// <returns>Whether the story started.</returns>
    public bool Start()
    {
        Stop();
        if (Story is null || Build() is not { } built)
        {
            OnChanged();
            return false;
        }
        definition = built;
        Run = new SagaRun(built);
        steps.Add(Run.Apply(SagaStep.Start()));
        Cursor = 0;
        OnChanged();
        return true;
    }

    /// <summary>
    /// Takes a step at the cursor: the steps after it are dropped.
    /// </summary>
    /// <returns>Null when the step applied, else why it can't.</returns>
    public string? Do(SagaStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (Run is null)
            return "The story is not playing.";
        if (Run.CanApply(step) is { } reason)
            return reason;
        steps.RemoveRange(Cursor + 1, steps.Count - Cursor - 1);
        DivergedAt = null;
        Divergence = string.Empty;
        steps.Add(Run.Apply(step));
        ++Cursor;
        OnChanged();
        return null;
    }

    /// <summary>
    /// Moves the run to a step: back by playing the steps again from the start, forward by playing the next ones; it stops before
    /// a step that no longer applies (<see cref="DivergedAt"/>).
    /// </summary>
    public void GoTo(int index)
    {
        if (Run is null || definition is null)
            return;
        index = Math.Clamp(index, 0, steps.Count - 1);
        if (index == Cursor)
            return;
        if (index < Cursor)
        {
            NewRun(definition);
            Cursor = -1;
        }
        PlayTo(index);
        OnChanged();
    }

    /// <summary>
    /// Builds the story again from its assets and plays the steps up to the cursor on it, marking those that play differently
    /// and stopping before the first that no longer applies.
    /// </summary>
    /// <returns>False when the story has errors: the run stays as it was.</returns>
    public bool Replay()
    {
        if (Run is null || Story is null)
            return false;
        if (Build() is not { } built)
        {
            OnChanged();
            return false;
        }
        definition = built;
        IsStale = false;
        DivergedAt = null;
        Divergence = string.Empty;
        foreach (var step in steps)
            step.PlaysDifferently = false;
        var target = Cursor;
        NewRun(built);
        Cursor = -1;
        PlayTo(target);
        OnChanged();
        return true;
    }

    /// <summary>
    /// Whether a dialogue asset is one the run plays.
    /// </summary>
    public bool Plays(AssetId dialogue) => Run is not null && dialogues.Contains(dialogue);

    /// <summary>
    /// The nodes a dialogue went through in its last play up to the cursor, and whether it is still open; null when it hasn't
    /// played.
    /// </summary>
    public (IReadOnlyList<string> Path, bool IsOpen)? DialoguePath(string dialogueName)
    {
        for (var i = Cursor; i >= 0; --i)
        {
            if (steps[i].DialogueName == dialogueName)
                return (steps[i].DialoguePath, Run?.Dialogue?.Dialogue.Name == dialogueName);
        }
        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Stop();
        Story = null;
        if (instances.TryGetValue(session, out var instance) && instance == this)
            instances.Remove(session);
    }

    // Plays the recorded steps after the cursor up to an index, each result replacing the recorded one
    private void PlayTo(int index)
    {
        for (var i = Cursor + 1; i <= index; ++i)
        {
            var recorded = steps[i];
            if (Run!.CanApply(recorded.Step) is { } reason)
            {
                DivergedAt = i;
                Divergence = reason;
                return;
            }
            var result = Run.Apply(recorded.Step);
            result.PlaysDifferently = recorded.PlaysDifferently || result.Signature != recorded.Signature;
            steps[i] = result;
            Cursor = i;
        }
    }

    private void NewRun(StoryDefinition story)
    {
        Run?.Dispose();
        Run = new SagaRun(story);
    }

    private void Stop()
    {
        Run?.Dispose();
        Run = null;
        definition = null;
        steps.Clear();
        Cursor = -1;
        IsStale = false;
        DivergedAt = null;
        Divergence = string.Empty;
        Watch([]);
        dialogues.Clear();
    }

    // Checks the story for the panel, without starting it
    private void Check()
    {
        if (Story is not null)
            Build();
        else
            diagnostics = [];
    }

    /// <summary>
    /// Builds the story and its dialogues from the assets in memory, and follows their changes; null when they have errors.
    /// </summary>
    private StoryDefinition? Build()
    {
        var story = Story!;
        var resolved = new List<DialogueViewModel>();
        var found = new List<SagaBuildDiagnostic>();
        var built = SagaPlayback.Create(story.Asset, story.Url, reference =>
        {
            if (AttachedReferenceManager.GetAttachedReference(reference) is not { } target || session.GetAssetById(target.Id) is not DialogueViewModel dialogue)
                return null;
            resolved.Add(dialogue);
            return (dialogue.Asset, dialogue.Url);
        }, found);
        diagnostics = found;
        if (built is null)
            return null;
        dialogues.Clear();
        dialogues.UnionWith(resolved.Select(x => x.Id));
        Watch([story, .. resolved.Distinct()]);
        return built.Runtime.Story;
    }

    private void Watch(IReadOnlyList<AssetViewModel> assets)
    {
        foreach (var asset in watched)
        {
            asset.PropertyGraph.Changed -= OnAssetChanged;
            asset.PropertyGraph.ItemChanged -= OnAssetChanged;
        }
        watched.Clear();
        foreach (var asset in assets)
        {
            asset.PropertyGraph.Changed += OnAssetChanged;
            asset.PropertyGraph.ItemChanged += OnAssetChanged;
            watched.Add(asset);
        }
    }

    private void OnAssetChanged(object? sender, EventArgs e)
    {
        if (Run is null || IsStale)
            return;
        IsStale = true;
        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
