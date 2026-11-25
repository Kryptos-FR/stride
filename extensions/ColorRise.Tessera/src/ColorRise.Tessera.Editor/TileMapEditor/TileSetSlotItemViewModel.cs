using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Palette;
using ColorRise.Tessera.Editor.Pictures;
using Stride.Core.Presentation.ViewModels;

namespace ColorRise.Tessera.Editor.TileMapEditor;

/// <summary>
/// A tile set of the map in the list of the map editor, with the palette its tiles are picked from.
/// </summary>
public sealed class TileSetSlotItemViewModel : DispatcherViewModel
{
    internal TileSetSlotItemViewModel(IViewModelServiceProvider services, TileSetSlot item, TileSetPictures? pictures)
        : base(services)
    {
        Item = item;
        Slot = (ushort)item.Slot;
        Pictures = pictures;
        Name = pictures?.TileSet.Name ?? $"missing (slot {Slot})";
        Palette = pictures is null ? null : new TilePaletteViewModel(services, pictures, Slot);
    }

    /// <summary>
    /// The slot of the map: the cells of the tile set name it.
    /// </summary>
    public ushort Slot { get; }

    /// <summary>
    /// The name of the tile set's asset, or "missing (slot 2)" when it is not in the session.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Whether the tile set is not in the session: its cells show as missing.
    /// </summary>
    public bool IsMissing => Pictures is null;

    /// <summary>
    /// The palette of the tile set; null when it is missing.
    /// </summary>
    public TilePaletteViewModel? Palette { get; }

    /// <summary>
    /// The slot as the asset has it.
    /// </summary>
    internal TileSetSlot Item { get; }

    /// <summary>
    /// The pictures of the tile set; null when it is missing.
    /// </summary>
    internal TileSetPictures? Pictures { get; }

    /// <inheritdoc/>
    public override void Destroy()
    {
        Palette?.Destroy();
        base.Destroy();
    }
}
