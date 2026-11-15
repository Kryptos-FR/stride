using ColorRise.Saga;
using ColorRise.Saga.Content;
using ColorRise.Saga.Expressions;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Input;

namespace SagaSandbox;

/// <summary>
/// A small playable scene over the story: the player walks among the characters (blobs on a plane), talks to them (T),
/// uses things (E) and lets days pass (N); the screen shows the story's state, a log of what happened and the dialogues.
/// </summary>
/// <remarks>
/// Everything here is game code, the part Saga leaves to the game: the dialogue box, the prompt, the name tags, what an event
/// means in the world. The cave-in is raised a few seconds after the start, as gameplay would.
/// </remarks>
public sealed class DemoGame : SyncScript
{
    private const int LogLength = 9;

    private static readonly Color[] SpeakerColors = [new(255, 170, 70), new(255, 110, 110), new(220, 140, 140), new(150, 200, 255)];

    private readonly List<string> journal = [];
    private readonly List<string> items = [];
    private readonly Dictionary<string, string> questOfDialogue = [];
    private SagaRuntime runtime = null!;
    private DemoHud hud = null!;
    private DialogueSession? session;
    private bool caveInRaised;

    public StoryDefinition? Story { get; set; }

    public SpriteFont? Font { get; set; }

    public Entity? Player { get; set; }

    public Entity? Camera { get; set; }

    /// <summary>
    /// How close the player must be to talk to a character or use something, in metres.
    /// </summary>
    public float Reach { get; set; } = 2.5f;

    /// <summary>
    /// Seconds before the gameplay raises the cave-in.
    /// </summary>
    public float CaveInAfter { get; set; } = 3f;

    private IEnumerable<ActDefinition> Acts => runtime.Story.Plot.Acts;

    public override void Start()
    {
        if (Story is null || Font is null || Player is null)
            throw new InvalidOperationException("The demo needs a story, a font and a player.");

        runtime = Services.GetService<SagaSystem>()!.Load(Story);
        runtime.Changed += (_, change) => Write(Describe(change));
        runtime.On("give_item", signal =>
        {
            var item = signal.Arguments[0].AsString();
            items.Add(item);
            Write($"You got the {item}.");
        });
        runtime.On("play_sound", signal => Write($"(sound: {signal.Arguments[0].AsString()})"));
        runtime.RegisterFunction("has_item", arguments => SagaValue.Bool(items.Contains(arguments[0].AsString())));

        foreach (var quest in Acts.SelectMany(x => x.Quests))
            foreach (var dialogue in quest.Dialogues)
                questOfDialogue[dialogue.Name] = quest.Id;

        hud = new DemoHud(Font);
        Entity.Add(new UIComponent { Page = hud.Page, Resolution = new Vector3(1280, 720, 1000) });
        Write($"Act: {ActTitle(runtime.State.CurrentActId)}");
    }

    public override void Update()
    {
        if (Input.IsKeyPressed(Keys.Escape))
        {
            ((Stride.Games.GameBase)Game).Exit();
            return;
        }

        if (!caveInRaised && Game.UpdateTime.Total.TotalSeconds >= CaveInAfter)
        {
            caveInRaised = true;
            Write("The ground shakes.");
            runtime.Raise("cave_in");
        }

        if (session is not null)
            UpdateDialogue();
        else
            UpdateWorld();

        UpdateUsables();
        UpdateTags();
        hud.SetStatus(Status());
        hud.SetLog(journal.TakeLast(LogLength));
    }

    private void UpdateWorld()
    {
        Player!.Get<PlayerController>()!.CanMove = true;

        var character = Nearest(Characters());
        var usable = Nearest(Usables().Where(CanUse));
        var dialogue = character is null ? null : runtime.DialogueFor(character.CharacterId);

        var prompts = new List<string>();
        if (character is not null)
            prompts.Add(dialogue is null ? $"{DisplayName(character.CharacterId)} has nothing to say." : $"T: talk to {DisplayName(character.CharacterId)}");
        if (usable is not null)
            prompts.Add($"E: {usable.Label.ToLowerInvariant()}");
        hud.SetPrompt(prompts.Count == 0 ? null : string.Join("     ", prompts));

        if (Input.IsKeyPressed(Keys.T) && character is not null && dialogue is not null)
        {
            session = runtime.Talk(character.CharacterId);
            Player.Get<PlayerController>()!.CanMove = false;
            ShowSession();
        }
        else if (Input.IsKeyPressed(Keys.E) && usable is not null)
        {
            foreach (var other in Usables().Where(x => x.RaiseEvent == usable.RaiseEvent))
                other.IsUsed = true;
            Write($"You {usable.Label.ToLowerInvariant()}.");
            runtime.Raise(usable.RaiseEvent);
        }
        else if (Input.IsKeyPressed(Keys.N))
        {
            var day = runtime.Get("day").AsInt() + 1;
            Write($"Night falls. Day {day}.");
            runtime.Set("day", SagaValue.Int(day));
        }
    }

