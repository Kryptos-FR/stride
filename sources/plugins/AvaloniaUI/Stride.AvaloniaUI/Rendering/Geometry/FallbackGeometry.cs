// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Fallback;

namespace Stride.AvaloniaUI.Rendering.Geometry;

/// <summary>
/// A geometry the fallback made (a widened outline, an intersection, a segment): only the fallback knows its outline.
/// </summary>
internal sealed class FallbackGeometry(FallbackChannel channel, IGeometryImpl inner) : StrideGeometry(channel)
{
    /// <summary>
    /// Gets the fallback's geometry.
    /// </summary>
    public IGeometryImpl Inner { get; } = inner;

    public override PathData? Path => null;
}
