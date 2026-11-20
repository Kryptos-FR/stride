// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Linq;
using Avalonia;
using Stride.AvaloniaUI;
using Stride.AvaloniaUI.Platform;
using Stride.AvaloniaUI.Skia;
using Stride.Engine;

namespace OrbHunt;

/// <summary>
/// Orb Hunt: a small game whose menus and HUD are Avalonia views written in XAML, drawn by Stride over the game.
/// </summary>
/// <remarks>
/// The game is its package's assets (<c>OrbHunt.Game.sdpkg</c>), as any game made in Game Studio: the game
/// settings, the main scene with the field and the two views, and the graphics compositor.
/// </remarks>
public sealed class OrbHuntGame(AvaloniaUIRenderer renderer) : Game
{
    /// <summary>
    /// Gets the renderer Avalonia was set up with, which draws the views.
    /// </summary>
    public AvaloniaUIRenderer Renderer { get; } = renderer;

    /// <summary>
    /// Sets Avalonia up with the game's own application, then runs the game. The executable of each platform calls it.
    /// </summary>
    /// <param name="args">The command line: <c>--native</c> draws the UI with Stride's native renderer instead of Skia.</param>
    public static void Run(string[] args)
    {
        var renderer = args.Contains("--native", StringComparer.OrdinalIgnoreCase) ? AvaloniaUIRenderer.Native : AvaloniaUIRenderer.Skia;

        // Avalonia runs on the game thread: set it up there, before the game runs, with the game's application (theme, styles,
        // resources). A game that doesn't do this gets the Fluent theme, dark.
        AppBuilder.Configure<OrbHuntApp>()
            .UseStride(renderer)
            .UseSkiaFallback()
            .WithInterFont()
            .SetupWithoutStarting();

        using var game = new OrbHuntGame(renderer);
        game.Run();
    }
}
