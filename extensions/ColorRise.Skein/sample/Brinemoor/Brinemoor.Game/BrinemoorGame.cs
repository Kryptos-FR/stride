using ColorRise.Skein;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Input;

namespace Brinemoor;

/// <summary>
/// The game of Brinemoor over its story: the courier walks on the island, talks to its people (T) and uses things (E),
/// lets time pass (N), switches language (L), saves (F5) and loads (F9).
/// </summary>
/// <remarks>
/// The story is in the Yarn scripts. The game owns the world: where people stand, what the courier carries, what time
/// it is. Its <see cref="StoryDirector"/> plays the story's dialogues and events on the dialogue runner of the scene,
/// and every word of this screen is a line of the story's node <c>Interface</c>, read by its line id in the language
/// of the moment.
/// </remarks>
[Display("Brinemoor game")]
[ComponentCategory("Brinemoor")]
public sealed class BrinemoorGame : SyncScript
{
    private const string PlayerPlace = "Courier";
    private const double ToastSeconds = 4;
    private const double TagSeconds = 0.5;

    private readonly List<Talkable> talkables = [];
    private readonly Dictionary<Talkable, bool> hasSomethingToSay = [];
    private StoryDirector story = null!;
    private DialogueSystem dialogues = null!;
    private Hud hud = null!;
    private string? toast;
    private double toastLeft;
    private double tagsIn;

    /// <summary>
    /// The dialogue runner that plays the story.
    /// </summary>
    /// <userdoc>The dialogue runner that plays the story, with its Yarn project and its presenters.</userdoc>
    [DataMember(10)]
    public DialogueRunnerComponent? Runner { get; set; }

    /// <summary>
    /// The courier.
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
    /// How close the courier must be to talk to someone or to use something, in metres.
    /// </summary>
    /// <userdoc>How close the courier must be to talk to someone or use something, in metres.</userdoc>
    [DataMember(50)]
    public float Reach { get; set; } = 2.5f;

    /// <summary>
    /// The file of the save.
    /// </summary>
    [DataMemberIgnore]
    public string SavePath { get; set; } = SaveSlot.DefaultPath;

    /// <inheritdoc />
    public override void Start()
    {
        if (Runner?.Project is null || Player is null || Font is null)
            throw new InvalidOperationException("The game of Brinemoor needs a dialogue runner with its Yarn project, a player and a font.");

        story = new StoryDirector(new RunnerStage(Runner), Runner.Variables, Log);
        StoryCommands.Story = story;
        dialogues = Services.GetService<DialogueSystem>()!;
        story.Journal.Added += entry => Toast($"{Text("ui_journal")}: {Text("quest_" + entry.Quest)} ({Text("state_" + entry.State.ToString().ToLowerInvariant())})");

        Game.Window.Title = "Brinemoor";
        hud = new Hud(Font);
        Entity.Add(new UIComponent { Page = hud.Page, Resolution = new Vector3(1280, 720, 1000) });
        Collect(Entity.Scene.Entities);

        // The story opens with its first event
        Lead(story.RunEvents);
    }

    /// <inheritdoc />
    public override void Cancel()
    {
        if (StoryCommands.Story == story)
            StoryCommands.Story = null;
    }

    /// <inheritdoc />
    public override void Update()
    {
        if (Input.IsKeyPressed(Keys.Escape))
        {
            ((GameBase)Game).Exit();
            return;
        }

        var busy = story.IsBusy;
        var free = !busy && story.Ending is null;
        if (Player!.Get<PlayerController>() is { } controller)
            controller.CanMove = free;

        var person = free ? Nearest(isThing: false) : null;
        var thing = free ? Nearest(isThing: true) : null;
        if (free)
        {
            if (Input.IsKeyPressed(Keys.T) && person is not null)
                Lead(() => story.Talk(person.Node));
            else if (Input.IsKeyPressed(Keys.E) && thing is not null)
                Lead(() => story.Talk(thing.Node));
            else if (Input.IsKeyPressed(Keys.N))
                PassTime();
            else if (Input.IsKeyPressed(Keys.F5))
                Save();
        }

        if (!busy && Input.IsKeyPressed(Keys.F9))
            Load();
        if (Input.IsKeyPressed(Keys.L))
            SwitchLanguage();

        Show(free, person, thing);
    }

    private void PassTime()
    {
        story.Clock.Advance();
        Toast($"{Text("ui_day")} {story.Clock.Day}, {Text("time_" + story.Clock.TimeName)}");
        Lead(story.RunEvents);
    }

