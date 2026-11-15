using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Editor.Services;
using Stride.Core.Presentation.Quantum.ViewModels;

namespace ColorRise.Skein.Editor.Tests;

/// <summary>
/// The session compiles its Yarn projects in the background, from the scripts as they are in memory.
/// </summary>
public sealed class AnalysisServiceTests
{
    private const string Act1 = "title: Start\n---\nOdile: The lamp's out again.\n<<jump Pier>>\n===\n";
    private const string Pier = "title: Pier\n---\nFisher: Mind the nets.\n===\n";

    [Fact]
    public async Task A_project_is_compiled_with_the_scripts_it_includes()
    {
        await using var session = await SkeinSession.OpenAsync(
            SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1), SkeinSession.Script("Dialogue/Chatter/Pier", Pier), SkeinSession.Script("Other/Lost", Pier));
        var analysis = session.Analysis;
        var project = session.ProjectAt("Dialogue/Story");
        var changes = new List<YarnProjectAnalysis>();
        analysis.Changed += changes.Add;

        // Nothing is there before someone asks; asking starts the first compilation
        Assert.Null(analysis.AnalysisOf(project));
        await analysis.WhenIdle();

        var result = Assert.Single(changes);
        Assert.Same(result, analysis.AnalysisOf(project));
        Assert.Same(project, result.Project);
        Assert.Equal(["Dialogue/Act1.yarn", "Dialogue/Chatter/Pier.yarn"], result.Scripts.Select(s => s.FileName));
        Assert.Empty(result.Diagnostics);
        Assert.Equal(["Pier", "Start"], result.Nodes.Select(n => n.Title).Order());
        Assert.False(result.NodesAreOlder);
        Assert.NotNull(result.Build.Project);
        Assert.Same(session.ScriptAt("Dialogue/Act1"), result.ScriptOf("Dialogue/Act1.yarn"));
        Assert.Equal("Dialogue/Chatter/Pier.yarn", result.FileNameOf(session.ScriptAt("Dialogue/Chatter/Pier")));

        // A script of the project gives the analysis of its project; asking again compiles nothing
        Assert.Same(result, analysis.AnalysisOf(session.ScriptAt("Dialogue/Act1")));
        await analysis.WhenIdle();
        Assert.Single(changes);
    }

    [Fact]
    public async Task Changes_in_a_row_are_compiled_once_and_an_error_keeps_the_nodes_of_before()
    {
        await using var session = await SkeinSession.OpenAsync(SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1), SkeinSession.Script("Dialogue/Pier", Pier));
        var analysis = session.Analysis;
        // No compilation starts by itself in this test
        analysis.Delay = TimeSpan.FromHours(1);
        var script = session.ScriptAt("Dialogue/Act1");
        analysis.AnalysisOf(script);
        await analysis.WhenIdle();
        var changes = new List<YarnProjectAnalysis>();
        analysis.Changed += changes.Add;

        var jump = Act1.IndexOf("<<jump", StringComparison.Ordinal);
        script.TextBuffer.Replace(jump, 0, "<<if $lamp", "Typing");
        script.TextBuffer.Replace(jump + 10, 0, " == ", "Typing");
        Assert.Empty(changes);
        await analysis.WhenIdle();

        var broken = Assert.Single(changes);
        var errors = broken.Diagnostics.Where(d => d.Severity == YarnSeverity.Error).ToList();
        Assert.NotEmpty(errors);
        Assert.All(errors, e => Assert.Equal("Dialogue/Act1.yarn", e.File));
        Assert.Contains(errors, e => e.Line == 4);
        Assert.True(broken.NodesAreOlder);
        Assert.Equal(["Pier", "Start"], broken.Nodes.Select(n => n.Title).Order());
        Assert.Null(broken.Build.Project);

        session.UndoRedo.Undo();
        session.UndoRedo.Undo();
        await analysis.WhenIdle();

        Assert.Equal(2, changes.Count);
        Assert.Empty(changes[1].Diagnostics);
        Assert.False(changes[1].NodesAreOlder);
    }

    [Fact]
    public async Task A_script_in_no_project_is_compiled_alone_and_one_in_two_goes_with_the_first()
    {
        await using var session = await SkeinSession.OpenAsync(
            SkeinSession.Project("Dialogue/Story"), SkeinSession.Project("Dialogue/Chatter/Background"),
            SkeinSession.Script("Dialogue/Act1", Act1), SkeinSession.Script("Dialogue/Chatter/Pier", Pier), SkeinSession.Script("Other/Lost", Act1));
        var analysis = session.Analysis;
        var lost = session.ScriptAt("Other/Lost");
        var pier = session.ScriptAt("Dialogue/Chatter/Pier");

        Assert.Empty(analysis.ProjectsOf(lost));
        Assert.Equal([session.ProjectAt("Dialogue/Chatter/Background"), session.ProjectAt("Dialogue/Story")], analysis.ProjectsOf(pier));
        analysis.AnalysisOf(lost);
        analysis.AnalysisOf(pier);
        await analysis.WhenIdle();

        var alone = analysis.AnalysisOf(lost)!;
        Assert.Null(alone.Project);
        Assert.Equal(["Other/Lost.yarn"], alone.Scripts.Select(s => s.FileName));
        // Alone, the jump to a node of another file goes nowhere: Yarn Spinner's warning, not an error
        var warning = Assert.Single(alone.Diagnostics);
        Assert.Equal(("YS0012", YarnSeverity.Warning), (warning.Code, warning.Severity));

        var shared = analysis.AnalysisOf(pier)!;
        Assert.Same(session.ProjectAt("Dialogue/Chatter/Background"), shared.Project);
        Assert.Equal(["Dialogue/Chatter/Pier.yarn"], shared.Scripts.Select(s => s.FileName));
    }

    [Fact]
    public async Task A_change_of_the_project_compiles_it_again()
    {
        await using var session = await SkeinSession.OpenAsync(SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1), SkeinSession.Script("Dialogue/Pier", Pier));
        var analysis = session.Analysis;
        var project = session.ProjectAt("Dialogue/Story");
        analysis.AnalysisOf(project);
        await analysis.WhenIdle();
        Assert.Equal(2, analysis.AnalysisOf(project)!.Scripts.Count);

        var grid = (await session.Session.AssetViewProperties.GenerateSelectionPropertiesAsync([project]))!;
        grid.RootNode.Children.Single(x => x.Name == "SourceFiles").Children.Single().NodeValue = "Act*.yarn";
        await Stride.Core.Assets.Editor.Testing.ExclusiveDispatcherService.WaitUntilAsync(
            () => analysis.AnalysisOf(project)!.Scripts.Count == 1, "the project to compile again with its new source files");

        // The jump to Pier now goes nowhere, which a new project makes an error
        var result = analysis.AnalysisOf(project)!;
        Assert.Equal(["Dialogue/Act1.yarn"], result.Scripts.Select(s => s.FileName));
        Assert.Equal("YS0012", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task A_session_that_closes_does_not_wait_for_the_delay()
    {
        var session = await SkeinSession.OpenAsync(SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1));
        var analysis = session.Analysis;
        analysis.Delay = TimeSpan.FromHours(1);
        analysis.AnalysisOf(session.ProjectAt("Dialogue/Story"));

        await session.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));

        // Closed, the service starts nothing
        analysis.Invalidate();
        await analysis.WhenIdle();
    }
}
