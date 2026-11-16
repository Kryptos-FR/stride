namespace Ashcombe;

/// <summary>
/// The states of a quest, in the order of the story's own <c>enum QuestState</c>: a quest variable holds the rank.
/// </summary>
public enum QuestState
{
    NotStarted,
    Offered,
    Active,
    Succeeded,
    Failed,
}

/// <summary>
/// One line of the journal: a quest took a state, on a day.
/// </summary>
/// <param name="Day">The day it happened.</param>
/// <param name="Quest">The id of the quest: <c>lamp</c> for the variable <c>$quest_lamp</c>.</param>
/// <param name="State">The state the quest took.</param>
public sealed record JournalEntry(int Day, string Quest, QuestState State);

/// <summary>
/// What happened to the quests, in order. The story tells it with <c>&lt;&lt;quest_changed lamp&gt;&gt;</c>.
/// </summary>
public sealed class Journal
{
    private readonly List<JournalEntry> entries = [];

    /// <summary>
    /// A quest took a state.
    /// </summary>
    public event Action<JournalEntry>? Added;

    /// <summary>
    /// Every entry, the oldest first.
    /// </summary>
    public IReadOnlyList<JournalEntry> Entries => entries;

    /// <summary>
    /// The quests the cartographer has to do: the last entry of each quest that is offered or active, in the order they came.
    /// </summary>
    public IEnumerable<JournalEntry> Open
    {
        get
        {
            var last = new Dictionary<string, JournalEntry>();
            foreach (var entry in entries)
            {
                last[entry.Quest] = entry;
            }

            return entries.Where(e => ReferenceEquals(last[e.Quest], e) && e.State is QuestState.Offered or QuestState.Active);
        }
    }

    /// <summary>
    /// Writes an entry.
    /// </summary>
    public void Add(JournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        entries.Add(entry);
        Added?.Invoke(entry);
    }

    /// <summary>
    /// Replaces every entry: what a save file holds. Tells nothing.
    /// </summary>
    public void Replace(IEnumerable<JournalEntry> saved)
    {
        ArgumentNullException.ThrowIfNull(saved);

        entries.Clear();
        entries.AddRange(saved);
    }
}
