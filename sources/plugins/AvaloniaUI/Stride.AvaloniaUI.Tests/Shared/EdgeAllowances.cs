// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Graphics;

namespace Stride.AvaloniaUI.Tests;

/// <summary>
/// Gives the edge allowances of the shape cases and the reference views on the device a test runs on.
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
        => platform == PlatformType.Linux && driverId == "MesaLLVMPipe" ? measured : measured + (measured + 9) / 10;
}
