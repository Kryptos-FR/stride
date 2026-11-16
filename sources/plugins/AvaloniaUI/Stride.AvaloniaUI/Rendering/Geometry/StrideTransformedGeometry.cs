// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Platform;

namespace Stride.AvaloniaUI.Rendering.Geometry;

/// <summary>
/// A geometry with a transform, outlined as its source was when it was made.
/// </summary>
internal sealed class StrideTransformedGeometry(StrideGeometry source, Matrix transform) : StrideGeometry(source.Channel), ITransformedGeometryImpl
{
    public StrideGeometry Source { get; } = source;

    public Matrix Transform { get; } = transform;

    // A perspective doesn't keep curves curves of the same kind: only the fallback knows that outline
    public override PathData? Path { get; } = transform.ContainsPerspective() ? null : source.Path?.Transform(transform);

    internal override bool HasFill => Source.HasFill;

    IGeometryImpl ITransformedGeometryImpl.SourceGeometry => Source;
}
