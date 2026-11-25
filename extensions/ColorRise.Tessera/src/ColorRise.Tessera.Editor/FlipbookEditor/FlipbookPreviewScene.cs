using ColorRise.Tessera.Editor.Canvas;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.FlipbookEditor;

/// <summary>
/// The preview of the flipbook editor: the frame of a clip at a time, around a cross where the pivot is, played by the
/// canvas's clock.
/// </summary>
/// <remarks>The pivot is the origin of the canvas, whose y is down.</remarks>
public sealed class FlipbookPreviewScene : ICanvasScene, ICanvasClock
{
    // The cross of the pivot, 3 pixels around the origin
    private const float CrossReach = 3;
    private static readonly RectangleF Cross = new(-CrossReach, -CrossReach, 2 * CrossReach, 2 * CrossReach);
    private static readonly Color CrossColor = new(255, 80, 80, 255);
    // As the frame timeline: a time this close to the end of the pass is past it
    private const double EndTolerance = 1e-6;

    /// <inheritdoc/>
    public event EventHandler? Changed;

    /// <inheritdoc/>
    public event EventHandler? IsRunningChanged;

    /// <summary>
    /// Raised when <see cref="FrameIndex"/> changed.
    /// </summary>
    public event EventHandler? FrameIndexChanged;

    /// <summary>
    /// The clip played; another clip starts at the time 0.
    /// </summary>
    public FlipbookClip? Clip
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                Time = 0;
                Refresh();
            }
        }
    }

    /// <summary>
    /// What the clip does at its end: the loop mode of its animation.
    /// </summary>
    public FlipbookLoopMode LoopMode
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                Refresh();
            }
        }
    }

    /// <summary>
    /// The picture of a frame; null for a frame without one.
    /// </summary>
    public Func<FlipbookFrame, FlipbookFramePicture?>? PictureOf
    {
        get;
        set
        {
            field = value;
            Refresh();
        }
    }

    /// <summary>
    /// Whether the time moves with the clock. Played again after a <see cref="FlipbookLoopMode.Once"/> stopped at its end, the
    /// clip starts at 0.
    /// </summary>
    public bool IsPlaying
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }
            field = value;
            if (value && HasEnded())
            {
                Time = 0;
            }
            UpdateRunning();
        }
    }

    /// <summary>
    /// How fast the time moves: 1 as the game plays it.
    /// </summary>
    public float Speed { get; set; } = 1;

    /// <summary>
    /// Whether the clip starts again at its end, whatever its loop mode: a <see cref="FlipbookLoopMode.Once"/> is played as a
    /// <see cref="FlipbookLoopMode.Loop"/>.
    /// </summary>
    public bool Loop
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                Refresh();
            }
        }
    }

    /// <summary>
    /// The time in the clip, in seconds.
    /// </summary>
    public double Time
    {
        get;
        set
        {
            field = value;
            UpdateFrame(raise: true);
        }
    }

    /// <summary>
    /// The index in the clip's frames of the frame shown; -1 when nothing is shown.
    /// </summary>
    public int FrameIndex { get; private set; } = -1;

    /// <inheritdoc/>
    /// <remarks>While <see cref="IsPlaying"/> and the clip has a frame.</remarks>
    public bool IsRunning
    {
        get;
        private set
        {
            if (field != value)
            {
                field = value;
                IsRunningChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// All the frames of the clip and the cross of the pivot, so that the view does not jump from frame to frame; null while
    /// no frame has a picture, so that a fit asked on an empty clip waits for its first frame instead of showing the cross alone.
    /// </remarks>
    public RectangleF? Bounds
    {
        get
        {
            RectangleF? bounds = null;
            if (Clip is { } clip && PictureOf is { } pictureOf)
            {
                foreach (var frame in clip.Frames)
                {
                    if (frame is not null && pictureOf(frame) is { } picture)
                    {
                        bounds = RectangleF.Union(bounds ?? Cross, OnCanvas(picture.Bounds));
                    }
                }
            }
            return bounds;
        }
    }

    /// <inheritdoc/>
    public Vector2 GridSize => Vector2.Zero;

    // The mode the clip is played in
    private FlipbookLoopMode ShownMode => Loop && LoopMode == FlipbookLoopMode.Once ? FlipbookLoopMode.Loop : LoopMode;

    /// <summary>
    /// Shows the clip again, after a change of its frames or of their pictures.
    /// </summary>
    public void Refresh()
    {
        UpdateFrame(raise: false);
        UpdateRunning();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    /// <remarks>While playing, the time moves by the seconds times <see cref="Speed"/>; a <see cref="FlipbookLoopMode.Once"/>
    /// stops at its end. <see cref="Changed"/> is raised when the frame is another one.</remarks>
    public void Advance(double seconds)
    {
        if (!IsPlaying || FrameIndex < 0)
        {
            return;
        }
        Time += seconds * Speed;
        if (HasEnded())
        {
            IsPlaying = false;
        }
    }

    /// <inheritdoc/>
    public void Collect(RectangleF visible, float zoom, CanvasFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (Clip is { } clip && FrameIndex >= 0 && FrameIndex < clip.Frames.Count && clip.Frames[FrameIndex] is { } shown
            && PictureOf?.Invoke(shown) is { } picture)
        {
            var source = picture.Picture;
            frame.Sprites.Add(new CanvasSprite(source, new Rectangle(0, 0, source.Width, source.Height), OnCanvas(picture.Bounds)));
        }
        frame.Shapes.Add(new CanvasShape(CanvasMark.Cross, Cross, CrossColor));
    }

    // Bounds with y up, on the canvas whose y is down
    private static RectangleF OnCanvas(Rectangle bounds) => new(bounds.X, -(bounds.Y + bounds.Height), bounds.Width, bounds.Height);

    // Whether a clip played once is at its end
    private bool HasEnded()
        => ShownMode == FlipbookLoopMode.Once && Clip is { } clip && FrameIndex >= 0 && Time >= PassLength(clip) - EndTolerance;

    private void UpdateFrame(bool raise)
    {
        var index = IndexAt(Time);
        if (index == FrameIndex)
        {
            return;
        }
        FrameIndex = index;
        FrameIndexChanged?.Invoke(this, EventArgs.Empty);
        if (raise)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateRunning() => IsRunning = IsPlaying && FrameIndex >= 0;

    private double PassLength(FlipbookClip clip) => Shown(clip).GetPassLength(ShownMode);

    private int IndexAt(double time)
    {
        if (Clip is not { } clip)
        {
            return -1;
        }
        var shown = Shown(clip);
        var index = shown.FrameAt(time, ShownMode);
        return shown == clip || index < 0 ? index : clip.Frames.IndexOf(shown.Frames[index]);
    }

    // A merge can leave an empty item in the frames: the clip is played without it
    private static FlipbookClip Shown(FlipbookClip clip)
    {
        if (!clip.Frames.Contains(null!))
        {
            return clip;
        }
        var shown = new FlipbookClip { Direction = clip.Direction };
        shown.Frames.AddRange(clip.Frames.Where(frame => frame is not null));
        return shown;
    }
}
