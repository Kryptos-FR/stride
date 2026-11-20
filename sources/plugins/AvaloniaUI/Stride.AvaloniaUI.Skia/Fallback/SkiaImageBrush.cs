// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Stride.AvaloniaUI.Skia.Fallback;

/// <summary>
/// An immutable image brush with the tile properties and opacity of another tile brush.
/// </summary>
/// <remarks>
/// Avalonia's own immutable image brush is internal.
/// </remarks>
internal sealed class SkiaImageBrush(ITileBrush tileProperties, IImageBrushSource? source) : ImmutableTileBrush(tileProperties), IImageBrush
{
    public IImageBrushSource? Source { get; } = source;
}
