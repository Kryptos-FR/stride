using System.Collections.Specialized;
using System.Globalization;
using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Canvas;
using ColorRise.Tessera.Editor.Palette;
using ColorRise.Tessera.Editor.Pictures;
using ColorRise.Tessera.Editor.ViewModels;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Assets.Quantum;
using Stride.Core.Extensions;
using Stride.Core.Presentation.Collections;
using Stride.Core.Presentation.Commands;
using Stride.Core.Presentation.Quantum;
using Stride.Core.Presentation.Services;
using Stride.Core.Presentation.ViewModels;
using Stride.Core.Quantum;

namespace ColorRise.Tessera.Editor.FlipbookEditor;

/// <summary>
/// The editor of a flipbook: its animations and their clips in a tree, the frames of the selected clip in a strip, and the
/// palette of its tile set, whose picks add frames.
/// </summary>
[AssetEditorViewModel<FlipbookViewModel>]
public sealed class FlipbookEditorViewModel : AssetEditorViewModel<FlipbookViewModel>
{
    // The duration of a frame put first in a clip
    private const float FirstFrameDuration = 0.1f;
    private const string NewAnimationName = "animation";
    // The slot the palette gives its cells; a flipbook has one tile set
    private const ushort PaletteSlot = 1;

    private readonly ObservableList<FlipbookAnimationItemViewModel> animationItems = [];
    private readonly ObservableList<FlipbookFrameItemViewModel> frameItems = [];
    // The pictures of the frames, made once and kept until the asset or the pictures of the tile set change
    private readonly Dictionary<FlipbookFrame, FlipbookFramePicture?> framePictures = new(ReferenceEqualityComparer.Instance);
    // A change of the asset came during a transaction, an undo or a redo: the editor follows it once that is over
    private bool followPending;
    private bool initialized;

    /// <summary>
    /// Makes the editor of a flipbook; <see cref="Initialize"/> opens it.
    /// </summary>
    public FlipbookEditorViewModel(FlipbookViewModel asset)
        : base(asset)
    {
        PickTileSetCommand = new AnonymousTaskCommand(ServiceProvider, PickTileSetAsync);
        AddAnimationCommand = new AnonymousCommand(ServiceProvider, AddAnimation);
        AddClipCommand = new AnonymousCommand<FlipbookDirection>(ServiceProvider, AddClip, direction => FreeDirections.Contains(direction));
        RemoveSelectedCommand = new AnonymousTaskCommand(ServiceProvider, RemoveSelectedAsync);
        RemoveFramesCommand = new AnonymousCommand(ServiceProvider, RemoveFrames);
        MoveFramesLeftCommand = new AnonymousCommand(ServiceProvider, () => MoveFramesBy(-1));
        MoveFramesRightCommand = new AnonymousCommand(ServiceProvider, () => MoveFramesBy(1));
        PlayPauseCommand = new AnonymousCommand(ServiceProvider, () => IsPlaying = !IsPlaying);
        Preview.PictureOf = PictureOf;
        Preview.IsPlaying = true;
        Preview.IsRunningChanged += PreviewRunningChanged;
        Preview.FrameIndexChanged += PreviewFrameChanged;
        Follow();
        SelectedItem = FirstClip();
        UpdateCommands();
    }

    /// <summary>
    /// The animations of the flipbook, in the order of the asset, each with its clips.
    /// </summary>
    public IReadOnlyObservableList<FlipbookAnimationItemViewModel> Animations => animationItems;

    /// <summary>
    /// What the tree selects: an animation item, a clip item, or nothing. Setting it empties <see cref="SelectedFrames"/>.
    /// </summary>
    public object? SelectedItem
    {
        get;
        set
        {
            var item = value is FlipbookAnimationItemViewModel or FlipbookClipItemViewModel ? value : null;
            if (SetValue(ref field, item))
            {
                SelectedFrames = [];
                SelectionFollows();
                ShowProperties();
            }
        }
    }

