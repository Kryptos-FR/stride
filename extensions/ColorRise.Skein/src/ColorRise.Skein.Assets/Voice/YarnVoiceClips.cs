using Stride.Core.Assets;
using Stride.Core.IO;

namespace ColorRise.Skein.Assets.Voice;

/// <summary>
/// Finds the voice-over clips of a Yarn project: the sounds of the folder its <see cref="YarnProjectAsset.VoiceOverFolder"/>
/// names for each language, and of the folders under it.
/// </summary>
/// <remarks>
/// A sound is known by the full name of its content type, so that Skein's assets do not depend on Stride's audio, which a
/// game may not have.
/// </remarks>
public static class YarnVoiceClips
{
    /// <summary>
    /// The content type of a clip.
    /// </summary>
    public const string SoundType = "Stride.Audio.Sound";

    /// <summary>
    /// What <see cref="YarnProjectAsset.VoiceOverFolder"/> has where the language goes.
    /// </summary>
    public const string LanguageToken = "{language}";

    private const string LinePrefix = "line:";

    /// <summary>
    /// The name of the clip of a line: its id without <c>line:</c>.
    /// </summary>
    public static string NameOf(string lineId) => lineId.StartsWith(LinePrefix, StringComparison.Ordinal) ? lineId[LinePrefix.Length..] : lineId;

    /// <summary>
    /// The languages of a project, the base language first.
    /// </summary>
    public static IEnumerable<string> LanguagesOf(YarnProjectAsset project)
    {
        return new[] { project.BaseLanguage }.Concat(project.Localisations.Select(l => l.Language)).Where(l => l.Length > 0).Distinct(StringComparer.Ordinal);
    }

    /// <summary>
    /// The folder of the clips of a language, as a location in the project's package, or null when the project has no
    /// voice-over.
    /// </summary>
    public static UDirectory? FolderOf(AssetItem project, string language)
    {
        var folder = ((YarnProjectAsset)project.Asset).VoiceOverFolder?.Trim();
        if (string.IsNullOrEmpty(folder))
            return null;

        return UPath.Combine(project.Location.GetFullDirectory(), new UDirectory(folder.Replace(LanguageToken, language, StringComparison.Ordinal)));
    }

    /// <summary>
    /// The clips of each language of a project, by name, from the project's package. Two clips of a language with the same
    /// name are a problem, and the first one by location is kept.
    /// </summary>
    public static YarnVoiceFolders Find(AssetItem project)
    {
        var languages = new List<YarnVoiceFolder>();
        var problems = new List<string>();
        var asset = (YarnProjectAsset)project.Asset;
        var sounds = project.Package?.Assets.Where(IsClip).OrderBy(a => a.Location.FullPath, StringComparer.Ordinal).ToList() ?? [];

        foreach (var language in LanguagesOf(asset))
        {
            if (FolderOf(project, language) is not { } folder)
                continue;

            var prefix = folder.FullPath.TrimEnd('/') + "/";
            var clips = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var sound in sounds.Where(s => s.Location.FullPath.StartsWith(prefix, StringComparison.Ordinal)))
            {
                var name = sound.Location.GetFileNameWithoutExtension() ?? "";
                if (!clips.TryAdd(name, sound.Location.FullPath))
                    problems.Add($"{clips[name]} and {sound.Location} have the same name: the line '{LinePrefix}{name}' speaks {clips[name]} in '{language}'.");
            }

            languages.Add(new YarnVoiceFolder(language, folder.FullPath, clips));
        }

        return new YarnVoiceFolders(languages, problems);
    }

    /// <summary>
    /// Whether an asset of a package is a sound.
    /// </summary>
    public static bool IsClip(AssetItem item) => AssetRegistry.GetContentType(item.Asset.GetType())?.FullName == SoundType;
}

/// <summary>
/// The voice-over clips of a Yarn project, as <see cref="YarnVoiceClips.Find"/> found them.
/// </summary>
/// <param name="Languages">The clips of each language that has a folder.</param>
/// <param name="Problems">What is wrong with the clips: two of a language with the same name.</param>
public sealed record YarnVoiceFolders(IReadOnlyList<YarnVoiceFolder> Languages, IReadOnlyList<string> Problems);

/// <summary>
/// The voice-over clips of one language of a Yarn project.
/// </summary>
/// <param name="Language">The language.</param>
/// <param name="Folder">The folder of its clips, as a location in the package.</param>
/// <param name="Clips">The location of each clip, by its name: the line id without <c>line:</c>.</param>
public sealed record YarnVoiceFolder(string Language, string Folder, IReadOnlyDictionary<string, string> Clips);
