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
using Stride.AvaloniaUI.Rendering;
using Stride.Core.Reflection;
using Stride.Graphics;

namespace Stride.AvaloniaUI.Platform;

/// <summary>
/// The Avalonia platform of a Stride game: no native windows, a dispatcher and a render loop that the game loop drives,
/// and offscreen top levels that the game draws with the renderer chosen when Avalonia is set up.
/// </summary>
public static class StrideAvaloniaPlatform
{
    private static StrideDispatcherImpl? dispatcher;
    private static StrideRenderTimer? renderTimer;
    private static RendererBackend? backend;
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
    /// Gets the renderer of the Avalonia views, or <c>null</c> before Avalonia is set up on the Stride platform.
    /// </summary>
    public static AvaloniaUIRenderer? Renderer { get; private set; }

    /// <summary>
    /// Uses the Stride platform, with a renderer of the Avalonia views and HarfBuzz text shaping.
    /// </summary>
    /// <remarks>
    /// Call <see cref="AppBuilder.SetupWithoutStarting"/> on the game thread, which becomes Avalonia's UI thread. Avalonia has one
    /// renderer per process. The renderers need the Stride.AvaloniaUI.Skia package for now.
    /// </remarks>
    public static AppBuilder UseStride(this AppBuilder builder, AvaloniaUIRenderer renderer = AvaloniaUIRenderer.Skia)
        => builder
            .UseStandardRuntimePlatformSubsystem()
            .UseWindowingSubsystem(Initialize, "Stride")
            .UseRenderingSubsystem(() => InitializeRenderer(renderer), "Skia")
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
        if (renderTimer == null || backend == null)
            return false;

        backend.BeginRender();
        try
        {
            return renderTimer.Render();
        }
        finally
        {
            backend.EndRender();
        }
    }

    /// <summary>
    /// Gets the compositor of the top levels, which renders with the graphics of the renderer.
    /// </summary>
    internal static Compositor Compositor
        => compositor ??= new Compositor(backend?.PlatformGraphics);

    /// <summary>
    /// Makes the renderer draw the views on a game's graphics device from the next frame on, if it can.
    /// </summary>
    /// <returns><c>true</c> if it draws on the device, <c>false</c> if it draws on the CPU.</returns>
    internal static bool AttachGraphicsDevice(GraphicsDevice device) => backend?.AttachGraphicsDevice(device) ?? false;

    /// <summary>
    /// Makes the renderer draw the views on the CPU from the next frame on, if it draws on a graphics device that goes.
    /// </summary>
    /// <remarks>
    /// The renderer releases what it has on the device now, while the device is still there.
    /// </remarks>
    internal static void DetachGraphicsDevice(GraphicsDevice device) => backend?.DetachGraphicsDevice(device);

    /// <summary>
    /// Gets a function that says when a CPU render target created now is corrupted.
    /// </summary>
    internal static Func<bool> CaptureRenderTargetLoss() => backend?.CaptureRenderTargetLoss() ?? (static () => false);

    /// <summary>
    /// Makes the compositor redraw the whole top levels at the next frame, on render targets it creates again.
    /// </summary>
    /// <remarks>
    /// The renderer calls it when the render targets of the top levels are corrupted, such as when their GPU is lost.
    /// </remarks>
    internal static void RedrawTopLevels() => compositor?.RequestCommitAsync();

    private static void InitializeRenderer(AvaloniaUIRenderer renderer)
    {
        // Given by UseSkiaFallback(), otherwise found among the plugins the game references
        var skia = AvaloniaLocator.Current.GetService<IAvaloniaUISkia>() ?? SkiaPackageLocator.Find(AssemblyRegistry.FindAll());
        backend = RendererBackend.Create(renderer, skia);
        Renderer = renderer;
    }

    private static void Initialize()
    {
        dispatcher = new StrideDispatcherImpl(Thread.CurrentThread);
        Dispatcher.InitializeUIThreadDispatcher(dispatcher);
        renderTimer = new StrideRenderTimer();

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