    /// <summary>
    /// The animation of the selection: the selected animation item, or the animation of the selected clip item.
    /// </summary>
    public FlipbookAnimationItemViewModel? SelectedAnimation
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// The clip the strip shows: the selected clip item, or the first clip of the selected animation item.
    /// </summary>
    public FlipbookClipItemViewModel? SelectedClip
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// The directions <see cref="SelectedAnimation"/> has no clip for, in the order of <see cref="FlipbookDirection"/>.
    /// </summary>
    public IReadOnlyList<FlipbookDirection> FreeDirections
    {
        get;
        private set => SetValue(ref field, value);
    } = [];

    /// <summary>
    /// The frames of <see cref="SelectedClip"/>, in order.
    /// </summary>
    public IReadOnlyObservableList<FlipbookFrameItemViewModel> Frames => frameItems;

    /// <summary>
    /// The frames selected in the strip, in their order; the property grid shows them.
    /// </summary>
    public IReadOnlyList<FlipbookFrameItemViewModel> SelectedFrames
    {
        get;
        set
        {
            // Only frames of the strip, in the order of the strip
            var frames = frameItems.Where(value.Contains).ToList();
            if (!frames.SequenceEqual(field))
            {
                SetValue(ref field, frames);
                UpdateCommands();
                ShowProperties();
                // One frame selected: the preview pauses on it
                if (frames is [var only])
                {
                    IsPlaying = false;
                    Preview.Time = StartOf(only.Index);
                }
            }
        }
    } = [];

    /// <summary>
    /// The pictures of the tile set; null without a tile set, or when the session does not have it.
    /// </summary>
    public TileSetPictures? Pictures
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// The palette of the tile set, whose picks add frames; null when <see cref="Pictures"/> is.
    /// </summary>
    public TilePaletteViewModel? Palette
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// What is wrong in the flipbook: the checks of the build, a tile set that cannot be found, and the frames whose tiles
    /// the tile set does not have.
    /// </summary>
    public IReadOnlyList<string> Problems
    {
        get;
        private set => SetValue(ref field, value);
    } = [];

    /// <summary>
    /// Why the last command did nothing; null when it did something.
    /// </summary>
    public string? Message
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// The preview, which plays <see cref="SelectedClip"/> as its animation's loop mode says.
    /// </summary>
    public FlipbookPreviewScene Preview { get; } = new();

    /// <summary>
    /// The zoom and the pan of the preview.
    /// </summary>
    public CanvasView PreviewView { get; } = new();

    /// <summary>
    /// Whether the preview plays; true when the editor opens. A clip played once stops at its end.
    /// </summary>
    public bool IsPlaying
    {
        get => Preview.IsPlaying;
        set
        {
            if (Preview.IsPlaying != value)
            {
                Preview.IsPlaying = value;
                OnPropertyChanged(nameof(IsPlaying));
            }
        }
    }

    /// <summary>
    /// The speeds the preview can play at.
    /// </summary>
    public IReadOnlyList<float> PreviewSpeeds { get; } = [0.25f, 0.5f, 1, 2];

    /// <summary>
    /// The speed the preview plays at; 1 as the game plays it.
    /// </summary>
    public float PreviewSpeed
    {
        get => Preview.Speed;
        set
        {
            if (Preview.Speed != value)
            {
                Preview.Speed = value;
                OnPropertyChanged(nameof(PreviewSpeed));
            }
        }
    }

    /// <summary>
    /// Whether the preview starts again at the end of the clip, whatever the animation's loop mode.
    /// </summary>
    public bool LoopPreview
    {
        get => Preview.Loop;
        set
        {
            if (Preview.Loop != value)
            {
                Preview.Loop = value;
                OnPropertyChanged(nameof(LoopPreview));
            }
        }
    }

    /// <summary>
    /// Plays or pauses the preview.
    /// </summary>
    public ICommandBase PlayPauseCommand { get; }

    /// <summary>
    /// Picks a tile set in the session, and makes it the flipbook's.
    /// </summary>
    public ICommandBase PickTileSetCommand { get; }

