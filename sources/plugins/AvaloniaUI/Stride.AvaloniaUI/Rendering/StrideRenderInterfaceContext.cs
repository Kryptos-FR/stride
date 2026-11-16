// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;

namespace Stride.AvaloniaUI.Rendering;

/// <summary>
/// The render context of the native renderer, which makes its render targets.
/// </summary>
internal sealed class StrideRenderInterfaceContext : IPlatformRenderInterfaceContext
{
    public bool IsLost => false;

    public IReadOnlyDictionary<Type, object> PublicFeatures { get; } = new Dictionary<Type, object>();

    public PixelSize? MaxOffscreenRenderTargetPixelSize => null;

    public IRenderTarget CreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces)
        => throw new NotSupportedException("The native renderer of the Avalonia UI has no render targets.");

    public IDrawingContextLayerImpl CreateOffscreenRenderTarget(PixelSize pixelSize, Vector scaling, bool enableTextAntialiasing)
        => throw new NotSupportedException("The native renderer of the Avalonia UI has no offscreen render targets.");

    public object? TryGetFeature(Type featureType) => null;

    public void Dispose()
    {
    }
}
