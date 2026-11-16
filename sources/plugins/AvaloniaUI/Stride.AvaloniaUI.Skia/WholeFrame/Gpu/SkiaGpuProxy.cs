// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using Avalonia.Skia;

namespace Stride.AvaloniaUI.Skia.WholeFrame;

/// <summary>
/// Gives a <see cref="StrideSkiaGpu"/> to Avalonia as the GPU of its Skia backend.
/// </summary>
/// <remarks>
/// <para>
/// Avalonia's Skia backend renders on a GPU it gets as an <c>ISkiaGpu</c>, which is internal to Avalonia.Skia (its
/// public way is a Vulkan context of Avalonia's own, that only renders to windows). This proxy implements that interface
/// at runtime, the methods it has in Avalonia 12 forwarding to the <see cref="StrideSkiaGpu"/>.
/// </para>
/// <para>
/// It needs runtime code generation: where there is none (NativeAOT), <see cref="IsSupported"/> is <c>false</c> and
/// Avalonia renders on the CPU.
/// </para>
/// </remarks>
internal class SkiaGpuProxy : DispatchProxy
{
    private const string SkiaGpuTypeName = "Avalonia.Skia.ISkiaGpu";

    private static readonly Type? SkiaGpuType = typeof(ISkiaGpuRenderTarget).Assembly.GetType(SkiaGpuTypeName, throwOnError: false);

    private StrideSkiaGpu gpu = null!;

    /// <summary>
    /// Gets whether Avalonia's Skia backend can take a <see cref="StrideSkiaGpu"/>.
    /// </summary>
    public static bool IsSupported => SkiaGpuType != null && System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported;

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The proxy only runs where dynamic code is supported.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The proxy only runs where dynamic code is supported.")]
    public static IPlatformGraphicsContext Create(StrideSkiaGpu gpu)
    {
        if (!IsSupported)
            throw new PlatformNotSupportedException($"Avalonia's Skia backend has no {SkiaGpuTypeName}, or this runtime can't generate code.");

        var proxy = (SkiaGpuProxy)DispatchProxy.Create(SkiaGpuType!, typeof(SkiaGpuProxy));
        proxy.gpu = gpu;
        return (IPlatformGraphicsContext)proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        switch (targetMethod?.Name)
        {
            // IPlatformGraphicsContext
            case "get_IsLost":
                return gpu.IsLost;
            case "EnsureCurrent":
                return EmptyScope.Instance;
            case "TryGetFeature":
                return null;
            case "Dispose":
                gpu.Release();
                return null;

            // ISkiaGpu
            case "get_PlatformGraphicsContext":
                return null;
            case "TryCreateRenderTarget":
                return gpu.TryCreateRenderTarget((IEnumerable<IPlatformRenderSurface>)args![0]!);
            case "IsReadyToCreateRenderTarget":
                return gpu.IsReadyToCreateRenderTarget((IEnumerable<IPlatformRenderSurface>)args![0]!);
            case "TryCreateSurface":
                return gpu.TryCreateSurface((PixelSize)args![0]!);
            case "TryGetGrContext":
                return gpu.TryGetGrContext();

            default:
                throw new NotSupportedException($"Avalonia called {targetMethod}, which the Stride platform doesn't know.");
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        public static readonly EmptyScope Instance = new();

        public void Dispose()
        {
        }
    }
}