    /// <summary>
    /// Adds an animation with a name no animation has, and one clip for any direction, and selects it.
    /// </summary>
    public ICommandBase AddAnimationCommand { get; }

    /// <summary>
    /// Adds a clip to the selected animation for the direction given, one of <see cref="FreeDirections"/>, and selects it.
    /// </summary>
    public ICommandBase AddClipCommand { get; }

    /// <summary>
    /// Removes the selected clip, or the selected animation with its clips, after a question when there are frames.
    /// </summary>
    public ICommandBase RemoveSelectedCommand { get; }

    /// <summary>
    /// Removes the selected frames.
    /// </summary>
    public ICommandBase RemoveFramesCommand { get; }

    /// <summary>
    /// Moves the selected frames, as a block, one place to the left.
    /// </summary>
    public ICommandBase MoveFramesLeftCommand { get; }

    /// <summary>
    /// Moves the selected frames, as a block, one place to the right.
    /// </summary>
    public ICommandBase MoveFramesRightCommand { get; }

    private IEditorDialogService Dialogs => ServiceProvider.Get<IEditorDialogService>();

    /// <summary>
    /// Follows the asset and the session, and shows the selection in the property grid.
    /// </summary>
    public override Task<bool> Initialize()
    {
        Asset.PropertyGraph.Changed += GraphChanged;
        Asset.PropertyGraph.ItemChanged += GraphItemChanged;
        UndoRedoService.Done += HistoryMoved;
        UndoRedoService.Undone += HistoryMoved;
        UndoRedoService.Redone += HistoryMoved;
        UndoRedoService.TransactionDiscarded += HistoryMoved;
        Session.DeletedAssetsChanged += DeletedAssetsChanged;
        initialized = true;
        ShowProperties();
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public override void Destroy()
    {
        Asset.PropertyGraph.Changed -= GraphChanged;
        Asset.PropertyGraph.ItemChanged -= GraphItemChanged;
        UndoRedoService.Done -= HistoryMoved;
        UndoRedoService.Undone -= HistoryMoved;
        UndoRedoService.Redone -= HistoryMoved;
        UndoRedoService.TransactionDiscarded -= HistoryMoved;
        Session.DeletedAssetsChanged -= DeletedAssetsChanged;
        Preview.IsRunningChanged -= PreviewRunningChanged;
        Preview.FrameIndexChanged -= PreviewFrameChanged;
        Preview.IsPlaying = false;
        SetPictures(null);
        foreach (var item in frameItems)
        {
            item.Destroy();
        }
        foreach (var item in animationItems)
        {
            item.Destroy();
        }
        base.Destroy();
    }

    /// <summary>
    /// Gives an animation a name, trimmed, as one step; false, with a <see cref="Message"/>, for an empty name or the name of
    /// another animation. Its own name is true, and no step.
    /// </summary>
    public bool Rename(FlipbookAnimationItemViewModel animation, string name)
    {
        ArgumentNullException.ThrowIfNull(animation);
        var trimmed = name?.Trim() ?? "";
        var target = animation.Animation;
        if (trimmed.Length == 0)
        {
            Message = "An animation needs a name.";
            return false;
        }
        if (Asset.Asset.Animations.Any(other => other is not null && other != target && other.Name == trimmed))
        {
            Message = $"Another animation is named '{trimmed}'.";
            return false;
        }
        Message = null;
        if (trimmed == target.Name || !Asset.Asset.Animations.Contains(target))
        {
            return trimmed == target.Name;
        }
        using (var transaction = UndoRedoService.CreateTransaction())
        {
            FlipbookGraph.RenameAnimation(Asset, target, trimmed);
            UndoRedoService.SetName(transaction, "Rename animation");
        }
        return true;
    }

    /// <summary>
    /// Adds a frame for each cell of a stamp that has a tile, the stamp's top row first, each row from the left: one cell of
    /// that tile, without offset, with the cell's flip. They go after the last selected frame, or at the end, in one step, and
    /// become the selection; each lasts as the frame before the place they go to, or 0.1 s.
    /// </summary>
    public void AddFrames(TileStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(stamp);
        if (SelectedClip is not { } clipItem || !IsInAsset(clipItem))
        {
            Message = "Select or add an animation first.";
            return;
        }
        var clip = clipItem.Clip;
        var index = SelectedFrames.Count > 0 ? SelectedFrames.Max(frame => frame.Index) + 1 : clip.Frames.Count;
        var duration = index > 0 && clip.Frames[index - 1] is { } before ? before.Duration : FirstFrameDuration;
        var frames = new List<FlipbookFrame>();
        // The stamp's row 0 is its bottom row
        for (var y = stamp.Size.Y - 1; y >= 0; y--)
        {
            for (var x = 0; x < stamp.Size.X; x++)
            {
                if (stamp[x, y] is { IsEmpty: false } cell)
                {
                    var frame = new FlipbookFrame { Duration = duration };
                    frame.Cells.Add(new FlipbookCell { Tile = cell.Tile, Flip = cell.Flip });
                    frames.Add(frame);
                }
            }
        }
        if (frames.Count == 0)
        {
            return;
        }
        using (var transaction = UndoRedoService.CreateTransaction())
        {
            FlipbookGraph.AddFrames(Asset, clip, index, frames);
            UndoRedoService.SetName(transaction, "Add frames");
        }
        Message = null;
        SelectedFrames = [.. frameItems.Where(item => frames.Contains(item.Frame))];
    }

    /// <summary>
    /// Moves the selected frames, as a block that keeps its order, to before the frame that is at an index now; the count of
    /// the frames moves them to the end. They stay selected.
    /// </summary>
    public void MoveSelectedFrames(int target)
    {
        if (SelectedClip is not { } clipItem || !IsInAsset(clipItem) || SelectedFrames.Count == 0)
        {
            return;
        }
        var clip = clipItem.Clip;
        var frames = clip.Frames;
        target = Math.Clamp(target, 0, frames.Count);
        var indices = SelectedFrames.Select(frame => frame.Index).ToList();
        // A block already at its place is no step
        var moved = indices.Order().ToList();
        var order = frames.Where((_, index) => !moved.Contains(index)).ToList();
        order.InsertRange(target - moved.Count(index => index < target), moved.Select(index => frames[index]));
        if (order.SequenceEqual(frames))
        {
            return;
        }
        using (var transaction = UndoRedoService.CreateTransaction())
        {
            FlipbookGraph.MoveFrames(Asset, clip, indices, target);
            UndoRedoService.SetName(transaction, "Move frames");
        }
        Message = null;
    }

    /// <summary>
    /// Whether the property grid shows a member: the tree and the strip own the names, the clips and the frames, and a cell's
    /// tile is chosen in the palette.
    /// </summary>
    internal static bool ShowsMember(IMemberNode member)
    {
        var (type, name) = (member.Parent.Type, member.Name);
        return !(type == typeof(FlipbookAnimation) && name is nameof(FlipbookAnimation.Name) or nameof(FlipbookAnimation.Clips))
            && !(type == typeof(FlipbookClip) && name == nameof(FlipbookClip.Frames))
            && !(type == typeof(FlipbookCell) && name == nameof(FlipbookCell.Tile));
    }

    /// <summary>
    /// "1 frame", "4 frames".
    /// </summary>
    internal static string FramesText(int count)
        => count == 1 ? "1 frame" : string.Create(CultureInfo.InvariantCulture, $"{count:N0} frames");

    /// <summary>
    /// Makes a list hold the wanted items, in their order, with as few changes as it takes; the items it lets go of are
    /// destroyed.
    /// </summary>
    internal static void Sync<T>(ObservableList<T> list, IReadOnlyList<T> wanted)
        where T : DispatcherViewModel
    {
        foreach (var gone in list.Where(item => !wanted.Contains(item)).ToList())
        {
            list.Remove(gone);
            gone.Destroy();
        }
        for (var index = 0; index < wanted.Count; index++)
        {
            var item = wanted[index];
            var at = list.IndexOf(item);
            if (at == index)
            {
                continue;
            }
            if (at >= 0)
            {
                list.RemoveAt(at);
            }
            list.Insert(index, item);
        }
    }

    /// <summary>
    /// The path from the root of the asset to an animation, one of its clips, or a frame of that clip.
    /// </summary>
    internal GraphNodePath PathOf(FlipbookAnimation animation, FlipbookClip? clip = null, FlipbookFrame? frame = null)
    {
        var path = new GraphNodePath(Asset.PropertyGraph.RootNode);
        path.PushMember(nameof(FlipbookAsset.Animations));
        path.PushIndex(new NodeIndex(Asset.Asset.Animations.IndexOf(animation)));
        path.PushTarget();
        if (clip is not null)
        {
            path.PushMember(nameof(FlipbookAnimation.Clips));
            path.PushIndex(new NodeIndex(animation.Clips.IndexOf(clip)));
            path.PushTarget();
            if (frame is not null)
            {
                path.PushMember(nameof(FlipbookClip.Frames));
                path.PushIndex(new NodeIndex(clip.Frames.IndexOf(frame)));
                path.PushTarget();
            }
        }
        return path;
    }

    // The first clip of the first animation; the first animation when it has no clip; nothing without an animation
    private object? FirstClip()
        => animationItems.FirstOrDefault() is { } first ? first.Clips.FirstOrDefault() ?? (object)first : null;

    private bool IsInAsset(FlipbookClipItemViewModel item)
        => animationItems.Contains(item.Animation) && item.Animation.Clips.Contains(item);

    // The items follow the asset; a selection that is still in it stays
    private void Follow()
    {
        if (IsDestroyed)
        {
            return;
        }
        followPending = false;
        framePictures.Clear();
        SetPictures(TilePictureService.Of(Session).Get(Asset.Asset.TileSet));
        var wanted = new List<FlipbookAnimationItemViewModel>();
        foreach (var animation in Asset.Asset.Animations)
        {
            if (animation is not null)
            {
                var item = animationItems.FirstOrDefault(existing => existing.Animation == animation);
                if (item is null)
                {
                    item = new FlipbookAnimationItemViewModel(this, animation);
                }
                else
                {
                    item.Follow();
                }
                wanted.Add(item);
            }
        }
        Sync(animationItems, wanted);

        var gone = SelectedItem switch
        {
            FlipbookAnimationItemViewModel animation => !animationItems.Contains(animation),
            FlipbookClipItemViewModel clip => !IsInAsset(clip),
            _ => false,
        };
        if (gone)
        {
            SelectedItem = FirstClip();
        }
        else
        {
            SelectionFollows();
        }
        UpdateProblems();
        Preview.Refresh();
    }

    // The animation, the clip, the free directions and the frames of the selected item
    private void SelectionFollows()
    {
        var (animation, clip) = SelectedItem switch
        {
            FlipbookAnimationItemViewModel item => (item, item.Clips.FirstOrDefault()),
            FlipbookClipItemViewModel item => (item.Animation, item),
            _ => ((FlipbookAnimationItemViewModel?)null, (FlipbookClipItemViewModel?)null),
        };
        SelectedAnimation = animation;
        SelectedClip = clip;
        Preview.LoopMode = animation?.Animation.LoopMode ?? FlipbookLoopMode.Loop;
        Preview.Clip = clip?.Clip;
        var free = animation is null
            ? []
            : Enum.GetValues<FlipbookDirection>().Where(direction => !animation.Animation.Clips.Any(other => other?.Direction == direction)).ToList();
        if (!free.SequenceEqual(FreeDirections))
        {
            FreeDirections = free;
        }
        FramesFollow();
        UpdateCommands();
    }

    // The strip shows the frames of the selected clip; the selected frames that are gone leave the selection
    private void FramesFollow()
    {
        var wanted = new List<FlipbookFrameItemViewModel>();
        if (SelectedClip is { } clipItem)
        {
            var frames = clipItem.Clip.Frames;
            var number = 0;
            for (var index = 0; index < frames.Count; index++)
            {
                if (frames[index] is not { } frame)
                {
                    continue;
                }
                var item = frameItems.FirstOrDefault(existing => existing.Frame == frame && existing.Clip == clipItem)
                    ?? new FlipbookFrameItemViewModel(this, clipItem, frame);
                item.Follow(index, ++number, PictureOf(frame));
                item.IsCurrent = index == Preview.FrameIndex;
                wanted.Add(item);
            }
        }
        Sync(frameItems, wanted);
        SelectedFrames = SelectedFrames;
    }

    // The picture of a frame, made once; none without the pictures of the tile set
    private FlipbookFramePicture? PictureOf(FlipbookFrame frame)
    {
        if (Pictures is not { } pictures)
        {
            return null;
        }
        if (!framePictures.TryGetValue(frame, out var picture))
        {
            picture = FlipbookFrameComposer.Compose(frame, Asset.Asset.Pivot, pictures.Find, pictures.TileSet.Asset.TileSize);
            framePictures.Add(frame, picture);
        }
        return picture;
    }

    // The time the preview shows a frame of the selected clip from: the durations of the frames before it
    private double StartOf(int index)
    {
        var frames = SelectedClip?.Clip.Frames ?? [];
        var start = 0.0;
        for (var i = 0; i < index && i < frames.Count; i++)
        {
            if (frames[i] is { Duration: > 0 } frame)
            {
                start += frame.Duration;
            }
        }
        return start;
    }

    private void PreviewRunningChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(IsPlaying));

