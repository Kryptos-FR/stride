namespace Ashcombe;

/// <summary>
/// Where the cartographer is, as the story and a save see it: the root scene that is up, the interiors loaded under it
/// (the inn, then its cellar), and where the people who moved stand. A place is named by the URL of its scene
/// (<c>Places/Village/Inn</c>); the scripts know it by its short name (<c>Inn</c>).
/// </summary>
/// <remarks>
/// Only one chain of interiors is loaded at a time: going into a house leaves the one before. The places the story
/// loads by itself (the fair) are not interiors and are not here: they follow the variables.
/// </remarks>
public sealed class Whereabouts
{
    private readonly List<string> interiors = [];

    /// <summary>
    /// The cartographer went somewhere.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// The URL of the root scene that is up; empty before the first.
    /// </summary>
    public string Root { get; private set; } = "";

    /// <summary>
    /// The URLs of the interiors loaded under the root, the outermost first: <c>Inn</c>, then <c>Cellar</c>.
    /// </summary>
    public IReadOnlyList<string> Interiors => interiors;

    /// <summary>
    /// The URL of the place the cartographer is in: the deepest interior, else the root.
    /// </summary>
    public string Current => interiors.Count > 0 ? interiors[^1] : Root;

    /// <summary>
    /// The short name of where the cartographer is, as <c>here()</c> gives it to the scripts.
    /// </summary>
    public string Here => ShortName(Current);

    /// <summary>
    /// Where the people who moved stand in their place, by <see cref="PositionKey"/>: X, Y and Z in the place's scene.
    /// A person keeps its place when its scene is unloaded and loaded again.
    /// </summary>
    public Dictionary<string, float[]> Positions { get; } = [];

    /// <summary>
    /// The short name of a place: the last part of its URL.
    /// </summary>
    public static string ShortName(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var slash = url.LastIndexOf('/');
        return slash < 0 ? url : url[(slash + 1)..];
    }

    /// <summary>
    /// The key of someone's position: the URL of the place and the name of the entity.
    /// </summary>
    public static string PositionKey(string place, string entity) => $"{place}|{entity}";

    /// <summary>
    /// A new root scene is up, with no interior.
    /// </summary>
    public void SetRoot(string url)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);
        Root = url;
        interiors.Clear();
        Changed?.Invoke();
    }

    /// <summary>
    /// The interiors loaded under the root now, the outermost first.
    /// </summary>
    public void SetInteriors(IEnumerable<string> urls)
    {
        ArgumentNullException.ThrowIfNull(urls);
        interiors.Clear();
        interiors.AddRange(urls);
        Changed?.Invoke();
    }

    /// <summary>
    /// Replaces everything: what a save file holds. Tells nothing.
    /// </summary>
    public void Replace(string root, IEnumerable<string> savedInteriors, IEnumerable<KeyValuePair<string, float[]>> positions)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(savedInteriors);
        ArgumentNullException.ThrowIfNull(positions);

        Root = root;
        interiors.Clear();
        interiors.AddRange(savedInteriors);
        Positions.Clear();
        foreach (var (key, position) in positions)
        {
            Positions[key] = position;
        }
    }
}
