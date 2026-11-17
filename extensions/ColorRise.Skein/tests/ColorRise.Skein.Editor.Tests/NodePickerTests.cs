using ColorRise.Skein.Editor.PropertyGrid;
using ColorRise.Skein.Editor.Text;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Core;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Presentation.Quantum;
using Stride.Core.Presentation.Quantum.ViewModels;
using Stride.Core.Quantum;
using Stride.Core.Serialization;

namespace ColorRise.Skein.Editor.Tests;

/// <summary>
/// A string member that names a Yarn node (<see cref="YarnNodeAttribute"/>) gets the nodes of its project to pick from in
/// the property grid.
/// </summary>
public sealed class NodePickerTests
{
    private const string Act1 = "title: Arrival\nquest: letters\n---\nNarrator: Here.\n<<jump Pier>>\n===\ntitle: Pier\nquest: Lamp\n---\nNarrator: The pier.\n===\ntitle: Todo\nstyle: note\n---\nLater.\n===\n";
    private const string Act2 = "title: Lighthouse\nwhen: always\n---\nUp.\n===\ntitle: Lighthouse\nwhen: once\n---\nUp again.\n===\n";

    // The second project is in a folder without a script
    private static async DispatcherTask<SkeinSession> OpenAsync()
        => await SkeinSession.OpenAsync(
            SkeinSession.Project("Dialogue/Story"),
            SkeinSession.Project("Other/Empty"),
            SkeinSession.Script("Dialogue/Act1", Act1),
            SkeinSession.Script("Dialogue/Act2", Act2));

    [Fact]
    public async Task The_start_node_of_a_runner_is_picked_among_the_nodes_of_its_project()
    {
        await using var session = await OpenAsync();
        var runner = new DialogueRunnerComponent { Project = Reference(session.ProjectAt("Dialogue/Story")), StartNode = "Arrival" };

        var grid = await ShowAsync(session, runner);
        await session.Analysis.WhenIdle();

        // The nodes of every script of the project, a node group once, without the notes of the writer
        var names = NodeNames(grid, nameof(DialogueRunnerComponent.StartNode));
        Assert.NotNull(names);
        Assert.Equal(["Arrival", "Lighthouse", "Pier"], names);
        Assert.Null(NodeNames(grid, nameof(DialogueRunnerComponent.AutoStart)));

        // The list follows the scripts as they are typed
        session.ScriptAt("Dialogue/Act1").TextBuffer.Replace(0, 0, "title: Dawn\n---\nEarly.\n===\n", YarnTextBuffer.TypingActionName);
        await session.Analysis.WhenIdle();
        Assert.Equal(["Arrival", "Dawn", "Lighthouse", "Pier"], names);
        Assert.Same(names, NodeNames(grid, nameof(DialogueRunnerComponent.StartNode)));
    }

    [Fact]
    public async Task The_nodes_follow_the_project_of_the_component()
    {
        await using var session = await OpenAsync();
        var runner = new DialogueRunnerComponent { Project = Reference(session.ProjectAt("Dialogue/Story")) };
        var grid = await ShowAsync(session, runner);
        await session.Analysis.WhenIdle();
        Assert.Equal(3, NodeNames(grid, nameof(DialogueRunnerComponent.StartNode))?.Count);

        // Another project: its nodes
        Child(grid, nameof(DialogueRunnerComponent.Project)).NodeValue = Reference(session.ProjectAt("Other/Empty"));
        await session.Analysis.WhenIdle();
        Assert.Empty(NodeNames(grid, nameof(DialogueRunnerComponent.StartNode))!);

        // No project: nothing to pick from, the property is a text
        Child(grid, nameof(DialogueRunnerComponent.Project)).NodeValue = null;
        Assert.Null(NodeNames(grid, nameof(DialogueRunnerComponent.StartNode)));
    }

    [Fact]
    public async Task A_component_of_the_game_marks_its_own_members()
    {
        await using var session = await OpenAsync();
        var quest = new QuestGiver { Story = Reference(session.ProjectAt("Dialogue/Story")) };

        var grid = await ShowAsync(session, quest);
        await session.Analysis.WhenIdle();

        Assert.Equal(["Arrival", "Lighthouse", "Pier"], NodeNames(grid, nameof(QuestGiver.Offer)));
        Assert.Equal(["Arrival", "Lighthouse", "Pier"], NodeNames(grid, nameof(QuestGiver.Thanks)));
        // A member whose attribute names no project of the object is a text
        Assert.Null(NodeNames(grid, nameof(QuestGiver.Lost)));
        Assert.Null(NodeNames(grid, nameof(QuestGiver.Giver)));
    }

