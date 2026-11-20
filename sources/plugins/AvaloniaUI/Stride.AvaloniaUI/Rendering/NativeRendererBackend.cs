// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Fallback;

namespace Stride.AvaloniaUI.Rendering;

/// <summary>
/// The native renderer: Avalonia's objects are the core's, and their drawing calls are recorded and drawn by the fallback on the CPU.
/// </summary>
internal sealed class NativeRendererBackend : RendererBackend
{
    /// <summary>
    /// Creates the renderer and binds the core's render interface, and the fallback's font manager.
    /// </summary>
    public NativeRendererBackend(IRenderFallback fallback)
    {
        Channel = new FallbackChannel(fallback);
        AvaloniaLocator.CurrentMutable
            .Bind<IPlatformRenderInterface>().ToConstant(new StrideRenderInterface(Channel))
            .Bind<IFontManagerImpl>().ToConstant(fallback.FontManager);
    }

    /// <summary>
    /// Gets the channel to the fallback, which counts the calls that go through it.
    /// </summary>
    public FallbackChannel Channel { get; }
}
