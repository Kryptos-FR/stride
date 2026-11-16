using ColorRise.Skein;
using ColorRise.Skein.Assets;
using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Assets.Localisation;
using Stride.Core.Diagnostics;

namespace Ashcombe.Tests;

/// <summary>
/// The tests of the story run one after the other: the commands of the game act on one story at a time
/// (<see cref="StoryCommands.Story"/>).
/// </summary>
[CollectionDefinition(Name)]
public sealed class StoryCollection
{
    public const string Name = "Story";
}

/// <summary>
/// A story without its game: the scripts built as the game's build builds them, against the game's commands and
/// functions, a session that plays them, and the game's director with its inventory, clock, journal, map and
/// whereabouts. The doors of the world are a table (<see cref="Doors"/>): <c>&lt;&lt;go Door&gt;&gt;</c> moves the
/// cartographer as the game's doors do, a journey to another root scene once the dialogue and its events are over.
/// </summary>
internal sealed class TestStory
{
    /// <summary>
    /// The doors of the game's scenes, by the scene they are in and their entity name: where each one leads, and whether
    /// it is a journey (a root scene change, held until the dialogue ends).
    /// </summary>
    public static readonly IReadOnlyDictionary<(string Place, string Door), (string Target, bool Journey)> Doors = new Dictionary<(string, string), (string, bool)>
    {
        [("Places/Village", "InnDoor")] = ("Places/Village/Inn", false),
        [("Places/Village", "MillDoor")] = ("Places/Village/Mill", false),
        [("Places/Village", "CottageDoor")] = ("Places/Village/Cottage", false),
        [("Places/Village", "ChapelDoor")] = ("Places/Village/Chapel", false),
        [("Places/Village", "CastleRoad")] = ("Places/Castle", true),
        [("Places/Village", "MineRoad")] = ("Places/Mine", true),
        [("Places/Village/Inn", "InnExit")] = ("Places/Village", false),
        [("Places/Village/Inn", "CellarStairs")] = ("Places/Village/Cellar", false),
        [("Places/Village/Cellar", "CellarUp")] = ("Places/Village/Inn", false),
        [("Places/Village/Mill", "MillExit")] = ("Places/Village", false),
        [("Places/Village/Cottage", "CottageExit")] = ("Places/Village", false),
        [("Places/Village/Chapel", "ChapelExit")] = ("Places/Village", false),
        [("Places/Castle", "VillageRoad")] = ("Places/Village", true),
        [("Places/Castle", "MineRoad")] = ("Places/Mine", true),
        [("Places/Mine", "Lift")] = ("Places/Mine/Gallery", false),
        [("Places/Mine", "VillageRoad")] = ("Places/Village", true),
        [("Places/Mine/Gallery", "GalleryLift")] = ("Places/Mine", false),
        [("Places/Mine/Gallery", "OldShaft")] = ("Places/Mine/Deep", false),
        [("Places/Mine/Gallery", "FloodedPassage")] = ("Places/Mine/Flooded", false),
        [("Places/Mine/Gallery", "LongWay")] = ("Places/Mine/Deep", false),
        [("Places/Mine/Flooded", "FloodedBack")] = ("Places/Mine/Gallery", false),
        [("Places/Mine/Flooded", "FloodedOn")] = ("Places/Mine/Deep", false),
        [("Places/Mine/Deep", "DeepLift")] = ("Places/Mine", false),
    };

    /// <summary>
    /// The nodes the game's doors play instead of going through, when they have something to say (the doors' Node).
    /// </summary>
    public static readonly IReadOnlyDictionary<(string Place, string Door), string> DoorNodes = new Dictionary<(string, string), string>
    {
        [("Places/Village", "CastleRoad")] = "CastleRoad",
        [("Places/Village", "MineRoad")] = "MineRoad",
        [("Places/Village/Inn", "CellarStairs")] = "CellarStairs",
        [("Places/Castle", "VillageRoad")] = "VillageRoad",
        [("Places/Castle", "MineRoad")] = "MineRoad",
        [("Places/Mine", "Lift")] = "LiftDown",
        [("Places/Mine", "VillageRoad")] = "MineToVillage",
        [("Places/Mine/Gallery", "OldShaft")] = "OldShaft",
        [("Places/Mine/Gallery", "FloodedPassage")] = "FloodedPassage",
        [("Places/Mine/Gallery", "LongWay")] = "LongWay",
        [("Places/Mine/Flooded", "FloodedOn")] = "FloodedOn",
        [("Places/Mine/Deep", "DeepLift")] = "DeepLift",
    };

