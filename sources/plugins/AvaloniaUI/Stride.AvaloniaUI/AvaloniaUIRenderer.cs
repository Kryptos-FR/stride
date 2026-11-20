// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.AvaloniaUI;

/// <summary>
/// The renderer that draws the Avalonia views of the games of a process.
/// </summary>
public enum AvaloniaUIRenderer
{
    /// <summary>
    /// Skia draws whole frames, on the game's Vulkan device when it can share it, otherwise on the CPU (the Stride.AvaloniaUI.Skia package).
    /// </summary>
    Skia,
}
