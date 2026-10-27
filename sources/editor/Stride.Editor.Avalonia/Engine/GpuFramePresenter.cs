// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Stride.Core.Diagnostics;
using Stride.Graphics;

namespace Stride.Editor.Avalonia.Engine;

/// <summary>
/// Option B of the viewport spike: hands the frames of an offscreen game to Avalonia's compositor through a texture
/// they share (<see cref="ICompositionGpuInterop"/>), with no copy through the CPU.
/// </summary>
/// <remarks>
/// <para>
/// Two pairs are supported, the ones Avalonia 12.1 imports:
/// </para>
/// <list type="bullet">
///   <item>Direct3D 11 (the Windows default of the editor) with Avalonia on ANGLE (its Windows default): a texture created
///   with the <c>SharedKeyedMutex</c> and <c>SharedNtHandle</c> options, synchronized by its keyed mutex (the game
///   acquires key 0 and releases key 1, the compositor acquires key 1 and releases key 0).</item>
///   <item>Vulkan (Linux) with Avalonia on OpenGL (GLX, with <c>GL_EXT_memory_object_fd</c> and <c>GL_EXT_semaphore_fd</c>)
///   or on Vulkan: an <see cref="ExportableTexture"/>, synchronized by a pair of binary semaphores.</item>
/// </list>
/// <para>
/// One frame is in flight at a time: the game skips the frames it renders while the compositor still uses the texture.
/// When the compositor has no interop, or none of these pairs, the view falls back to the readback of option B'.
/// </para>
/// </remarks>
internal sealed class GpuFramePresenter : IDisposable
{
    private const string D3D11NtHandle = KnownPlatformGraphicsExternalImageHandleTypes.D3D11TextureNtHandle;
    private const string VulkanFd = KnownPlatformGraphicsExternalImageHandleTypes.VulkanOpaquePosixFileDescriptor;
    private const string VulkanSemaphoreFd = KnownPlatformGraphicsExternalSemaphoreHandleTypes.VulkanOpaquePosixFileDescriptor;

    // Direct3D 11's TextureOptions.SharedKeyedMutex | SharedNtHandle, which only exist in the Direct3D 11 build of Stride.Graphics
    private const TextureOptions SharedKeyedMutexNtHandle = (TextureOptions)(256 | 2048);

    private const int StatePending = 0;
    private const int StateActive = 1;
    private const int StateFailed = 2;

    private readonly OffscreenGameView view;
    private readonly ViewportStats stats;
    private readonly Logger logger;
    private volatile int state;
    private volatile bool frameInFlight;
    private string? handleType;

    // Game thread
    private Texture? sharedTexture;
    private ExportableTexture? exportableTexture;
    private int textureVersion;
    private bool firstFrame;
    private bool signalPending;

    // UI thread
    private ICompositionGpuInterop? interop;
    private CompositionDrawingSurface? surface;
    private int importedVersion = -1;
    private ICompositionImportedGpuImage? importedImage;
    private ICompositionImportedGpuSemaphore? readySemaphore;
    private ICompositionImportedGpuSemaphore? releasedSemaphore;

    private GpuFramePresenter(OffscreenGameView view, ViewportStats stats, Logger logger)
    {
        this.view = view;
        this.stats = stats;
        this.logger = logger;
    }

    /// <summary>
    /// Gets whether the compositor accepted the shared texture: until then (and after a failure) the view shows the frames
    /// by readback.
    /// </summary>
    public bool IsActive => state == StateActive;

    /// <summary>
    /// Creates the presenter of a view. Its interop is set up once the view is in a visual tree, and it reports through
    /// <see cref="IsActive"/> whether the frames go through the GPU or through a readback.
    /// </summary>
    /// <remarks>Called before the game creates its graphics device: a Vulkan device must enable the export extensions.</remarks>
    public static GpuFramePresenter TryCreate(OffscreenGameView view, ViewportStats stats, Logger logger)
    {
        ExportableTexture.EnableExternalObjects = true;
        var presenter = new GpuFramePresenter(view, stats, logger);
        Dispatcher.UIThread.Post(() =>
        {
            if (view.Compositor != null)
                _ = presenter.InitializeAsync();
            else
                view.AttachedToVisualTree += presenter.OnViewAttached;
        });
        return presenter;
    }