    private string? journey;

    /// <param name="scripts">The scripts of the story.</param>
    /// <param name="seed">The seed of what the story leaves to chance.</param>
    /// <param name="language">The language to play in, when not the base language; <paramref name="translation"/> has its lines.</param>
    /// <param name="translation">The rows of the strings file of <paramref name="language"/>.</param>
    public TestStory(IReadOnlyList<(string FileName, string Text)> scripts, int? seed = 1, string? language = null, IReadOnlyList<YarnStringRow>? translation = null)
    {
        Actions.RegisterType(typeof(StoryCommands));
        var functions = Actions.Describe().Where(a => a.Kind == YarnActionKind.Function).Select(YarnFunctionDeclaration.From).ToList();

        var asset = new YarnProjectAsset();
        Dictionary<string, IReadOnlyList<YarnStringRow>>? translations = null;
        if (language is not null && translation is not null)
        {
            asset.Localisations.Add(new YarnLocalisation { Language = language });
            translations = new() { [language] = translation };
        }

        Build = YarnProjectBuilder.Build(asset, "Test", scripts, functions, translations);
        Assert.Empty(Build.Compilation.Diagnostics.Where(d => d.Severity == YarnSeverity.Error).Select(d => d.ToString()));
        var project = Build.Project!;
        var tables = Build.Translations.Append(Build.BaseStrings!).ToDictionary(t => t.Language);

        Lines = new TextLineProvider(tables.GetValueOrDefault, project.BaseLanguage, Log)
        {
            LineMetadata = project.LineMetadata,
            ShadowLines = project.ShadowLines,
        };
        if (language is not null)
            Lines.Language = language;
        this.language = language;
        if (translation is not null)
            Reader.Translate(YarnStringsCsv.RowsOf(Build.Compilation, language!), translation);

        Variables = new VariableStorage(Log);
        Reader.OnCommand = OnCommand;
        Session = new DialogueSession(project.GetProgram(), Variables, Lines, Actions, Reader, new DialogueSessionOptions
        {
            Saliency = project.Saliency,
            RandomSeed = seed,
            Clock = new NoWait(),
            Log = Log,
            Nodes = project.Nodes,
        });
        Director = new StoryDirector(Variables, Log) { Stage = new SessionStage(Session) };
        StoryCommands.Story = Director;
        Director.Whereabouts.SetRoot(Village);
        Director.Map.Visit(Director.Whereabouts.Here);
    }

    public const string Village = "Places/Village";

    /// <summary>
    /// The story of the sample, from the scripts of the game's assets.
    /// </summary>
    /// <param name="seed">The seed of what the story leaves to chance.</param>
    /// <param name="language">The language to play in, when not English: its strings file is read from the assets.</param>
    public static TestStory Ashcombe(int? seed = 1, string? language = null)
        => new(StoryFiles.Scripts(), seed, language, language is null ? null : StoryFiles.Strings(language));

    public YarnProjectBuild Build { get; }

    public YarnActionRegistry Actions { get; } = new();

    public TextLineProvider Lines { get; }

    public VariableStorage Variables { get; }

    public DialogueSession Session { get; }

    public StoryDirector Director { get; }

    public Reader Reader { get; } = new();

    public LoggerResult Log { get; } = new();

    public List<string> Transcript => Reader.Transcript;

    public IEnumerable<string> Errors => Log.Messages.Where(m => m.Type == LogMessageType.Error).Select(m => m.Text);

