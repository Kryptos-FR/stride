using ColorRise.Skein.Audio;
using ColorRise.Skein.Tests.Engine;
using ColorRise.Skein.Tests.Session;
using Google.Protobuf;
using Stride.Audio;
using Stride.Engine;
using Stride.Media;

namespace ColorRise.Skein.Tests.Voice;

/// <summary>
/// The voice-over presenter plays the clip of each line and goes on when it ends.
/// </summary>
[Collection(GameCollection.Name)]
public sealed class VoiceOverPresenterTests
{
    private const string Post = """
        title: Start
        ---
        Odile: The post is late. #line:post
        Odile: Then we wait. #line:wait
        ===
        """;

    /// <summary>
    /// A runner whose lines have the clips of a voice table, with a voice-over presenter that plays into a test's output
    /// and a presenter that holds the first line until it is cancelled.
    /// </summary>
    private static (DialogueRunnerComponent Runner, VoiceOverPresenter Voice, FakeVoiceOutput Output, RecordingPresenter Text) Setup(
        GameHarness harness, Dictionary<string, string> clips, Action<VoiceOverPresenter>? configure = null)
    {
        var compilation = Scripts.Compile(Post);
        var project = new YarnProject { Program = compilation.Program!.ToByteArray() };
        project.Nodes.AddRange(TestDialogue.NodesOf(compilation));
        var strings = new YarnStringTable { Language = "en" };
        foreach (var (id, line) in compilation.Lines)
        {
            strings.Strings[id] = line.Text;
        }

        var voices = new YarnVoiceTable { Language = "en" };
        foreach (var (id, url) in clips)
        {
            voices.Clips[id] = url;
        }

        var lines = new TextLineProvider(l => l == "en" ? strings : null, "en") { VoiceTables = l => l == "en" ? voices : null };
        var output = new FakeVoiceOutput();
        var voice = new VoiceOverPresenter { Output = output };
        configure?.Invoke(voice);
        var text = new RecordingPresenter();
        text.LineGates.Enqueue(new TaskCompletionSource());
        var runner = new DialogueRunnerComponent { Project = project, LinesOverride = lines };
        runner.Presenters.AddRange([voice, text]);
        harness.Add("Runner", runner, voice, text);
        harness.Step();
        return (runner, voice, output, text);
    }

    [Fact]
    public void A_line_speaks_its_clip_loaded_with_its_node_and_the_dialogue_goes_on_when_it_ends()
    {
        using var harness = new GameHarness();
        var (runner, _, output, text) = Setup(harness, new() { ["line:post"] = "Voice/en/post" });

        var dialogue = runner.StartDialogue("Start");
        harness.Step(2);
        Assert.Equal(["load Voice/en/post", "play Voice/en/post"], output.Seen);
        Assert.Equal("line: Odile: The post is late.", text.Seen[^1]);

        // The clip plays on while the text waits
        harness.Step(10);
        Assert.Equal("line: Odile: The post is late.", text.Seen[^1]);
        Assert.Equal(1f, output.Playing!.Volume);

        output.Playing.PlayState = PlayState.Stopped;
        harness.Step(3);

        // The second line has no clip: it is not spoken and does not hold the dialogue
        Assert.True(dialogue.IsCompletedSuccessfully);
        Assert.Contains("line: Odile: Then we wait.", text.Seen);
        Assert.Equal(["load Voice/en/post", "play Voice/en/post", "release Voice/en/post", "unload Voice/en/post"], output.Seen);
    }