    // The strip marks the frame the preview shows
    private void PreviewFrameChanged(object? sender, EventArgs e)
    {
        foreach (var item in frameItems)
        {
            item.IsCurrent = item.Index == Preview.FrameIndex;
        }
    }

    // The pictures and the palette are made again when the flipbook names another tile set
    private void SetPictures(TileSetPictures? pictures)
    {
        if (pictures == Pictures)
        {
            return;
        }
        Pictures?.Changed -= PicturesChanged;
        if (Palette is { } old)
        {
            old.PickedChanged -= PalettePicked;
            old.Destroy();
        }
        Pictures = pictures;
        Palette = pictures is null ? null : new TilePaletteViewModel(ServiceProvider, pictures, PaletteSlot);
        Palette?.PickedChanged += PalettePicked;
        Pictures?.Changed += PicturesChanged;
    }

    private void UpdateProblems()
    {
        var asset = Asset.Asset;
        var problems = new List<string>(FlipbookAssetChecks.Of(asset));
        if (asset.TileSet is not null && Pictures is null)
        {
            problems.Add("The tile set of the flipbook cannot be found.");
        }
        if (Pictures is { } pictures)
        {
            foreach (var animation in asset.Animations.Where(animation => animation is not null))
            {
                foreach (var clip in animation.Clips.Where(clip => clip is not null))
                {
                    var number = 0;
                    foreach (var frame in clip.Frames.Where(frame => frame is not null))
                    {
                        number++;
                        foreach (var cell in frame.Cells.Where(cell => cell is not null && pictures.Find(cell.Tile) is not { IsMissing: false }))
                        {
                            problems.Add(string.Create(CultureInfo.InvariantCulture,
                                $"The frame {number} of the clip {clip.Direction} of '{animation.Name}' uses the tile {cell.Tile}, which the tile set does not have."));
                        }
                    }
                }
            }
        }
        if (!problems.SequenceEqual(Problems))
        {
            Problems = problems;
        }
    }

