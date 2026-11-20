// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Diagnostics;
using Avalonia.Rendering;

namespace Stride.AvaloniaUI.Platform;

/// <summary>
/// Avalonia's render timer, ticked by the game's draw instead of a clock.
/// </summary>
/// <remarks>
/// It doesn't run in the background: a tick runs Avalonia's render loop on the game thread, which lays out, composes and
/// renders the surfaces that changed. Avalonia sets <see cref="Tick"/> only while something needs rendering.
/// </remarks>
internal sealed class StrideRenderTimer : IRenderTimer
{
    private readonly Stopwatch clock = Stopwatch.StartNew();

    public Action<TimeSpan>? Tick { get; set; }

    public bool RunsInBackground => false;

    /// <summary>
    /// Runs Avalonia's render loop once, with the real time since start (game thread).
    /// </summary>
    /// <returns><c>true</c> if Avalonia had something to render.</returns>
    public bool Render()
    {
        var tick = Tick;
        if (tick == null)
            return false;
        tick(clock.Elapsed);
        return true;
    }
}
