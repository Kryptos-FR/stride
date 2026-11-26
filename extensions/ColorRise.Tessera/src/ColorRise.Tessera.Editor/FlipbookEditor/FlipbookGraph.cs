using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.ViewModels;
using Stride.Core.Assets.Presentation.Services;
using Stride.Core.Quantum;

namespace ColorRise.Tessera.Editor.FlipbookEditor;

/// <summary>
/// Every change the editor makes to a flipbook asset, through its graph, so that the history can undo it. The caller owns
/// the transaction.
/// </summary>
internal static class FlipbookGraph
{
    /// <summary>
    /// Sets the tile set whose tiles the frames show; null for none.
    /// </summary>
    public static void SetTileSet(FlipbookViewModel flipbook, TileSetViewModel? tileSet)
        => Root(flipbook)[nameof(FlipbookAsset.TileSet)].Update(tileSet is null ? null : ContentReferenceHelper.CreateReference<TileSet>(tileSet));

    /// <summary>
    /// Adds an animation at the end, with one clip <see cref="FlipbookDirection.None"/> without frames unless told not to.
    /// </summary>
    public static FlipbookAnimation AddAnimation(FlipbookViewModel flipbook, string name, bool withClip = true)
    {
        var animation = new FlipbookAnimation { Name = name };
        if (withClip)
        {
            animation.Clips.Add(new FlipbookClip());
        }
        Animations(flipbook).Add(animation, new NodeIndex(flipbook.Asset.Animations.Count));
        return animation;
    }

    /// <summary>
    /// Removes an animation and its clips.
    /// </summary>
    public static void RemoveAnimation(FlipbookViewModel flipbook, FlipbookAnimation animation)
        => Animations(flipbook).Remove(animation, new NodeIndex(IndexOf(flipbook, animation)));

    /// <summary>
    /// Gives an animation a name.
    /// </summary>
    public static void RenameAnimation(FlipbookViewModel flipbook, FlipbookAnimation animation, string name)
        => AnimationNode(flipbook, animation)[nameof(FlipbookAnimation.Name)].Update(name);

    /// <summary>
    /// Adds a clip without frames for a direction, after the clips of an animation.
    /// </summary>
    public static FlipbookClip AddClip(FlipbookViewModel flipbook, FlipbookAnimation animation, FlipbookDirection direction)
    {
        var clip = new FlipbookClip { Direction = direction };
        Clips(flipbook, animation).Add(clip, new NodeIndex(animation.Clips.Count));
        return clip;
    }

    /// <summary>
    /// Removes a clip of an animation, and its frames.
    /// </summary>
    public static void RemoveClip(FlipbookViewModel flipbook, FlipbookAnimation animation, FlipbookClip clip)
        => Clips(flipbook, animation).Remove(clip, new NodeIndex(IndexOf(animation, clip)));

    /// <summary>
    /// Puts frames in a clip, the first at an index of its frames.
    /// </summary>
    public static void AddFrames(FlipbookViewModel flipbook, FlipbookClip clip, int index, IReadOnlyList<FlipbookFrame> frames)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, clip.Frames.Count);
        var node = Frames(flipbook, clip);
        for (var i = 0; i < frames.Count; i++)
        {
            node.Add(frames[i], new NodeIndex(index + i));
        }
    }

    /// <summary>
    /// Removes the frames at some indices of a clip, given in any order.
    /// </summary>
    public static void RemoveFrames(FlipbookViewModel flipbook, FlipbookClip clip, IReadOnlyCollection<int> indices)
    {
        var node = Frames(flipbook, clip);
        foreach (var index in Enumerable.Reverse(Sorted(clip, indices)))
        {
            node.Remove(clip.Frames[index], new NodeIndex(index));
        }
    }

    /// <summary>
    /// Moves the frames at the indices, in their order, to before the frame that is at target now (Frames.Count: to the end).
    /// </summary>
    public static void MoveFrames(FlipbookViewModel flipbook, FlipbookClip clip, IReadOnlyCollection<int> indices, int target)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(target);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(target, clip.Frames.Count);
        var sorted = Sorted(clip, indices);
        var moved = sorted.Select(index => clip.Frames[index]).ToList();
        // The place among the frames that stay
        var place = target - sorted.Count(index => index < target);
        var node = Frames(flipbook, clip);
        foreach (var index in Enumerable.Reverse(sorted))
        {
            node.Remove(clip.Frames[index], new NodeIndex(index));
        }
        for (var i = 0; i < moved.Count; i++)
        {
            node.Add(moved[i], new NodeIndex(place + i));
        }
    }

    /// <summary>
    /// Puts other frames in the place of all the frames of a clip.
    /// </summary>
    public static void ReplaceFrames(FlipbookViewModel flipbook, FlipbookClip clip, IReadOnlyList<FlipbookFrame> frames)
    {
        var node = Frames(flipbook, clip);
        for (var index = clip.Frames.Count - 1; index >= 0; index--)
        {
            node.Remove(clip.Frames[index], new NodeIndex(index));
        }
        AddFrames(flipbook, clip, 0, frames);
    }

    /// <summary>
    /// Gives a cell of a frame another tile.
    /// </summary>
    public static void SetCellTile(FlipbookViewModel flipbook, FlipbookCell cell, TileId tile)
    {
        var node = flipbook.Session.AssetNodeContainer.GetNode(cell)
            ?? throw new ArgumentException("The cell is not one of the flipbook.", nameof(cell));
        node[nameof(FlipbookCell.Tile)].Update(tile);
    }

    private static List<int> Sorted(FlipbookClip clip, IReadOnlyCollection<int> indices)
    {
        var sorted = indices.Distinct().Order().ToList();
        if (sorted.Count > 0 && (sorted[0] < 0 || sorted[^1] >= clip.Frames.Count))
        {
            throw new ArgumentOutOfRangeException(nameof(indices), "An index is not one of a frame of the clip.");
        }
        return sorted;
    }

    private static IObjectNode Root(FlipbookViewModel flipbook) => flipbook.PropertyGraph.RootNode;

    private static IObjectNode Animations(FlipbookViewModel flipbook) => Root(flipbook)[nameof(FlipbookAsset.Animations)].Target!;

    private static IObjectNode AnimationNode(FlipbookViewModel flipbook, FlipbookAnimation animation)
        => Animations(flipbook).IndexedTarget(new NodeIndex(IndexOf(flipbook, animation)))!;

    private static IObjectNode Clips(FlipbookViewModel flipbook, FlipbookAnimation animation)
        => AnimationNode(flipbook, animation)[nameof(FlipbookAnimation.Clips)].Target!;

    private static IObjectNode Frames(FlipbookViewModel flipbook, FlipbookClip clip)
    {
        var animation = flipbook.Asset.Animations.FirstOrDefault(animation => animation is not null && animation.Clips.Contains(clip))
            ?? throw new ArgumentException("The clip is not one of the flipbook.", nameof(clip));
        return Clips(flipbook, animation).IndexedTarget(new NodeIndex(IndexOf(animation, clip)))![nameof(FlipbookClip.Frames)].Target!;
    }

    private static int IndexOf(FlipbookViewModel flipbook, FlipbookAnimation animation)
    {
        var index = flipbook.Asset.Animations.IndexOf(animation);
        return index >= 0 ? index : throw new ArgumentException("The animation is not one of the flipbook.", nameof(animation));
    }

    private static int IndexOf(FlipbookAnimation animation, FlipbookClip clip)
    {
        var index = animation.Clips.IndexOf(clip);
        return index >= 0 ? index : throw new ArgumentException("The clip is not one of the animation.", nameof(clip));
    }
}
