using System.Globalization;
using ColorRise.Tessera.Editor.Pictures;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.ViewModels;

namespace ColorRise.Tessera.Editor.TileSetEditor;

/// <summary>
/// One frame of the animated tile of the tile set editor, as the bar of Animation mode shows it.
/// </summary>
public sealed class TileFrameItemViewModel : DispatcherViewModel
{
    internal TileFrameItemViewModel(IViewModelServiceProvider serviceProvider, int index, TileAnimationFrame frame, TileSlot? slot)
        : base(serviceProvider)
    {
        Index = index;
        Label = (index + 1).ToString(CultureInfo.InvariantCulture);
        Tile = frame.Tile;
        Duration = frame.Duration;
        Picture = slot?.Picture;
        Region = slot?.Region ?? default;
    }

    /// <summary>
    /// Its place among the frames, from 0.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Its number, from 1, as the canvas writes it on its tile.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// The tile it shows.
    /// </summary>
    public TileId Tile { get; }

    /// <summary>
    /// How long it is shown, in seconds.
    /// </summary>
    public float Duration { get; }

    /// <summary>
    /// The picture its tile is in; null when the tile set no longer has the tile, or the tile has no picture.
    /// </summary>
    public TilePicture? Picture { get; }

    /// <summary>
    /// The pixels of its tile in <see cref="Picture"/>.
    /// </summary>
    public Rectangle Region { get; }

    /// <summary>
    /// Whether the frame has no picture to show.
    /// </summary>
    public bool IsMissing => Picture is null;
}
