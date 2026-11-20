// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using Avalonia.Skia;
using SkiaSharp;
using Stride.AvaloniaUI.Platform;
using Stride.Core.Diagnostics;
using Stride.Graphics;

namespace Stride.AvaloniaUI.Skia.WholeFrame;

/// <summary>
/// Skia on the Vulkan graphics device of a game: it renders the top levels into textures of the device, with the
/// device's queue, so the game draws them without a copy.
/// </summary>
/// <remarks>
/// <para>
/// Avalonia takes it as the GPU of its Skia backend, through <see cref="SkiaGpuProxy"/>. One without a device renders
/// on the CPU, into the framebuffer of each top level.
/// </para>
/// <para>
/// It is lost when its device goes or another one comes (<see cref="Lose"/>): Avalonia then asks
/// <see cref="StridePlatformGraphics"/> for the current one at its next frame, and renders the top levels again.
/// </para>
/// </remarks>
internal sealed class StrideSkiaGpu
{
    private static readonly Logger Log = GlobalLogger.GetLogger("AvaloniaUI");

    private readonly List<StrideSkiaRenderTarget> renderTargets = [];
    private readonly List<LayerSurface> layers = [];
    private readonly GRVkBackendContext? backendContext;
    private readonly object? queueLock;
    private GRContext? grContext;
    private CommandList? commandList;

    private StrideSkiaGpu(GraphicsDevice? device, GRVkBackendContext? backendContext, GRContext? grContext, uint queueFamilyIndex)
    {
        Device = device;
        this.backendContext = backendContext;
        this.grContext = grContext;
        QueueFamilyIndex = queueFamilyIndex;
        if (device != null)
            queueLock = VulkanInterop.GetQueueLock(device);
    }

    /// <summary>
    /// Gets the graphics device Skia renders with, or <c>null</c> when it renders on the CPU.
    /// </summary>
    public GraphicsDevice? Device { get; }

    /// <summary>
    /// Gets Skia's context on the device.
    /// </summary>
    public GRContext? GrContext => IsLost ? null : grContext;

    /// <summary>
    /// Gets the queue family of the device's queue.
    /// </summary>
    public uint QueueFamilyIndex { get; }

    public bool IsLost { get; private set; }

    /// <summary>
    /// Creates a GPU that renders on the CPU, for when the game has no graphics device Skia can share.
    /// </summary>
    public static StrideSkiaGpu CreateWithoutDevice() => new(null, null, null, 0);

    /// <summary>
    /// Creates Skia's context on a graphics device.
    /// </summary>
    /// <returns><c>null</c> if Skia can't share the device (not Vulkan, or Skia failed).</returns>
    public static StrideSkiaGpu? TryCreate(GraphicsDevice device)
    {
        if (!VulkanInterop.IsSupported(device))
            return null;

        var handles = VulkanInterop.GetDevice(device);
        var backendContext = new GRVkBackendContext
        {
            VkInstance = handles.Instance,
            VkPhysicalDevice = handles.PhysicalDevice,
            VkDevice = handles.Device,
            VkQueue = handles.Queue,
            GraphicsQueueIndex = handles.QueueFamilyIndex,
            MaxAPIVersion = handles.ApiVersion,
            GetProcedureAddress = (name, instance, nativeDevice) => VulkanInterop.GetProcAddress(device, name, instance, nativeDevice),
        };

        GRContext? grContext;
        try
        {
            lock (VulkanInterop.GetQueueLock(device))
                grContext = GRContext.CreateVulkan(backendContext);
        }
        catch (Exception e)
        {
            Log.Warning("Skia can't render the Avalonia views on the game's Vulkan device: they render on the CPU.", e);
            grContext = null;
        }

        if (grContext == null)
        {
            backendContext.Dispose();
            return null;
        }
        return new StrideSkiaGpu(device, backendContext, grContext, handles.QueueFamilyIndex);
    }

