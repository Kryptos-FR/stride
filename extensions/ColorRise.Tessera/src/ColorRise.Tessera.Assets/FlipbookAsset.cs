using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// A flipbook as Game Studio keeps it: the tile set whose tiles its frames show, its pivot, and its animations, each with a
/// clip of frames for each direction it has.
/// </summary>
[DataContract("FlipbookAsset")]
[AssetDescription(FileExtension)]
[AssetContentType(typeof(Flipbook))]
[AssetFormatVersion(FormatName, CurrentVersion)]
[Display(100, "Flipbook")]
public sealed class FlipbookAsset : Asset
{
    /// <summary>
    /// The extension of the files of flipbook assets.
    /// </summary>
    public const string FileExtension = ".sdflipbook";

    private const string FormatName = "ColorRise.Tessera";
    private const string CurrentVersion = "1.0.0.0";

    /// <summary>
    /// The tile set whose tiles the frames show.
    /// </summary>
    /// <userdoc>The tile set whose tiles the frames show.</userdoc>
    [DataMember(10)]
    [Display("Tile set")]
    public TileSet? TileSet { get; set; }

    /// <summary>
    /// The point of a frame that is on the entity, as a part of the size of the frame's first tile from its bottom-left
    /// corner.
    /// </summary>
    /// <userdoc>The point of a frame that stands on the entity, as a part of the size of the frame's first tile from its
    /// bottom-left corner: (0.5, 0) is the middle of its bottom edge, where a character's feet are.</userdoc>
    [DataMember(20)]
    public Vector2 Pivot { get; set; } = new(0.5f, 0);

    /// <summary>
    /// The animations.
    /// </summary>
    /// <userdoc>The animations, each with a clip of frames for each direction it has. Game code plays one by its name.</userdoc>
    [DataMember(30)]
    public List<FlipbookAnimation> Animations { get; } = [];

    /// <summary>
    /// The content: the tile set's reference, the pivot, and copies of the animations, without the empty items that a merge
    /// can leave in a list.
    /// </summary>
    public Flipbook ToFlipbook()
    {
        var flipbook = new Flipbook { TileSet = TileSet, Pivot = Pivot };
        foreach (var animation in Animations)
        {
            if (animation is not null)
            {
                flipbook.Animations.Add(Copy(animation));
            }
        }
        return flipbook;
    }

    private static FlipbookAnimation Copy(FlipbookAnimation animation)
    {
        var copy = new FlipbookAnimation { Name = animation.Name, LoopMode = animation.LoopMode };
        foreach (var clip in animation.Clips)
        {
            if (clip is not null)
            {
                copy.Clips.Add(Copy(clip));
            }
        }
        return copy;
    }

    private static FlipbookClip Copy(FlipbookClip clip)
    {
        var copy = new FlipbookClip { Direction = clip.Direction };
        foreach (var frame in clip.Frames)
        {
            if (frame is not null)
            {
                var frameCopy = new FlipbookFrame { Duration = frame.Duration };
                foreach (var cell in frame.Cells)
                {
                    if (cell is not null)
                    {
                        frameCopy.Cells.Add(new FlipbookCell { Tile = cell.Tile, Offset = cell.Offset, Flip = cell.Flip });
                    }
                }
                copy.Frames.Add(frameCopy);
            }
        }
        return copy;
    }
}