    private void UpdateCommands()
    {
        var sorted = SelectedFrames.Select(frame => frame.Index).Order().ToList();
        var count = SelectedClip?.Clip.Frames.Count ?? 0;
        RemoveFramesCommand.IsEnabled = sorted.Count > 0;
        MoveFramesLeftCommand.IsEnabled = sorted.Count > 0 && sorted[0] > 0;
        MoveFramesRightCommand.IsEnabled = sorted.Count > 0 && sorted[0] < count - sorted.Count;
        AddClipCommand.IsEnabled = SelectedAnimation is not null && FreeDirections.Count > 0;
        RemoveSelectedCommand.IsEnabled = SelectedItem is not null;
    }

    // The grid shows the selected frames; without them the selected clip or animation; else nothing, as the flipbook's own
    // members are in the grid of the asset view
    private void ShowProperties()
    {
        if (!initialized || IsDestroyed)
        {
            return;
        }
        if (SelectedFrames.Count > 0)
        {
            EditorProperties.TypeDescription = SelectedFrames.Count == 1 ? "Frame" : "Frames";
            EditorProperties.Name = string.Join(", ", SelectedFrames.Select(frame => frame.Label));
            EditorProperties.GenerateSelectionPropertiesAsync(SelectedFrames).Forget();
        }
        else if (SelectedItem is FlipbookClipItemViewModel clip)
        {
            EditorProperties.TypeDescription = "Clip";
            EditorProperties.Name = $"{clip.Animation.Name}, {clip.Label}";
            EditorProperties.GenerateSelectionPropertiesAsync([clip]).Forget();
        }
        else if (SelectedItem is FlipbookAnimationItemViewModel animation)
        {
            EditorProperties.TypeDescription = "Animation";
            EditorProperties.Name = animation.Name;
            EditorProperties.GenerateSelectionPropertiesAsync([animation]).Forget();
        }
        else
        {
            EditorProperties.GenerateSelectionPropertiesAsync([]).Forget();
        }
    }

