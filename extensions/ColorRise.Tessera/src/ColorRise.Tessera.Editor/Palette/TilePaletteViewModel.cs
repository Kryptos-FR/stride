using ColorRise.Tessera.Editor.Canvas;
using ColorRise.Tessera.Editor.Pictures;
using Stride.Core.Presentation.ViewModels;

namespace ColorRise.Tessera.Editor.Palette;

/// <summary>
/// The palette of a map editor for one tile set of the map: a source of the set on a canvas, where a click or a drag picks
/// the cells a tool paints.
/// </summary>
public sealed class TilePaletteViewModel : DispatcherViewModel
{
    /// <summary>
    /// The palette of the tile set in a slot of the map.
    /// </summary>
    public TilePaletteViewModel(IViewModelServiceProvider services, TileSetPictures pictures, ushort slot)
        : base(services)
    {
        ArgumentNullException.ThrowIfNull(pictures);
        Pictures = pictures;
        Slot = slot;
        Scene.SelectionChanged += SceneSelectionChanged;
        pictures.Changed += PicturesChanged;
        Sources = pictures.Sources;
        SelectedSource = Sources.FirstOrDefault();
    }

    /// <summary>
    /// The slot of the map the tile set is in: the cells picked are of this slot.
    /// </summary>
    public ushort Slot { get; }

    /// <summary>
    /// The pictures of the tile set.
    /// </summary>
    public TileSetPictures Pictures { get; }

    /// <summary>
    /// The sources of the tile set, as they are now.
    /// </summary>
    public IReadOnlyList<TileSourceLayout> Sources
    {
        get;
        private set => SetValue(ref field, value, nameof(Sources), nameof(HasSeveralSources));
    } = [];

    /// <summary>
    /// Whether the tile set has more than one source, so that the palette shows a choice of source.
    /// </summary>
    public bool HasSeveralSources => Sources.Count > 1;

    /// <summary>
    /// The source shown; the first one at first.
    /// </summary>
    public TileSourceLayout? SelectedSource
    {
        get;
        set
        {
            if (SetValue(ref field, value))
            {
                Scene.Layout = value;
                Scene.Mode = value is { IsGrid: true } ? TilePickMode.Block : TilePickMode.One;
            }
        }
    }

    /// <summary>
    /// The source on the canvas: a block of a grid source is picked, or one tile of a sheet.
    /// </summary>
    public TileSourceScene Scene { get; } = new();

    /// <summary>
    /// The zoom and the pan of the canvas.
    /// </summary>
    public CanvasView View { get; } = new();

    /// <summary>
    /// What the last click or drag picked, as cells of <see cref="Slot"/>; null until something was picked.
    /// </summary>
    public TileStamp? Picked { get; private set; }

    /// <summary>
    /// Raised when a click or a drag picked, even the same cells again.
    /// </summary>
    public event EventHandler? PickedChanged;

    /// <summary>
    /// Shows a tile: selects its source and the tile, and leaves <see cref="Picked"/> as it is.
    /// </summary>
    public void Show(TileId tile)
    {
        if (Sources.FirstOrDefault(layout => layout.SlotOf(tile) is not null) is not { } layout)
        {
            return;
        }
        SelectedSource = layout;
        Scene.Select([tile]);
    }

    /// <summary>
    /// Stops following the tile set: the pictures live as long as the session.
    /// </summary>
    public override void Destroy()
    {
        Pictures.Changed -= PicturesChanged;
        base.Destroy();
    }

    private void SceneSelectionChanged(object? sender, EventArgs e)
    {
        // A pick that cleared the selection keeps what was picked
        if (Scene.ToStamp(Slot) is not { IsEmpty: false } stamp)
        {
            return;
        }
        Picked = stamp;
        OnPropertyChanged(nameof(Picked));
        PickedChanged?.Invoke(this, EventArgs.Empty);
    }

    // The pictures were made again: the same source stays selected while the tile set has it
    private void PicturesChanged(object? sender, EventArgs e)
    {
        var selected = SelectedSource?.Source;
        Sources = Pictures.Sources;
        SelectedSource = Sources.FirstOrDefault(layout => ReferenceEquals(layout.Source, selected))
            ?? Sources.FirstOrDefault(layout => layout.Source.Id == selected?.Id)
            ?? Sources.FirstOrDefault();
    }
}
