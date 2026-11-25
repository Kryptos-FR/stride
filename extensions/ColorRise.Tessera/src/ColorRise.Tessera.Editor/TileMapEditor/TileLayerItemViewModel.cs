using ColorRise.Tessera.Assets;
using Stride.Core.Assets.Presentation.Components.Properties;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Presentation.Quantum;
using Stride.Core.Presentation.ViewModels;
using Stride.Core.Quantum;

namespace ColorRise.Tessera.Editor.TileMapEditor;

/// <summary>
/// A layer in the list of the map editor, and what the property grid shows for it.
/// </summary>
public sealed class TileLayerItemViewModel : DispatcherViewModel, IAssetPropertyProviderViewModel
{
    private readonly TileMapEditorViewModel editor;

    internal TileLayerItemViewModel(TileMapEditorViewModel editor, TileLayerAsset layer)
        : base(editor.ServiceProvider)
    {
        this.editor = editor;
        Layer = layer;
    }

    /// <summary>
    /// The layer of the asset.
    /// </summary>
    public TileLayerAsset Layer { get; }

    /// <summary>
    /// The name of the layer. A new name is one step "Rename layer"; an empty name, or the name of another layer, is refused
    /// and the editor's message says why.
    /// </summary>
    public string Name
    {
        get => Layer.Name;
        set
        {
            editor.RenameLayer(Layer, value);
            Refresh();
        }
    }

    /// <summary>
    /// Whether the name is being typed in the list.
    /// </summary>
    public bool IsEditing
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Whether the layer is drawn: the asset's <see cref="TileLayerAsset.Visible"/>, changed as one step "Layer visibility".
    /// </summary>
    public bool IsVisible
    {
        get => Layer.Visible;
        set
        {
            editor.SetLayerVisible(Layer, value);
            Refresh();
        }
    }

    /// <summary>
    /// Whether no tool changes the cells of the layer. The editor keeps it in its settings, not in the asset.
    /// </summary>
    public bool IsLocked
    {
        get => editor.IsLayerLocked(Layer);
        set
        {
            editor.LockLayer(Layer, value);
            Refresh();
        }
    }

    bool IPropertyProviderViewModel.CanProvidePropertiesViewModel => !IsDestroyed;

    AssetViewModel IAssetPropertyProviderViewModel.RelatedAsset => editor.Asset;

    /// <summary>
    /// Tells the views that the layer's name, visibility or lock may have changed.
    /// </summary>
    internal void Refresh() => OnPropertyChanged(nameof(Name), nameof(IsVisible), nameof(IsLocked));

    IObjectNode? IPropertyProviderViewModel.GetRootNode() => editor.Session.AssetNodeContainer.GetNode(Layer);

    GraphNodePath IAssetPropertyProviderViewModel.GetAbsolutePathToRootNode()
    {
        var path = new GraphNodePath(editor.Asset.PropertyGraph.RootNode);
        path.PushMember(nameof(TileMapAsset.Layers));
        path.PushIndex(new NodeIndex(editor.Asset.Asset.Layers.IndexOf(Layer)));
        path.PushTarget();
        return path;
    }

    // Chunks is not browsable: the editor paints them
    bool IPropertyProviderViewModel.ShouldConstructMember(IMemberNode member) => ((IPropertyProviderViewModel)editor.Asset).ShouldConstructMember(member);

    bool IPropertyProviderViewModel.ShouldConstructItem(IObjectNode collection, NodeIndex index)
        => ((IPropertyProviderViewModel)editor.Asset).ShouldConstructItem(collection, index);
}
