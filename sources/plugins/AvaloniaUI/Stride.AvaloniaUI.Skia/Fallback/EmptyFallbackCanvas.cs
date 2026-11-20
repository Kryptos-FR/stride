// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Stride.AvaloniaUI.Rendering.Drawing;
using Stride.AvaloniaUI.Rendering.Fallback;

namespace Stride.AvaloniaUI.Skia.Fallback;

/// <summary>
/// The canvas of a target with no pixels, which draws nothing.
/// </summary>
internal sealed class EmptyFallbackCanvas : IFallbackCanvas
{
    public void Draw(DrawCommandList commands)
    {
    }

    public object? GetFeature(Type featureType) => null;

    public void Dispose()
    {
    }
}
