using ColorRise.Skein;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Input;

namespace Ashcombe;

/// <summary>
/// The stage of a root scene of Ashcombe: its dialogue runner plays the story while the scene is up. The cartographer
/// walks, talks to people (T), uses things and doors (E), lets time pass (N), switches language (L), saves (F5) and
/// loads (F9).
/// </summary>
/// <remarks>
/// Each root scene (the village, the castle, the mine) has its own stage, with the same parts: the runner and its
/// presenters, the cartographer, the camera. The story itself is not in a scene: the <see cref="StorySystem"/> keeps
/// it, and the stage of the scene that is up attaches to it. Every word of the screen is a line of the story's node
/// <c>Interface</c>, read by its line id in the language of the moment.
/// </remarks>
[Display("Stage")]
[ComponentCategory("Ashcombe")]
public sealed class Stage : SyncScript
{
    private const double ToastSeconds = 4;
    private const double TagSeconds = 0.5;
    private const float FadeSpeed = 3f;

    private readonly List<Talkable> talkables = [];
    private readonly List<Doorway> doorways = [];
    private readonly List<StoryPlace> storyPlaces = [];
    private readonly Dictionary<Talkable, bool> hasSomethingToSay = [];
    private StorySystem system = null!;
    private DialogueSystem dialogues = null!;
    private Hud? hud;
    private string? toast;
    private double toastLeft;
    private double tagsIn;
    private string? shownPlace;

    /// <summary>
    /// The dialogue runner that plays the story in this scene.
    /// </summary>
    /// <userdoc>The dialogue runner that plays the story while this scene is up, with the story's Yarn project and its presenters.</userdoc>
    [DataMember(10)]
    public DialogueRunnerComponent? Runner { get; set; }

    /// <summary>
    /// The cartographer.
    /// </summary>
    /// <userdoc>The entity the player moves, with its player controller.</userdoc>
    [DataMember(20)]
    public Entity? Player { get; set; }

    /// <summary>
    /// The camera, to put the names over the people.
    /// </summary>
    /// <userdoc>The camera of the scene, used to put the names over the people.</userdoc>
    [DataMember(30)]
    public Entity? Camera { get; set; }

    /// <summary>
    /// The font of the screen.
    /// </summary>
    /// <userdoc>The font of the game's screen.</userdoc>
    [DataMember(40)]
    public SpriteFont? Font { get; set; }

    /// <summary>
    /// How close the cartographer must be to talk to someone or to use something, in metres.
    /// </summary>
    /// <userdoc>How close the cartographer must be to talk to someone or use something, in metres.</userdoc>
    [DataMember(50)]
    public float Reach { get; set; } = 2.5f;

    /// <summary>
    /// The other Yarn projects of the game, which the variables must know from the start, whichever scene uses them.
    /// </summary>
    /// <userdoc>The other Yarn projects of the game (the echo of the mine). The variables know them from the start, so that a save made anywhere keeps their variables.</userdoc>
    [DataMember(60)]
    public List<YarnProject> Projects { get; } = [];

    /// <summary>
    /// The file of the save.
    /// </summary>
    [DataMemberIgnore]
    public string SavePath { get; set; } = SaveSlot.DefaultPath;

    /// <summary>
    /// The runner of this scene, as the stage of the story.
    /// </summary>
    internal IStoryStage StoryStage { get; private set; } = null!;

    private StoryDirector Story => system.Story;

    /// <inheritdoc />
    public override void Start()
    {
        if (Runner?.Project is null || Player is null)
            throw new InvalidOperationException("The stage of a scene of Ashcombe needs a dialogue runner with its Yarn project, and a player.");

        system = Services.GetSafeServiceAs<StorySystem>();
        dialogues = Services.GetSafeServiceAs<DialogueSystem>();
        StoryStage = new RunnerStage(Runner);
        system.Attach(this);
        system.PlacesChanged += Collect;
        Story.Journal.Added += OnJournal;

        if (Game.Window is { } window)
            window.Title = "Ashcombe";
        if (Font is not null)
        {
            hud = new Hud(Font);
            Entity.Add(new UIComponent { Page = hud.Page, Resolution = new Vector3(1280, 720, 1000) });
        }

        Collect();
        FollowArea();

        // What happens here: the first event of the story, or what greets the cartographer at the end of a road
        Lead(Story.RunEvents);
    }