    private void UpdateDialogue()
    {
        var current = session!;
        if (current.Choices.Count == 0)
        {
            if (Input.IsKeyPressed(Keys.Space) || Input.IsKeyPressed(Keys.Enter) || Input.IsKeyPressed(Keys.T))
                current.Continue();
        }
        else
        {
            // The keys count the enabled choices only, as the box numbers them
            var enabled = current.Choices.Where(x => x.IsEnabled).ToList();
            for (var i = 0; i < Math.Min(enabled.Count, 9); i++)
            {
                if (Input.IsKeyPressed(Keys.D1 + i) || Input.IsKeyPressed(Keys.NumPad1 + i))
                {
                    current.Choose(enabled[i].Index);
                    break;
                }
            }
        }

        if (current.IsEnded)
        {
            session = null;
            hud.HideDialogue();
        }
        else
        {
            ShowSession();
        }
    }

    private void ShowSession()
    {
        if (session is null)
            return;
        if (session.IsEnded)
        {
            session = null;
            return;
        }

        var lineOfSession = session.CurrentLine;
        var speakerId = lineOfSession?.SpeakerId ?? string.Empty;
        var options = session.Choices.Select(x => (x.Text, x.IsEnabled)).ToList();
        hud.ShowDialogue(DisplayName(speakerId), SpeakerColor(speakerId), lineOfSession?.Text ?? string.Empty, options);
    }

    private void UpdateUsables()
    {
        foreach (var usable in Usables())
        {
            var progress = runtime.State.Quests.GetValueOrDefault(usable.QuestId);
            var reached = progress is not null
                && (progress.State == QuestState.Succeeded || (progress.State == QuestState.Active && progress.Stage >= usable.AtStage));
            var visible = !(usable.HideAfter && usable.IsUsed) && !(usable.HideBefore && !reached);
            if (usable.Entity.Get<ModelComponent>() is { } model)
                model.Enabled = visible;
        }
    }

    private void UpdateTags()
    {
        var camera = Camera?.Get<CameraComponent>();
        foreach (var character in Characters())
        {
            var dialogue = runtime.DialogueFor(character.CharacterId);
            var marker = dialogue is null ? string.Empty
                : questOfDialogue.TryGetValue(dialogue.Name, out var questId) && runtime.State.Quests.GetValueOrDefault(questId)?.State == QuestState.Available ? "  (!)"
                : "  (...)";
            hud.SetTag(character.Entity, DisplayName(character.CharacterId) + marker, SpeakerColor(character.CharacterId),
                camera is null ? null : ToScreen(camera, character.Entity.Transform.Position + new Vector3(0, 2.3f, 0)));
        }
    }

    private string Status()
    {
        var text = $"{ActTitle(runtime.State.CurrentActId)}     Day {runtime.Get("day").AsInt()}";
        foreach (var act in Acts)
        {
            foreach (var scenario in act.Scenarios.Where(x => runtime.State.Scenarios.GetValueOrDefault(x.Id)?.State == ScenarioState.Running))
                text += $"\nUnder way: {scenario.Title}";
            foreach (var quest in act.Quests)
            {
                var progress = runtime.State.Quests.GetValueOrDefault(quest.Id);
                text += progress?.State switch
                {
                    QuestState.Available => $"\nQuest offered: {quest.Title}",
                    QuestState.Active => $"\nQuest: {quest.Title}, {(progress.Stage < quest.Stages.Count ? quest.Stages[progress.Stage].Title.ToLowerInvariant() : "finish it")}",
                    _ => string.Empty,
                };
            }
        }
        return text + (items.Count > 0 ? $"\nItems: {string.Join(", ", items)}" : string.Empty);
    }

