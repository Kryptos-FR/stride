// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Stride.AvaloniaUI.Platform.Gpu;
using Stride.Graphics;

namespace Stride.AvaloniaUI.Platform;

/// <summary>
/// The Avalonia platform of a Stride game: no native windows, a dispatcher and a render loop that the game loop drives,
/// and offscreen top levels that the game draws. Skia renders them on the game's Vulkan device when it can, otherwise
/// on the CPU.
/// </summary>
public static class StrideAvaloniaPlatform
{
    private static StrideDispatcherImpl? dispatcher;
    private static StrideRenderTimer? renderTimer;
    private static StridePlatformGraphics? graphics;
    private static Compositor? compositor;

    /// <summary>
    /// Gets whether Avalonia runs on the Stride platform in this process.
    /// </summary>
    public static bool IsInitialized => dispatcher != null;

    /// <summary>
    /// Gets whether the current thread is Avalonia's UI thread (the game thread that set Avalonia up).
    /// </summary>
    public static bool IsUIThread => dispatcher?.CurrentThreadIsLoopThread ?? false;

    /// <summary>
    /// Uses the Stride platform, with Skia rendering and HarfBuzz text shaping.
    /// </summary>
    /// <remarks>Call <see cref="AppBuilder.SetupWithoutStarting"/> on the game thread, which becomes Avalonia's UI thread.</remarks>
    public static AppBuilder UseStride(this AppBuilder builder)
        => builder
            .UseStandardRuntimePlatformSubsystem()
            .UseWindowingSubsystem(Initialize, "Stride")
            .UseSkia()
            .UseHarfBuzz();

    /// <summary>
    /// Runs the dispatcher's due timers and queued jobs (game thread, once per update).
    /// </summary>
    internal static void Pump() => dispatcher?.Pump();

    /// <summary>
    /// Runs Avalonia's render loop: layout, composition and rendering of the changed surfaces (game thread, once per draw).
    /// </summary>
    internal static bool Render()
    {
        if (renderTimer == null)
            return false;

        // Skia submits its commands to the device's queue at various points of the rendering
        using var queueScope = graphics?.Current?.LockQueue() ?? default;
        return renderTimer.Render();
    }

    /// <summary>
    /// Gets the compositor of the top levels, which renders with <see cref="StridePlatformGraphics"/>.
    /// </summary>
    internal static Compositor Compositor
        => compositor ??= new Compositor(SkiaGpuProxy.IsSupported ? graphics : null);

    /// <summary>
    /// Makes Skia render the views on a game's graphics device from the next frame on, if it can share it (Vulkan).
    /// </summary>
    /// <returns><c>true</c> if Skia renders on the device, <c>false</c> if it renders on the CPU.</returns>
    internal static bool AttachGraphicsDevice(GraphicsDevice device)
    {
        if (graphics == null || !SkiaGpuProxy.IsSupported)
            return false;

        var previous = graphics.Current;
        var rendersOnDevice = graphics.Attach(device);
        if (graphics.Current != previous)
            RedrawTopLevels();
        return rendersOnDevice;
    }

    /// <summary>
    /// Makes Skia render the views on the CPU from the next frame on, if it renders on a graphics device that goes.
    /// </summary>
    /// <remarks>
    /// Skia releases what it has on the device now, while the device is still there.
    /// </remarks>
    internal static void DetachGraphicsDevice(GraphicsDevice device)
    {
        if (graphics != null && graphics.Detach(device))
            RedrawTopLevels();
    }

    /// <summary>
    /// Gets the GPU Skia renders the views with, if Avalonia was set up on the Stride platform.
    /// </summary>
    internal static StrideSkiaGpu? CurrentGpu => graphics?.Current;

    // The render targets of the top levels are corrupted once their GPU is lost: the compositor creates them again on
    // the current GPU and redraws the whole top levels when it renders, at the next frame
    private static void RedrawTopLevels() => compositor?.RequestCommitAsync();

    private static void Initialize()
    {
        dispatcher = new StrideDispatcherImpl(Thread.CurrentThread);
        Dispatcher.InitializeUIThreadDispatcher(dispatcher);
        renderTimer = new StrideRenderTimer();
        graphics = new StridePlatformGraphics();

        AvaloniaLocator.CurrentMutable
            .Bind<ICursorFactory>().ToConstant(new StrideCursorFactory())
            .Bind<IPlatformSettings>().ToSingleton<DefaultPlatformSettings>()
            .Bind<IKeyboardDevice>().ToConstant(new KeyboardDevice())
            .Bind<IRenderLoop>().ToConstant(RenderLoop.FromTimer(renderTimer))
            .Bind<IWindowingPlatform>().ToConstant(new StrideWindowingPlatform())
            .Bind<PlatformHotkeyConfiguration>().ToSingleton<PlatformHotkeyConfiguration>()
            .Bind<KeyGestureFormatInfo>().ToConstant(new KeyGestureFormatInfo(new Dictionary<Key, string>()));
    }

    private sealed class StrideWindowingPlatform : IWindowingPlatform
    {
        public IWindowImpl CreateWindow() => throw new PlatformNotSupportedException("A Stride game has no native Avalonia windows.");

        public ITopLevelImpl CreateEmbeddableTopLevel() => new StrideTopLevelImpl();

        public IWindowImpl CreateEmbeddableWindow() => throw new PlatformNotSupportedException("A Stride game has no native Avalonia windows.");

        public ITrayIconImpl? CreateTrayIcon() => null;

        public void GetWindowsZOrder(ReadOnlySpan<IWindowImpl> windows, Span<long> zOrder) => zOrder.Clear();
    }
}
