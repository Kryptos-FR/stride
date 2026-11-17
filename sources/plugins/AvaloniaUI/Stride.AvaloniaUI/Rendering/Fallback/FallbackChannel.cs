// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Stride.AvaloniaUI.Rendering.Fallback;

/// <summary>
/// The core's way to the fallback, which counts the calls it hands over by kind, and by the reason the native canvas gave.
/// </summary>
internal sealed class FallbackChannel(IRenderFallback fallback)
{
    private readonly int[] counts = new int[Enum.GetValues<DrawCallKind>().Length];
    private readonly int[] reasonCounts = new int[Enum.GetValues<FallbackReason>().Length];

    /// <summary>
    /// Gets the fallback the calls go to.
    /// </summary>
    public IRenderFallback Fallback { get; } = fallback;

    /// <summary>
    /// Counts a call handed to the fallback.
    /// </summary>
    public void Count(DrawCallKind kind) => counts[(int)kind]++;

    /// <summary>
    /// Counts a call or a group that the native canvas handed to the fallback, by why it did.
    /// </summary>
    public void Count(FallbackReason reason) => reasonCounts[(int)reason]++;

    /// <summary>
    /// Gets how many calls of a kind went to the fallback since the counts were last reset.
    /// </summary>
    public int GetCount(DrawCallKind kind) => counts[(int)kind];

    /// <summary>
    /// Gets how many calls and groups the native canvas handed to the fallback for a reason since the counts were last reset.
    /// </summary>
    public int GetCount(FallbackReason reason) => reasonCounts[(int)reason];

    /// <summary>
    /// Gets the reasons counted since the counts were last reset, in the order of the enumeration.
    /// </summary>
    public IReadOnlyList<FallbackReason> GetReasons()
    {
        var reasons = new List<FallbackReason>();
        for (var i = 0; i < reasonCounts.Length; i++)
        {
            if (reasonCounts[i] > 0)
                reasons.Add((FallbackReason)i);
        }
        return reasons;
    }

    /// <summary>
    /// Sets every count back to zero, by kind and by reason.
    /// </summary>
    public void ResetCounts()
    {
        Array.Clear(counts);
        Array.Clear(reasonCounts);
    }
}
