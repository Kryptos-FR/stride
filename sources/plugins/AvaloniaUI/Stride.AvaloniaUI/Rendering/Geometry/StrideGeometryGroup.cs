// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Stride.AvaloniaUI.Rendering.Fallback;

namespace Stride.AvaloniaUI.Rendering.Geometry;

/// <summary>
/// Geometries filled together by one fill rule.
/// </summary>
internal sealed class StrideGeometryGroup(FallbackChannel channel, FillRule fillRule, IReadOnlyList<StrideGeometry> children) : StrideGeometry(channel)
{
    public FillRule FillRule { get; } = fillRule;

    public IReadOnlyList<StrideGeometry> Children { get; } = children;

    // The figures of the children, when they all have an outline of their own
    public override PathData? Path { get; } = children.All(x => x.Path != null) ? PathData.Concat(fillRule, children.Select(x => x.Path!)) : null;
}