    /// <summary>
    /// Where the cartographer is: the short name of the place (<c>Inn</c>).
    /// </summary>
    public string Here => Director.Whereabouts.Here;

    /// <summary>
    /// The lines said since the last call; the transcript starts again.
    /// </summary>
    public List<string> TakeLines()
    {
        var lines = Transcript.Where(t => t.StartsWith("line: ", StringComparison.Ordinal)).Select(t => t["line: ".Length..]).ToList();
        Transcript.Clear();
        return lines;
    }

    /// <summary>
    /// Plays the events that have something to say, as the game does when it starts.
    /// </summary>
    public async Task Start()
    {
        await Director.RunEvents();
        await Settle();
    }

    /// <summary>
    /// Talks to a person or uses a thing, as the cartographer does, and picks the given options in order.
    /// </summary>
    public async Task Talk(string node, params string[] choices)
    {
        foreach (var choice in choices)
        {
            Reader.Choices.Enqueue(choice);
        }

        Assert.True(Director.CanTalk(node), $"'{node}' has nothing to say now, in {Here}.");
        Assert.True(await Director.Talk(node));
        Assert.True(Reader.Choices.Count == 0, $"'{node}' did not offer: {string.Join(" | ", Reader.Choices)}");
        Assert.Empty(Errors);
        await Settle();
    }

    /// <summary>
    /// Uses a door of the place the cartographer is in, as the game's E key does: its node when it has something to say,
    /// else through it, and the events of the place reached.
    /// </summary>
    public async Task Use(string door, params string[] choices)
    {
        Assert.True(Doors.ContainsKey((Director.Whereabouts.Current, door)), $"There is no door '{door}' in {Director.Whereabouts.Current}.");
        if (DoorNodes.TryGetValue((Director.Whereabouts.Current, door), out var node) && Director.CanTalk(node))
        {
            await Talk(node, choices);
            return;
        }

        Assert.Empty(choices);
        Go(door);

        // Through a door, the game plays what greets the cartographer there; at the end of a road, the stage of the
        // next scene does
        if (journey is null)
        {
            await Director.RunEvents();
            Assert.Empty(Errors);
        }

        await Settle();
    }

    /// <summary>
    /// Goes through doors one after the other.
    /// </summary>
    public async Task Walk(params string[] doors)
    {
        foreach (var door in doors)
        {
            await Use(door);
        }
    }

    /// <summary>
    /// Talks to the echo of the deep level, as its own runner in the deep level's scene does: the echo's project, built
    /// apart from the story's, in a session of its own over the same variables. The events after it play on the story.
    /// </summary>
    /// <param name="answers">The answers the cartographer knows, picked when the echo offers one of them.</param>
    public async Task TalkToEcho(params string[] answers)
    {
        echo ??= MakeEcho();
        Reader.Answers.Clear();
        Reader.Answers.UnionWith(answers);
        var stage = new SessionStage(echo);
        Assert.True(Director.CanTalk("Echo", stage), "The echo has nothing to say now.");
        Assert.True(await Director.Talk("Echo", stage));
        Reader.Answers.Clear();
        Assert.Empty(Errors);
        await Settle();
    }

    private DialogueSession MakeEcho()
    {
        var functions = Actions.Describe().Where(a => a.Kind == YarnActionKind.Function).Select(YarnFunctionDeclaration.From).ToList();
        // The echo speaks the story's language, from its own strings file
        var asset = new YarnProjectAsset();
        Dictionary<string, IReadOnlyList<YarnStringRow>>? translations = null;
        if (language is not null)
        {
            asset.Localisations.Add(new YarnLocalisation { Language = language });
            translations = new() { [language] = StoryFiles.Strings(language, echo: true) };
        }

        var build = YarnProjectBuilder.Build(asset, "Echo", StoryFiles.EchoScripts(), functions, translations);
        Assert.Empty(build.Compilation.Diagnostics.Where(d => d.Severity == YarnSeverity.Error).Select(d => d.ToString()));
        echoEnglish = YarnStringsCsv.RowsOf(build.Compilation, "en");
        if (language is not null)
            Reader.Translate(echoEnglish, translations![language]);
        var project = build.Project!;
        var lines = new TextLineProvider(build.Translations.Append(build.BaseStrings!).ToDictionary(t => t.Language).GetValueOrDefault, project.BaseLanguage, Log)
        {
            LineMetadata = project.LineMetadata,
            ShadowLines = project.ShadowLines,
            Language = Lines.Language,
        };
        return new DialogueSession(project.GetProgram(), Variables, lines, Actions, Reader, new DialogueSessionOptions
        {
            Saliency = project.Saliency,
            RandomSeed = 1,
            Clock = new NoWait(),
            Log = Log,
            Nodes = project.Nodes,
        });
    }

