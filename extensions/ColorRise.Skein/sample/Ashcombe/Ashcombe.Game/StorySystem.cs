using ColorRise.Skein;
using Stride.Core;
using Stride.Core.Diagnostics;
using Stride.Core.Mathematics;
using Stride.Core.Serialization.Contents;
using Stride.Engine;
using Stride.Engine.Processors;
using Stride.Games;

namespace Ashcombe;

/// <summary>
/// What outlives the scenes of Ashcombe: the story (its director, with the inventory, the clock, the journal, the map
/// and where the cartographer is), the places loaded, the Yarn projects of the game, and the save slot. A game has
/// one, made with the game; the <see cref="Stage"/> of each root scene attaches to it when its scene starts.
/// </summary>
/// <remarks>
/// <para>
/// A place is a scene. The root scenes (the village, the castle, the mine) are swapped: one is up at a time. The
/// interiors (the inn, its cellar, the mill, the levels of the mine) are child scenes loaded under the root while the
/// cartographer is in them, one chain at a time. A <see cref="StoryPlace"/> (the fair) is a child scene the story
/// loads by itself.
/// </para>
/// <para>
/// A dialogue goes on while an interior loads: its runner is in the root scene. A journey to another root scene is
/// held until the dialogue ends, since the runner goes with its scene.
/// </para>
/// </remarks>
[GameSystem]
public sealed class StorySystem : GameSystemBase
{
    private static readonly Logger Log = GlobalLogger.GetLogger("Ashcombe");

    private readonly List<(string Url, Scene Scene)> interiors = [];
    private readonly Dictionary<string, Scene> storyPlaces = [];
    private readonly List<YarnProject> projects = [];
    private StoryDirector? story;
    private IPlaceLoader? loader;
    private (string Url, string? Spawn)? heldJourney;
    private Vector3? playerOnArrival;
    private bool moving;

    /// <summary>
    /// Makes the system of a game.
    /// </summary>
    public StorySystem(IServiceRegistry registry)
        : base(registry)
    {
    }

    /// <summary>
    /// The places loaded changed: the stage looks again for the people and the doors.
    /// </summary>
    public event Action? PlacesChanged;

    /// <summary>
    /// The story of the game.
    /// </summary>
    public StoryDirector Story => story ??= CreateStory();

    /// <summary>
    /// The stage of the root scene that is up; null between two root scenes.
    /// </summary>
    public Stage? Stage { get; private set; }

    /// <summary>
    /// What loads the scenes of the places: the game's content, unless a test gives another.
    /// </summary>
    public IPlaceLoader Loader
    {
        get => loader ??= new ContentPlaceLoader(Services.GetSafeServiceAs<ContentManager>());
        set => loader = value;
    }

    /// <summary>
    /// Whether the player waits: a dialogue or an event plays, or the cartographer is on the way somewhere.
    /// </summary>
    public bool IsBusy => Story.IsBusy || moving || heldJourney is not null;

    /// <summary>
    /// How dark the screen is, from 0 to 1, while the root scene changes. The stage's screen shows it.
    /// </summary>
    public float Fade { get; set; }

    /// <summary>
    /// The Yarn projects the variables know, whichever scene uses them.
    /// </summary>
    public IReadOnlyList<YarnProject> Projects => projects;

    /// <summary>
    /// The interiors loaded under the root scene, the outermost first.
    /// </summary>
    public IEnumerable<Scene> Interiors => interiors.Select(i => i.Scene);

    /// <summary>
    /// The places the story loaded by itself under the root scene.
    /// </summary>
    public IEnumerable<Scene> StoryPlaces => storyPlaces.Values;

    private DialogueSystem Dialogues => Services.GetSafeServiceAs<DialogueSystem>();

    private SceneSystem Scenes => Services.GetSafeServiceAs<SceneSystem>();

    private ScriptSystem Scripts => Services.GetSafeServiceAs<ScriptSystem>();

    /// <summary>
    /// Makes a Yarn project known to the variables of the game, and keeps it loaded: its variables read their defaults
    /// and its smart variables are computed from the start, before a scene that uses the project starts.
    /// </summary>
    public void Declare(YarnProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (projects.Contains(project))
            return;

        projects.Add(project);
        Dialogues.Variables.Declare(project);
    }

