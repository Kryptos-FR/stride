using ColorRise.Skein.Editor.Services;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Extensions;
using Stride.Core.Presentation.Quantum.Presenters;
using Stride.Core.Quantum;
using Stride.Core.Serialization;

namespace ColorRise.Skein.Editor.PropertyGrid;

/// <summary>
/// Gives the property grid the node names to pick from, for a string member marked <see cref="YarnNodeAttribute"/>: the
/// nodes of the Yarn project that the member named by the attribute refers to, in the same object.
/// </summary>
public sealed class YarnNodeUpdater : NodePresenterUpdaterBase
{
    private readonly ISessionViewModel session;

    /// <summary>
    /// Makes the updater of a session.
    /// </summary>
    public YarnNodeUpdater(ISessionViewModel session)
    {
        this.session = session;
    }

    /// <inheritdoc/>
    public override void FinalizeTree(INodePresenter root)
    {
        var members = new List<(MemberNodePresenter Presenter, IMemberNode Member)>();
        foreach (var presenter in root.Yield().Concat(root.Children.DepthFirst(x => x.Children)).OfType<MemberNodePresenter>())
        {
            if (presenter.GetNodeAccessor().Node is IMemberNode member)
            {
                members.Add((presenter, member));
            }
        }

        foreach (var (node, member) in members)
        {
            if (node.Type != typeof(string) || node.MemberAttributes.OfType<YarnNodeAttribute>().FirstOrDefault() is not { } attribute)
                continue;

            // The member of the same object, wherever the grid shows it (a category moves a property)
            if (members.Find(m => m.Member.Parent == member.Parent && m.Member.Name == attribute.ProjectMember).Presenter is not { } project)
                continue;

            // Another project gives other nodes
            node.AddDependency(project, false);
            if (ProjectOf(project.Value) is { } viewModel && session.ServiceProvider.TryGet<YarnAnalysisService>() is { } analysis)
            {
                node.AttachedProperties.Set(YarnNodeKeys.NodeNamesKey, analysis.NodeNamesOf(viewModel));
            }
        }
    }

    // A component refers to a project through a stand-in object that carries the id of the asset
    private YarnProjectViewModel? ProjectOf(object? reference)
        => reference is YarnProject && AttachedReferenceManager.GetAttachedReference(reference) is { } attached
            ? session.GetAssetById(attached.Id) as YarnProjectViewModel
            : null;
}
