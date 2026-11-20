// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Stride.AvaloniaUI.Rendering.Drawing;

namespace Stride.AvaloniaUI.Rendering.Fallback;

/// <summary>
/// A canvas of the fallback over a <see cref="FallbackTarget"/>, which draws the calls the core recorded.
/// </summary>
internal interface IFallbackCanvas : IDisposable
{
    /// <summary>
    /// Draws a command list, and keeps its state (transform, pushes) for the next list.
    /// </summary>
    void Draw(DrawCommandList commands);

    /// <summary>
    /// Gets a feature of the fallback's drawing context, such as a lease of its canvas.
    /// </summary>
    object? GetFeature(Type featureType);
}
