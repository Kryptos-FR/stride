// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

#if STRIDE_GRAPHICS_API_VULKAN

using System;
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
        var texture = new Texture(device) { IsFormatMutable = true, IsMetalTextureExportable = device.HasMetalObjectsSupport };
        return texture.InitializeFrom(TextureDescription.New2D(width, height, PixelFormat.R8G8B8A8_UNorm, TextureFlags.RenderTarget | TextureFlags.ShaderResource));
    }

    private static partial VulkanImageHandles GetImageImpl(Texture texture)
    {
        var device = texture.GraphicsDevice;
        device.NativeDeviceApi.vkGetImageMemoryRequirements(device.NativeDevice, texture.NativeImage, out var memoryRequirements);
        return new VulkanImageHandles(texture.NativeImage.Handle, texture.NativeMemory.Handle, memoryRequirements.size,
            (uint)texture.NativeFormat, (uint)VkImageTiling.Optimal, (uint)texture.NativeImageUsage,
            (uint)texture.MipLevelCount, (uint)Math.Max((int)texture.MultisampleCount, 1));
    }

    private static partial object GetQueueLockImpl(GraphicsDevice device) => device.QueueLock;

    private static unsafe partial MetalDeviceHandles? GetMetalDeviceImpl(GraphicsDevice device)
    {
        if (!device.HasMetalObjectsSupport)
            return null;

        var commandQueueInfo = new VkExportMetalCommandQueueInfoEXT
        {
            sType = VkStructureType.ExportMetalCommandQueueInfoEXT,
            queue = device.NativeCommandQueue,
        };
        var deviceInfo = new VkExportMetalDeviceInfoEXT
        {
            sType = VkStructureType.ExportMetalDeviceInfoEXT,
            pNext = &commandQueueInfo,
        };
        var objectsInfo = new VkExportMetalObjectsInfoEXT
        {
            sType = VkStructureType.ExportMetalObjectsInfoEXT,
            pNext = &deviceInfo,
        };
        // The queue is externally synchronized
        lock (device.QueueLock)
            device.NativeDeviceApi.vkExportMetalObjectsEXT(device.NativeDevice, &objectsInfo);

        return deviceInfo.mtlDevice != 0 && commandQueueInfo.mtlCommandQueue != 0
            ? new MetalDeviceHandles(deviceInfo.mtlDevice, commandQueueInfo.mtlCommandQueue)
            : null;
    }

    private static unsafe partial nint GetMetalTextureImpl(Texture texture)
    {
        if (!texture.IsMetalTextureExportable)
            throw new NotSupportedException("The Texture isn't on a device that runs on Metal, or wasn't made by VulkanInterop.CreateRenderTarget.");

        var device = texture.GraphicsDevice;
        var textureInfo = new VkExportMetalTextureInfoEXT
        {
            sType = VkStructureType.ExportMetalTextureInfoEXT,
            image = texture.NativeImage,
            // The only plane of an image that isn't multi-planar
            plane = VkImageAspectFlags.Plane0,
        };
        var objectsInfo = new VkExportMetalObjectsInfoEXT
        {
            sType = VkStructureType.ExportMetalObjectsInfoEXT,
            pNext = &textureInfo,
        };
        device.NativeDeviceApi.vkExportMetalObjectsEXT(device.NativeDevice, &objectsInfo);
        return textureInfo.mtlTexture;
    }

    private static partial void WaitForQueueImpl(GraphicsDevice device)
    {
        lock (device.QueueLock)
            device.CheckResult(device.NativeDeviceApi.vkQueueWaitIdle(device.NativeCommandQueue));
    }
}

#endif
