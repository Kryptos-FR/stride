using ColorRise.Tessera.Assets;
using Stride.Core.Assets;
using Stride.Core.Presentation.Quantum;
using Stride.Core.Presentation.ViewModels;
using Stride.Core.Quantum;
using Stride.Core.Reflection;

namespace ColorRise.Tessera.Editor.TileSetEditor;

/// <summary>
/// The data of a selected tile for the property grid: a copy of its definition, with a graph of its own, that the editor
/// writes to the asset after each change. The grid shows one per selected tile, several at once.
/// </summary>
public sealed class TileDataViewModel : DispatcherViewModel, IPropertyProviderViewModel
{
    private readonly IObjectNode rootNode;
    private readonly GraphNodeChangeListener listener;

    internal TileDataViewModel(IViewModelServiceProvider serviceProvider, NodeContainer container, TileId tile, TileDefinition? definition)
        : base(serviceProvider)
    {
        Tile = tile;
        // A tile without data has a default copy
        Data = definition is null ? new TileDefinition { Tile = tile } : AssetCloner.Clone(definition);
        rootNode = container.GetOrCreateNode(Data);
        listener = new GraphNodeChangeListener(rootNode);
        listener.Initialize();
        listener.ValueChanged += DataChanged;
        listener.ItemChanged += DataChanged;
    }

    /// <summary>
    /// The tile whose data this is.
    /// </summary>
    public TileId Tile { get; }

    /// <summary>
    /// The copy that the property grid edits.
    /// </summary>
    internal TileDefinition Data { get; }

    /// <summary>
    /// Raised when the property grid changed the copy, inside the grid's own transaction.
    /// </summary>
    internal event EventHandler? Changed;

    bool IPropertyProviderViewModel.CanProvidePropertiesViewModel => !IsDestroyed;

    /// <summary>
    /// The definition to put in the asset: a copy of the data, without the properties that have no value yet. A property
    /// added in the grid has no value until its type is chosen.
    /// </summary>
    internal TileDefinition ToDefinition()
    {
        var definition = AssetCloner.Clone(Data);
        definition.Tile = Tile;
        foreach (var name in definition.Properties.Where(property => property.Value is null).Select(property => property.Key).ToList())
        {
            definition.Properties.Remove(name);
            if (CollectionItemIdHelper.TryGetCollectionItemIds(definition.Properties, out var ids) && ids.ContainsKey(name))
            {
                ids.Delete(name, markAsDeleted: false);
            }
        }
        return definition;
    }

    /// <inheritdoc/>
    public override void Destroy()
    {
        listener.ValueChanged -= DataChanged;
        listener.ItemChanged -= DataChanged;
        listener.Dispose();
        base.Destroy();
    }

    IObjectNode IPropertyProviderViewModel.GetRootNode() => rootNode;

    // The grid does not show the animation of the tile: the copy keeps it, and writes it back as it was
    bool IPropertyProviderViewModel.ShouldConstructMember(IMemberNode member)
        => !(member.Parent.Type == typeof(TileDefinition) && member.Name == nameof(TileDefinition.Animation));

    bool IPropertyProviderViewModel.ShouldConstructItem(IObjectNode collection, NodeIndex index) => true;

    private void DataChanged(object? sender, INodeChangeEventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}