    private void Save()
    {
        var data = SaveSlot.Capture(story);
        data.Places[PlayerPlace] = PlaceOf(Player!);
        foreach (var talkable in talkables)
        {
            data.Places[talkable.Entity.Name] = PlaceOf(talkable.Entity);
        }

        SaveSlot.Write(data, SavePath);
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

        SaveSlot.Restore(story, data);
        Put(Player!, data.Places.GetValueOrDefault(PlayerPlace));
        foreach (var talkable in talkables)
        {
            Put(talkable.Entity, data.Places.GetValueOrDefault(talkable.Entity.Name));
        }

        Toast(Text("ui_loaded"));
        Lead(story.RunEvents);
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

    private void Show(bool free, Talkable? person, Talkable? thing)
    {
        var elapsed = Game.UpdateTime.Elapsed.TotalSeconds;

        var status = $"{Text("ui_day")} {story.Clock.Day}, {Text("time_" + story.Clock.TimeName)}";
        if (story.Variables.TryGetValue<float>("$act", out var act))
            status = $"{Text("act_" + (int)act)}\n{status}";
        if (story.Inventory.Items.Count > 0)
            status += $"\n{Text("ui_carrying")}: {string.Join(", ", story.Inventory.Items.Select(i => i.Value > 1 ? $"{Text("item_" + i.Key)} x{i.Value}" : Text("item_" + i.Key)))}";
        hud.SetStatus(status);

        hud.SetJournal(Text("ui_journal"), [.. story.Journal.Open.Select(e => e.State == QuestState.Offered ? $"{Text("quest_" + e.Quest)} ({Text("state_offered")})" : Text("quest_" + e.Quest))]);
        hud.SetHelp(Text("ui_help"));

        var prompts = new List<string>();
        if (person is not null)
            prompts.Add($"T: {Text("ui_talk")} {person.Entity.Name}");
        if (thing is not null)
            prompts.Add($"E: {LineText(thing.Label)}");
        hud.SetPrompt(prompts.Count == 0 ? null : string.Join("      ", prompts), showHelp: free);

        if (toastLeft > 0 && (toastLeft -= elapsed) <= 0)
            toast = null;
        hud.SetToast(toast);

        if (story.Ending is { } ending)
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
                hasSomethingToSay[talkable] = talkable.IsShown && story.CanTalk(talkable.Node);
            }
        }

        var camera = Camera?.Get<CameraComponent>();
        foreach (var talkable in talkables)
        {
            if (talkable.IsThing)
                continue;

            var point = camera is null || !talkable.IsShown ? null : ToScreen(camera, talkable.Entity.Transform.WorldMatrix.TranslationVector + new Vector3(0, 2.3f, 0));
            hud.SetTag(talkable.Entity, talkable.Entity.Name, hasSomethingToSay.GetValueOrDefault(talkable), point);
        }
    }

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
        // On the ground: the cat on the roof is as near as the wall under it
        var position = Player!.Transform.WorldMatrix.TranslationVector.XZ();
        return talkables
            .Where(t => t.IsThing == isThing && t.IsShown)
            .Select(t => (Talkable: t, Distance: Vector2.Distance(t.Entity.Transform.WorldMatrix.TranslationVector.XZ(), position)))
            .Where(t => t.Distance <= Reach)
            .OrderBy(t => t.Distance)
            .Select(t => t.Talkable)
            .FirstOrDefault(t => story.CanTalk(t.Node));
    }

    private void Collect(IEnumerable<Entity> entities)
    {
        foreach (var entity in entities)
        {
            if (entity.Get<Talkable>() is { } talkable)
                talkables.Add(talkable);
            Collect(entity.Transform.Children.Select(t => t.Entity));
        }
    }

    /// <summary>
    /// A word of the screen: the line <c>line:id</c> of the story's node <c>Interface</c>, in the language of the moment.
    /// </summary>
    private string Text(string id) => LineText("line:" + id);

    private string LineText(string lineId) => lineId.Length == 0 ? "" : dialogues.GetLineProvider(Runner!.Project!).GetLine(lineId, []).Text.Text;

    // The story of a save is played by a script task of the game, so that a failure is reported as any script's
    private void Lead(Func<Task> play) => Script.AddTask(play);

    private static float[] PlaceOf(Entity entity) => [entity.Transform.Position.X, entity.Transform.Position.Z];

    private static void Put(Entity entity, float[]? place)
    {
        if (place is [var x, var z])
            entity.Transform.Position = new Vector3(x, entity.Transform.Position.Y, z);
    }

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
    /// The dialogue runner of the scene as the stage of the director.
    /// </summary>
    private sealed class RunnerStage(DialogueRunnerComponent runner) : IStoryStage
    {
        public bool IsPlaying => runner.IsRunning;

        public bool HasContent(string node) => runner.NodeExists(node) && runner.HasSalientContent(node);

        public Task Play(string node) => runner.StartDialogue(node);
    }
}
