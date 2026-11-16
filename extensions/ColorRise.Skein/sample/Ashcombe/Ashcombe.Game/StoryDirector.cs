using ColorRise.Skein;
using Stride.Core.Diagnostics;

namespace Ashcombe;

/// <summary>
/// What plays the dialogues of the story: the dialogue runner of a scene, or a session in a test.
/// </summary>
public interface IStoryStage
{
    /// <summary>
    /// Whether a dialogue plays.
    /// </summary>
    bool IsPlaying { get; }

    /// <summary>
    /// Whether playing this node now would say something: the story has it, and for a node group one of its nodes has
    /// all its conditions true.
    /// </summary>
    bool HasContent(string node);

    /// <summary>
    /// Plays a node, or a node of a node group, to the end of its dialogue.
    /// </summary>
    Task Play(string node);
}

/// <summary>
/// Leads the story of Ashcombe over its Yarn scripts: it plays the dialogue the player asks for, then the events of
/// the story that became true, and keeps what the game owns of the story (the inventory, the clock, the journal, the
/// map, where the cartographer is). It knows no scene: the scene that is up lends it a stage.
/// </summary>
/// <remarks>
/// The scripts hold the acts, the scenarios and the quests as variables. An event is a node of the node group
/// <see cref="EventsGroup"/>: its <c>when:</c> headers say when it happens, whatever the player is doing, and where
/// (<c>here() == "Castle"</c>). An event must make its own conditions false, or be <c>when: once</c>, so that it
/// happens once.
/// </remarks>
public sealed class StoryDirector
{
    /// <summary>
    /// The node group of the events of the story.
    /// </summary>
    public const string EventsGroup = "Events";

    /// <summary>
    /// What the variable of a quest starts with: <c>$quest_lamp</c> for the quest <c>lamp</c>.
    /// </summary>
    public const string QuestVariablePrefix = "$quest_";

    // More events than this in a row is a story whose event never turns false
    private const int MostEventsInARow = 20;

    private readonly ILogger? log;
    private bool leading;

    /// <summary>
    /// Makes the director of a story.
    /// </summary>
    /// <param name="variables">The variables of the story.</param>
    /// <param name="log">Where the director says what is wrong with the story.</param>
    public StoryDirector(VariableStorage variables, ILogger? log = null)
    {
        ArgumentNullException.ThrowIfNull(variables);

        this.log = log;
        Variables = variables;
    }

    /// <summary>
    /// The story reached an ending; the name is the one the script gave to <c>&lt;&lt;ending&gt;&gt;</c>.
    /// </summary>
    public event Action<string>? Ended;

    /// <summary>
    /// The variables of the story.
    /// </summary>
    public VariableStorage Variables { get; }

    /// <summary>
    /// What plays the dialogues now: the runner of the scene that is up. Null between two scenes, when nothing plays
    /// and no event happens; the events wait for the next stage.
    /// </summary>
    public IStoryStage? Stage { get; set; }

    /// <summary>
    /// What the cartographer carries.
    /// </summary>
    public Inventory Inventory { get; } = new();

    /// <summary>
    /// The day and the time of the day.
    /// </summary>
    public DayClock Clock { get; } = new();

    /// <summary>
    /// What happened to the quests.
    /// </summary>
    public Journal Journal { get; } = new();

    /// <summary>
    /// The landmarks surveyed and the places been to.
    /// </summary>
    public Map Map { get; } = new();

    /// <summary>
    /// Where the cartographer is: the place the scripts read with <c>here()</c>.
    /// </summary>
    public Whereabouts Whereabouts { get; } = new();

    /// <summary>
    /// The ending the story reached; null while it goes on.
    /// </summary>
    public string? Ending { get; private set; }

    /// <summary>
    /// Whether a dialogue or an event plays: the player waits, and the game saves nothing.
    /// </summary>
    public bool IsBusy => leading || Stage?.IsPlaying == true;

    /// <summary>
    /// Whether <see cref="Talk"/> would play this node now.
    /// </summary>
    /// <param name="node">The node, or the node group.</param>
    /// <param name="on">Another stage than the scene's: the runner of someone who speaks another project.</param>
    public bool CanTalk(string node, IStoryStage? on = null) => !IsBusy && Ending is null && (on ?? Stage)?.HasContent(node) == true;

    /// <summary>
    /// Plays a node, or a node of a node group, then the events that became true.
    /// </summary>
    /// <param name="node">The node, or the node group.</param>
    /// <param name="on">Another stage than the scene's: the runner of someone who speaks another project. The events play on the scene's.</param>
    /// <returns>False, with nothing played, when something plays already, when the story is over, or when the node has nothing to say now.</returns>
    public async Task<bool> Talk(string node, IStoryStage? on = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!CanTalk(node, on))
            return false;

        leading = true;
        try
        {
            await (on ?? Stage)!.Play(node);
            await PlayEvents();
        }
        finally
        {
            leading = false;
        }

        return true;
    }

    /// <summary>
    /// Plays the events that are true now, one after the other. The game calls it when a fact of the world changed
    /// outside a dialogue (time passed, the cartographer went somewhere, a save was loaded); it does nothing while
    /// something plays, as <see cref="Talk"/> plays the events after its dialogue.
    /// </summary>
    public async Task RunEvents()
    {
        if (IsBusy)
            return;

        leading = true;
        try
        {
            await PlayEvents();
        }
        finally
        {
            leading = false;
        }
    }

    /// <summary>
    /// The state of a quest, read from its variable.
    /// </summary>
    /// <returns>Null when the story has no variable for this quest.</returns>
    public QuestState? StateOf(string quest)
    {
        // An enum of the scripts is a number: the rank of the case
        if (Variables.TryGetValue<float>(QuestVariablePrefix + quest, out var rank) && Enum.IsDefined((QuestState)(int)rank))
            return (QuestState)(int)rank;

        return null;
    }

    /// <summary>
    /// Writes the state of a quest in the journal: what <c>&lt;&lt;quest_changed lamp&gt;&gt;</c> does.
    /// </summary>
    public void QuestChanged(string quest)
    {
        ArgumentNullException.ThrowIfNull(quest);

        if (StateOf(quest) is not { } state)
        {
            log?.Error($"<<quest_changed {quest}>>: the story has no quest variable '{QuestVariablePrefix}{quest}' of the type QuestState.");
            return;
        }

        Journal.Add(new JournalEntry(Clock.Day, quest, state));
    }

    /// <summary>
    /// Ends the story: what <c>&lt;&lt;ending keeper&gt;&gt;</c> does. No event plays after it.
    /// </summary>
    public void End(string ending)
    {
        ArgumentException.ThrowIfNullOrEmpty(ending);

        Ending = ending;
        Ended?.Invoke(ending);
    }

    /// <summary>
    /// Sets the ending without telling it: what a save file holds.
    /// </summary>
    public void RestoreEnding(string? ending) => Ending = ending;

    private async Task PlayEvents()
    {
        for (var played = 0; Ending is null && Stage is { } stage && stage.HasContent(EventsGroup); played++)
        {
            if (played == MostEventsInARow)
            {
                log?.Error($"The node group '{EventsGroup}' still has an event to play after {MostEventsInARow} in a row: an event must make its own 'when:' false, or have 'when: once'.");
                return;
            }

            await stage.Play(EventsGroup);
        }
    }
}
