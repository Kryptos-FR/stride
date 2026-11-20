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
using Avalonia.Threading;

namespace Stride.AvaloniaUI.Platform;

/// <summary>
/// The Avalonia platform of a Stride game: no native windows, a dispatcher and a render loop that the game loop drives,
/// and offscreen top levels that the game draws.
/// </summary>
public static class StrideAvaloniaPlatform
{
    private static StrideDispatcherImpl? dispatcher;
    private static StrideRenderTimer? renderTimer;

    /// <summary>
    /// Gets whether Avalonia runs on the Stride platform in this process.
    /// </summary>
    public static bool IsInitialized => dispatcher != null;

    /// <summary>
    /// Gets whether the current thread is Avalonia's UI thread (the game thread that set Avalonia up).
    /// </summary>
    public static bool IsUIThread => dispatcher?.CurrentThreadIsLoopThread ?? false;

    /// <summary>
    /// Uses the Stride platform, with Skia rendering on the CPU and HarfBuzz text shaping.
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
    internal static bool Render() => renderTimer?.Render() ?? false;

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
