// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;

namespace Stride.Editor.Avalonia.Engine;

/// <summary>
/// Shows the frames of a game that renders offscreen (viewport spike, options B and B'). The control is an ordinary
/// Avalonia control: other controls can draw over it, and it receives the keyboard and pointer input.
/// </summary>
/// <remarks>
/// <para>
/// B' (<see cref="ViewportMode.CpuReadback"/>): the game thread writes each frame into one of three
/// <see cref="WriteableBitmap"/>s and publishes it; the control draws the latest published bitmap.
/// </para>
/// <para>
/// B (<see cref="ViewportMode.GpuInterop"/>): the control hosts a <see cref="CompositionDrawingSurface"/> as its child
/// visual, and <see cref="GpuFramePresenter"/> updates it with a texture the game shares with the compositor.
/// </para>
/// </remarks>
public sealed class OffscreenGameView : Control
{
    private readonly object bitmapLock = new();
    private readonly ViewportStats stats;
    private WriteableBitmap? front;
    private WriteableBitmap? pending;
    private WriteableBitmap? free;
    private bool invalidatePosted;
    private TopLevel? topLevel;
    private bool animationFrameRequested;

    private CompositionSurfaceVisual? surfaceVisual;

    static OffscreenGameView()
    {
        FocusableProperty.OverrideMetadata<OffscreenGameView>(new StyledPropertyMetadata<bool>(true));
        ClipToBoundsProperty.OverrideMetadata<OffscreenGameView>(new StyledPropertyMetadata<bool>(true));
    }

    internal OffscreenGameView(ViewportStats stats)
    {
        this.stats = stats;
        MinWidth = 32;
        MinHeight = 32;
    }

    /// <summary>
    /// Raised on the UI thread when the size of the control in device pixels changes.
    /// </summary>
    public event Action<int, int>? PixelSizeChanged;

    /// <summary>
    /// Gets the size of the control in device pixels.
    /// </summary>
    public PixelSize PixelSize { get; private set; }

    /// <summary>
    /// Gets the compositor of the visual tree this control is in, or <c>null</c> while it is detached.
    /// </summary>
    internal Compositor? Compositor => ElementComposition.GetElementVisual(this)?.Compositor;

    /// <summary>
    /// Gets a bitmap of the given size that the game thread can write a frame into, then <see cref="PublishBitmap"/>.
    /// </summary>
    internal WriteableBitmap AcquireBitmap(int width, int height, PixelFormat format)
    {
        lock (bitmapLock)
        {
            var bitmap = free;
            free = null;
            if (bitmap != null && (bitmap.PixelSize.Width != width || bitmap.PixelSize.Height != height || bitmap.Format != format))
            {
                bitmap.Dispose();
                bitmap = null;
            }
            return bitmap ?? new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), format, AlphaFormat.Opaque);
        }
    }

    /// <summary>
    /// Publishes a bitmap filled by the game thread: the control shows it at its next render.
    /// </summary>
    internal void PublishBitmap(WriteableBitmap bitmap)
    {
        lock (bitmapLock)
        {
            // A frame the control never showed is dropped: the game is ahead of the editor's renders
            if (pending != null)
            {
                free?.Dispose();
                free = pending;
                stats.FrameSkipped();
            }
            pending = bitmap;
            // By default, the view picks the frame up at its next animation frame (OnAnimationFrame)
            if (!ViewportModeSelector.PumpByPost || invalidatePosted)
                return;
            invalidatePosted = true;
        }
        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
    }

    /// <summary>
    /// Creates the drawing surface of the GPU interop mode and makes it the content of this control (UI thread).
    /// </summary>
    internal CompositionDrawingSurface? CreateDrawingSurface()
    {
        var compositor = Compositor;
        if (compositor == null)
            return null;
        var surface = compositor.CreateDrawingSurface();
        surfaceVisual = compositor.CreateSurfaceVisual();
        surfaceVisual.Surface = surface;
        surfaceVisual.Size = new Vector(Bounds.Width, Bounds.Height);
        ElementComposition.SetElementChildVisual(this, surfaceVisual);
        return surface;
    }

    /// <summary>
    /// Removes the drawing surface of the GPU interop mode, after a failure: the control shows the readback frames again.
    /// </summary>
    internal void RemoveDrawingSurface()
    {
        if (surfaceVisual == null)
            return;
        ElementComposition.SetElementChildVisual(this, null);
        surfaceVisual = null;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var start = stats.Now;
        WriteableBitmap? bitmap;
        var isNewFrame = false;
        lock (bitmapLock)
        {
            invalidatePosted = false;
            if (pending != null)
            {
                isNewFrame = true;
                if (front != null)
                {
                    free?.Dispose();
                    free = front;
                }
                front = pending;
                pending = null;
            }
            bitmap = front;
        }

        // The background shows while the game has no frame yet, and behind the surface of the GPU interop mode
        context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
        if (bitmap != null)
        {
            context.DrawImage(bitmap, new Rect(bitmap.Size), new Rect(Bounds.Size));
            stats.FrameShown(start, isNewFrame);
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (surfaceVisual != null)
            surfaceVisual.Size = new Vector(e.NewSize.Width, e.NewSize.Height);
        UpdatePixelSize();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        topLevel = TopLevel.GetTopLevel(this);
        RequestAnimationFrame();
        UpdatePixelSize();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void RequestAnimationFrame()
    {
        if (topLevel == null || animationFrameRequested)
            return;
        animationFrameRequested = true;
        topLevel.RequestAnimationFrame(OnAnimationFrame);
    }

    private void OnAnimationFrame(TimeSpan time)
    {
        animationFrameRequested = false;
        // A detached view stops asking for animation frames: its next attachment asks again
        if (topLevel == null)
            return;
        stats.UiTick();
        if (!ViewportModeSelector.PumpByPost)
        {
            bool hasPending;
            lock (bitmapLock)
                hasPending = pending != null;
            if (hasPending)
                InvalidateVisual();
        }
        RequestAnimationFrame();
    }

    private void UpdatePixelSize()
    {
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        var size = PixelSize.FromSize(Bounds.Size, scaling);
        if (size == PixelSize || size.Width <= 0 || size.Height <= 0)
            return;
        PixelSize = size;
        stats.SizeRequested(size.Width, size.Height);
        PixelSizeChanged?.Invoke(size.Width, size.Height);
    }
}
