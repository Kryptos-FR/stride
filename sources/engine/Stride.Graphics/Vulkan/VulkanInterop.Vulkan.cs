// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

#if STRIDE_GRAPHICS_API_VULKAN

using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Stride.Graphics;

internal static partial class VulkanInterop
{
    // The instance is created for this version (GraphicsAdapterFactoryInstance)
    private static readonly VkVersion InstanceApiVersion = new(1, 3, 0);

    private static partial bool IsSupportedImpl(GraphicsDevice device) => true;

    private static partial VulkanDeviceHandles GetDeviceImpl(GraphicsDevice device)
        => new(device.NativeInstance.Handle, device.NativePhysicalDevice.Handle, device.NativeDevice.Handle, device.NativeCommandQueue.Handle,
            // The device creates its queue in the first family (InitializePlatformDevice)
            QueueFamilyIndex: 0,
            InstanceApiVersion);

    private static unsafe partial nint GetProcAddressImpl(GraphicsDevice device, string name, nint instance, nint nativeDevice)
    {
        if (nativeDevice != 0)
        {
            var address = (nint)device.NativeInstanceApi.vkGetDeviceProcAddr(new VkDevice(nativeDevice), name).Value;
            if (address != 0)
                return address;
        }
        return (nint)vkGetInstanceProcAddr(new VkInstance(instance), name).Value;
    }

    private static partial Texture CreateRenderTargetImpl(GraphicsDevice device, int width, int height)
    {
        var texture = new Texture(device) { IsFormatMutable = true };
        return texture.InitializeFrom(TextureDescription.New2D(width, height, PixelFormat.R8G8B8A8_UNorm, TextureFlags.RenderTarget | TextureFlags.ShaderResource));
    }

    private static partial VulkanImageHandles GetImageImpl(Texture texture)
    {
        var device = texture.GraphicsDevice;
        device.NativeDeviceApi.vkGetImageMemoryRequirements(device.NativeDevice, texture.NativeImage, out var memoryRequirements);
        return new VulkanImageHandles(texture.NativeImage.Handle, texture.NativeMemory.Handle, memoryRequirements.size,
            (uint)texture.NativeFormat, (uint)VkImageTiling.Optimal, (uint)texture.NativeImageUsage,
            (uint)texture.MipLevelCount, (uint)System.Math.Max((int)texture.MultisampleCount, 1));
    }

    private static partial object GetQueueLockImpl(GraphicsDevice device) => device.QueueLock;
}

#endif
