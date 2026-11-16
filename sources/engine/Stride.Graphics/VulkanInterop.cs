// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;

namespace Stride.Graphics;

/// <summary>
///   The native Vulkan objects of a graphics device that another renderer needs to share it.
/// </summary>
/// <param name="Instance">The <c>VkInstance</c>.</param>
/// <param name="PhysicalDevice">The <c>VkPhysicalDevice</c>.</param>
/// <param name="Device">The <c>VkDevice</c>.</param>
/// <param name="Queue">The <c>VkQueue</c> the device submits its command lists to.</param>
/// <param name="QueueFamilyIndex">The family of <paramref name="Queue"/>.</param>
/// <param name="ApiVersion">The Vulkan version the instance was created for, as <c>VK_MAKE_API_VERSION</c> encodes it.</param>
internal readonly record struct VulkanDeviceHandles(nint Instance, nint PhysicalDevice, nint Device, nint Queue, uint QueueFamilyIndex, uint ApiVersion);

/// <summary>
///   The native Vulkan image of a Texture, as another renderer that draws into it needs it.
/// </summary>
/// <param name="Image">The <c>VkImage</c>.</param>
/// <param name="Memory">The <c>VkDeviceMemory</c> bound to the image.</param>
/// <param name="MemorySize">The size of the memory the image needs, in bytes.</param>
/// <param name="Format">The <c>VkFormat</c> of the image.</param>
/// <param name="Tiling">The <c>VkImageTiling</c> of the image.</param>
/// <param name="Usage">The <c>VkImageUsageFlags</c> the image was created with.</param>
/// <param name="LevelCount">The number of mip levels.</param>
/// <param name="SampleCount">The number of samples per pixel.</param>
internal readonly record struct VulkanImageHandles(ulong Image, ulong Memory, ulong MemorySize, uint Format, uint Tiling, uint Usage, uint LevelCount, uint SampleCount);

/// <summary>
///   Lets another renderer that shares a Vulkan graphics device, such as Skia, draw into Stride Textures on the device's queue.
/// </summary>
/// <remarks>
///   <para>
///     Only the Vulkan backend supports it (<see cref="IsSupported"/>). It is internal: only the Avalonia UI of games
///     (<c>Stride.AvaloniaUI</c>) uses it for now, until Direct3D 12 has its counterpart and both share one API.
///   </para>
///   <para>
///     The other renderer submits to the device's queue under <see cref="GetQueueLock"/>, so its commands are ordered
///     with Stride's by submission. A frame it draws into a Texture of <see cref="CreateRenderTarget"/> goes in three steps:
///   </para>
///   <list type="number">
///     <item>
///       A command list moves the Texture to <see cref="BarrierLayout.RenderTarget"/> and is submitted, before the other
///       renderer submits its commands.
///     </item>
///     <item>
///       The other renderer draws with the image in <c>VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL</c>, leaves it in that layout,
///       and submits its commands.
///     </item>
///     <item>
///       The command lists that then use the Texture move it from <see cref="BarrierLayout.RenderTarget"/> as usual: their
///       barriers come after the other renderer's commands on the queue, so they wait for them.
///     </item>
///   </list>
///   <para>
///     Stride never sees the other renderer's commands: what it tracks of the Texture stays right because the Texture
///     is in the same layout before and after them.
///   </para>
/// </remarks>
internal static partial class VulkanInterop
{
    /// <summary>
    ///   Gets a value indicating whether a graphics device can be shared with another Vulkan renderer.
    /// </summary>
    /// <param name="device">The graphics device.</param>
    /// <returns><see langword="true"/> on the Vulkan backend; otherwise <see langword="false"/>.</returns>
    public static bool IsSupported(GraphicsDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        return IsSupportedImpl(device);
    }

    /// <summary>
    ///   Gets the native Vulkan objects of a graphics device.
    /// </summary>
    /// <param name="device">The graphics device.</param>
    /// <returns>The native objects.</returns>
    /// <exception cref="NotSupportedException">The device is not a Vulkan device (see <see cref="IsSupported"/>).</exception>
    public static VulkanDeviceHandles GetDevice(GraphicsDevice device)
    {
        ThrowIfNotSupported(device);
        return GetDeviceImpl(device);
    }