    /// <summary>
    /// Creates the render target of a top level on the device.
    /// </summary>
    /// <returns><c>null</c> to render on the CPU (no device, or not a top level of the game).</returns>
    public ISkiaGpuRenderTarget? TryCreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces)
    {
        if (GrContext == null)
            return null;

        foreach (var surface in surfaces)
        {
            if (surface is StrideTopLevelImpl topLevel)
            {
                var renderTarget = new StrideSkiaRenderTarget(this, topLevel);
                renderTargets.Add(renderTarget);
                return renderTarget;
            }
        }
        return null;
    }

    public bool IsReadyToCreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces)
    {
        foreach (var surface in surfaces)
        {
            if (surface.IsReady)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Creates a surface for a layer of Avalonia's compositor (the copy of the previous frame it draws the changes on).
    /// </summary>
    /// <remarks>
    /// The GPU keeps them, to release them when it is lost: Skia frees its memory on the device only once its last
    /// surface is gone, and Avalonia releases its layers only when it renders again.
    /// </remarks>
    public ISkiaSurface? TryCreateSurface(PixelSize size)
    {
        if (GrContext is not { } context)
            return null;

        var imageInfo = new SKImageInfo(Math.Max(size.Width, 1), Math.Max(size.Height, 1), SKColorType.Rgba8888, SKAlphaType.Premul);
        if (SKSurface.Create(context, false, imageInfo, new SKSurfaceProperties(SKPixelGeometry.RgbHorizontal)) is not { } surface)
            return null;

        var layer = new LayerSurface(this, surface);
        layers.Add(layer);
        return layer;
    }

    public IScopedResource<GRContext>? TryGetGrContext()
        => GrContext is { } context ? ScopedResource<GRContext>.Create(context, static () => { }) : null;

    /// <summary>
    /// Takes the lock of the device's queue, under which Skia submits its commands.
    /// </summary>
    public QueueScope LockQueue() => new(GrContext != null ? queueLock : null);

    /// <summary>
    /// Moves a texture to the render target layout Skia draws in, with a command list submitted before Skia's commands.
    /// </summary>
    internal void PrepareForSkia(Texture texture)
    {
        var device = Device ?? throw new InvalidOperationException("Skia renders on the CPU.");
        commandList ??= CommandList.New(device);
        commandList.Reset();
        commandList.ResourceBarrierTransition(texture, BarrierLayout.RenderTarget);
        device.ExecuteCommandList(commandList.Close());
    }

    /// <summary>
    /// Submits what Skia drew to the device's queue.
    /// </summary>
    internal void Submit()
    {
        if (GrContext is not { } context)
            return;
        lock (queueLock!)
            context.Flush(true, false);
    }

    internal void Remove(StrideSkiaRenderTarget renderTarget) => renderTargets.Remove(renderTarget);

    /// <summary>
    /// Loses the GPU, because its device goes or another one comes: Skia releases what it has on the device, and
    /// the textures of the top levels go.
    /// </summary>
    public void Lose()
    {
        if (IsLost)
            return;
        IsLost = true;

        foreach (var renderTarget in renderTargets.ToArray())
            renderTarget.ReleaseTexture();
        renderTargets.Clear();

        if (grContext != null)
        {
            // Skia waits for its commands and frees its objects now, while the device is still there; the memory they
            // were in goes with the context, once its last reference is gone (ours and those of the layers)
            lock (queueLock!)
                grContext.AbandonContext(releaseResources: true);
            foreach (var layer in layers.ToArray())
                layer.Dispose();
            grContext.Dispose();
            grContext = null;
        }
        commandList?.Dispose();
        commandList = null;
    }

    /// <summary>
    /// Releases what remains of Skia's context once Avalonia no longer uses it (after the GPU was lost).
    /// </summary>
    public void Release()
    {
        // Avalonia releases its context when the GPU is lost; the current GPU stays
        if (!IsLost)
            return;

        backendContext?.Dispose();
    }

    /// <summary>
    /// A surface of Avalonia's compositor on the device, which goes when the GPU is lost if Avalonia still has it.
    /// </summary>
    private sealed class LayerSurface(StrideSkiaGpu gpu, SKSurface surface) : ISkiaSurface
    {
        private SKSurface? surface = surface;

        public SKSurface Surface => surface ?? throw new ObjectDisposedException(nameof(LayerSurface));

        public bool CanBlit => false;

        public void Blit(SKCanvas canvas) => throw new NotSupportedException();

        public void Dispose()
        {
            surface?.Dispose();
            surface = null;
            gpu.layers.Remove(this);
        }
    }

    /// <summary>
    /// The lock of the device's queue, held while Avalonia renders (Skia submits its commands at various points).
    /// </summary>
    public readonly struct QueueScope : IDisposable
    {
        private readonly object? queueLock;

        public QueueScope(object? queueLock)
        {
            this.queueLock = queueLock;
            if (queueLock != null)
                System.Threading.Monitor.Enter(queueLock);
        }

        public void Dispose()
        {
            if (queueLock != null)
                System.Threading.Monitor.Exit(queueLock);
        }
    }
}
