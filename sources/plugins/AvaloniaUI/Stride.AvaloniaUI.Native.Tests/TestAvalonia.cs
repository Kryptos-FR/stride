// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Avalonia;
using Stride.AvaloniaUI.Platform;
using Stride.AvaloniaUI.Skia;

namespace Stride.AvaloniaUI.Tests;

/// <summary>
/// The Avalonia thread of these tests, with Avalonia set up with the native renderer before any game sets it up.
/// </summary>
internal static class TestAvalonia
{
    /// <summary>
    /// Runs an action on the Avalonia thread once Avalonia is set up, and waits for it.
    /// </summary>
    public static void Run(Action action) => AvaloniaThread.Run(() =>
    {
        if (!StrideAvaloniaPlatform.IsInitialized)
        {
            AppBuilder.Configure<StrideAvaloniaApplication>()
                .UseStride(AvaloniaUIRenderer.Native)
                .UseSkiaFallback()
                .WithInterFont()
                .SetupWithoutStarting();
        }
        action();
    });
}