    /// <summary>
    ///   Gets the address of a Vulkan function, as <c>vkGetDeviceProcAddr</c> or <c>vkGetInstanceProcAddr</c> give it.
    /// </summary>
    /// <param name="device">The graphics device.</param>
    /// <param name="name">The name of the function.</param>
    /// <param name="instance">The <c>VkInstance</c> to get an instance function of, or 0 for a global function.</param>
    /// <param name="nativeDevice">The <c>VkDevice</c> to get a device function of, or 0.</param>
    /// <returns>The address of the function, or 0 if there is none.</returns>
    /// <exception cref="NotSupportedException">The device is not a Vulkan device (see <see cref="IsSupported"/>).</exception>
    public static nint GetProcAddress(GraphicsDevice device, string name, nint instance, nint nativeDevice)
    {
        ThrowIfNotSupported(device);
        ArgumentNullException.ThrowIfNull(name);
        return GetProcAddressImpl(device, name, instance, nativeDevice);
    }

    /// <summary>
    ///   Creates a 2D Texture that another renderer can draw into: <see cref="PixelFormat.R8G8B8A8_UNorm"/>, render target
    ///   and shader resource, initially in the <see cref="BarrierLayout.RenderTarget"/> layout.
    /// </summary>
    /// <param name="device">The graphics device.</param>
    /// <param name="width">The width of the Texture, in pixels.</param>
    /// <param name="height">The height of the Texture, in pixels.</param>
    /// <returns>The Texture.</returns>
    /// <remarks>
    ///   Its views can also be <see cref="PixelFormat.R8G8B8A8_UNorm_SRgb"/>, to sample what the other renderer drew in
    ///   sRGB as linear colors.
    /// </remarks>
    /// <exception cref="NotSupportedException">The device is not a Vulkan device (see <see cref="IsSupported"/>).</exception>
    public static Texture CreateRenderTarget(GraphicsDevice device, int width, int height)
    {
        ThrowIfNotSupported(device);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        return CreateRenderTargetImpl(device, width, height);
    }

    /// <summary>
    ///   Gets the native Vulkan image of a Texture.
    /// </summary>
    /// <param name="texture">The Texture (not a View).</param>
    /// <returns>The native image.</returns>
    /// <exception cref="NotSupportedException">The Texture is not on a Vulkan device (see <see cref="IsSupported"/>).</exception>
    public static VulkanImageHandles GetImage(Texture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ThrowIfNotSupported(texture.GraphicsDevice);
        if (texture.ParentTexture != null)
            throw new ArgumentException("A Texture View shares the image of its parent: pass the parent.", nameof(texture));
        return GetImageImpl(texture);
    }

    /// <summary>
    ///   Gets the lock under which the commands are submitted to the device's queue. Another renderer submits its own
    ///   commands under it too.
    /// </summary>
    /// <param name="device">The graphics device.</param>
    /// <returns>The lock.</returns>
    /// <exception cref="NotSupportedException">The device is not a Vulkan device (see <see cref="IsSupported"/>).</exception>
    public static object GetQueueLock(GraphicsDevice device)
    {
        ThrowIfNotSupported(device);
        return GetQueueLockImpl(device);
    }

    private static void ThrowIfNotSupported(GraphicsDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (!IsSupportedImpl(device))
            throw new NotSupportedException("This graphics device is not a Vulkan device.");
    }

    private static partial bool IsSupportedImpl(GraphicsDevice device);

    private static partial VulkanDeviceHandles GetDeviceImpl(GraphicsDevice device);

    private static unsafe partial nint GetProcAddressImpl(GraphicsDevice device, string name, nint instance, nint nativeDevice);

    private static partial Texture CreateRenderTargetImpl(GraphicsDevice device, int width, int height);

    private static partial VulkanImageHandles GetImageImpl(Texture texture);

    private static partial object GetQueueLockImpl(GraphicsDevice device);

#if !STRIDE_GRAPHICS_API_VULKAN
    private static partial bool IsSupportedImpl(GraphicsDevice device) => false;

    private static partial VulkanDeviceHandles GetDeviceImpl(GraphicsDevice device) => throw new NotSupportedException();

    private static unsafe partial nint GetProcAddressImpl(GraphicsDevice device, string name, nint instance, nint nativeDevice) => throw new NotSupportedException();

    private static partial Texture CreateRenderTargetImpl(GraphicsDevice device, int width, int height) => throw new NotSupportedException();

    private static partial VulkanImageHandles GetImageImpl(Texture texture) => throw new NotSupportedException();

    private static partial object GetQueueLockImpl(GraphicsDevice device) => throw new NotSupportedException();
#endif
}
