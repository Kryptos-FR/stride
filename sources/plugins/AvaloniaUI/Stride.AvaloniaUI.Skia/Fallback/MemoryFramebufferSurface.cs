// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using Stride.AvaloniaUI.Rendering.Fallback;

namespace Stride.AvaloniaUI.Skia.Fallback;

/// <summary>
/// A framebuffer over the memory of a fallback target, which it doesn't own.
/// </summary>
internal sealed class MemoryFramebufferSurface(FallbackTarget target) : IFramebufferPlatformSurface
{
    public IFramebufferRenderTarget CreateFramebufferRenderTarget() => new RenderTarget(target);

    // Each lock is the target's memory, whose previous frame is retained as the target says; releasing it does nothing
    private sealed class RenderTarget(FallbackTarget target) : IFramebufferRenderTarget
    {
        public bool RetainsFrameContents => target.PreviousFrameRetained;

        public ILockedFramebuffer Lock(IRenderTarget.RenderTargetSceneInfo sceneInfo, out FramebufferLockProperties properties)
        {
            properties = new FramebufferLockProperties(target.PreviousFrameRetained);
            return new LockedFramebuffer(target.Address, target.Size, target.RowBytes, target.Dpi, target.Format, AlphaFormat.Premul, null);
        }

        public void Dispose()
        {
        }
    }
}