    private DialogueSession? echo;

    private IReadOnlyList<YarnStringRow> echoEnglish = [];

    /// <summary>
    /// The English texts of the story's and the echo's lines that have no value in them, as they are said in English.
    /// </summary>
    public HashSet<string> EnglishTexts()
        => [.. YarnStringsCsv.RowsOf(Build.Compilation, "en").Concat(echoEnglish).Select(r => r.Text).Where(t => !t.Contains('{', StringComparison.Ordinal))];

    // The language the story plays in, when not the base language
    private readonly string? language;

    /// <summary>
    /// Lets one part of the day pass, as the cartographer does, and plays the events that follow.
    /// </summary>
    public async Task PassTime()
    {
        Director.Clock.Advance();
        await Director.RunEvents();
        Assert.Empty(Errors);
        await Settle();
    }

    /// <summary>
    /// Lets time pass until the given time of the day.
    /// </summary>
    public async Task Until(TimeOfDay time)
    {
        while (Director.Clock.Time != time && Director.Ending is null)
        {
            await PassTime();
        }
    }

    /// <summary>
    /// A true or false variable of the story.
    /// </summary>
    public bool Flag(string variable) => Variables.TryGetValue<bool>(variable, out var value) && value;

    /// <summary>
    /// A number of the story, or the rank of an enum.
    /// </summary>
    public int Number(string variable) => Variables.TryGetValue<float>(variable, out var value) ? (int)value : 0;

    public QuestState? Quest(string quest) => Director.StateOf(quest);

    // A journey asked during a dialogue waits for the dialogue and its events, as the game's travel does; the stage of
    // the scene reached then plays its events
    private async Task Settle()
    {
        while (journey is not null)
        {
            var target = journey;
            journey = null;
            Director.Whereabouts.SetInteriors([]);
            Director.Whereabouts.SetRoot(target);
            Director.Map.Visit(Director.Whereabouts.Here);
            await Director.RunEvents();
            Assert.Empty(Errors);
        }
    }

    private void OnCommand(string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts is ["go", var door])
            Go(door);
    }

    private void Go(string door)
    {
        // The game finds a door in any scene of the root tree: here, as there, the door is looked for from the place
        // the cartographer is in, then in its parents
        var where = Director.Whereabouts;
        var places = where.Interiors.Reverse().Append(where.Root);
        var from = places.FirstOrDefault(p => Doors.ContainsKey((p, door)));
        Assert.True(from is not null, $"No door '{door}' in {string.Join(", ", places)}.");
        var (target, isJourney) = Doors[(from!, door)];
        if (isJourney)
        {
            journey = target;
            return;
        }

        if (target == where.Root)
            where.SetInteriors([]);
        else if (target.StartsWith(where.Root + "/", StringComparison.Ordinal))
            where.SetInteriors(target == "Places/Village/Cellar" ? ["Places/Village/Inn", target] : [target]);
        else
            Assert.Fail($"'{door}' leads out of {where.Root} without a journey.");
        Director.Map.Visit(where.Here);
    }

    /// <summary>
    /// A session as the stage of the director, the way the game gives it its dialogue runner.
    /// </summary>
    private sealed class SessionStage(DialogueSession session) : IStoryStage
    {
        public bool IsPlaying => session.IsRunning;

        public bool HasContent(string node) => session.NodeExists(node) && session.HasSalientContent(node);

        public Task Play(string node) => session.RunAsync(node);
    }

    /// <summary>
    /// A clock whose waits are over at once.
    /// </summary>
    private sealed class NoWait : IDialogueClock
    {
        public ValueTask Delay(TimeSpan duration, CancellationToken cancellation) => default;
    }
}

