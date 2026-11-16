using ColorRise.Skein.Assets.Localisation;

namespace Brinemoor.Tests;

/// <summary>
/// The scripts and the strings files of the sample's story, read from the assets of the game beside the tests.
/// </summary>
internal static class StoryFiles
{
    /// <summary>
    /// The folder of the story in the assets of the game.
    /// </summary>
    public static string Folder { get; } = Find();

    /// <summary>
    /// The scripts of the story, named from the assets folder as the game's build names them (<c>Dialogue/Act1.yarn</c>).
    /// </summary>
    public static IReadOnlyList<(string FileName, string Text)> Scripts()
        => [.. Directory.EnumerateFiles(Folder, "*.yarn", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(f => (Path.GetRelativePath(Path.GetDirectoryName(Folder)!, f).Replace('\\', '/'), File.ReadAllText(f)))];

    /// <summary>
    /// The rows of the strings file of a language.
    /// </summary>
    public static IReadOnlyList<YarnStringRow> Strings(string language)
        => YarnStringsCsv.Read(File.ReadAllText(Path.Combine(Folder, "Strings", $"Brinemoor.{language}.csv")));

    // The tests run from the output folder of their project, under the folder of the sample
    private static string Find()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var story = Path.Combine(folder.FullName, "Brinemoor.Game", "Assets", "Dialogue");
            if (Directory.Exists(story))
                return story;
        }

        throw new DirectoryNotFoundException($"The story of the sample is not above '{AppContext.BaseDirectory}'.");
    }
}