    /// <summary>
    /// Copies the back buffer into the shared texture (game thread, before the command list of the frame is closed).
    /// </summary>
    /// <returns><c>false</c> when the compositor still uses the previous frame and this one is dropped.</returns>
    public bool CopyFrame(CommandList commandList, Texture backBuffer)
    {
        if (frameInFlight)
            return false;

        try
        {
            return handleType == D3D11NtHandle
                ? CopyFrameD3D11(commandList, backBuffer)
                : CopyFrameVulkan(commandList, backBuffer);
        }
        catch (Exception e)
        {
            Fail($"The game could not share its frame: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Signals the compositor that the frame is ready (game thread, after the command list was submitted).
    /// </summary>
    public void AfterSubmit()
    {
        if (!signalPending)
            return;
        signalPending = false;
        exportableTexture?.SignalReady();
        firstFrame = false;

        var version = textureVersion;
        var d3dTexture = sharedTexture;
        var vkTexture = exportableTexture;
        var start = stats.Now;
        Dispatcher.UIThread.Post(() => Present(version, d3dTexture, vkTexture, start), DispatcherPriority.Render);
    }

    public void Dispose()
    {
        sharedTexture?.Dispose();
        exportableTexture?.Dispose();
    }

    private bool CopyFrameD3D11(CommandList commandList, Texture backBuffer)
    {
        if (sharedTexture == null || sharedTexture.Width != backBuffer.Width || sharedTexture.Height != backBuffer.Height)
        {
            // The compositor opened its own reference to the texture: this one can go
            sharedTexture?.Dispose();
            sharedTexture = Texture.New2D(commandList.GraphicsDevice, backBuffer.Width, backBuffer.Height, Stride.Graphics.PixelFormat.R8G8B8A8_UNorm,
                TextureFlags.RenderTarget | TextureFlags.ShaderResource, options: SharedKeyedMutexNtHandle);
            textureVersion++;
        }

        // A new keyed mutex is free for key 0; afterwards the compositor releases it with key 0
        if (!sharedTexture.AcquireSync(0, 0))
            return false;
        // The sRGB back buffer and the UNorm texture are in the same typeless group: a plain copy keeps the bytes
        commandList.Copy(backBuffer, sharedTexture);
        sharedTexture.ReleaseSync(1);
        frameInFlight = true;
        signalPending = true;
        return true;
    }

    private bool CopyFrameVulkan(CommandList commandList, Texture backBuffer)
    {
        if (exportableTexture == null || exportableTexture.Texture.Width != backBuffer.Width || exportableTexture.Texture.Height != backBuffer.Height)
        {
            if (exportableTexture == null && !ExportableTexture.IsSupported(commandList.GraphicsDevice))
            {
                Fail("The Vulkan device has no VK_KHR_external_memory_fd and VK_KHR_external_semaphore_fd.");
                return false;
            }
            // The compositor imported its own reference to the memory: this one can go
            exportableTexture?.Dispose();
            exportableTexture = ExportableTexture.Create(commandList.GraphicsDevice, backBuffer.Width, backBuffer.Height);
            textureVersion++;
            firstFrame = true;
        }

        // The compositor signaled the released semaphore when its last update completed
        if (!firstFrame)
            exportableTexture.WaitReleased();
        commandList.Copy(backBuffer, exportableTexture.Texture);
        // The compositor reads the image in the transfer source layout
        commandList.ResourceBarrierTransition(exportableTexture.Texture, BarrierLayout.CopySource);
        frameInFlight = true;
        signalPending = true;
        return true;
    }

    private void OnViewAttached(object? sender, global::Avalonia.VisualTreeAttachmentEventArgs e)
    {
        view.AttachedToVisualTree -= OnViewAttached;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var compositor = view.Compositor;
            interop = compositor != null ? await compositor.TryGetCompositionGpuInterop() : null;
            if (interop == null)
            {
                Fail("The compositor has no GPU interop (software rendering, or a backend without external objects).");
                return;
            }

            var imageTypes = string.Join(", ", interop.SupportedImageHandleTypes);
            var semaphoreTypes = string.Join(", ", interop.SupportedSemaphoreTypes);
            Console.WriteLine($"[viewport] compositor interop: images [{imageTypes}], semaphores [{semaphoreTypes}], graphics API {GraphicsDevice.Platform}");

            if (GraphicsDevice.Platform == GraphicsPlatform.Direct3D11
                && interop.SupportedImageHandleTypes.Contains(D3D11NtHandle)
                && interop.GetSynchronizationCapabilities(D3D11NtHandle).HasFlag(CompositionGpuImportedImageSynchronizationCapabilities.KeyedMutex))
            {
                handleType = D3D11NtHandle;
            }
            else if (GraphicsDevice.Platform == GraphicsPlatform.Vulkan
                && interop.SupportedImageHandleTypes.Contains(VulkanFd)
                && interop.SupportedSemaphoreTypes.Contains(VulkanSemaphoreFd)
                && interop.GetSynchronizationCapabilities(VulkanFd).HasFlag(CompositionGpuImportedImageSynchronizationCapabilities.Semaphores))
            {
                handleType = VulkanFd;
            }
            else
            {
                Fail($"The compositor can't import a {GraphicsDevice.Platform} texture (images [{imageTypes}], semaphores [{semaphoreTypes}]).");
                return;
            }

            surface = view.CreateDrawingSurface();
            if (surface == null)
            {
                Fail("The view has no compositor.");
                return;
            }
            state = StateActive;
            Console.WriteLine($"[viewport] GPU interop active with {handleType}");
            logger.Info($"Preview viewport: GPU interop with {handleType}");
        }
        catch (Exception e)
        {
            Fail($"The GPU interop could not be set up: {e.Message}");
        }
    }

    private void Present(int version, Texture? d3dTexture, ExportableTexture? vkTexture, long start)
    {
        if (state != StateActive || surface == null || interop == null)
        {
            frameInFlight = false;
            return;
        }

        try
        {
            if (importedVersion != version)
                Import(version, d3dTexture, vkTexture);

            var update = handleType == D3D11NtHandle
                ? surface.UpdateWithKeyedMutexAsync(importedImage!, 1, 0)
                : surface.UpdateWithSemaphoresAsync(importedImage!, readySemaphore!, releasedSemaphore!);
            update.ContinueWith(task =>
            {
                if (task.IsFaulted)
                    Fail($"The compositor could not update the surface: {task.Exception?.GetBaseException().Message}");
                else
                    stats.FrameShown(start);
                frameInFlight = false;
            }, TaskScheduler.Default);
        }
        catch (Exception e)
        {
            Fail($"The compositor could not import the frame: {e.Message}");
            frameInFlight = false;
        }
    }

    private void Import(int version, Texture? d3dTexture, ExportableTexture? vkTexture)
    {
        _ = importedImage?.DisposeAsync();
        _ = readySemaphore?.DisposeAsync();
        _ = releasedSemaphore?.DisposeAsync();
        importedImage = null;
        readySemaphore = releasedSemaphore = null;

        if (handleType == D3D11NtHandle)
        {
            var texture = d3dTexture ?? throw new InvalidOperationException("No shared texture");
            importedImage = interop!.ImportImage(new PlatformHandle(texture.SharedHandle, D3D11NtHandle), new PlatformGraphicsExternalImageProperties
            {
                Width = texture.Width,
                Height = texture.Height,
                Format = PlatformGraphicsExternalImageFormat.R8G8B8A8UNorm,
            });
        }
        else
        {
            var texture = vkTexture ?? throw new InvalidOperationException("No exportable texture");
            importedImage = interop!.ImportImage(new PlatformHandle(texture.ExportMemory(), VulkanFd), new PlatformGraphicsExternalImageProperties
            {
                Width = texture.Texture.Width,
                Height = texture.Texture.Height,
                Format = PlatformGraphicsExternalImageFormat.R8G8B8A8UNorm,
                MemorySize = texture.MemorySize,
            });
            readySemaphore = interop.ImportSemaphore(new PlatformHandle(texture.ExportReadySemaphore(), VulkanSemaphoreFd));
            releasedSemaphore = interop.ImportSemaphore(new PlatformHandle(texture.ExportReleasedSemaphore(), VulkanSemaphoreFd));
        }
        importedVersion = version;
    }

    private void Fail(string reason)
    {
        if (state == StateFailed)
            return;
        state = StateFailed;
        Dispatcher.UIThread.Post(view.RemoveDrawingSurface);
        Console.WriteLine($"[viewport] GPU interop unavailable, falling back to readback: {reason}");
        logger.Warning($"Preview viewport: GPU interop unavailable, falling back to readback. {reason}");
    }
}
