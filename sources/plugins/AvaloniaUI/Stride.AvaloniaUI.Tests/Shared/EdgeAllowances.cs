// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Graphics;

namespace Stride.AvaloniaUI.Tests;

/// <summary>
/// Gives the edge allowances of the shape cases, the text cases and the reference views on the device a test runs on.
/// </summary>
internal static class EdgeAllowances
{
    /// <summary>
    /// Gets the allowance on the adapter a game ran on.
    /// </summary>
    public static int On(GraphicsAdapter adapter, int measured) => On(Stride.Core.Platform.Type, adapter.DriverInfo?.DriverId, measured);

    /// <summary>
    /// Gets the allowance on a system and a driver, named as <see cref="GraphicsAdapter.DriverInfo"/> names it: the count
    /// measured on Linux with Lavapipe, where the counts come from, and a tenth more, rounded up, anywhere else.
    /// </summary>
    /// <remarks>
    /// Other rasterizers, and Skia's text on other systems, put a few more edge pixels beyond the tolerance: one more on
    /// Windows (WARP for Direct3D, Lavapipe for Vulkan), and up to 14 more of 250 on the macOS runner.
    /// </remarks>
    public static int On(PlatformType platform, string? driverId, int measured)
        => platform == PlatformType.Linux && driverId == "MesaLLVMPipe" ? measured : WithATenthMore(measured);

    /// <summary>
    /// Gets the allowance of a frame with native text on the adapter a game ran on (<see cref="ForText(PlatformType, string?, int, OtherGlyphs)"/>).
    /// </summary>
    public static int ForText(GraphicsAdapter adapter, int measured, OtherGlyphs otherGlyphs)
        => ForText(Stride.Core.Platform.Type, adapter.DriverInfo?.DriverId, measured, otherGlyphs);

    /// <summary>
    /// Gets the allowance of a frame with native text on a system and a driver: where Skia rasterizes glyphs with FreeType,
    /// as the native renderer does (Linux, Android), the edge allowance (<see cref="On(PlatformType, string?, int)"/>);
    /// elsewhere, the count measured with Skia's own glyphs there, CoreText's on macOS and iOS and DirectWrite's otherwise,
    /// and a tenth more.
    /// </summary>
    public static int ForText(PlatformType platform, string? driverId, int measured, OtherGlyphs otherGlyphs)
        => UsesFreeType(platform) ? On(platform, driverId, measured)
            : WithATenthMore(platform is PlatformType.macOS or PlatformType.iOS ? otherGlyphs.CoreText : otherGlyphs.DirectWrite);

    /// <summary>
    /// Gets whether Skia rasterizes glyphs with FreeType on a system, as the native renderer does everywhere.
    /// </summary>
    public static bool UsesFreeType(PlatformType platform) => platform is PlatformType.Linux or PlatformType.Android;

    private static int WithATenthMore(int count) => count + (count + 9) / 10;
}

/// <summary>
/// How many pixels of a page or case with text the native renderer draws more than 12 away from Skia on the CPU where Skia's
/// glyphs aren't FreeType's: with DirectWrite's (Windows) and with CoreText's (macOS).
/// </summary>
/// <param name="DirectWrite">The count with DirectWrite's glyphs.</param>
/// <param name="CoreText">The count with CoreText's glyphs.</param>
internal readonly record struct OtherGlyphs(int DirectWrite, int CoreText);