    private string Describe(SagaChangedEventArgs change)
    {
        var quest = Acts.SelectMany(x => x.Quests).FirstOrDefault(x => x.Id == change.Id);
        var scenario = Acts.SelectMany(x => x.Scenarios).FirstOrDefault(x => x.Id == change.Id);
        return change.Kind switch
        {
            SagaChangeKind.ActEntered => $"Act: {ActTitle(change.Id)}",
            SagaChangeKind.ActEnded => $"{ActTitle(change.Id)} is over.",
            SagaChangeKind.ScenarioStarted => $"{scenario?.Title}: it begins.",
            SagaChangeKind.ScenarioEnded => $"{scenario?.Title}: over ({runtime.State.Scenarios.GetValueOrDefault(change.Id)?.Outcome ?? "no outcome"}).",
            SagaChangeKind.QuestAvailable => $"New quest offered: {quest?.Title}",
            SagaChangeKind.QuestActivated => $"Quest taken: {quest?.Title}",
            SagaChangeKind.QuestStageDone => $"{quest?.Title}: {DoneStage(quest)} done.",
            SagaChangeKind.QuestSucceeded => $"Quest done: {quest?.Title}",
            SagaChangeKind.QuestFailed => $"Quest failed: {quest?.Title}",
            _ => $"{change.Kind} {change.Id}",
        };
    }

    private string DoneStage(QuestDefinition? quest)
    {
        var done = quest is null ? 0 : runtime.State.Quests.GetValueOrDefault(quest.Id)?.Stage ?? 0;
        return quest is not null && done > 0 && done <= quest.Stages.Count ? quest.Stages[done - 1].Title.ToLowerInvariant() : "a stage";
    }

    private void Write(string text)
    {
        var time = Game.UpdateTime.Total;
        journal.Add($"{(int)time.TotalMinutes}:{time.Seconds:00}  {text}");
        Log.Info(text);
    }

    private T? Nearest<T>(IEnumerable<T> components) where T : EntityComponent
    {
        var position = Player!.Transform.Position;
        return components
            .Select(x => (Component: x, Distance: Vector3.Distance(x.Entity.Transform.Position, position)))
            .Where(x => x.Distance <= Reach)
            .OrderBy(x => x.Distance)
            .Select(x => x.Component)
            .FirstOrDefault();
    }

    private bool CanUse(StoryUsable usable)
    {
        var progress = runtime.State.Quests.GetValueOrDefault(usable.QuestId);
        return !usable.IsUsed && progress?.State == QuestState.Active && progress.Stage == usable.AtStage;
    }

    private IEnumerable<SagaCharacterComponent> Characters() => Components<SagaCharacterComponent>();

    private IEnumerable<StoryUsable> Usables() => Components<StoryUsable>();

    private IEnumerable<T> Components<T>() where T : EntityComponent
        => SceneSystem.SceneInstance.RootScene.Entities.Select(x => x.Get<T>()).OfType<T>();

    private string ActTitle(string? actId) => Acts.FirstOrDefault(x => x.Id == actId)?.Title ?? "The end";

    private string DisplayName(string characterId)
        => runtime.Story.Declarations.Characters.FirstOrDefault(x => x.Id == characterId)?.DisplayName is { Length: > 0 } name ? name
            : characterId == "player" ? "You" : characterId;

    private Color SpeakerColor(string characterId)
    {
        var index = runtime.Story.Declarations.Characters.FindIndex(x => x.Id == characterId);
        return index < 0 ? Color.White : SpeakerColors[index % SpeakerColors.Length];
    }

    /// <summary>
    /// Where a point of the world is on the screen, (0, 0) at the top left and (1, 1) at the bottom right, or null behind the camera.
    /// </summary>
    private static Vector2? ToScreen(CameraComponent camera, Vector3 position)
    {
        var clip = Vector4.Transform(new Vector4(position, 1f), camera.ViewProjectionMatrix);
        if (clip.W <= 0f)
            return null;
        return new Vector2((clip.X / clip.W + 1f) / 2f, (1f - clip.Y / clip.W) / 2f);
    }
}
