// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Engine;

namespace Stride.AvaloniaUI.Sample;

/// <summary>
/// Orb Hunt: a small game whose menus and HUD are Avalonia views written in XAML, drawn by Stride over the game.
/// </summary>
/// <remarks>
/// The game is its package's assets (<c>Stride.AvaloniaUI.Sample.sdpkg</c>), as any game made in Game Studio: the game
/// settings, the main scene with the field and the two views, and the graphics compositor.
/// </remarks>
public sealed class OrbHuntGame(AvaloniaUIRenderer renderer) : Game
{
    /// <summary>
    /// Gets the renderer Avalonia was set up with, which draws the views.
    /// </summary>
    public AvaloniaUIRenderer Renderer { get; } = renderer;
}
