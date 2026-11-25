using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Pictures;
using Stride.Core.Assets.Presentation.Components.Properties;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Presentation.Quantum;
using Stride.Core.Presentation.ViewModels;
using Stride.Core.Quantum;

namespace ColorRise.Tessera.Editor.TileSetEditor;

/// <summary>
/// A source in the list of the tile set editor, and what the property grid shows for it.
/// </summary>
public sealed class TileSourceItemViewModel : DispatcherViewModel, IAssetPropertyProviderViewModel
{
    private readonly TileSetEditorViewModel editor;

    internal TileSourceItemViewModel(TileSetEditorViewModel editor, TileSetSource source)
        : base(editor.ServiceProvider)
    {
        this.editor = editor;
        Source = source;
        Id = source.Id;
        Name = source.Name;
    }

    /// <summary>
    /// The source of the asset.
    /// </summary>
    public TileSetSource Source { get; }

    /// <summary>
    /// The id of the source, in the ids of its tiles.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// The name of the source, as the asset has it.
    /// </summary>
    public string Name
    {
        get;
        internal set => SetValue(ref field, value);
    } = "";

    /// <summary>
    /// Whether the source is an image cut on a grid, rather than a sprite sheet.
    /// </summary>
    public bool IsGrid => Source is GridTileSetSource;

    /// <summary>
    /// The source as the editors show it; null until its pictures are made.
    /// </summary>
    public TileSourceLayout? Layout
    {
        get;
        internal set => SetValue(ref field, value, nameof(Layout), nameof(Problem));
    }

    /// <summary>
    /// Why the source, or some of its tiles, have no picture; null when nothing is wrong.
    /// </summary>
    public string? Problem => Layout?.Problem;

    bool IPropertyProviderViewModel.CanProvidePropertiesViewModel => !IsDestroyed;

    AssetViewModel IAssetPropertyProviderViewModel.RelatedAsset => editor.Asset;

    IObjectNode? IPropertyProviderViewModel.GetRootNode() => editor.Session.AssetNodeContainer.GetNode(Source);

    GraphNodePath IAssetPropertyProviderViewModel.GetAbsolutePathToRootNode()
    {
        var path = new GraphNodePath(editor.Asset.PropertyGraph.RootNode);
        path.PushMember(nameof(TileSetAsset.Sources));
        path.PushIndex(new NodeIndex(editor.Asset.Asset.Sources.IndexOf(Source)));
        path.PushTarget();
        return path;
    }

    // Id, Bindings and LastNumber are not browsable: the editor gives them their values
    bool IPropertyProviderViewModel.ShouldConstructMember(IMemberNode member) => ((IPropertyProviderViewModel)editor.Asset).ShouldConstructMember(member);

    bool IPropertyProviderViewModel.ShouldConstructItem(IObjectNode collection, NodeIndex index)
        => ((IPropertyProviderViewModel)editor.Asset).ShouldConstructItem(collection, index);
}
