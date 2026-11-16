// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.Core.Reflection;

namespace Stride.AvaloniaUI.Rendering;

/// <summary>
/// What the Stride.AvaloniaUI.Skia package gives the core, found among the registered assemblies.
/// </summary>
[AssemblyScan]
internal interface IAvaloniaUISkia
{
    /// <summary>
    /// Binds Skia's render interface and font manager, and creates the whole-frame Skia renderer.
    /// </summary>
    RendererBackend CreateWholeFrameRenderer();

    /// <summary>
    /// Binds Skia's render interface and font manager, and creates a fallback over them for the native renderer.
    /// </summary>
    IRenderFallback CreateFallback();
}
