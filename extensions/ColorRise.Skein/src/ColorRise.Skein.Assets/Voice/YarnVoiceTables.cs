namespace ColorRise.Skein.Assets.Voice;

/// <summary>
/// Makes the voice tables of a compiled Yarn project from the clips of its folders.
/// </summary>
public static class YarnVoiceTables
{
    /// <summary>
    /// The voice table of each language that has clips, and what the build says of them: how many lines of a language
    /// have no clip, and the clips that are of no line.
    /// </summary>
    /// <param name="projectName">The name of the project in the messages.</param>
    /// <param name="lines">The ids of the lines that have a text of their own.</param>
    /// <param name="shadowLines">The lines that repeat another (<see cref="YarnProject.ShadowLines"/>): they speak its clip when they have none of their own.</param>
    /// <param name="folders">The clips, as <see cref="YarnVoiceClips.Find"/> found them.</param>
    public static (IReadOnlyList<YarnVoiceTable> Tables, IReadOnlyList<string> Warnings) Make(
        string projectName, IReadOnlyCollection<string> lines, IReadOnlyDictionary<string, string> shadowLines, YarnVoiceFolders folders)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(shadowLines);
        ArgumentNullException.ThrowIfNull(folders);

        var tables = new List<YarnVoiceTable>();
        var warnings = new List<string>(folders.Problems);
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in lines.Concat(shadowLines.Keys))
        {
            ids.TryAdd(YarnVoiceClips.NameOf(id), id);
        }

        foreach (var folder in folders.Languages)
        {
            if (folder.Clips.Count == 0)
                continue;

            var table = new YarnVoiceTable { Language = folder.Language };
            foreach (var (name, url) in folder.Clips.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                if (ids.TryGetValue(name, out var id))
                    table.Clips[id] = url;
                else
                    warnings.Add($"{projectName}: {url} is the clip of no line, since no line has the id 'line:{name}'. Name it after the id of its line.");
            }

            var silent = lines.Count(l => !table.Clips.ContainsKey(l));
            if (silent > 0)
                warnings.Add($"{projectName}: in '{folder.Language}', {silent} of {lines.Count} {(lines.Count == 1 ? "line has" : "lines have")} no voice-over clip in {folder.Folder}.");

            tables.Add(table);
        }

        return (tables, warnings);
    }
}
