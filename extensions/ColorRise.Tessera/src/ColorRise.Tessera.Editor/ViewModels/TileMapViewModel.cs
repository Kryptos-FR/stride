using ColorRise.Tessera.Assets;
using Stride.Core.Assets.Presentation.Annotations;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Quantum;

namespace ColorRise.Tessera.Editor.ViewModels;

/// <summary>
/// A tile map in the session.
/// </summary>
[AssetViewModel<TileMapAsset>]
public sealed class TileMapViewModel : AssetViewModel<TileMapAsset>
{
    /// <summary>
    /// Makes the view model of a tile map asset.
    /// </summary>
    public TileMapViewModel(AssetViewModelConstructionParameters parameters)
        : base(parameters)
    {
    }

    /// <summary>
    /// The property grid has no node for the chunks of a layer: they are hidden, and each stroke would rebuild their nodes
    /// while the grid may be building on another thread.
    /// </summary>
    protected override bool ShouldConstructPropertyMember(IMemberNode member)
        => !(member.Name == nameof(TileLayerAsset.Chunks) && member.Parent.Type == typeof(TileLayerAsset)) && base.ShouldConstructPropertyMember(member);
}
