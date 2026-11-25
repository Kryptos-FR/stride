using ColorRise.Tessera.Rendering;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.Rendering;

namespace ColorRise.Tessera;

/// <summary>
/// Shows an animation of a flipbook on its entity, in the plane z = 0 with y up, the flipbook's pivot on the entity.
/// </summary>
/// <remarks>
/// What plays is kept by name and by time: <see cref="CurrentClip"/> and <see cref="CurrentFrameIndex"/> are found again
/// from <see cref="Flipbook"/> each time they are read, so a flipbook changed or read again under the component changes
/// only what is shown.
/// </remarks>
[DataContract("FlipbookComponent")]
[Display("Flipbook", Expand = ExpandRule.Once)]
[ComponentCategory("2D")]
[DefaultEntityComponentProcessor(typeof(FlipbookProcessor))]
[DefaultEntityComponentRenderer(typeof(FlipbookRenderProcessor))]
public sealed class FlipbookComponent : ActivableEntityComponent
{
    // The animations to play after the current one, in order
    private readonly Queue<string> queued = new();

    // The clip and the frame FrameChanged last told of
    private FlipbookClip? reportedClip;
    private int reportedIndex = -1;

    /// <summary>
    /// The flipbook whose animations are shown.
    /// </summary>
    /// <userdoc>The flipbook whose animations are shown.</userdoc>
    [DataMember(10)]
    public Flipbook? Flipbook { get; set; }

