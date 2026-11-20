// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Stride.Graphics;

namespace Stride.AvaloniaUI.Tests;

/// <summary>
/// Compares two frames of the same size and format, channel by channel.
/// </summary>
internal static class FrameComparison
{
    /// <summary>
    /// Gets the largest difference between a channel of a pixel of one frame and the same channel of the other, alpha included.
    /// </summary>
    public static int MaxDifference(Image a, Image b)
    {
        var max = 0;
        Compare(a, b, difference => max = Math.Max(max, difference));
        return max;
    }

    /// <summary>
    /// Counts the pixels that have a channel more than a tolerance apart in the two frames.
    /// </summary>
    public static int CountBeyond(Image a, Image b, int tolerance)
    {
        var count = 0;
        Compare(a, b, difference =>
        {
            if (difference > tolerance)
                count++;
        });
        return count;
    }

    // Calls back with the largest channel difference of each pixel
    private static unsafe void Compare(Image a, Image b, Action<int> pixel)
    {
        var bufferA = a.PixelBuffer[0];
        var bufferB = b.PixelBuffer[0];
        if (bufferA.Width != bufferB.Width || bufferA.Height != bufferB.Height || bufferA.Format != bufferB.Format)
            throw new ArgumentException($"The frames differ in size or format: {bufferA.Width}×{bufferA.Height} {bufferA.Format}, {bufferB.Width}×{bufferB.Height} {bufferB.Format}.");
        if (bufferA.PixelSize != 4)
            throw new ArgumentException($"The frames' format {bufferA.Format} doesn't have 4 channels of a byte.");

        for (var y = 0; y < bufferA.Height; y++)
        {
            var rowA = new ReadOnlySpan<byte>((byte*)bufferA.DataPointer + y * bufferA.RowStride, bufferA.Width * 4);
            var rowB = new ReadOnlySpan<byte>((byte*)bufferB.DataPointer + y * bufferB.RowStride, bufferB.Width * 4);
            for (var x = 0; x < bufferA.Width; x++)
            {
                var difference = 0;
                for (var channel = 0; channel < 4; channel++)
                    difference = Math.Max(difference, Math.Abs(rowA[x * 4 + channel] - rowB[x * 4 + channel]));
                pixel(difference);
            }
        }
    }
}
