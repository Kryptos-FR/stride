// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;

namespace Stride.AvaloniaUI.Rendering.Fallback;

/// <summary>
/// The core's way to the fallback, which counts the calls it hands over by kind.
/// </summary>
internal sealed class FallbackChannel(IRenderFallback fallback)
{
    private readonly int[] counts = new int[Enum.GetValues<DrawCallKind>().Length];

    /// <summary>
    /// Gets the fallback the calls go to.
    /// </summary>
    public IRenderFallback Fallback { get; } = fallback;

    /// <summary>
    /// Counts a call handed to the fallback.
    /// </summary>
    public void Count(DrawCallKind kind) => counts[(int)kind]++;

    /// <summary>
    /// Gets how many calls of a kind went to the fallback since the counts were last reset.
    /// </summary>
    public int GetCount(DrawCallKind kind) => counts[(int)kind];

    /// <summary>
    /// Sets every count back to zero.
    /// </summary>
    public void ResetCounts() => Array.Clear(counts);
}