    private async Task PickTileSetAsync()
    {
        if (await Dialogs.OpenAssetPickerAsync(Session, [typeof(TileSetAsset)]) is not TileSetViewModel tileSet || IsDestroyed)
        {
            return;
        }
        if (Pictures?.TileSet == tileSet)
        {
            return;
        }
        using (var transaction = UndoRedoService.CreateTransaction())
        {
            FlipbookGraph.SetTileSet(Asset, tileSet);
            UndoRedoService.SetName(transaction, "Set tile set");
        }
        Message = null;
    }

    private void AddAnimation()
    {
        var names = Asset.Asset.Animations.Where(animation => animation is not null).Select(animation => animation.Name).ToHashSet(StringComparer.Ordinal);
        var name = NewAnimationName;
        for (var number = 2; names.Contains(name); number++)
        {
            name = string.Create(CultureInfo.InvariantCulture, $"{NewAnimationName} {number}");
        }
        FlipbookAnimation animation;
        using (var transaction = UndoRedoService.CreateTransaction())
        {
            animation = FlipbookGraph.AddAnimation(Asset, name);
            UndoRedoService.SetName(transaction, "Add animation");
        }
        Message = null;
        SelectedItem = animationItems.FirstOrDefault(item => item.Animation == animation);
    }

    private void AddClip(FlipbookDirection direction)
    {
        if (SelectedAnimation is not { } item || !animationItems.Contains(item) || !FreeDirections.Contains(direction))
        {
            return;
        }
        FlipbookClip clip;
        using (var transaction = UndoRedoService.CreateTransaction())
        {
            clip = FlipbookGraph.AddClip(Asset, item.Animation, direction);
            UndoRedoService.SetName(transaction, "Add clip");
        }
        Message = null;
        SelectedItem = item.Clips.FirstOrDefault(clipItem => clipItem.Clip == clip);
    }

