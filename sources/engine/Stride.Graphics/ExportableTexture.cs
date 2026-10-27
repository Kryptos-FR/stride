// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;

namespace Stride.Graphics;

/// <summary>
///   A 2D Texture whose memory, and the semaphores that order its use, can be exported as POSIX file descriptors and
///   imported by another Vulkan or OpenGL device, such as the compositor of a UI framework.
/// </summary>
/// <remarks>
///   <para>
///     Only the Vulkan backend supports it, with the <c>VK_KHR_external_memory_fd</c> and <c>VK_KHR_external_semaphore_fd</c>
///     extensions (Linux). The extensions are only enabled on the devices created after a host sets
///     <see cref="EnableExternalObjects"/>, so a game's device is not changed. On Direct3D 11, a Texture created with the
///     <c>SharedKeyedMutex</c> and <c>SharedNtHandle</c> options plays this role (<see cref="Texture.SharedHandle"/>,
///     <see cref="Texture.AcquireSync"/>).
///   </para>
///   <para>
///     A frame is handed over in four steps: <see cref="WaitReleased"/> (from the second frame on), a copy into
///     <see cref="Texture"/> that leaves it in the <see cref="BarrierLayout.CopySource"/> layout, the submission of the
///     command list, then <see cref="SignalReady"/>. The importer waits for the ready semaphore and signals the released one.
///   </para>
/// </remarks>
public sealed partial class ExportableTexture : IDisposable
{
    /// <summary>
    ///   Gets or sets a value indicating whether the graphics devices created from now on enable the extensions that
    ///   <see cref="ExportableTexture"/> needs. A host (the editor) sets it before it creates its devices.
    /// </summary>
    public static bool EnableExternalObjects { get; set; }

    /// <summary>
    ///   Gets the Texture to copy the frames into: <see cref="PixelFormat.R8G8B8A8_UNorm"/>, render target and shader resource.
    /// </summary>
    public Texture Texture { get; private set; }

    /// <summary>
    ///   Gets the size of the memory of <see cref="Texture"/>, which the importer needs.
    /// </summary>
    public ulong MemorySize { get; private set; }

    /// <summary>
    ///   Gets a value indicating whether a device can create an <see cref="ExportableTexture"/>.
    /// </summary>
    public static bool IsSupported(GraphicsDevice device) => IsSupportedImpl(device);

    /// <summary>
    ///   Creates an exportable Texture.
    /// </summary>
    /// <exception cref="NotSupportedException">The device can't export textures (see <see cref="IsSupported"/>).</exception>
    public static ExportableTexture Create(GraphicsDevice device, int width, int height)
    {
        if (!IsSupported(device))
            throw new NotSupportedException("This graphics device can't export textures.");
        return CreateImpl(device, width, height);
    }

    private static partial bool IsSupportedImpl(GraphicsDevice device);

    private static partial ExportableTexture CreateImpl(GraphicsDevice device, int width, int height);

#if !STRIDE_GRAPHICS_API_VULKAN
    private ExportableTexture() => throw new NotSupportedException();

    private static partial bool IsSupportedImpl(GraphicsDevice device) => false;

    private static partial ExportableTexture CreateImpl(GraphicsDevice device, int width, int height) => throw new NotSupportedException();

    /// <summary>
    ///   Exports the memory of <see cref="Texture"/> as a new file descriptor, which the caller (or the importer) owns.
    /// </summary>
    public int ExportMemory() => throw new NotSupportedException();

    /// <summary>
    ///   Exports the semaphore signaled when a frame is ready (<see cref="SignalReady"/>) as a new file descriptor.
    /// </summary>
    public int ExportReadySemaphore() => throw new NotSupportedException();

    /// <summary>
    ///   Exports the semaphore the importer signals when it is done with a frame (<see cref="WaitReleased"/>) as a new file descriptor.
    /// </summary>
    public int ExportReleasedSemaphore() => throw new NotSupportedException();

    /// <summary>
    ///   Signals the ready semaphore after every command submitted so far (after the command list of the copy was submitted).
    /// </summary>
    public void SignalReady() => throw new NotSupportedException();

    /// <summary>
    ///   Makes the commands submitted from now on wait for the importer to release the previous frame.
    /// </summary>
    public void WaitReleased() => throw new NotSupportedException();

    public void Dispose()
    {
    }
#endif
}
