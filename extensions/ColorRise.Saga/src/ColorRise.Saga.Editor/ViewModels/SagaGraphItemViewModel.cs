using Stride.Core.Assets.Presentation.Components.Properties;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Presentation.Quantum;
using Stride.Core.Presentation.ViewModels;
using Stride.Core.Quantum;

namespace ColorRise.Saga.Editor.ViewModels;

/// <summary>
/// A node or link of a Saga graph: when it stands for a card, selecting it shows the card in the property grid.
/// </summary>
public abstract class SagaGraphItemViewModel : DispatcherViewModel, IAssetPropertyProviderViewModel
{
    private readonly IObjectNode? cardNode;
    private readonly Func<GraphNodePath>? path;

    /// <param name="serviceProvider">The services of the editor.</param>
    /// <param name="relatedAsset">The asset the card belongs to, whose undo history and dirty flag its changes go to.</param>
    /// <param name="card">The card the item stands for, or <c>null</c>.</param>
    /// <param name="path">The path from the asset to the card.</param>
    protected SagaGraphItemViewModel(IViewModelServiceProvider serviceProvider, AssetViewModel relatedAsset, object? card, Func<GraphNodePath>? path)
        : base(serviceProvider)
    {
        RelatedAsset = relatedAsset;
        Card = card;
        this.path = path;
        cardNode = card is null || path is null ? null : relatedAsset.Session.AssetNodeContainer.GetOrCreateNode(card);
    }

    /// <summary>
    /// The card the item stands for, or <c>null</c>.
    /// </summary>
    public object? Card { get; }

    /// <inheritdoc />
    public AssetViewModel RelatedAsset { get; }

    /// <inheritdoc />
    public bool CanProvidePropertiesViewModel => cardNode is not null;

    /// <inheritdoc />
    public IObjectNode? GetRootNode() => cardNode;

    /// <inheritdoc />
    public GraphNodePath GetAbsolutePathToRootNode() => path?.Invoke() ?? throw new InvalidOperationException("The item stands for no card.");

    /// <inheritdoc />
    public bool ShouldConstructMember(IMemberNode member) => ((IPropertyProviderViewModel)RelatedAsset).ShouldConstructMember(member);

    /// <inheritdoc />
    public bool ShouldConstructItem(IObjectNode collection, NodeIndex index) => ((IPropertyProviderViewModel)RelatedAsset).ShouldConstructItem(collection, index);
}
