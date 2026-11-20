// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.AvaloniaUI.Rendering.Fallback;

/// <summary>
/// A kind of call the core hands to the fallback, counted by <see cref="FallbackChannel"/>.
/// </summary>
internal enum DrawCallKind
{
    GeometryBounds,
    GeometryFillContains,
    GeometryRenderBounds,
    GeometryStrokeContains,
    GeometryWidening,
    GeometryIntersection,
    GeometryContourMeasuring,
    GlyphRunGeometry,
    GlyphRunIntersections,
}