    /// <inheritdoc />
    public override void Cancel()
    {
        if (system is null)
            return;

        system.PlacesChanged -= Collect;
        Story.Journal.Added -= OnJournal;
        system.Detach(this);
    }

    /// <inheritdoc />
    public override void Update()
    {
        if (Input.IsKeyPressed(Keys.Escape) && Game is GameBase game)
        {
            game.Exit();
            return;
        }

        system.FollowStoryPlaces(storyPlaces);

        var busy = system.IsBusy;
        var free = !busy && Story.Ending is null;
        if (Player!.Get<PlayerController>() is { } controller)
            controller.CanMove = free;

        var person = free ? Nearest(isThing: false) : null;
        var thing = free ? Nearest(isThing: true) : null;
        var door = free && thing is null ? NearestDoor() : null;
        if (free)
        {
            if (Input.IsKeyPressed(Keys.T) && person is not null)
                Lead(() => Talk(person));
            else if (Input.IsKeyPressed(Keys.E) && thing is not null)
                Lead(() => Talk(thing));
            else if (Input.IsKeyPressed(Keys.E) && door is not null)
                Lead(() => Use(door));
            else if (Input.IsKeyPressed(Keys.N))
                PassTime();
            else if (Input.IsKeyPressed(Keys.F5))
                Save();
        }

        if (!busy && Input.IsKeyPressed(Keys.F9))
            Load();
        if (Input.IsKeyPressed(Keys.L))
            SwitchLanguage();

        Show(free, person, thing, door);
    }

    /// <summary>
    /// Bounds the cartographer's walk to the area of the place where the cartographer is, and names the place.
    /// </summary>
    internal void FollowArea()
    {
        var here = system.SceneOf(Story.Whereabouts.Current) ?? Entity.Scene;
        var area = here?.Entities.Select(e => e.Get<PlaceArea>()).OfType<PlaceArea>().FirstOrDefault();
        if (area is not null && Player?.Get<PlayerController>() is { } controller)
        {
            var centre = StorySystem.WorldPosition(area.Entity);
            controller.SetArea(new Vector2(centre.X, centre.Z), area.HalfSize);
        }

        var place = Story.Whereabouts.Current;
        if (shownPlace != place)
        {
            shownPlace = place;
            if (area is { Title.Length: > 0 })
                Toast(LineText(area.Title));
        }
    }

    private async Task Talk(Talkable talkable)
    {
        if (talkable.Runner is { } own)
            await Story.Talk(talkable.Node, new RunnerStage(own));
        else
            await Story.Talk(talkable.Node);
    }

    private async Task Use(Doorway door)
    {
        if (door.Node.Length > 0 && Story.CanTalk(door.Node))
        {
            await Story.Talk(door.Node);
            return;
        }

        await door.Go();

        // At the end of a road, the stage of the next scene plays what greets the cartographer
        if (!door.IsJourney)
            await Story.RunEvents();
    }

    private void PassTime()
    {
        Story.Clock.Advance();
        Toast($"{Text("ui_day")} {Story.Clock.Day}, {Text("time_" + Story.Clock.TimeName)}");
        Lead(Story.RunEvents);
    }

    private void Save()
    {
        SaveSlot.Write(system.Capture(), SavePath);
        Toast(Text("ui_saved"));
    }

    private void Load()
    {
        SaveData? data;
        try
        {
            data = SaveSlot.Read(SavePath);
        }
        catch (FormatException exception)
        {
            Log.Error($"The save '{SavePath}' cannot be loaded: {exception.Message}");
            data = null;
        }

        if (data is null)
        {
            Toast(Text("ui_no_save"));
            return;
        }

        var sameRoot = data.Root.Length == 0 || Places.Normalize(data.Root) == Story.Whereabouts.Root;
        system.Restore(data);
        if (!sameRoot)
            return; // The stage of the saved scene takes over

        Toast(Text("ui_loaded"));
        Lead(Story.RunEvents);
    }

