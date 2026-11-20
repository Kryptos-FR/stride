// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using Avalonia.Rendering.Composition;
using Texture = Stride.Graphics.Texture;

namespace Stride.AvaloniaUI.Platform;

/// <summary>
/// The window of an Avalonia UI in a game: an offscreen top level whose frames the renderer draws, on the game's graphics
/// device into a texture when it can, otherwise on the CPU into memory the game uploads to a texture.
/// </summary>
/// <remarks>
/// <para>
/// The frame memory keeps the previous frame, so Avalonia's compositor redraws only what changed.
/// <see cref="FrameVersion"/> changes each time a frame is rendered, which tells the game when to upload it.
/// </para>
/// <para>
/// Everything runs on the game thread, which is Avalonia's UI thread and render thread.
/// </para>
/// </remarks>
internal sealed unsafe class StrideTopLevelImpl : ITopLevelImpl, IFramebufferPlatformSurface
{
    private readonly IPlatformRenderSurface[] surfaces;
    private readonly IKeyboardDevice keyboard;
    private readonly MouseDevice mouseDevice;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private double scaling = 1;
    private Size clientSize;

    private byte* pixels;
    private PixelSize pixelSize;
    private bool previousFrameRetained;

    public StrideTopLevelImpl()
    {
        // The surface the renderer draws to, on the GPU or on the CPU
        surfaces = [this];
        keyboard = AvaloniaLocator.Current.GetRequiredService<IKeyboardDevice>();
        mouseDevice = new MouseDevice(new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true));
        Compositor = StrideAvaloniaPlatform.Compositor;
    }

    public IPlatformRenderSurface[] Surfaces => surfaces;

    public IMouseDevice MouseDevice => mouseDevice;

    public Compositor Compositor { get; }

    public IInputRoot? InputRoot { get; private set; }

    public double DesktopScaling => scaling;

    public IPlatformHandle? Handle => null;

    public Size ClientSize => clientSize;

    public double RenderScaling => scaling;

    public Action<RawInputEventArgs>? Input { get; set; }

    public Action<Rect>? Paint { get; set; }

    public Action<Size, WindowResizeReason>? Resized { get; set; }

    public Action<double>? ScalingChanged { get; set; }

    public Action<WindowTransparencyLevel>? TransparencyLevelChanged { get; set; }

    public Action? Closed { get; set; }

    public Action? LostFocus { get; set; }

    public WindowTransparencyLevel TransparencyLevel => WindowTransparencyLevel.None;

    public AcrylicPlatformCompensationLevels AcrylicCompensationLevels { get; } = new(1, 1, 1);

    /// <summary>
    /// Gets a number that changes each time Avalonia renders a frame of this top level.
    /// </summary>
    public int FrameVersion { get; private set; }

    /// <summary>
    /// Gets the cursor that Avalonia asks for over this top level.
    /// </summary>
    public StandardCursorType Cursor { get; private set; } = StandardCursorType.Arrow;

    /// <summary>
    /// Gets the texture of the last frame when the renderer drew it on the game's graphics device, or <c>null</c> when it
    /// drew it on the CPU (<see cref="TryGetFrame"/>).
    /// </summary>
    public Texture? GpuFrame { get; private set; }

    /// <summary>
    /// Sets the size of the top level in pixels and its scaling (pixels per logical unit).
    /// </summary>
    public void Resize(PixelSize size, double scaling)
    {
        if (this.scaling != scaling)
        {
            this.scaling = scaling;
            ScalingChanged?.Invoke(scaling);
        }
        var newClientSize = size.ToSize(scaling);
        if (clientSize != newClientSize)
        {
            clientSize = newClientSize;
            Resized?.Invoke(newClientSize, WindowResizeReason.Unspecified);
        }
    }

    /// <summary>
    /// Gets the last frame the renderer drew on the CPU: premultiplied RGBA, 4 bytes a pixel, rows packed.
    /// </summary>
    /// <returns><c>false</c> before the first frame, when the last one was rendered on the GPU, and when it has no pixels.</returns>
    public bool TryGetFrame(out IntPtr data, out PixelSize size, out int rowPitch)
    {
        data = (IntPtr)pixels;
        size = pixelSize;
        rowPitch = pixelSize.Width * 4;
        return FrameVersion > 0 && pixels != null && GpuFrame == null && pixelSize.Width > 0 && pixelSize.Height > 0;
    }

    /// <summary>
    /// Takes a frame the renderer drew into a texture on the game's graphics device.
    /// </summary>
    internal void OnGpuFrameRendered(Texture texture)
    {
        if (GpuFrame == null)
            FreePixels();
        GpuFrame = texture;
        FrameVersion++;
    }

    /// <summary>
    /// Forgets the texture of the GPU frames when it goes (the graphics device went, or the size changed).
    /// </summary>
    internal void OnGpuFrameReleased(Texture texture)
    {
        if (ReferenceEquals(GpuFrame, texture))
            GpuFrame = null;
    }

    public void SetInputRoot(IInputRoot inputRoot) => InputRoot = inputRoot;

    public Point PointToClient(PixelPoint point) => point.ToPoint(1);

    public PixelPoint PointToScreen(Point point) => PixelPoint.FromPoint(point, 1);

    public void SetCursor(ICursorImpl? cursor)
    {
        Cursor = (cursor as StrideCursorFactory.CursorImpl)?.Type ?? StandardCursorType.Arrow;
    }

    public IPopupImpl? CreatePopup() => null;

    public void SetTransparencyLevelHint(IReadOnlyList<WindowTransparencyLevel> transparencyLevels)
    {
    }

    public void SetFrameThemeVariant(PlatformThemeVariant? themeVariant)
    {
    }

    public object? TryGetFeature(Type featureType) => null;

    public void Dispose()
    {
        FreePixels();
        GpuFrame = null;
    }

    IFramebufferRenderTarget IFramebufferPlatformSurface.CreateFramebufferRenderTarget()
        => new FramebufferRenderTarget(this, StrideAvaloniaPlatform.CaptureRenderTargetLoss());

    private ILockedFramebuffer Lock(IRenderTarget.RenderTargetSceneInfo sceneInfo, out FramebufferLockProperties properties)
    {
        var size = sceneInfo.Size;
        if (size != pixelSize || pixels == null)
        {
            FreePixels();
            var byteCount = (nuint)Math.Max(1, size.Width) * (nuint)Math.Max(1, size.Height) * 4;
            pixels = (byte*)NativeMemory.AllocZeroed(byteCount);
            pixelSize = size;
            previousFrameRetained = false;
        }

        properties = new FramebufferLockProperties(previousFrameRetained);
        var dpi = 96 * sceneInfo.Scaling;
        return new LockedFramebuffer((IntPtr)pixels, pixelSize, pixelSize.Width * 4, new Vector(dpi, dpi),
            PixelFormat.Rgba8888, AlphaFormat.Premul, OnFrameRendered);
    }

    private void OnFrameRendered()
    {
        previousFrameRetained = true;
        GpuFrame = null;
        FrameVersion++;
    }

    private void FreePixels()
    {
        if (pixels != null)
        {
            NativeMemory.Free(pixels);
            pixels = null;
        }
        pixelSize = default;
    }

    private ulong Timestamp => (ulong)clock.ElapsedMilliseconds;

    /// <summary>
    /// The render target of the CPU frames, corrupted once the renderer says so (<paramref name="isLost"/>): Avalonia
    /// then creates the render target again, on the game's device if one came.
    /// </summary>
    private sealed class FramebufferRenderTarget(StrideTopLevelImpl topLevel, Func<bool> isLost) : IFramebufferRenderTarget
    {
        public bool RetainsFrameContents => true;

        public PlatformRenderTargetState State => isLost() ? PlatformRenderTargetState.Corrupted : PlatformRenderTargetState.Ready;

        public ILockedFramebuffer Lock(IRenderTarget.RenderTargetSceneInfo sceneInfo, out FramebufferLockProperties properties)
            => topLevel.Lock(sceneInfo, out properties);

        public void Dispose()
        {
        }
    }

    public void RaisePointer(RawPointerEventType type, Point position, RawInputModifiers modifiers)
    {
        if (InputRoot is { } root)
            Input?.Invoke(new RawPointerEventArgs(mouseDevice, Timestamp, root, type, position, modifiers));
    }

    public void RaiseWheel(Point position, Vector delta, RawInputModifiers modifiers)
    {
        if (InputRoot is { } root)
            Input?.Invoke(new RawMouseWheelEventArgs(mouseDevice, Timestamp, root, position, delta, modifiers));
    }

    public bool RaiseKey(RawKeyEventType type, Key key, RawInputModifiers modifiers)
    {
        if (InputRoot is not { } root || Input is not { } input)
            return false;
        var args = new RawKeyEventArgs(keyboard, Timestamp, root, type, key, modifiers, PhysicalKey.None, keySymbol: null);
        input(args);
        return args.Handled;
    }

    public void RaiseText(string text)
    {
        if (InputRoot is { } root)
            Input?.Invoke(new RawTextInputEventArgs(keyboard, Timestamp, root, text));
    }
}
