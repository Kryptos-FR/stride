// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Xunit;

namespace Stride.AvaloniaUI.Tests;

/// <summary>
/// Checks the edge allowances given to each device: the counts measured on Linux with Lavapipe, and some headroom elsewhere.
/// </summary>
public sealed class EdgeAllowancesTests
{
    [Fact]
    public void LavapipeOnLinuxKeepsTheCountMeasured()
        => Assert.Equal(144, EdgeAllowances.On(PlatformType.Linux, "MesaLLVMPipe", 144));

    [Theory]
    [InlineData(PlatformType.Windows, "MesaLLVMPipe")]
    [InlineData(PlatformType.Windows, "Microsoft")]
    [InlineData(PlatformType.macOS, "MoltenVK")]
    [InlineData(PlatformType.Linux, null)]
    public void OtherDevicesGetATenthMore(PlatformType platform, string? driverId)
    {
        Assert.Equal(159, EdgeAllowances.On(platform, driverId, 144));
        Assert.Equal(275, EdgeAllowances.On(platform, driverId, 250));
        Assert.Equal(5, EdgeAllowances.On(platform, driverId, 4));
        Assert.Equal(0, EdgeAllowances.On(platform, driverId, 0));
    }
}
