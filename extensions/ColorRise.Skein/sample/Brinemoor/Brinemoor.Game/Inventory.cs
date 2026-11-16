namespace Brinemoor;

/// <summary>
/// What the courier carries: things by name, each with a count.
/// </summary>
public sealed class Inventory
{
    private readonly SortedDictionary<string, int> items = new(StringComparer.Ordinal);

    /// <summary>
    /// A thing came or went.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// The things carried, by name, with how many of each.
    /// </summary>
    public IReadOnlyDictionary<string, int> Items => items;

    /// <summary>
    /// How many of a thing the courier carries.
    /// </summary>
    public int Count(string item) => items.GetValueOrDefault(item);

    /// <summary>
    /// Whether the courier carries at least one of a thing.
    /// </summary>
    public bool Has(string item) => Count(item) > 0;

    /// <summary>
    /// Adds a thing.
    /// </summary>
    public void Give(string item, int count = 1)
    {
        ArgumentException.ThrowIfNullOrEmpty(item);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        items[item] = Count(item) + count;
        Changed?.Invoke();
    }

    /// <summary>
    /// Removes a thing.
    /// </summary>
    /// <returns>False, with nothing removed, when the courier carries fewer.</returns>
    public bool Take(string item, int count = 1)
    {
        ArgumentException.ThrowIfNullOrEmpty(item);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var left = Count(item) - count;
        if (left < 0)
            return false;

        if (left == 0)
            items.Remove(item);
        else
            items[item] = left;
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Replaces everything carried: what a save file holds.
    /// </summary>
    public void Replace(IEnumerable<KeyValuePair<string, int>> saved)
    {
        ArgumentNullException.ThrowIfNull(saved);

        items.Clear();
        foreach (var (item, count) in saved)
        {
            if (count > 0)
                items[item] = count;
        }

        Changed?.Invoke();
    }
}
