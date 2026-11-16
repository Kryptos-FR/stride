// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Linq;
using Stride.AvaloniaUI.Platform;
using Stride.AvaloniaUI.Rendering;
using Stride.AvaloniaUI.Skia;
using Stride.Core.Reflection;
using Xunit;

namespace Stride.AvaloniaUI.Tests.Rendering;

/// <summary>
/// Checks how the core finds the Skia package and chooses the renderer.
/// </summary>
public sealed class PackageTests
{
    [Fact]
    public void CoreDoesNotReferenceSkia()
    {
        var names = typeof(AvaloniaUISystem).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToList();
        Assert.DoesNotContain("SkiaSharp", names);
        Assert.DoesNotContain("Avalonia.Skia", names);
    }

    [Fact]
    public void SkiaPackageIsFoundAmongTheRegisteredAssemblies()
        // On the Avalonia thread, whose setup registers the plugins as a game does
        => TestAvalonia.Run(() => Assert.IsType<SkiaPackage>(SkiaPackageLocator.Find(AssemblyRegistry.FindAll())));

    [Fact]
    public void NoSkiaPackageAmongOtherAssemblies()
        => Assert.Null(SkiaPackageLocator.Find([typeof(AvaloniaUISystem).Assembly]));

    [Fact]
    public void SkiaRendererWithoutTheSkiaPackageSaysWhatToReference()
    {
        var error = Assert.Throws<InvalidOperationException>(() => RendererBackend.Create(AvaloniaUIRenderer.Skia, skia: null));
        Assert.Contains("Stride.AvaloniaUI.Skia", error.Message);
    }

    [Fact]
    public void GameSetupUsesTheWholeFrameSkiaRenderer()
        => TestAvalonia.Run(() => Assert.Equal(AvaloniaUIRenderer.Skia, StrideAvaloniaPlatform.Renderer));
}
