namespace Thornwick.Tests;

/// <summary>
/// What the world writes in the window's title.
/// </summary>
public sealed class WorldTests
{
    [Theory]
    [InlineData(12, "Thornwick: 12 brambles left")]
    [InlineData(1, "Thornwick: 1 bramble left")]
    [InlineData(0, "Thornwick: all the brambles are cut")]
    [InlineData(-3, "Thornwick: all the brambles are cut")]
    public void The_title_counts_the_brambles(int brambles, string title)
        => Assert.Equal(title, World.TitleFor(brambles));
}
