using Stride.Core;

namespace ColorRise.Skein.Editor.PropertyGrid;

/// <summary>
/// What <see cref="YarnNodeUpdater"/> attaches to the property of a component that names a Yarn node.
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
}
