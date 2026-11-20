// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Avalonia.Platform;
using Avalonia.Skia;
using SkiaSharp;
using Stride.Graphics;

namespace Stride.AvaloniaUI.Platform.Gpu;

/// <summary>
/// The render target of a top level on the game's graphics device: Skia draws its frames into a texture of the device,
/// which the game then draws.
/// </summary>
/// <remarks>
/// The texture is in the render target layout when Skia draws and stays in it (see <see cref="VulkanInterop"/>): the
/// game moves it to the shader resource layout when it draws it.
/// </remarks>
internal sealed class StrideSkiaRenderTarget : ISkiaGpuRenderTarget
{
    private const uint ColorAttachmentOptimalLayout = 2; // VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL

    private readonly StrideSkiaGpu gpu;
    private readonly StrideTopLevelImpl topLevel;
    private Texture? texture;

    public StrideSkiaRenderTarget(StrideSkiaGpu gpu, StrideTopLevelImpl topLevel)
    {
        this.gpu = gpu;
        this.topLevel = topLevel;
    }

    /// <summary>
    /// Gets the state of the render target: corrupted once the GPU is lost, so Avalonia creates another one.
    /// </summary>
    public PlatformRenderTargetState State => gpu.IsLost ? PlatformRenderTargetState.Corrupted : PlatformRenderTargetState.Ready;

    public ISkiaGpuRenderSession BeginRenderingSession(IRenderTarget.RenderTargetSceneInfo sceneInfo)
    {
        if (gpu.GrContext is not { } grContext || gpu.Device is not { } device)
            throw new PlatformGraphicsContextLostException();

        var size = sceneInfo.Size;
        if (size.Width <= 0 || size.Height <= 0)
            throw new InvalidOperationException($"A view can't be rendered at {size}.");

        if (texture == null || texture.Width != size.Width || texture.Height != size.Height)
        {
            ReleaseTexture();
            texture = VulkanInterop.CreateRenderTarget(device, size.Width, size.Height);
        }
        gpu.PrepareForSkia(texture);

        var image = VulkanInterop.GetImage(texture);
        var imageInfo = new GRVkImageInfo
        {
            CurrentQueueFamily = gpu.QueueFamilyIndex,
            Format = image.Format,
            Image = image.Image,
            ImageLayout = ColorAttachmentOptimalLayout,
            ImageTiling = image.Tiling,
            ImageUsageFlags = image.Usage,
            LevelCount = image.LevelCount,
            SampleCount = image.SampleCount,
            Alloc = new GRVkAlloc { Memory = image.Memory, Size = image.MemorySize },
        };

        grContext.ResetContext();
        using var backendRenderTarget = new GRBackendRenderTarget(size.Width, size.Height, imageInfo);
        // The texture is UNorm: Skia blends the sRGB colors as on the CPU, and the game samples them as sRGB when it draws in linear
        var surface = SKSurface.Create(grContext, backendRenderTarget, GRSurfaceOrigin.TopLeft, SKColorType.Rgba8888, SKColorSpace.CreateSrgb())
            ?? throw new InvalidOperationException("Skia can't draw into the texture of a view.");
        return new Session(this, grContext, surface, sceneInfo.Scaling);
    }

    /// <summary>
    /// Releases the texture (the device goes, or the size changed); the next session creates it again.
    /// </summary>
    public void ReleaseTexture()
    {
        if (texture == null)
            return;
        topLevel.OnGpuFrameReleased(texture);
        texture.Dispose();
        texture = null;
    }

    public void Dispose()
    {
        ReleaseTexture();
        gpu.Remove(this);
    }

    private void EndSession()
    {
        gpu.Submit();
        if (texture != null)
            topLevel.OnGpuFrameRendered(texture);
    }

    private sealed class Session(StrideSkiaRenderTarget renderTarget, GRContext grContext, SKSurface surface, double scaling) : ISkiaGpuRenderSession
    {
        public GRContext GrContext => grContext;

        public SKSurface SkSurface => surface;

        public double ScaleFactor => scaling;

        public GRSurfaceOrigin SurfaceOrigin => GRSurfaceOrigin.TopLeft;

        public void Dispose()
        {
            surface.Canvas.Flush();
            surface.Dispose();
            renderTarget.EndSession();
        }
    }
}
