using Stride.Audio;
using Stride.Engine;

namespace ColorRise.Skein.Audio;

/// <summary>
/// What a <see cref="VoiceOverPresenter"/> plays its clips through: the game's audio, or a test's.
/// </summary>
internal interface IVoiceOutput
{
    /// <summary>
    /// Loads the clip at a URL, once for every <see cref="Unload"/>.
    /// </summary>
    /// <returns>Whether the clip is there.</returns>
    ValueTask<bool> LoadAsync(string url);

    /// <summary>
    /// Unloads a clip <see cref="LoadAsync"/> loaded.
    /// </summary>
    void Unload(string url);

    /// <summary>
    /// Plays a loaded clip, from a speaker's entity when it has an audio emitter.
    /// </summary>
    /// <returns>The clip playing, or null when it does not play: the game's audio does not run (no audio device).</returns>
    /// <param name="url">The URL of the clip.</param>
    /// <param name="speaker">The emitter of the speaker, or null to play the clip as a plain sound.</param>
    IPlayableSound? Play(string url, AudioEmitterComponent? speaker);

    /// <summary>
    /// Stops a clip <see cref="Play"/> started and lets go of what playing it took.
    /// </summary>
    void Release(string url, AudioEmitterComponent? speaker, IPlayableSound playing);
}
