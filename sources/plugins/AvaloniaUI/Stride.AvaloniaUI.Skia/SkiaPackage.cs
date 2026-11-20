// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Platform;
using Avalonia.Skia;
using Stride.AvaloniaUI.Rendering;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.AvaloniaUI.Skia.Fallback;
using Stride.AvaloniaUI.Skia.WholeFrame;

namespace Stride.AvaloniaUI.Skia;

/// <summary>
/// What the Skia package gives the core: the whole-frame Skia renderer, and the fallback of the native renderer.
/// </summary>
internal sealed class SkiaPackage : IAvaloniaUISkia
{
    public RendererBackend CreateWholeFrameRenderer()
    {
        InitializeSkia();
        return new SkiaRendererBackend();
    }

    public IRenderFallback CreateFallback()
    {
        InitializeSkia();
        var locator = AvaloniaLocator.Current;
        return new SkiaRenderFallback(locator.GetRequiredService<IPlatformRenderInterface>(), locator.GetRequiredService<IFontManagerImpl>());
    }

    // As UseSkia() does: binds Skia's render interface and font manager
    private static void InitializeSkia() => SkiaPlatform.Initialize(AvaloniaLocator.Current.GetService<SkiaOptions>() ?? new SkiaOptions());
}
