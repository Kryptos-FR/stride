// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.Embedding.Offscreen;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using Avalonia.Rendering;

namespace Stride.AvaloniaUI.Platform;

/// <summary>
/// The window of an Avalonia UI in a game: an offscreen top level whose frames Skia renders on the CPU into memory the
/// game uploads to a texture.
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
internal sealed unsafe class StrideTopLevelImpl : OffscreenTopLevelImplBase, IFramebufferPlatformSurface
{
    private readonly IPlatformRenderSurface[] surfaces;
    private readonly IKeyboardDevice keyboard;
    private readonly MouseDevice mouseDevice;
    private readonly Stopwatch clock = Stopwatch.StartNew();

    private byte* pixels;
    private PixelSize pixelSize;
    private bool previousFrameRetained;

    public StrideTopLevelImpl()
    {
        surfaces = [this];
        keyboard = AvaloniaLocator.Current.GetRequiredService<IKeyboardDevice>();
        mouseDevice = new MouseDevice(new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true));
    }

    public override IPlatformRenderSurface[] Surfaces => surfaces;

    public override IMouseDevice MouseDevice => mouseDevice;

    /// <summary>
    /// Gets a number that changes each time Avalonia renders a frame of this top level.
    /// </summary>
    public int FrameVersion { get; private set; }

    /// <summary>
    /// Gets the cursor that Avalonia asks for over this top level.
    /// </summary>
    public StandardCursorType Cursor { get; private set; } = StandardCursorType.Arrow;

    /// <summary>
    /// Sets the size of the top level in pixels and its scaling (pixels per logical unit).
    /// </summary>
    public void Resize(PixelSize size, double scaling)
    {
        if (RenderScaling != scaling)
            RenderScaling = scaling;
        var clientSize = size.ToSize(scaling);
        if (ClientSize != clientSize)
            ClientSize = clientSize;
    }

    /// <summary>
    /// Gets the last rendered frame: premultiplied RGBA, 4 bytes a pixel, rows packed.
    /// </summary>
    /// <returns><c>false</c> before the first frame.</returns>
    public bool TryGetFrame(out IntPtr data, out PixelSize size, out int rowPitch)
    {
        data = (IntPtr)pixels;
        size = pixelSize;
        rowPitch = pixelSize.Width * 4;
        return FrameVersion > 0 && pixels != null;
    }

    public override void SetCursor(ICursorImpl? cursor)
    {
        Cursor = (cursor as StrideCursorFactory.CursorImpl)?.Type ?? StandardCursorType.Arrow;
    }

    public override void Dispose()
    {
        base.Dispose();
        FreePixels();
    }

    IFramebufferRenderTarget IFramebufferPlatformSurface.CreateFramebufferRenderTarget()
        => new FuncFramebufferRenderTarget(Lock, retainsFrameContents: true);

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
