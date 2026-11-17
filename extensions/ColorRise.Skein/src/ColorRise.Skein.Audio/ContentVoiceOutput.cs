using Stride.Audio;
using Stride.Core.Serialization.Contents;
using Stride.Engine;

namespace ColorRise.Skein.Audio;

/// <summary>
/// Plays the clips as the game's sounds, loaded from its content.
/// </summary>
internal sealed class ContentVoiceOutput(ContentManager content) : IVoiceOutput
{
    private Dictionary<string, Sound> Sounds { get; } = [];

    public async ValueTask<bool> LoadAsync(string url)
    {
        if (!content.Exists(url))
            return false;

        // Each load is a reference of the content manager's, given back by Unload
        Sounds[url] = await content.LoadAsync<Sound>(url);
        return true;
    }

    public void Unload(string url)
    {
        if (Sounds.TryGetValue(url, out var sound))
        {
            content.Unload(sound);
            if (!content.IsLoaded(url))
                Sounds.Remove(url);
        }
    }

    public IPlayableSound Play(string url, AudioEmitterComponent? speaker)
    {
        var sound = Sounds[url];
        IPlayableSound playing = speaker is not null ? speaker.AttachSound(sound) : sound.CreateInstance();
        playing.Play();
        return playing;
    }

    public void Release(string url, AudioEmitterComponent? speaker, IPlayableSound playing)
    {
        playing.Stop();
        if (speaker is not null)
            speaker.DetachSound(Sounds[url]);
        else
            ((SoundInstance)playing).Dispose();
    }
}
