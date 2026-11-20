// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Xunit;

namespace Stride.AvaloniaUI.Tests;

/// <summary>
/// Skips the tests of what the Avalonia UI doesn't do on macOS yet, each with its reason.
/// </summary>
internal static class MacOS
{
    /// <summary>
    /// Skips a test that needs Skia on the game's device: on the macOS runner (MoltenVK), Skia stays on the CPU.
    /// </summary>
    public static void SkipSkiaOnTheGpu()
        => Skip.If(OperatingSystem.IsMacOS(), "Skia doesn't render on the game's Vulkan device on macOS yet (MoltenVK); it renders on the CPU.");

    /// <summary>
    /// Skips a comparison with the reference views' golds, which have no macOS folder yet.
    /// </summary>
    public static void SkipGolds()
        => Skip.If(OperatingSystem.IsMacOS(), "The reference views have no macOS golds yet.");

    /// <summary>
    /// Skips a comparison of render target bitmaps' formats: Skia makes them RGBA on macOS, where the core makes them BGRA.
    /// </summary>
    public static void SkipRenderTargetFormat()
        => Skip.If(OperatingSystem.IsMacOS(), "Skia's render target bitmaps are RGBA on macOS, and the core's are BGRA.");
}