    /// <summary>
    /// Goes to the next language of the project; the lines and the screen follow.
    /// </summary>
    private void SwitchLanguage()
    {
        var languages = Runner!.Project!.Languages;
        if (languages.Count < 2)
            return;

        var current = dialogues.Language.Length > 0 ? dialogues.Language : Runner.Project.BaseLanguage;
        dialogues.Language = languages[(languages.IndexOf(current) + 1) % languages.Count];
    }

    private void OnJournal(JournalEntry entry)
        => Toast($"{Text("ui_journal")} · {Text("quest_" + entry.Quest)} ({Text("state_" + entry.State.ToString().ToLowerInvariant())})");

    private void Show(bool free, Talkable? person, Talkable? thing, Doorway? door)
    {
        var elapsed = Game.UpdateTime.Elapsed.TotalSeconds;
        system.Fade = Math.Max(0f, system.Fade - FadeSpeed * (float)elapsed);
        if (hud is null)
            return;

        var status = $"{Text("ui_day")} {Story.Clock.Day}, {Text("time_" + Story.Clock.TimeName)}";
        if (Story.Variables.TryGetValue<float>("$act", out var act))
            status = $"{Text("act_" + (int)act)}\n{status}";
        status += $"\n{Text("ui_map")} {Story.Map.Landmarks.Count}";
        if (Story.Inventory.Items.Count > 0)
            status += $"\n{Text("ui_carrying")} {string.Join(", ", Story.Inventory.Items.Select(i => i.Value > 1 ? $"{Text("item_" + i.Key)} x{i.Value}" : Text("item_" + i.Key)))}";
        hud.SetStatus(status);

        hud.SetJournal(Text("ui_journal"), [.. Story.Journal.Open.Select(e => e.State == QuestState.Offered ? $"{Text("quest_" + e.Quest)} ({Text("state_offered")})" : Text("quest_" + e.Quest))]);
        hud.SetHelp(Text("ui_help"));

        var prompts = new List<string>();
        if (person is not null)
            prompts.Add($"T · {Text("ui_talk")} {person.Entity.Name}");
        if (thing is not null)
            prompts.Add($"E · {LineText(thing.Label)}");
        else if (door is not null)
            prompts.Add($"E · {LineText(door.Label)}");
        hud.SetPrompt(prompts.Count == 0 ? null : string.Join("      ", prompts), showHelp: free);

        if (toastLeft > 0 && (toastLeft -= elapsed) <= 0)
            toast = null;
        hud.SetToast(toast);
        hud.SetFade(system.Fade);

        if (Story.Ending is { } ending)
            hud.SetEnding(Text("ui_the_end"), Text("ending_" + ending), Text("ui_end_help"));
        else
            hud.SetEnding(null, "", "");

        ShowNames(free, elapsed);
    }

    /// <summary>
    /// Puts the names over the people the story shows: bright when they have something to say.
    /// </summary>
    private void ShowNames(bool free, double elapsed)
    {
        // What the people have to say is asked to the story a few times a second, and not while it plays
        if (free && (tagsIn -= elapsed) <= 0)
        {
            tagsIn = TagSeconds;
            foreach (var talkable in talkables)
            {
                hasSomethingToSay[talkable] = talkable.IsShown && CanTalk(talkable);
            }
        }

        var camera = Camera?.Get<CameraComponent>();
        foreach (var talkable in talkables)
        {
            if (talkable.IsThing)
                continue;

            // No names while the story plays: they would show through its options
            var point = camera is null || !free || !talkable.IsShown ? null : ToScreen(camera, StorySystem.WorldPosition(talkable.Entity) + new Vector3(0, 2.3f, 0));
            hud!.SetTag(talkable.Entity, talkable.Entity.Name, hasSomethingToSay.GetValueOrDefault(talkable), point);
        }
    }

