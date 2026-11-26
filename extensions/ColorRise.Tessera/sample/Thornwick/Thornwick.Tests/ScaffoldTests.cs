namespace Thornwick.Tests;

/// <summary>
/// The game library is where the tests look for it, and its art comes with its licence and its notice.
/// </summary>
public sealed class ScaffoldTests
{
    [Fact]
    public void The_game_is_beside_the_tests()
    {
        Assert.True(File.Exists(Path.Combine(SampleFiles.Game, "Thornwick.Game.sdpkg")));
        Assert.True(File.Exists(SampleFiles.Asset("MainScene", ".sdscene")));
    }

    [Fact]
    public void The_art_comes_with_its_licence()
    {
        // Tessera's notice, two folders above the sample
        var notice = File.ReadAllText(Path.Combine(SampleFiles.Game, "..", "..", "..", "THIRD-PARTY-NOTICES.md"));
        var packs = Directory.GetDirectories(SampleFiles.Resource("Kenney"));
        Assert.NotEmpty(packs);
        foreach (var pack in packs)
        {
            var licence = Path.Combine(pack, "License.txt");
            Assert.True(File.Exists(licence), $"{pack} has no License.txt");
            Assert.Contains("Creative Commons Zero", File.ReadAllText(licence));
            Assert.NotEmpty(Directory.GetFiles(pack, "*.png"));
            Assert.Contains($"Resources/Kenney/{Path.GetFileName(pack)}/", notice);
        }
    }
}
