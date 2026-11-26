namespace Thornwick.Tests;

/// <summary>
/// The files of the game, read from the game library beside the tests: nothing is built.
/// </summary>
internal static class SampleFiles
{
    /// <summary>
    /// The folder of the game library, <c>Thornwick.Game</c>, found from the folder the tests run in.
    /// </summary>
    public static string Game { get; } = Find();

    /// <summary>
    /// The file of an asset of the game: <c>Asset("Maps/Town", ".sdtilemap")</c>.
    /// </summary>
    public static string Asset(string url, string extension) => Path.Combine(Game, "Assets", url + extension);

    /// <summary>
    /// A file of the game's resources: <c>Resource("Kenney/TinyTown/tilemap_packed.png")</c>.
    /// </summary>
    public static string Resource(string path) => Path.Combine(Game, "Resources", path);

    // The tests run from the output folder of their project, under the folder of the sample
    private static string Find()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var game = Path.Combine(folder.FullName, "Thornwick.Game");
            if (File.Exists(Path.Combine(game, "Thornwick.Game.csproj")))
            {
                return game;
            }
        }
        throw new DirectoryNotFoundException($"The game of the sample is not above '{AppContext.BaseDirectory}'.");
    }
}
