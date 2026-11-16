using ColorRise.Skein.Editor.PropertyGrid;
using Stride.Core.Assets.Editor.Avalonia.Views;
using Stride.Core.Presentation.Quantum.ViewModels;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// Matches a property that names a Yarn node and has the nodes of its project to pick from (<see cref="YarnNodeKeys.NodeNames"/>).
/// </summary>
public sealed class YarnNodeTemplateProvider : NodeViewModelTemplateProvider
{
    /// <inheritdoc/>
    public override string Name => "YarnNode";

    /// <inheritdoc/>
    public override bool MatchNode(NodeViewModel node)
    {
        // Several components with different projects have no list in common: the property is a text then
        return node.Type == typeof(string) && node.AssociatedData.TryGetValue(YarnNodeKeys.NodeNames, out var names) && names is IEnumerable<string>;
    }
}
