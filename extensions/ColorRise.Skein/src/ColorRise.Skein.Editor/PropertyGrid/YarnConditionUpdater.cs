using ColorRise.Skein.Editor.Nodes;
using Stride.Core.Presentation.Quantum.Presenters;

namespace ColorRise.Skein.Editor.PropertyGrid;

/// <summary>
/// Gives each <c>when:</c> header of the nodes the property grid shows what the condition builder offers: the
/// variables, functions and nodes of the project of their script.
/// </summary>
public sealed class YarnConditionUpdater : NodePresenterUpdaterBase
{
    /// <inheritdoc/>
    public override void UpdateNode(INodePresenter node)
    {
        if (node.Type == typeof(string) && node.Parent is { Name: nameof(YarnNodeHeaders.When) } when && when.Parent?.Value is YarnNodeHeaders { Knowledge: { } knowledge })
        {
            node.AttachedProperties.Set(YarnNodeKeys.ConditionKnowledgeKey, knowledge);
        }
    }
}
