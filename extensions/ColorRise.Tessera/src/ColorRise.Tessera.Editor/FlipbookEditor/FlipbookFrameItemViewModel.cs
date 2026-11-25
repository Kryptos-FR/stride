using System.Globalization;
using Stride.Core.Assets.Presentation.Components.Properties;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Presentation.Quantum;
using Stride.Core.Presentation.ViewModels;
using Stride.Core.Quantum;

namespace ColorRise.Tessera.Editor.FlipbookEditor;

/// <summary>
/// A frame of the selected clip in the strip of the flipbook editor, and what the property grid shows for it.
/// </summary>
public sealed class FlipbookFrameItemViewModel : DispatcherViewModel, IAssetPropertyProviderViewModel
{
    private readonly FlipbookEditorViewModel editor;

    internal FlipbookFrameItemViewModel(FlipbookEditorViewModel editor, FlipbookClipItemViewModel clip, FlipbookFrame frame)
        : base(editor.ServiceProvider)
    {
        this.editor = editor;
        Clip = clip;
        Frame = frame;
    }

    /// <summary>
    /// The frame of the asset.
    /// </summary>
    public FlipbookFrame Frame { get; }

    /// <summary>
    /// Its index in the frames of the clip.
    /// </summary>
    public int Index
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// Its number, from 1, as the strip shows it and the problems name it.
    /// </summary>
    public string Label
    {
        get;
        private set => SetValue(ref field, value);
    } = "";

    /// <summary>
    /// How long it is shown, in seconds.
    /// </summary>
    public float Duration => Frame.Duration;

    /// <summary>
    /// The tile of its first cell; null for a frame without a cell.
    /// </summary>
    public TileId? Tile => Frame.Cells.FirstOrDefault(cell => cell is not null)?.Tile;

    /// <summary>
    /// Whether a cell names a tile the tile set does not have.
    /// </summary>
    public bool HasMissing
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// The item of the clip the frame is in.
    /// </summary>
    internal FlipbookClipItemViewModel Clip { get; }

    bool IPropertyProviderViewModel.CanProvidePropertiesViewModel => !IsDestroyed;

    AssetViewModel IAssetPropertyProviderViewModel.RelatedAsset => editor.Asset;

    /// <summary>
    /// Follows the frame: its place among the frames of the clip, its number, and whether a tile of it is missing.
    /// </summary>
    internal void Follow(int index, int number, bool hasMissing)
    {
        Index = index;
        Label = number.ToString(CultureInfo.InvariantCulture);
        HasMissing = hasMissing;
        OnPropertyChanged(nameof(Duration), nameof(Tile));
    }

    IObjectNode? IPropertyProviderViewModel.GetRootNode() => editor.Session.AssetNodeContainer.GetNode(Frame);

    GraphNodePath IAssetPropertyProviderViewModel.GetAbsolutePathToRootNode() => editor.PathOf(Clip.Animation.Animation, Clip.Clip, Frame);

    bool IPropertyProviderViewModel.ShouldConstructMember(IMemberNode member) => FlipbookEditorViewModel.ShowsMember(member);

    bool IPropertyProviderViewModel.ShouldConstructItem(IObjectNode collection, NodeIndex index) => true;
}