    /// <summary>
    /// The stage of a root scene that started: the story plays its dialogues on its runner.
    /// </summary>
    internal void Attach(Stage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);

        Stage = stage;
        Story.Stage = stage.StoryStage;
        StoryCommands.Story = Story;
        if (stage.Runner?.Project is { } main)
            Declare(main);
        foreach (var project in stage.Projects)
        {
            Declare(project);
        }

        var root = stage.Entity.Scene;
        if (Story.Whereabouts.Root.Length == 0 && Loader.UrlOf(root) is { } url)
            Story.Whereabouts.SetRoot(url);
        Story.Map.Visit(Story.Whereabouts.Here);
        PlacePeople(Story.Whereabouts.Root, root);

        if (playerOnArrival is { } position && stage.Player is not null)
            Move(stage.Player, position);
        playerOnArrival = null;
        moving = false;
        PlacesChanged?.Invoke();
    }

    /// <summary>
    /// The stage of a root scene that goes.
    /// </summary>
    internal void Detach(Stage stage)
    {
        if (Stage != stage)
            return;

        Stage = null;
        Story.Stage = null;
    }

    /// <summary>
    /// Takes the cartographer through a door: into an interior, back out of one, or on the road to another root
    /// scene, which waits until no dialogue plays.
    /// </summary>
    /// <param name="url">The URL of the place.</param>
    /// <param name="spawn">The entity of the place where the cartographer arrives; null for the place's <c>Arrival</c>.</param>
    /// <param name="from">The scene of the door, which is the parent of an interior it opens.</param>
    /// <param name="isJourney">Whether the place is another root scene.</param>
    /// <returns>A task that completes once the cartographer is there, or, for a journey asked during a dialogue, at once.</returns>
    public Task GoTo(string url, string? spawn, Scene? from, bool isJourney)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);
        url = Places.Normalize(url);

        if (isJourney && url != Story.Whereabouts.Root)
        {
            if (heldJourney is not null)
                throw new InvalidOperationException($"The cartographer is already on the way to '{heldJourney.Value.Url}'.");

            // The dialogue that asked for it plays on the runner of this scene: the journey waits for its end
            heldJourney = (url, spawn);
            Scripts.AddTask(TravelWhenFree);
            return Task.CompletedTask;
        }

        return Enter(url, spawn, from);
    }

    /// <summary>
    /// Copies the state of the game: the story and where everyone is.
    /// </summary>
    /// <exception cref="InvalidOperationException">Something plays, or the cartographer is on the way.</exception>
    public SaveData Capture()
    {
        if (IsBusy)
            throw new InvalidOperationException("Something plays: the game saves between two dialogues.");

        RememberPeople();
        var data = SaveSlot.Capture(Story);
        if (Stage?.Player is { } player)
            data.Player = [player.Transform.Position.X, player.Transform.Position.Y, player.Transform.Position.Z];
        return data;
    }

    /// <summary>
    /// Puts the game in a saved state: the story, the root scene and the interiors of the save, where everyone stood.
    /// </summary>
    /// <returns>The variables of the save that the story declares with another type, which were dropped.</returns>
    /// <exception cref="InvalidOperationException">Something plays, or the cartographer is on the way.</exception>
    public IReadOnlyList<string> Restore(SaveData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (IsBusy)
            throw new InvalidOperationException("Something plays: the game loads between two dialogues.");

        var root = data.Root.Length > 0 ? Places.Normalize(data.Root) : Story.Whereabouts.Root;
        var sameRoot = root == Story.Whereabouts.Root;
        UnloadInteriors(0, remember: false);
        var dropped = SaveSlot.Restore(Story, data);
        Story.Whereabouts.Replace(root, [], data.Positions);
        var position = data.Player is [var x, var y, var z] ? new Vector3(x, y, z) : (Vector3?)null;

        if (!sameRoot)
        {
            SwapRoot(root);
            playerOnArrival = position;
        }
        else
        {
            PlacePeople(root, Scenes.SceneInstance.RootScene);
        }

        var parent = Scenes.SceneInstance.RootScene;
        foreach (var interior in data.Interiors)
        {
            parent = LoadInterior(Places.Normalize(interior), parent);
        }

        Story.Whereabouts.SetInteriors(interiors.Select(i => i.Url));
        if (sameRoot && position is { } here && Stage?.Player is { } player)
            Move(player, here);
        Stage?.FollowArea();
        PlacesChanged?.Invoke();
        return dropped;
    }

    /// <summary>
    /// Loads and unloads the places the story loads by itself, as their variables say.
    /// </summary>
    internal void FollowStoryPlaces(IEnumerable<StoryPlace> places)
    {
        var changed = false;
        var root = Scenes.SceneInstance.RootScene;
        foreach (var place in places)
        {
            if (place.Place?.Url is not { Length: > 0 } url)
                continue;

            url = Places.Normalize(url);
            var wanted = place.IsWanted(Dialogues.Variables);
            if (wanted && !storyPlaces.ContainsKey(url))
            {
                var scene = Loader.Load(url);
                root.Children.Add(scene);
                PlacePeople(url, scene);
                storyPlaces.Add(url, scene);
                changed = true;
            }
            else if (!wanted && storyPlaces.Remove(url, out var scene))
            {
                Remember(url, scene);
                root.Children.Remove(scene);
                Loader.Unload(scene);
                changed = true;
            }
        }

        if (changed)
            PlacesChanged?.Invoke();
    }

    /// <summary>
    /// The scene of a loaded place, by its URL: the root scene, an interior or a place of the story.
    /// </summary>
    public Scene? SceneOf(string url)
    {
        url = Places.Normalize(url);
        if (url == Story.Whereabouts.Root)
            return Scenes.SceneInstance?.RootScene;

        return interiors.FirstOrDefault(i => i.Url == url).Scene ?? storyPlaces.GetValueOrDefault(url);
    }

    /// <summary>
    /// The URL of a loaded place's scene.
    /// </summary>
    public string? UrlOf(Scene scene)
    {
        if (scene == Scenes.SceneInstance?.RootScene)
            return Story.Whereabouts.Root;

        foreach (var (url, interior) in interiors)
        {
            if (interior == scene)
                return url;
        }

        return storyPlaces.FirstOrDefault(p => p.Value == scene).Key;
    }

    /// <summary>
    /// Writes down where the people of every loaded place stand, for when their place loads again.
    /// </summary>
    public void RememberPeople()
    {
        if (Scenes.SceneInstance?.RootScene is { } root && Story.Whereabouts.Root.Length > 0)
            Remember(Story.Whereabouts.Root, root);
        foreach (var (url, scene) in interiors)
        {
            Remember(url, scene);
        }

        foreach (var (url, scene) in storyPlaces)
        {
            Remember(url, scene);
        }
    }

    private StoryDirector CreateStory()
    {
        var created = new StoryDirector(Dialogues.Variables, Log);
        StoryCommands.Story = created;
        return created;
    }

    private async Task Enter(string url, string? spawn, Scene? from)
    {
        moving = true;
        try
        {
            var root = Scenes.SceneInstance.RootScene;
            if (url == Story.Whereabouts.Root)
            {
                UnloadInteriors(0, remember: true);
            }
            else if (interiors.FindIndex(i => i.Url == url) is var index and >= 0)
            {
                // Up the stairs: the places under it go
                UnloadInteriors(index + 1, remember: true);
            }
            else
            {
                // Through a door of the root, or of an interior: the places that are not on the way go
                var parentIndex = from is null || from == root ? -1 : interiors.FindIndex(i => i.Scene == from);
                UnloadInteriors(parentIndex + 1, remember: true);
                LoadInterior(url, parentIndex < 0 ? root : interiors[parentIndex].Scene);
            }

            Story.Whereabouts.SetInteriors(interiors.Select(i => i.Url));
            Story.Map.Visit(Story.Whereabouts.Here);
            if (SceneOf(url) is { } scene && Stage?.Player is { } player && SpawnPoint(scene, spawn) is { } point)
                Move(player, point);
            Stage?.FollowArea();
            PlacesChanged?.Invoke();

            // One frame for the place to start before the cartographer looks around
            await Scripts.NextFrame();
        }
        finally
        {
            moving = false;
        }
    }

    private async Task TravelWhenFree()
    {
        while (Story.IsBusy)
        {
            await Scripts.NextFrame();
        }

        if (heldJourney is not { } journey)
            return;

        moving = true;
        heldJourney = null;
        try
        {
            Fade = 1;
            await Scripts.NextFrame();

            RememberPeople();
            UnloadInteriors(0, remember: false);
            var scene = SwapRoot(journey.Url);
            playerOnArrival = SpawnPoint(scene, journey.Spawn);
        }
        catch
        {
            moving = false;
            Fade = 0;
            throw;
        }

        // The next stage clears "moving" when it attaches
    }

    private Scene SwapRoot(string url)
    {
        foreach (var (placeUrl, scene) in storyPlaces)
        {
            Scenes.SceneInstance.RootScene.Children.Remove(scene);
            Loader.Unload(scene);
        }

        storyPlaces.Clear();
        var next = Loader.Load(url);
        var previous = Scenes.SceneInstance.RootScene;

        // The stage of the scene that goes detaches here, and the stage of the next one attaches when its scripts start
        Story.Whereabouts.SetRoot(url);
        Scenes.SceneInstance.RootScene = next;
        if (previous is not null)
            Loader.Unload(previous);
        PlacePeople(url, next);
        return next;
    }

    private Scene LoadInterior(string url, Scene parent)
    {
        var scene = Loader.Load(url);
        parent.Children.Add(scene);
        PlacePeople(url, scene);
        interiors.Add((url, scene));
        return scene;
    }

    /// <summary>
    /// Unloads the interiors from an index of the chain on, the deepest first.
    /// </summary>
    private void UnloadInteriors(int from, bool remember)
    {
        for (var i = interiors.Count - 1; i >= from; i--)
        {
            var (url, scene) = interiors[i];
            if (remember)
                Remember(url, scene);
            scene.Parent = null;
            Loader.Unload(scene);
            interiors.RemoveAt(i);
        }
    }

    private void Remember(string url, Scene scene)
    {
        foreach (var talkable in Talkables(scene))
        {
            var position = talkable.Entity.Transform.Position;
            Story.Whereabouts.Positions[Whereabouts.PositionKey(url, talkable.Entity.Name)] = [position.X, position.Y, position.Z];
        }
    }

    private void PlacePeople(string url, Scene scene)
    {
        foreach (var talkable in Talkables(scene))
        {
            if (Story.Whereabouts.Positions.TryGetValue(Whereabouts.PositionKey(url, talkable.Entity.Name), out var position) && position is [var x, var y, var z])
                talkable.Entity.Transform.Position = new Vector3(x, y, z);
        }
    }

    private static IEnumerable<Talkable> Talkables(Scene scene)
        => scene.Entities.SelectMany(Self).Select(e => e.Get<Talkable>()).OfType<Talkable>();

    private static IEnumerable<Entity> Self(Entity entity)
        => entity.Transform.Children.Select(t => t.Entity).SelectMany(Self).Prepend(entity);

    /// <summary>
    /// Where the cartographer arrives in a place, in the world: the entity named so, else the place's <c>Arrival</c>.
    /// </summary>
    // The camera follows the cartographer from where the scene has it
    private static void Move(Entity player, Vector3 position)
    {
        if (player.Get<PlayerController>() is { } controller)
            controller.MoveTo(position);
        else
            player.Transform.Position = position;
    }

    private static Vector3? SpawnPoint(Scene scene, string? spawn)
    {
        var name = string.IsNullOrEmpty(spawn) ? "Arrival" : spawn;
        var point = scene.Entities.SelectMany(Self).FirstOrDefault(e => e.Name == name);
        if (point is null)
        {
            Log.Warning($"The place '{scene.Name}' has no entity '{name}' to arrive at.");
            return null;
        }

        return WorldPosition(point);
    }

    /// <summary>
    /// Where an entity of a place is in the world, with the offsets of its scene and of the scenes above it.
    /// </summary>
    internal static Vector3 WorldPosition(Entity entity)
    {
        var position = entity.Transform.Position;
        for (var parent = entity.Transform.Parent; parent is not null; parent = parent.Parent)
        {
            position += parent.Position;
        }

        for (var scene = entity.Scene; scene is not null; scene = scene.Parent)
        {
            position += scene.Offset;
        }

        return position;
    }
}
