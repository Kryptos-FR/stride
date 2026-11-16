// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Drawing;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.AvaloniaUI.Rendering.Geometry;
using Stride.AvaloniaUI.Rendering.Text;

namespace Stride.AvaloniaUI.Tests.Rendering;

/// <summary>
/// A fallback whose canvases replay the commands into a <see cref="DrawingCallLog"/>, and log what is asked of them.
/// </summary>
internal sealed class LoggingFallback : IRenderFallback
{
    /// <summary>
    /// Gets the calls the canvases replayed.
    /// </summary>
    public DrawingCallLog Calls { get; } = new();

    /// <summary>
    /// Gets what was asked of the canvases, in order: <c>Draw[kinds]</c>, <c>GetFeature</c> and <c>Dispose</c>.
    /// </summary>
    public List<string> Events { get; } = [];

    /// <summary>
    /// Gets the targets the canvases were made for.
    /// </summary>
    public List<FallbackTarget> Targets { get; } = [];

    public IFontManagerImpl FontManager => throw new NotSupportedException();

    public IFallbackCanvas CreateCanvas(FallbackTarget target)
    {
        Targets.Add(target);
        return new Canvas(this);
    }

    public IGeometryImpl GetGeometry(StrideGeometry geometry) => throw new NotSupportedException();

    public IGlyphRunImpl GetGlyphRun(StrideGlyphRun glyphRun) => throw new NotSupportedException();

    public IGeometryImpl BuildGlyphRunGeometry(GlyphRun glyphRun) => throw new NotSupportedException();

    private sealed class Canvas(LoggingFallback fallback) : IFallbackCanvas
    {
        public void Draw(DrawCommandList commands)
        {
            fallback.Events.Add($"Draw[{string.Join(", ", commands.Commands.Select(command => command.Kind))}]");
            commands.Replay(fallback.Calls);
        }

        public object? GetFeature(Type featureType)
        {
            fallback.Events.Add("GetFeature");
            return null;
        }

        public void Dispose() => fallback.Events.Add("Dispose");
    }
}
