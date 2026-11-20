// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.AvaloniaUI.Rendering.Native;
using Stride.AvaloniaUI.Tests;
using Stride.Graphics;
using Stride.Graphics.Regression;
using Stride.Shaders;
using Xunit;

namespace Stride.AvaloniaUI.Native.Tests;

/// <summary>
/// Runs games whose Avalonia views the native canvas draws on the game's graphics device.
/// </summary>
public sealed class NativeCanvasTests : GameTestBase
{
    [SkippableFact]
    public void CanvasShaderIsEmbeddedForTheDevice() => TestAvalonia.Run(() =>
    {
        // Direct3D has no bytecode until it is generated on Windows
        Skip.If(AvaloniaCanvasEffect.Bytecode == null && GraphicsDevice.Platform != GraphicsPlatform.Vulkan, $"The canvas shader has no {GraphicsDevice.Platform} bytecode yet.");
        Assert.NotNull(AvaloniaCanvasEffect.Bytecode);
        Assert.Contains(AvaloniaCanvasEffect.Bytecode!.Stages, s => s.Stage == ShaderStage.Vertex);
        Assert.Contains(AvaloniaCanvasEffect.Bytecode!.Stages, s => s.Stage == ShaderStage.Pixel);
    });
}
