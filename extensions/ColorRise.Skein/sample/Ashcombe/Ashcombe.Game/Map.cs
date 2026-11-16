namespace Ashcombe;

/// <summary>
/// The cartographer's map: the landmarks surveyed and the places been to. The scripts read it with
/// <c>surveyed("bell")</c>, <c>survey_count()</c> and <c>been_to("Castle")</c>, and add to it with
/// <c>&lt;&lt;survey bell&gt;&gt;</c>; the game adds a place when the cartographer first gets there.
/// </summary>
public sealed class Map
{
    private readonly SortedSet<string> landmarks = new(StringComparer.Ordinal);
    private readonly SortedSet<string> places = new(StringComparer.Ordinal);

    /// <summary>
    /// A landmark was surveyed, or a place reached for the first time.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// The landmarks surveyed, by name.
    /// </summary>
    public IReadOnlyCollection<string> Landmarks => landmarks;

    /// <summary>
    /// The places been to, by their short name (<c>Inn</c>, <c>Castle</c>).
    /// </summary>
    public IReadOnlyCollection<string> Places => places;

    /// <summary>
    /// Whether a landmark is on the map.
    /// </summary>
    public bool IsSurveyed(string landmark) => landmarks.Contains(landmark);

    /// <summary>
    /// Whether the cartographer has been to a place.
    /// </summary>
    public bool HasBeenTo(string place) => places.Contains(place);

    /// <summary>
    /// Puts a landmark on the map.
    /// </summary>
    /// <returns>False when it was on it already.</returns>
    public bool Survey(string landmark)
    {
        ArgumentException.ThrowIfNullOrEmpty(landmark);
        if (!landmarks.Add(landmark))
            return false;

        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Writes down that the cartographer reached a place.
    /// </summary>
    public void Visit(string place)
    {
        ArgumentException.ThrowIfNullOrEmpty(place);
        if (places.Add(place))
            Changed?.Invoke();
    }

    /// <summary>
    /// Replaces the map: what a save file holds. Tells nothing.
    /// </summary>
    public void Replace(IEnumerable<string> savedLandmarks, IEnumerable<string> savedPlaces)
    {
        ArgumentNullException.ThrowIfNull(savedLandmarks);
        ArgumentNullException.ThrowIfNull(savedPlaces);

        landmarks.Clear();
        landmarks.UnionWith(savedLandmarks);
        places.Clear();
        places.UnionWith(savedPlaces);
    }
}