    private bool CanTalk(Talkable talkable)
        => talkable.Runner is { } own ? Story.CanTalk(talkable.Node, new RunnerStage(own)) : Story.CanTalk(talkable.Node);

    private void Toast(string text)
    {
        toast = text;
        toastLeft = ToastSeconds;
    }

    /// <summary>
    /// The nearest person, or thing, within reach that the story shows and that has something to say now.
    /// </summary>
    private Talkable? Nearest(bool isThing)
    {
        var position = Player!.Transform.Position.XZ();
        return talkables
            .Where(t => t.IsThing == isThing && t.IsShown)
            .Select(t => (Talkable: t, Distance: Vector2.Distance(StorySystem.WorldPosition(t.Entity).XZ(), position)))
            .Where(t => t.Distance <= Reach)
            .OrderBy(t => t.Distance)
            .Select(t => t.Talkable)
            .FirstOrDefault(CanTalk);
    }

    /// <summary>
    /// The nearest doorway within reach that the story shows.
    /// </summary>
    private Doorway? NearestDoor()
    {
        var position = Player!.Transform.Position.XZ();
        return doorways
            .Where(d => d.IsShown)
            .Select(d => (Door: d, Distance: Vector2.Distance(StorySystem.WorldPosition(d.Entity).XZ(), position)))
            .Where(d => d.Distance <= Reach)
            .OrderBy(d => d.Distance)
            .Select(d => d.Door)
            .FirstOrDefault();
    }

    /// <summary>
    /// Finds the people, the things and the doors of the scenes loaded now.
    /// </summary>
    private void Collect()
    {
        talkables.Clear();
        doorways.Clear();
        storyPlaces.Clear();
        hasSomethingToSay.Clear();
        hud?.ClearTags();

        var scenes = new List<Scene>();
        if (Entity.Scene is { } root)
            scenes.Add(root);
        scenes.AddRange(system.Interiors);
        scenes.AddRange(system.StoryPlaces);
        foreach (var scene in scenes)
        {
            Collect(scene.Entities);
        }
    }

    private void Collect(IEnumerable<Entity> entities)
    {
        foreach (var entity in entities)
        {
            if (entity.Get<Talkable>() is { } talkable)
                talkables.Add(talkable);
            if (entity.Get<Doorway>() is { } doorway)
                doorways.Add(doorway);
            if (entity.Get<StoryPlace>() is { } place)
                storyPlaces.Add(place);
            Collect(entity.Transform.Children.Select(t => t.Entity));
        }
    }

    /// <summary>
    /// A word of the screen: the line <c>line:id</c> of the story's node <c>Interface</c>, in the language of the moment.
    /// </summary>
    private string Text(string id) => LineText("line:" + id);

    private string LineText(string lineId) => lineId.Length == 0 ? "" : dialogues.GetLineProvider(Runner!.Project!).GetLine(lineId, []).Text.Text;

    // The story is played by a script task of the game, so that a failure is reported as any script's
    private void Lead(Func<Task> play) => Script.AddTask(play);

    /// <summary>
    /// Where a point of the world is on the screen, from (0, 0) at the top left to (1, 1) at the bottom right; null
    /// behind the camera.
    /// </summary>
    private static Vector2? ToScreen(CameraComponent camera, Vector3 position)
    {
        var clip = Vector4.Transform(new Vector4(position, 1f), camera.ViewProjectionMatrix);
        return clip.W <= 0f ? null : new Vector2((clip.X / clip.W + 1f) / 2f, (1f - clip.Y / clip.W) / 2f);
    }

    /// <summary>
    /// A dialogue runner as the stage of the director.
    /// </summary>
    internal sealed class RunnerStage(DialogueRunnerComponent runner) : IStoryStage
    {
        public bool IsPlaying => runner.IsRunning;

        public bool HasContent(string node) => runner.NodeExists(node) && runner.HasSalientContent(node);

        public Task Play(string node) => runner.StartDialogue(node);
    }
}
