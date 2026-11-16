using ColorRise.Skein.Assets.Localisation;

namespace Ashcombe.Tests;

/// <summary>
/// The scripts and the strings files of the sample's two Yarn projects, read from the assets of the game beside the
/// tests: the story (every script of <c>Dialogue</c> but the echo's folder) and the echo (<c>Dialogue/Echo</c>).
/// </summary>
internal static class StoryFiles
{
    /// <summary>
    /// The folder of the story in the assets of the game.
    /// </summary>
    public static string Folder { get; } = Find();

    /// <summary>
    /// The folder of the echo's project.
    /// </summary>
    public static string EchoFolder => Path.Combine(Folder, "Echo");

    /// <summary>
    /// The scripts of the story, named from the assets folder as the game's build names them (<c>Dialogue/Act1/Inn.yarn</c>).
    /// </summary>
    public static IReadOnlyList<(string FileName, string Text)> Scripts()
        => Read(Folder, f => !f.StartsWith(EchoFolder + Path.DirectorySeparatorChar, StringComparison.Ordinal));

    /// <summary>
    /// The scripts of the echo's project.
    /// </summary>
    public static IReadOnlyList<(string FileName, string Text)> EchoScripts() => Read(EchoFolder, _ => true);

    /// <summary>
    /// The rows of the strings file of a language, for the story or the echo.
    /// </summary>
    public static IReadOnlyList<YarnStringRow> Strings(string language, bool echo = false)
        => YarnStringsCsv.Read(File.ReadAllText(echo
            ? Path.Combine(EchoFolder, "Strings", $"Echo.{language}.csv")
            : Path.Combine(Folder, "Strings", $"Ashcombe.{language}.csv")));

    private static IReadOnlyList<(string FileName, string Text)> Read(string folder, Func<string, bool> keep)
        => [.. Directory.EnumerateFiles(folder, "*.yarn", SearchOption.AllDirectories)
            .Where(keep)
            .Order(StringComparer.Ordinal)
            .Select(f => (Path.GetRelativePath(Path.GetDirectoryName(Folder)!, f).Replace('\\', '/'), File.ReadAllText(f)))];

    // The tests run from the output folder of their project, under the folder of the sample
    private static string Find()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var story = Path.Combine(folder.FullName, "Ashcombe.Game", "Assets", "Dialogue");
            if (Directory.Exists(story))
                return story;
        }

        throw new DirectoryNotFoundException($"The story of the sample is not above '{AppContext.BaseDirectory}'.");
    }
}