    private async Task RemoveSelectedAsync()
    {
        switch (SelectedItem)
        {
            case FlipbookClipItemViewModel clip:
                await RemoveClipAsync(clip);
                break;
            case FlipbookAnimationItemViewModel animation:
                await RemoveAnimationAsync(animation);
                break;
        }
    }

    private async Task RemoveClipAsync(FlipbookClipItemViewModel item)
    {
        if (item.FrameCount > 0
            && await Dialogs.MessageBoxAsync(item.RemoveQuestion(), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }
        // The clip may have gone while the question was open
        if (IsDestroyed || !IsInAsset(item))
        {
            return;
        }
        var animation = item.Animation;
        var position = animation.Clips.IndexOf(item);
        using (var transaction = UndoRedoService.CreateTransaction())
        {
            FlipbookGraph.RemoveClip(Asset, animation.Animation, item.Clip);
            UndoRedoService.SetName(transaction, "Remove clip");
        }
        Message = null;
        // The clip after it, else the one before, else the animation
        SelectedItem = animation.Clips.Count > 0 ? animation.Clips[Math.Min(position, animation.Clips.Count - 1)] : animation;
    }

    private async Task RemoveAnimationAsync(FlipbookAnimationItemViewModel item)
    {
        var frames = item.Clips.Sum(clip => clip.FrameCount);
        if (frames > 0)
        {
            var question = $"Remove the animation '{item.Name}' and its {FramesText(frames)}?";
            if (await Dialogs.MessageBoxAsync(question, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }
        }
        if (IsDestroyed || !animationItems.Contains(item))
        {
            return;
        }
        var position = animationItems.IndexOf(item);
        using (var transaction = UndoRedoService.CreateTransaction())
        {
            FlipbookGraph.RemoveAnimation(Asset, item.Animation);
            UndoRedoService.SetName(transaction, "Remove animation");
        }
        Message = null;
        // The animation after it, else the one before, else nothing
        SelectedItem = animationItems.Count > 0 ? animationItems[Math.Min(position, animationItems.Count - 1)] : null;
    }

    private void RemoveFrames()
    {
        if (SelectedClip is not { } clipItem || !IsInAsset(clipItem) || SelectedFrames.Count == 0)
        {
            return;
        }
        var indices = SelectedFrames.Select(frame => frame.Index).ToList();
        using (var transaction = UndoRedoService.CreateTransaction())
        {
            FlipbookGraph.RemoveFrames(Asset, clipItem.Clip, indices);
            UndoRedoService.SetName(transaction, "Remove frames");
        }
        Message = null;
        SelectedFrames = [];
    }

    // Moves the block of the selected frames by one place: its first frame goes one place to the left or to the right
    private void MoveFramesBy(int delta)
    {
        if (SelectedClip is not { } clipItem || SelectedFrames.Count == 0)
        {
            return;
        }
        var count = clipItem.Clip.Frames.Count;
        var moved = SelectedFrames.Select(frame => frame.Index).Order().ToList();
        var staying = Enumerable.Range(0, count).Where(index => !moved.Contains(index)).ToList();
        var place = moved[0] + delta;
        if (place < 0 || place > staying.Count)
        {
            return;
        }
        MoveSelectedFrames(place < staying.Count ? staying[place] : count);
    }

    private void PalettePicked(object? sender, EventArgs e)
    {
        if (sender is TilePaletteViewModel { Picked: { } picked })
        {
            AddFrames(picked);
        }
    }

    // During a transaction, an undo or a redo, the asset is followed once it is over: a move takes its frames out before it
    // puts them back, and they stay selected
    private void GraphChanged(object? sender, AssetMemberNodeChangeEventArgs e) => AssetChanged();

    private void GraphItemChanged(object? sender, AssetItemNodeChangeEventArgs e) => AssetChanged();

    private void AssetChanged()
    {
        if (UndoRedoService.TransactionInProgress || UndoRedoService.UndoRedoInProgress)
        {
            followPending = true;
        }
        else
        {
            Follow();
        }
    }

    private void HistoryMoved(object? sender, EventArgs e)
    {
        if (followPending)
        {
            Follow();
        }
    }

    private void PicturesChanged(object? sender, EventArgs e) => Follow();

    // The tile set may be gone from the session, or back
    private void DeletedAssetsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Dispatcher.InvokeAsync(Follow);
}
