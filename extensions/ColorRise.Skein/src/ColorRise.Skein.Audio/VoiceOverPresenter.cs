using Stride.Audio;
using Stride.Core;
using Stride.Core.Annotations;
using Stride.Engine;
using Stride.Media;

namespace ColorRise.Skein.Audio;

/// <summary>
/// Plays the voice-over clip of each line (<see cref="LocalizedLine.VoiceOver"/>), from the speaker's entity when it has an
/// audio emitter, and goes to the next line when the clip ends.
/// </summary>
/// <remarks>
/// The clips of a node are loaded when the dialogue enters it and unloaded when it leaves it. A line without a clip in the
/// current language is not spoken, and waits for the other presenters.
/// </remarks>
[DataContract("SkeinVoiceOverPresenter")]
[Display("Voice-over presenter")]
[ComponentCategory("Dialogue")]
public sealed class VoiceOverPresenter : DialoguePresenter
{
    /// <summary>
    /// Whether the dialogue goes to the next line when the clip ends.
    /// </summary>
    /// <userdoc>Go to the next line when the clip of a line ends. Off, the line waits for the player (a line advancer) or for another presenter.</userdoc>
    [DataMember(10)]
    public bool EndLineWhenVoiceEnds { get; set; } = true;

    /// <summary>
    /// The seconds before a clip starts.
    /// </summary>
    /// <userdoc>How many seconds to wait before the clip of a line starts.</userdoc>
    [DataMember(20)]
    [DataMemberRange(0, 10, 0.05, 0.5, 2)]
    public float DelayBefore { get; set; }

    /// <summary>
    /// The seconds after a clip ends, before the next line.
    /// </summary>
    /// <userdoc>How many seconds to wait after the clip of a line ends, before the next line. Skipped when the player goes on.</userdoc>
    [DataMember(30)]
    [DataMemberRange(0, 10, 0.05, 0.5, 2)]
    public float DelayAfter { get; set; }

    /// <summary>
    /// The seconds a clip takes to fade out when the player goes to the next line before it ends.
    /// </summary>
    /// <userdoc>How many seconds a clip takes to fade out when the player goes on before it ends.</userdoc>
    [DataMember(40)]
    [DataMemberRange(0, 10, 0.05, 0.5, 2)]
    public float FadeOut { get; set; } = 0.05f;

    /// <summary>
    /// The volume of the clips, from 0 to 1.
    /// </summary>
    /// <userdoc>The volume of the clips, from 0 (silent) to 1.</userdoc>
    [DataMember(50)]
    [DataMemberRange(0, 1, 0.01, 0.1, 2)]
    public float Volume { get; set; } = 1;

    /// <summary>
    /// What plays the clips: the game's audio, unless a test gives its own.
    /// </summary>
    [DataMemberIgnore]
    internal IVoiceOutput Output
    {
        get => field ??= new ContentVoiceOutput(Content);
        set;
    }

    // The clips loaded for the node the dialogue is in, by URL
    private HashSet<string> Loaded { get; } = [];

    /// <inheritdoc />
    public override async Task PrepareForLinesAsync(IReadOnlyList<string> lineIds)
    {
        if (LinesOf(Runner) is not { } lines)
            return;

        foreach (var url in lineIds.Select(lines.GetVoiceOver).OfType<string>().Distinct())
        {
            await LoadAsync(url);
        }
    }

    /// <inheritdoc />
    public override Task OnNodeExitAsync(string node)
    {
        UnloadAll();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnDialogueCompletedAsync()
    {
        UnloadAll();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Cancel()
    {
        UnloadAll();
        base.Cancel();
    }

    /// <inheritdoc />
    public override async Task RunLineAsync(LocalizedLine line, LineCancellation cancellation)
    {
        if (line.VoiceOver is not { } url || !await LoadAsync(url))
            return;

        var next = cancellation.NextLine;
        if (!await WaitAsync(DelayBefore, next))
            return;

        var speaker = SpeakerOf(line);
        var playing = Output.Play(url, speaker);
        try
        {
            playing.Volume = Volume;
            while (playing.PlayState == PlayState.Playing && !next.IsCancellationRequested)
            {
                await Script.NextFrame();
            }

            // The player went on before the end: the clip fades out, in the time of the computer
            if (playing.PlayState == PlayState.Playing && FadeOut > 0)
            {
                var left = FadeOut;
                while (left > 0 && playing.PlayState == PlayState.Playing)
                {
                    await Script.NextFrame();
                    left -= (float)Game.UpdateTime.Elapsed.TotalSeconds;
                    playing.Volume = Volume * Math.Max(0, left / FadeOut);
                }
            }
        }
        finally
        {
            Output.Release(url, speaker, playing);
        }

        if (next.IsCancellationRequested || !await WaitAsync(DelayAfter, next))
            return;

        if (EndLineWhenVoiceEnds)
            Runner?.RequestNextLine();
    }

    private static ILineProvider? LinesOf(DialogueRunnerComponent? runner)
    {
        if (runner is null)
            return null;
        if (runner.LinesOverride is { } lines)
            return lines;

        return runner.Project is { } project && runner.Services?.GetService<DialogueSystem>() is { } system ? system.GetLineProvider(project) : null;
    }

    private async ValueTask<bool> LoadAsync(string url)
    {
        if (Loaded.Contains(url))
            return true;
        if (!await Output.LoadAsync(url))
        {
            Log.Warning($"The voice-over clip {url} is not in the game's content.");
            return false;
        }

        Loaded.Add(url);
        return true;
    }

    private void UnloadAll()
    {
        foreach (var url in Loaded)
        {
            Output.Unload(url);
        }

        Loaded.Clear();
    }

    // The emitter of the entity the line's speaker names, found as a command finds its target
    private AudioEmitterComponent? SpeakerOf(LocalizedLine line)
    {
        if (line.CharacterName is not { Length: > 0 } name)
            return null;

        return new EntityTargetResolver(Entity).Find(name, typeof(AudioEmitterComponent)) as AudioEmitterComponent;
    }

    private async ValueTask<bool> WaitAsync(float seconds, CancellationToken cancellation)
    {
        var left = TimeSpan.FromSeconds(seconds);
        while (left > TimeSpan.Zero)
        {
            if (cancellation.IsCancellationRequested)
                return false;
            await Script.NextFrame();
            left -= Game.UpdateTime.WarpElapsed;
        }

        return !cancellation.IsCancellationRequested;
    }
}
