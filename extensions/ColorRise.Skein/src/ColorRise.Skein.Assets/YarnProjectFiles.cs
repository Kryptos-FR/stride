using Microsoft.Extensions.FileSystemGlobbing;
using Stride.Core.Assets;

namespace ColorRise.Skein.Assets;

/// <summary>
/// Which scripts a Yarn project includes: the scripts of its package that its patterns match, from the folder of the project.
/// </summary>
public static class YarnProjectFiles
{
    /// <summary>
    /// The name a script has for the compiler and in its messages: its place among the assets of its package, as a file
    /// (<c>Dialogue/Act1.yarn</c>).
    /// </summary>
    public static string FileNameOf(AssetItem script)
    {
        ArgumentNullException.ThrowIfNull(script);
        return script.UnqualifiedUrl.FullPath + YarnScriptAsset.FileExtension;
    }

    /// <summary>
    /// The scripts a project includes, in the order of their names.
    /// </summary>
    public static IReadOnlyList<AssetItem> ScriptsOf(AssetItem project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.Asset is not YarnProjectAsset asset || project.Package is not { } package)
            return [];

        var scripts = package.Assets.Where(a => a.Asset is YarnScriptAsset).ToDictionary(a => Rooted(FileNameOf(a)));
        var folder = FolderOf(project);
        var included = Matched(asset.SourceFiles, folder, scripts.Keys);
        included.ExceptWith(Matched(asset.ExcludedFiles, folder, scripts.Keys));
        return [.. included.Select(f => scripts[f]).OrderBy(a => a.Location.FullPath, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The projects of a script's package that include it, in the order of their names.
    /// </summary>
    public static IReadOnlyList<AssetItem> ProjectsOf(AssetItem script)
    {
        ArgumentNullException.ThrowIfNull(script);
        if (script.Asset is not YarnScriptAsset || script.Package is not { } package)
            return [];

        string[] file = [Rooted(FileNameOf(script))];
        return [.. package.Assets
            .Where(a => a.Asset is YarnProjectAsset project && Matched(project.SourceFiles, FolderOf(a), file).Count > 0 && Matched(project.ExcludedFiles, FolderOf(a), file).Count == 0)
            .OrderBy(a => a.Location.FullPath, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The scripts of a package that none of its projects includes, in the order of their names.
    /// </summary>
    public static IReadOnlyList<AssetItem> ScriptsInNoProject(Package package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var included = package.Assets.Where(a => a.Asset is YarnProjectAsset).SelectMany(ScriptsOf).ToHashSet();
        return [.. package.Assets.Where(a => a.Asset is YarnScriptAsset && !included.Contains(a)).OrderBy(a => a.Location.FullPath, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The files that patterns match from a folder.
    /// </summary>
    private static HashSet<string> Matched(IEnumerable<string> patterns, string folder, IEnumerable<string> files)
    {
        // The matcher finds nothing above its folder: a pattern that starts with ../ is matched from the folder it climbs to
        var matched = new HashSet<string>();
        foreach (var climb in patterns.Select(Climb).Where(p => p.Pattern.Length > 0).GroupBy(p => p.Levels, p => p.Pattern))
        {
            var from = folder;
            for (var i = 0; i < climb.Key && from is not null; i++)
            {
                from = Path.GetDirectoryName(from);
            }

            // Above the root of the package there is no script
            if (from is null)
                continue;

            // As Yarn Spinner's own project files: patterns ignore case
            var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            matcher.AddIncludePatterns(climb);
            matched.UnionWith(matcher.Match(from, files).Files.Select(f => Path.GetFullPath(Path.Combine(from, f.Path))));
        }

        return matched;
    }

    private static (int Levels, string Pattern) Climb(string pattern)
    {
        var rest = pattern.Trim().Replace('\\', '/');
        var levels = 0;
        while (true)
        {
            if (rest.StartsWith("./", StringComparison.Ordinal))
            {
                rest = rest[2..];
            }
            else if (rest.StartsWith("../", StringComparison.Ordinal))
            {
                rest = rest[3..];
                levels++;
            }
            else
            {
                return (levels, rest);
            }
        }
    }

    // The matcher works on rooted paths: the root of the package stands for the root of a disk
    private static string Rooted(string location) => Path.GetFullPath(Path.Combine(Root, location));

    private static string FolderOf(AssetItem project) => Rooted(project.UnqualifiedUrl.GetFullDirectory()?.FullPath ?? "");

    private static string Root => Path.GetPathRoot(Path.GetFullPath("."))!;
}
