using System.Text.Json;
using System.Text.Json.Serialization;
using ColorRise.Skein;

namespace Ashcombe;

/// <summary>
/// What a save file holds: the variables of the story, what the game owns of it, and where everyone is.
/// </summary>
public sealed class SaveData
{
    public Dictionary<string, float> Numbers { get; set; } = [];

    public Dictionary<string, string> Strings { get; set; } = [];

    public Dictionary<string, bool> Bools { get; set; } = [];

    public Dictionary<string, int> Items { get; set; } = [];

    public int Day { get; set; } = 1;

    public TimeOfDay Time { get; set; }

    public List<JournalEntry> Journal { get; set; } = [];

    public string? Ending { get; set; }

    public List<string> Landmarks { get; set; } = [];

    public List<string> Visited { get; set; } = [];

    /// <summary>
    /// The URL of the root scene that was up.
    /// </summary>
    public string Root { get; set; } = "";

    /// <summary>
    /// The URLs of the interiors that were loaded under it, the outermost first.
    /// </summary>
    public List<string> Interiors { get; set; } = [];

    /// <summary>
    /// Where the cartographer stood in the world: X, Y and Z. The game fills it.
    /// </summary>
    public float[]? Player { get; set; }

    /// <summary>
    /// Where the people stand in their places, by place URL and entity name (<see cref="Whereabouts.PositionKey"/>).
    /// </summary>
    public Dictionary<string, float[]> Positions { get; set; } = [];
}

/// <summary>
/// The one save of the game: a JSON file. The game saves between two dialogues, as a dialogue that plays is not part
/// of the variables.
/// </summary>
public static class SaveSlot
{
    private static readonly JsonSerializerOptions Format = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    /// <summary>
    /// The file of the save, in the user's own folder.
    /// </summary>
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify), "Ashcombe", "save.json");

    /// <summary>
    /// Copies the state of a story.
    /// </summary>
    /// <exception cref="InvalidOperationException">A dialogue plays.</exception>
    public static SaveData Capture(StoryDirector story)
    {
        ArgumentNullException.ThrowIfNull(story);
        if (story.IsBusy)
            throw new InvalidOperationException("A dialogue plays: the game saves between two dialogues.");

        var variables = story.Variables.Save();
        return new SaveData
        {
            Numbers = variables.Numbers,
            Strings = variables.Strings,
            Bools = variables.Bools,
            Items = new Dictionary<string, int>(story.Inventory.Items),
            Day = story.Clock.Day,
            Time = story.Clock.Time,
            Journal = [.. story.Journal.Entries],
            Ending = story.Ending,
            Landmarks = [.. story.Map.Landmarks],
            Visited = [.. story.Map.Places],
            Root = story.Whereabouts.Root,
            Interiors = [.. story.Whereabouts.Interiors],
            Positions = new Dictionary<string, float[]>(story.Whereabouts.Positions),
        };
    }

    /// <summary>
    /// Puts a story in a saved state. Where the cartographer is, the game restores: it loads the places.
    /// </summary>
    /// <returns>The variables of the save that the story no longer has.</returns>
    /// <exception cref="InvalidOperationException">A dialogue plays.</exception>
    public static IReadOnlyList<string> Restore(StoryDirector story, SaveData data)
    {
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(data);
        if (story.IsBusy)
            throw new InvalidOperationException("A dialogue plays: the game loads between two dialogues.");

        var variables = new VariableSnapshot();
        Copy(data.Numbers, variables.Numbers);
        Copy(data.Strings, variables.Strings);
        Copy(data.Bools, variables.Bools);
        var dropped = story.Variables.Load(variables);

        story.Inventory.Replace(data.Items);
        story.Clock.Set(Math.Max(data.Day, 1), data.Time);
        story.Journal.Replace(data.Journal);
        story.Map.Replace(data.Landmarks, data.Visited);
        story.RestoreEnding(data.Ending);
        return dropped;
    }

    /// <summary>
    /// Writes a save as JSON.
    /// </summary>
    public static string ToJson(SaveData data) => JsonSerializer.Serialize(data, Format);

    /// <summary>
    /// Reads a save from JSON.
    /// </summary>
    /// <exception cref="FormatException">The text is not a save.</exception>
    public static SaveData FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SaveData>(json, Format) ?? throw new FormatException("The save is empty.");
        }
        catch (JsonException exception)
        {
            throw new FormatException($"The save cannot be read: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// Writes a save to a file.
    /// </summary>
    public static void Write(SaveData data, string path)
    {
        var json = ToJson(data);
        if (Path.GetDirectoryName(path) is { Length: > 0 } folder)
            Directory.CreateDirectory(folder);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Reads a save from a file.
    /// </summary>
    /// <returns>Null when there is no file.</returns>
    /// <exception cref="FormatException">The file is not a save.</exception>
    public static SaveData? Read(string path) => File.Exists(path) ? FromJson(File.ReadAllText(path)) : null;

    private static void Copy<T>(Dictionary<string, T> from, Dictionary<string, T> to)
    {
        foreach (var (name, value) in from)
        {
            to[name] = value;
        }
    }
}
