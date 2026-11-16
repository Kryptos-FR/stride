// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Geometry;

namespace Stride.AvaloniaUI.Rendering.Fallback;

/// <summary>
/// What the core can't do itself yet, done by another renderer (the Skia package) from the core's own objects.
/// </summary>
internal interface IRenderFallback
{
    /// <summary>
    /// Gets the fallback's equivalent of a core geometry, kept in <see cref="StrideGeometry.FallbackObject"/>.
    /// </summary>
    IGeometryImpl GetGeometry(StrideGeometry geometry);
}