    [Fact]
    public void The_player_going_on_fades_the_clip_out_and_the_delay_after_waits_for_a_clip_that_ends()
    {
        using var harness = new GameHarness();
        var (runner, _, output, text) = Setup(harness, new() { ["line:post"] = "Voice/en/post", ["line:wait"] = "Voice/en/wait" }, v => v.DelayAfter = 0.5f);
        text.LineGates.Enqueue(new TaskCompletionSource());

        var dialogue = runner.StartDialogue("Start");
        harness.Step(2);
        var first = output.Playing!;
        runner.RequestNextLine();

        // 0.05 seconds of fade, in frames of a 60th of a second
        harness.Step(2);
        Assert.Equal(PlayState.Playing, first.PlayState);
        Assert.InRange(first.Volume, 0.1f, 0.9f);
        harness.Step(4);
        Assert.Contains("release Voice/en/post", output.Seen);
        Assert.Equal(0f, first.Volume);
        Assert.Equal(PlayState.Stopped, first.PlayState);
        Assert.Equal("play Voice/en/wait", output.Seen[^1]);

        // The second clip ends, and the next line waits half a second more
        output.Playing!.PlayState = PlayState.Stopped;
        harness.Step(20);
        Assert.False(dialogue.IsCompleted);
        harness.Step(15);
        Assert.True(dialogue.IsCompletedSuccessfully);
    }

    [Fact]
    public void A_speaker_with_an_audio_emitter_speaks_from_it_and_one_without_plays_a_plain_sound()
    {
        using var harness = new GameHarness();
        var emitter = new AudioEmitterComponent();
        harness.Add("Odile", emitter);
        var (runner, _, output, _) = Setup(harness, new() { ["line:post"] = "Voice/en/post" });

        runner.StartDialogue("Start");
        harness.Step(2);
        Assert.Same(emitter, output.Speaker);

        harness.Scene.Entities.Remove(emitter.Entity);
        runner.Stop();
        harness.Step(6);
        runner.StartDialogue("Start");
        harness.Step(2);
        Assert.Null(output.Speaker);
    }

    [Fact]
    public void A_clip_that_is_not_in_the_content_is_a_warning_and_the_line_waits_for_the_others()
    {
        using var harness = new GameHarness();
        var (runner, _, output, text) = Setup(harness, new() { ["line:post"] = "Voice/en/lost" });
        output.Missing.Add("Voice/en/lost");

        var dialogue = runner.StartDialogue("Start");
        harness.Step(5);

        Assert.False(dialogue.IsCompleted);
        Assert.DoesNotContain(output.Seen, s => s.StartsWith("play", StringComparison.Ordinal));
        Assert.Contains(harness.Logged(Stride.Core.Diagnostics.LogMessageType.Warning), m => m == "The voice-over clip Voice/en/lost is not in the game's content.");
        runner.Stop();
        harness.Step();
    }
}

/// <summary>
/// An output that writes down what it is asked, and whose clips play until the test stops them.
/// </summary>
internal sealed class FakeVoiceOutput : IVoiceOutput
{
    public List<string> Seen { get; } = [];

    public HashSet<string> Missing { get; } = [];

    public FakeSound? Playing { get; private set; }

    public AudioEmitterComponent? Speaker { get; private set; }

    public ValueTask<bool> LoadAsync(string url)
    {
        if (Missing.Contains(url))
            return ValueTask.FromResult(false);

        Seen.Add($"load {url}");
        return ValueTask.FromResult(true);
    }

    public void Unload(string url) => Seen.Add($"unload {url}");

    public IPlayableSound Play(string url, AudioEmitterComponent? speaker)
    {
        Seen.Add($"play {url}");
        Speaker = speaker;
        Playing = new FakeSound();
        Playing.Play();
        return Playing;
    }

    public void Release(string url, AudioEmitterComponent? speaker, IPlayableSound playing)
    {
        playing.Stop();
        Seen.Add($"release {url}");
    }
}

/// <summary>
/// A sound that plays until it is stopped.
/// </summary>
internal sealed class FakeSound : IPlayableSound
{
    public PlayState PlayState { get; set; } = PlayState.Stopped;

    public bool IsLooping { get; set; }

    public float Volume { get; set; } = 1;

    public void Play() => PlayState = PlayState.Playing;

    public void Pause() => PlayState = PlayState.Paused;

    public void Stop() => PlayState = PlayState.Stopped;
}