    [Fact]
    public async Task A_member_can_list_only_the_nodes_whose_header_passes_its_filter()
    {
        await using var session = await OpenAsync();
        var quest = new QuestGiver { Story = Reference(session.ProjectAt("Dialogue/Story")) };

        var grid = await ShowAsync(session, quest);
        await session.Analysis.WhenIdle();

        // A header of the nodes, without regard to case; a node group passes when one of its nodes does
        Assert.Equal(["Pier"], NodeNames(grid, nameof(QuestGiver.Lamp)));
        Assert.Equal(["Lighthouse"], NodeNames(grid, nameof(QuestGiver.Once)));

        // The title by default, and the other ways of the filter
        Assert.Equal(["Arrival", "Lighthouse"], NodeNames(grid, nameof(QuestGiver.WithL)));
        Assert.Equal(["Pier"], NodeNames(grid, nameof(QuestGiver.EndsInR)));
        Assert.Equal(["Lighthouse"], NodeNames(grid, nameof(QuestGiver.Lights)));
        Assert.Empty(NodeNames(grid, nameof(QuestGiver.Broken))!);

        // The filtered list follows the scripts too
        session.ScriptAt("Dialogue/Act1").TextBuffer.Replace(0, 0, "title: Shed\nquest: lamp\n---\nOil.\n===\n", YarnTextBuffer.TypingActionName);
        await session.Analysis.WhenIdle();
        Assert.Equal(["Pier", "Shed"], NodeNames(grid, nameof(QuestGiver.Lamp)));
        Assert.Equal(["Arrival", "Lighthouse", "Pier", "Shed"], NodeNames(grid, nameof(QuestGiver.Offer)));
    }

    private static YarnProject Reference(YarnProjectViewModel project) => AttachedReferenceManager.CreateProxyObject<YarnProject>(project.Id, project.Url);

    private static async Task<GraphViewModel> ShowAsync(SkeinSession session, object shown)
    {
        var properties = session.Session.AssetViewProperties;
        var grid = await properties.GenerateSelectionPropertiesAsync([new Provider(session.Session, shown)]);
        Assert.True(properties.CanDisplayProperties, properties.FallbackMessage);
        return grid!;
    }

    private static IReadOnlyList<string>? NodeNames(GraphViewModel grid, string member)
        => Child(grid, member).AssociatedData.GetValueOrDefault(YarnNodeKeys.NodeNames) as IReadOnlyList<string>;

    private static NodeViewModel Child(GraphViewModel grid, string name) => grid.RootNode.Children.Single(x => x.Name == name);

    /// <summary>
    /// What a game writes: a component with nodes of a story to start.
    /// </summary>
    [DataContract]
    public sealed class QuestGiver
    {
        public string Giver { get; set; } = "";

        public YarnProject? Story { get; set; }

        [YarnNode(nameof(Story))]
        public string Offer { get; set; } = "";

        [YarnNode(nameof(Story))]
        public string Thanks { get; set; } = "";

        [YarnNode("Missing")]
        public string Lost { get; set; } = "";

        [YarnNode(nameof(Story), Filter = "lamp", FilterHeader = "quest")]
        public string Lamp { get; set; } = "";

        [YarnNode(nameof(Story), Filter = "once", FilterHeader = "when")]
        public string Once { get; set; } = "";

        [YarnNode(nameof(Story), Filter = "l")]
        public string WithL { get; set; } = "";

        [YarnNode(nameof(Story), Filter = "R", FilterType = YarnNodeFilter.EndsWith)]
        public string EndsInR { get; set; } = "";

        [YarnNode(nameof(Story), Filter = "^Li", FilterType = YarnNodeFilter.MatchesRegex)]
        public string Lights { get; set; } = "";

        [YarnNode(nameof(Story), Filter = "(", FilterType = YarnNodeFilter.MatchesRegex)]
        public string Broken { get; set; } = "";
    }

    /// <summary>
    /// Gives the property grid an object that is no asset, as the scene editor gives it an entity.
    /// </summary>
    private sealed class Provider(ISessionViewModel session, object shown) : IPropertyProviderViewModel
    {
        private readonly IObjectNode root = session.AssetNodeContainer.GetOrCreateNode(shown);

        bool IPropertyProviderViewModel.CanProvidePropertiesViewModel => true;

        IObjectNode IPropertyProviderViewModel.GetRootNode() => root;

        bool IPropertyProviderViewModel.ShouldConstructMember(IMemberNode member) => true;

        bool IPropertyProviderViewModel.ShouldConstructItem(IObjectNode collection, NodeIndex index) => true;
    }
}
