using ColorRise.Skein.Editor.Conditions;
using Stride.Core;

namespace ColorRise.Skein.Editor.PropertyGrid;

/// <summary>
/// What <see cref="YarnNodeUpdater"/> attaches to the property of a component that names a Yarn node, and
/// <see cref="YarnConditionUpdater"/> to a <c>when:</c> header.
/// </summary>
public static class YarnNodeKeys
{
    /// <summary>
    /// The name of <see cref="NodeNamesKey"/> in the associated data of the property's view model.
    /// </summary>
    public const string NodeNames = nameof(NodeNames);

    /// <summary>
    /// The titles of the nodes of the project the property takes its node from, kept current.
    /// </summary>
    public static readonly PropertyKey<IReadOnlyList<string>> NodeNamesKey = new(NodeNames, typeof(YarnNodeKeys));

    /// <summary>
    /// The name of <see cref="ConditionKnowledgeKey"/> in the associated data of the property's view model.
    /// </summary>
    public const string ConditionKnowledge = nameof(ConditionKnowledge);

    /// <summary>
    /// What the condition builder offers for a <c>when:</c> header of a node: the variables, functions and nodes of its project.
    /// </summary>
    public static readonly PropertyKey<YarnConditionKnowledge> ConditionKnowledgeKey = new(ConditionKnowledge, typeof(YarnNodeKeys));
}
