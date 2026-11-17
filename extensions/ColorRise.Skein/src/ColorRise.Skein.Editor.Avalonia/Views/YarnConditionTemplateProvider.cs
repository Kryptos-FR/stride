using ColorRise.Skein.Editor.PropertyGrid;
using Stride.Core.Assets.Editor.Avalonia.Views;
using Stride.Core.Presentation.Quantum.ViewModels;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// Matches a <c>when:</c> header of a Yarn node, which the condition builder edits (<see cref="YarnNodeKeys.ConditionKnowledge"/>).
/// </summary>
public sealed class YarnConditionTemplateProvider : NodeViewModelTemplateProvider
{
    /// <inheritdoc/>
    public override string Name => "YarnCondition";

    /// <inheritdoc/>
    public override bool MatchNode(NodeViewModel node)
        => node.Type == typeof(string) && node.AssociatedData.TryGetValue(YarnNodeKeys.ConditionKnowledge, out var knowledge) && knowledge is not null;
}
