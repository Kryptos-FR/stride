using ColorRise.Skein;
using ColorRise.Skein.Assets;
using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Assets.Localisation;
using Stride.Core.Diagnostics;

namespace Brinemoor.Tests;

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
/// A story without its game: scripts built as the game's build builds them, against the game's commands and
/// functions, a session that plays them, and the game's director with its inventory, clock and journal.
/// </summary>
internal sealed class TestStory
{
    public TestStory(string script, int? seed = 1)
        : this([("Test.yarn", script)], seed)
    {
    }

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

        Variables = new VariableStorage(Log);
        Session = new DialogueSession(project.GetProgram(), Variables, Lines, Actions, Reader, new DialogueSessionOptions
        {
            Saliency = project.Saliency,
            RandomSeed = seed,
            Clock = new NoWait(),
            Log = Log,
            Nodes = project.Nodes,
        });
        Director = new StoryDirector(new SessionStage(Session), Variables, Log);
        StoryCommands.Story = Director;
    }

    /// <summary>
    /// The story of the sample, from the scripts of the game's assets.
    /// </summary>
    /// <param name="seed">The seed of what the story leaves to chance.</param>
    /// <param name="language">The language to play in, when not English: its strings file is read from the assets.</param>
    public static TestStory Brinemoor(int? seed = 1, string? language = null)
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
    public Task Start() => Director.RunEvents();

    /// <summary>
    /// Talks to a person or uses a thing, as the courier does, and picks the given options in order.
    /// </summary>
    public async Task Talk(string node, params string[] choices)
    {
        foreach (var choice in choices)
        {
            Reader.Choices.Enqueue(choice);
        }

        Assert.True(Director.CanTalk(node), $"'{node}' has nothing to say now.");
        Assert.True(await Director.Talk(node));
        Assert.True(Reader.Choices.Count == 0, $"'{node}' did not offer: {string.Join(" | ", Reader.Choices)}");
        Assert.Empty(Errors);
    }

    /// <summary>
    /// Lets one part of the day pass, as the courier does, and plays the events that follow.
    /// </summary>
    public async Task PassTime()
    {
        Director.Clock.Advance();
        await Director.RunEvents();
        Assert.Empty(Errors);
    }

    /// <summary>
    /// Lets time pass until the night's events brought the next morning, or the story ended.
    /// </summary>
    public async Task UntilNextDay()
    {
        var day = Director.Clock.Day;
        while (Director.Clock.Day == day && Director.Ending is null)
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
    /// does not have, and <c>complete</c> at the end of each dialogue.
    /// </summary>
    public List<string> Transcript { get; } = [];

    /// <summary>
    /// The options to pick, by their text, in order. With none left, the first option that can be picked.
    /// </summary>
    public Queue<string> Choices { get; } = new();

    /// <summary>
    /// Called with each line, while the dialogue waits on it.
    /// </summary>
    public Func<LocalizedLine, ValueTask>? OnLine { get; set; }

    public ValueTask PrepareForLinesAsync(IReadOnlyList<string> lineIds) => default;

    public async ValueTask HandleLineAsync(LocalizedLine line, LineCancellation cancellation)
    {
        Transcript.Add($"line: {line.Text.Text}");
        if (OnLine is not null)
            await OnLine(line);
    }

    public ValueTask<int?> HandleOptionsAsync(IReadOnlyList<DialogueOption> options, CancellationToken cancellation)
    {
        foreach (var option in options)
        {
            Transcript.Add($"option: {option.Line.Text.Text}{(option.IsAvailable ? "" : " [unavailable]")}");
        }

        if (!Choices.TryDequeue(out var wanted))
            return new(options.FirstOrDefault(o => o.IsAvailable)?.Index);

        var chosen = options.FirstOrDefault(o => o.IsAvailable && o.Line.Text.Text == wanted);
        Assert.True(chosen is not null, $"The option '{wanted}' is not offered. Offered: {string.Join(" | ", options.Select(o => o.Line.Text.Text + (o.IsAvailable ? "" : " [unavailable]")))}");
        return new(chosen!.Index);
    }

    public ValueTask HandleCommandAsync(string commandText, CancellationToken cancellation)
    {
        Transcript.Add($"command: {commandText}");
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
