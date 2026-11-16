using System.Text.RegularExpressions;

namespace Ashcombe.Tests;

/// <summary>
/// The scene files of the game, as the editor reads them: a child scene is kept under its parent only when both sides
/// say so, the parent by <c>ChildrenIds</c> and the child by <c>Parent</c>. Without the second, the editor drops the child
/// on load and the next save of the parent loses it.
/// </summary>
public sealed partial class SceneFileTests
{
    [Fact]
    public void Every_child_scene_names_its_parent_and_every_parent_lists_its_children()
    {
        var places = Path.Combine(StoryFiles.Folder, "..", "Places");
        var scenes = Directory.EnumerateFiles(places, "*.sdscene", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .Select(text => (Id: IdOf().Match(text).Groups[1].Value, Children: ChildOf().Matches(text).Select(m => m.Groups[1].Value).ToList(), Parent: ParentOf().Match(text) is { Success: true } p ? p.Groups[1].Value : null))
            .ToList();

        var pairs = scenes.SelectMany(s => s.Children.Select(c => (Parent: s.Id, Child: c))).ToList();
        Assert.Equal(9, pairs.Count);
        Assert.All(pairs, pair => Assert.Equal(pair.Parent, scenes.Single(s => s.Id == pair.Child).Parent));
        Assert.All(scenes.Where(s => s.Parent is not null), child => Assert.Contains(child.Id, scenes.Single(s => s.Id == child.Parent).Children));
    }

    [GeneratedRegex(@"^Id: ([0-9a-f-]+)", RegexOptions.Multiline)]
    private static partial Regex IdOf();

    [GeneratedRegex(@"^    - ([0-9a-f-]{36})\r?$", RegexOptions.Multiline)]
    private static partial Regex ChildOf();

    [GeneratedRegex(@"^Parent: ([0-9a-f-]+):", RegexOptions.Multiline)]
    private static partial Regex ParentOf();
}