/// <summary>
/// Reads a dialogue as a player would, at once: it writes down what is said and picks the options a test asked for.
/// </summary>
internal sealed class Reader : IDialogueResponder
{
    /// <summary>
    /// What was said, in order: <c>line: text</c>, <c>option: text</c>, <c>command: text</c> for a command the game
    /// has not registered (the doors' and the people's), and <c>complete</c> at the end of each dialogue.
    /// </summary>
    public List<string> Transcript { get; } = [];

    /// <summary>
    /// Every line said since the story started, which <see cref="TestStory.TakeLines"/> does not clear.
    /// </summary>
    public List<string> Said { get; } = [];

    /// <summary>
    /// The options to pick, by their text, in order. With none left, the first option that can be picked.
    /// </summary>
    public Queue<string> Choices { get; } = new();

    /// <summary>
    /// Answers to pick when no choice is queued: the first available option whose text is one of them.
    /// </summary>
    public HashSet<string> Answers { get; } = [];

    /// <summary>
    /// The texts of the options in the language played, by their English text: the tests name the options in English
    /// whatever the language.
    /// </summary>
    private readonly Dictionary<string, string> translated = [];

    /// <summary>
    /// Picks options by the translation of their English text, from the rows of the base language and of the language played.
    /// </summary>
    public void Translate(IReadOnlyList<YarnStringRow> english, IReadOnlyList<YarnStringRow> translation)
    {
        var texts = translation.ToDictionary(r => r.Id, r => r.Text);
        foreach (var row in english)
        {
            if (texts.TryGetValue(row.Id, out var text))
                translated[row.Text] = text;
        }
    }

    private string Local(string english) => translated.GetValueOrDefault(english, english);

    /// <summary>
    /// Called with each command the game has not registered.
    /// </summary>
    public Action<string>? OnCommand { get; set; }

    public ValueTask PrepareForLinesAsync(IReadOnlyList<string> lineIds) => default;

    public ValueTask HandleLineAsync(LocalizedLine line, LineCancellation cancellation)
    {
        Transcript.Add($"line: {line.Text.Text}");
        Said.Add(line.Text.Text);
        return default;
    }

    public ValueTask<int?> HandleOptionsAsync(IReadOnlyList<DialogueOption> options, CancellationToken cancellation)
    {
        foreach (var option in options)
        {
            Transcript.Add($"option: {option.Line.Text.Text}{(option.IsAvailable ? "" : " [unavailable]")}");
        }

        if (!Choices.TryDequeue(out var wanted))
            return new((options.FirstOrDefault(o => o.IsAvailable && Answers.Select(Local).Contains(o.Line.Text.Text)) ?? options.FirstOrDefault(o => o.IsAvailable))?.Index);

        wanted = Local(wanted);
        var chosen = options.FirstOrDefault(o => o.IsAvailable && o.Line.Text.Text == wanted);
        Assert.True(chosen is not null, $"The option '{wanted}' is not offered. Offered: {string.Join(" | ", options.Select(o => o.Line.Text.Text + (o.IsAvailable ? "" : " [unavailable]")))}");
        return new(chosen!.Index);
    }

    public ValueTask HandleCommandAsync(string commandText, CancellationToken cancellation)
    {
        Transcript.Add($"command: {commandText}");
        OnCommand?.Invoke(commandText);
        return default;
    }

    public ValueTask HandleNodeStartAsync(string node) => default;

    public ValueTask HandleNodeCompleteAsync(string node) => default;

    public ValueTask HandleDialogueCompleteAsync()
    {
        Transcript.Add("complete");
        return default;
    }
}
