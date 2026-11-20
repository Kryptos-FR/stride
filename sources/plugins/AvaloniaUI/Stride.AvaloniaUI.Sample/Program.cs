// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Linq;
using Avalonia;
using Stride.AvaloniaUI.Platform;
using Stride.AvaloniaUI.Skia;

namespace Stride.AvaloniaUI.Sample;

/// <summary>
/// Starts Orb Hunt: sets Avalonia up with the game's own application, then runs the game.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs the game. <c>--native</c> draws the UI with Stride's native renderer instead of Skia.
    /// </summary>
    public static void Main(string[] args)
    {
        var renderer = args.Contains("--native", StringComparer.OrdinalIgnoreCase) ? AvaloniaUIRenderer.Native : AvaloniaUIRenderer.Skia;

        // Avalonia runs on the game thread: set it up there, before the game runs, with the game's application (theme, styles,
        // resources). A game that doesn't do this gets the Fluent theme, dark.
        AppBuilder.Configure<SampleApp>()
            .UseStride(renderer)
            .UseSkiaFallback()
            .WithInterFont()
            .SetupWithoutStarting();

        using var game = new OrbHuntGame(renderer);
        game.Run();
    }
}
