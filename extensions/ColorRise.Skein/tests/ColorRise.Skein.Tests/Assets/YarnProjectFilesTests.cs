using ColorRise.Skein.Assets;
using Stride.Core.Assets;

namespace ColorRise.Skein.Tests.Assets;

/// <summary>
/// A project includes the scripts of its package that its patterns match, from the folder of the project.
/// </summary>
public sealed class YarnProjectFilesTests
{
    private static Package PackageOf(params AssetItem[] items)
    {
        var package = new Package();
        foreach (var item in items)
        {
            package.Assets.Add(item);
        }

        return package;
    }

    private static AssetItem Script(string location) => new(location, new YarnScriptAsset { Text = "title: Start\n---\n===\n" });

    private static AssetItem Project(string location, string[]? sources = null, string[]? excluded = null)
    {
        var project = new YarnProjectAsset();
        project.SourceFiles.AddRange(sources ?? [YarnProjectAsset.AllScripts]);
        project.ExcludedFiles.AddRange(excluded ?? []);
        return new AssetItem(location, project);
    }

    private static IEnumerable<string> Names(IEnumerable<AssetItem> items) => items.Select(i => i.Location.FullPath);

    [Fact]
    public void Every_script_under_the_folder_of_the_project_is_in_it()
    {
        var project = Project("Dialogue/Story");
        PackageOf(project, Script("Dialogue/Chatter/Pier"), Script("Dialogue/Act1"), Script("Other/X"), Script("Top"));

        Assert.Equal(["Dialogue/Act1", "Dialogue/Chatter/Pier"], Names(YarnProjectFiles.ScriptsOf(project)));
    }

    [Fact]
    public void A_project_at_the_root_takes_every_script_of_the_package()
    {
        var project = Project("Story");
        PackageOf(project, Script("Dialogue/Act1"), Script("Top"));

        Assert.Equal(["Dialogue/Act1", "Top"], Names(YarnProjectFiles.ScriptsOf(project)));
    }

    [Fact]
    public void An_excluded_pattern_leaves_scripts_out()
    {
        var project = Project("Dialogue/Story", excluded: ["Chatter/**"]);
        PackageOf(project, Script("Dialogue/Chatter/Pier"), Script("Dialogue/Act1"));

        Assert.Equal(["Dialogue/Act1"], Names(YarnProjectFiles.ScriptsOf(project)));
    }

    [Fact]
    public void Patterns_name_folders_and_files_whatever_their_case_and_reach_beside_the_folder()
    {
        var project = Project("Dialogue/Story", sources: ["chatter/*.YARN", "../Shared/Common.yarn", "  "]);
        PackageOf(project, Script("Dialogue/Chatter/Pier"), Script("Dialogue/Act1"), Script("Shared/Common"), Script("Shared/Other"));

        Assert.Equal(["Dialogue/Chatter/Pier", "Shared/Common"], Names(YarnProjectFiles.ScriptsOf(project)));
    }

    [Fact]
    public void A_project_without_a_pattern_or_a_package_has_no_script()
    {
        var empty = Project("Dialogue/Story", sources: []);
        PackageOf(empty, Script("Dialogue/Act1"));
        Assert.Empty(YarnProjectFiles.ScriptsOf(empty));

        Assert.Empty(YarnProjectFiles.ScriptsOf(Project("Dialogue/Alone")));
        Assert.Empty(YarnProjectFiles.ScriptsOf(Script("Dialogue/NotAProject")));
    }

    [Fact]
    public void A_script_knows_the_projects_that_include_it_in_the_order_of_their_names()
    {
        var both = Script("Dialogue/Chatter/Pier");
        var one = Script("Dialogue/Act1");
        var none = Script("Other/X");
        PackageOf(Project("Dialogue/Story"), Project("Dialogue/Chatter/Background"), Project("Dialogue/Alpha", excluded: ["Chatter/**"]), both, one, none);

        Assert.Equal(["Dialogue/Chatter/Background", "Dialogue/Story"], Names(YarnProjectFiles.ProjectsOf(both)));
        Assert.Equal(["Dialogue/Alpha", "Dialogue/Story"], Names(YarnProjectFiles.ProjectsOf(one)));
        Assert.Empty(YarnProjectFiles.ProjectsOf(none));
        Assert.Equal(["Other/X"], Names(YarnProjectFiles.ScriptsInNoProject(none.Package!)));
    }

    [Fact]
    public void A_script_is_named_as_its_file_in_the_package()
    {
        Assert.Equal("Dialogue/Act1.yarn", YarnProjectFiles.FileNameOf(Script("Dialogue/Act1")));
    }
}