    /// <summary>
    /// The animation shown at start, and the one playing now: another name starts at the time 0 and empties the queue, the
    /// same name changes nothing.
    /// </summary>
    /// <userdoc>The name of the animation shown at start, as it is written in the flipbook.</userdoc>
    [DataMember(20)]
    public string? Animation
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }
            field = value;
            Time = 0;
            queued.Clear();
        }
    }

    /// <summary>
    /// The direction whose clip is shown; a change keeps the time.
    /// </summary>
    /// <userdoc>The direction whose clip is shown. An animation without a clip for it shows its clip for any direction.</userdoc>
    [DataMember(30)]
    public FlipbookDirection Direction { get; set; }

    /// <summary>
    /// Whether the animation plays when the component is added to the scene.
    /// </summary>
    /// <userdoc>Whether the animation plays when the game starts. Off, its first frame is shown until game code plays it.</userdoc>
    [DataMember(40)]
    [Display("Play on start")]
    public bool PlayOnStart { get; set; } = true;

    /// <summary>
    /// How fast the animation plays: 1 is its own speed; 0 or less stops the time.
    /// </summary>
    /// <userdoc>How fast the animation plays: 1 is its own speed, 0 stops it.</userdoc>
    [DataMember(50)]
    public float Speed { get; set; } = 1;

    /// <summary>
    /// Whether the frames are mirrored left to right around the pivot.
    /// </summary>
    /// <userdoc>Mirrors the frames left to right around the pivot, to face the other way.</userdoc>
    [DataMember(60)]
    [Display("Flip X")]
    public bool FlipX { get; set; }

    /// <summary>
    /// The colour every frame is multiplied by.
    /// </summary>
    /// <userdoc>The colour every frame is multiplied by.</userdoc>
    [DataMember(70)]
    public Color4 Color { get; set; } = Color4.White;

    /// <userdoc>Point keeps pixel art sharp; linear smooths it.</userdoc>
    [DataMember(80)]
    public TileSampler Sampler { get; set; }

    /// <summary>
    /// Whether the flipbook is ordered by its y among the flipbooks of its depth: the lower is drawn over the higher.
    /// </summary>
    /// <userdoc>Among the flipbooks at the same depth, the lower one on screen is drawn over the higher one.</userdoc>
    [DataMember(90)]
    [Display("Sort by Y")]
    public bool SortByY { get; set; } = true;

    /// <userdoc>The render group of the flipbook, for the cameras and render stages that draw only some groups.</userdoc>
    [DataMember(100)]
    [Display("Render group")]
    public RenderGroup RenderGroup { get; set; }

    /// <summary>
    /// Whether the time moves.
    /// </summary>
    [DataMemberIgnore]
    public bool IsPlaying { get; private set; }

    /// <summary>
    /// The seconds since the animation started, at its speed.
    /// </summary>
    [DataMemberIgnore]
    public double Time { get; set; }

    /// <summary>
    /// The clip shown: of <see cref="Animation"/> and <see cref="Direction"/>, with the fallbacks of
    /// <see cref="FlipbookAnimation.FindClip"/>; null when there is none.
    /// </summary>
    [DataMemberIgnore]
    public FlipbookClip? CurrentClip => CurrentAnimation?.FindClip(Direction);

    /// <summary>
    /// The index of the frame shown in <see cref="CurrentClip"/>; -1 when nothing is shown.
    /// </summary>
    [DataMemberIgnore]
    public int CurrentFrameIndex => CurrentAnimation is { } animation && animation.FindClip(Direction) is { } clip ? clip.FrameAt(Time, animation.LoopMode) : -1;

    /// <summary>
    /// The frame shown; null when nothing is shown.
    /// </summary>
    [DataMemberIgnore]
    public FlipbookFrame? CurrentFrame => CurrentClip is { } clip && CurrentFrameIndex is var index and >= 0 ? clip.Frames[index] : null;

    private FlipbookAnimation? CurrentAnimation => Flipbook?.FindAnimation(Animation);

    // A Once that reached the end of its pass and stopped there
    private bool HasEnded
        => !IsPlaying && CurrentAnimation is { LoopMode: FlipbookLoopMode.Once } animation
           && (animation.FindClip(Direction) is not { } clip || Time >= clip.GetPassLength(FlipbookLoopMode.Once));

    /// <summary>
    /// Raised when the frame shown is another one: another clip or another index.
    /// </summary>
    public event EventHandler<FlipbookEventArgs>? FrameChanged;

    /// <summary>
    /// Raised when a <c>Once</c> animation reached its end, or a queued animation took the place of this one.
    /// </summary>
    public event EventHandler<FlipbookEventArgs>? AnimationEnded;

    /// <summary>
    /// Plays an animation: one playing already goes on, a paused one resumes, an ended one starts again;
    /// <paramref name="restart"/> always starts at 0. The queue is emptied.
    /// </summary>
    public void Play(string animation, bool restart = false)
    {
        ArgumentNullException.ThrowIfNull(animation);
        if (animation == Animation && !restart && !HasEnded)
        {
            queued.Clear();
            IsPlaying = true;
            return;
        }
        Animation = animation;
        Time = 0;
        queued.Clear();
        IsPlaying = true;
    }

    /// <summary>
    /// Plays an animation after the current pass of the current one: at the end of a <c>Once</c>, at the next turn of a
    /// <c>Loop</c> or a <c>PingPong</c>, at once when the current one shows nothing.
    /// </summary>
    public void Queue(string animation)
    {
        ArgumentNullException.ThrowIfNull(animation);
        queued.Enqueue(animation);
    }

    /// <summary>
    /// Holds the frame shown.
    /// </summary>
    public void Pause() => IsPlaying = false;

    /// <summary>
    /// Goes on from where <see cref="Pause"/> held it; an ended animation stays on its last frame.
    /// </summary>
    public void Resume()
    {
        if (!HasEnded)
        {
            IsPlaying = true;
        }
    }

    /// <summary>
    /// Stops, goes back to the first frame and empties the queue.
    /// </summary>
    public void Stop()
    {
        IsPlaying = false;
        Time = 0;
        queued.Clear();
    }

    /// <summary>
    /// Moves the playback by a time, in seconds, times <see cref="Speed"/>, and raises the events. The processor calls it
    /// each frame.
    /// </summary>
    public void Advance(double seconds)
    {
        if (IsPlaying && seconds > 0 && double.IsFinite(seconds) && Speed > 0)
        {
            Time += seconds * Speed;
        }
        EndWhatEnds(seconds > 0 && double.IsFinite(seconds) && Speed > 0 ? seconds * Speed : 0);
        ReportFrame();
    }

    /// <summary>
    /// Starts the animation from the time 0, playing when <see cref="PlayOnStart"/> is set.
    /// </summary>
    internal void Start()
    {
        Time = 0;
        IsPlaying = PlayOnStart;
        reportedClip = null;
        reportedIndex = -1;
    }

    // A queued animation takes the place of the current one at the end of its pass, each at most once a call; then a Once
    // at or past its pass stops there
    private void EndWhatEnds(double added)
    {
        var turns = queued.Count;
        while (turns > 0 && queued.Count > 0 && (IsPlaying || HasEnded))
        {
            var animation = CurrentAnimation;
            var clip = animation?.FindClip(Direction);
            var mode = animation?.LoopMode ?? FlipbookLoopMode.Loop;
            var pass = clip?.GetPassLength(mode) ?? 0;
            double end;
            if (!(pass > 0))
            {
                // Nothing is shown, or nothing lasts: the queue goes on at once
                end = Time;
            }
            else if (mode == FlipbookLoopMode.Once)
            {
                end = pass;
            }
            else
            {
                // The first turn of the pass after the time this call started from
                var before = Time - added;
                end = (Math.Floor(before / pass + 1e-9) + 1) * pass;
            }
            if (Time < end - 1e-6)
            {
                break;
            }
            var index = clip is null ? -1 : clip.FrameAt(Math.Max(end - 1e-5, 0), mode);
            var ended = new FlipbookEventArgs(Animation ?? "", clip?.Direction ?? Direction, index);
            var left = Math.Max(Time - end, 0);
            var next = queued.Dequeue();
            turns--;
            // Set without the setter, which would empty the rest of the queue
            SetAnimation(next);
            Time = left;
            IsPlaying = true;
            AnimationEnded?.Invoke(this, ended);
        }

        if (IsPlaying && CurrentAnimation is { LoopMode: FlipbookLoopMode.Once } once && once.FindClip(Direction) is { } onceClip)
        {
            var pass = onceClip.GetPassLength(FlipbookLoopMode.Once);
            if (Time >= pass - 1e-6)
            {
                Time = pass;
                IsPlaying = false;
                AnimationEnded?.Invoke(this, new FlipbookEventArgs(Animation!, onceClip.Direction, onceClip.FrameAt(pass, FlipbookLoopMode.Once)));
            }
        }
    }

    private void SetAnimation(string animation)
    {
        var rest = queued.ToArray();
        Animation = animation;
        foreach (var name in rest)
        {
            queued.Enqueue(name);
        }
    }

    private void ReportFrame()
    {
        var clip = CurrentClip;
        var index = CurrentFrameIndex;
        if (clip == reportedClip && index == reportedIndex)
        {
            return;
        }
        (reportedClip, reportedIndex) = (clip, index);
        if (clip is not null && index >= 0)
        {
            FrameChanged?.Invoke(this, new FlipbookEventArgs(Animation!, clip.Direction, index));
        }
    }
}
