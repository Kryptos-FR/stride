using System.Globalization;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// What is wrong in a flipbook that still builds, as the property grid, a merge or an edit by hand can leave it: the lines
/// the build warns with, and the editor shows.
/// </summary>
public static class FlipbookAssetChecks
{
    /// <summary>
    /// What is wrong in a flipbook that the asset alone shows, one line each, with the names. Past the names, only what game
    /// code plays is checked: the first animation of each name, and the first clip of each direction in it. Empty items, which
    /// the content leaves out, have no line.
    /// </summary>
    public static IReadOnlyList<string> Of(FlipbookAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var lines = new List<string>();
        if (asset.TileSet is null)
        {
            lines.Add("The flipbook has no tile set: it draws nothing.");
        }

        var animations = asset.Animations.Where(animation => animation is not null).ToList();
        if (animations.Any(animation => string.IsNullOrEmpty(animation.Name)))
        {
            lines.Add("An animation has no name: game code cannot play it.");
        }
        var named = animations.Where(animation => !string.IsNullOrEmpty(animation.Name)).GroupBy(animation => animation.Name, StringComparer.Ordinal).ToList();
        foreach (var group in named.Where(group => group.Count() > 1))
        {
            lines.Add($"The name '{group.Key}' is given to {Count(group.Count())} animations: the first is played.");
        }
        foreach (var group in named)
        {
            AddLines(lines, group.First());
        }
        return lines;
    }

    private static void AddLines(List<string> lines, FlipbookAnimation animation)
    {
        var clips = animation.Clips.Where(clip => clip is not null).ToList();
        if (clips.Count == 0)
        {
            lines.Add($"The animation '{animation.Name}' has no clip.");
            return;
        }
        var directions = clips.GroupBy(clip => clip.Direction).ToList();
        foreach (var group in directions.Where(group => group.Count() > 1))
        {
            lines.Add($"The animation '{animation.Name}' has {Count(group.Count())} clips for {FlipbookDirections.NameOf(group.Key)}: the first is played.");
        }
        foreach (var clip in directions.Select(group => group.First()))
        {
            var frames = clip.Frames.Where(frame => frame is not null).ToList();
            if (frames.Count == 0)
            {
                lines.Add($"The clip {FlipbookDirections.NameOf(clip.Direction)} of '{animation.Name}' has no frame.");
                continue;
            }
            for (var i = 0; i < frames.Count; i++)
            {
                if (!frames[i].Cells.Any(cell => cell is not null))
                {
                    lines.Add($"The frame {Count(i + 1)} of the clip {FlipbookDirections.NameOf(clip.Direction)} of '{animation.Name}' has no cell.");
                }
            }
        }
    }

    private static string Count(int count) => count.ToString(CultureInfo.InvariantCulture);
}
