// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Stride.Graphics;

namespace OrbHunt.Gameplay;

/// <summary>
/// The white shapes the field is drawn with, tinted by the sprite batch: made in code, as the sample has no assets.
/// </summary>
internal sealed class OrbTextures : IDisposable
{
    private const int Size = 128;

    public OrbTextures(GraphicsDevice device)
    {
        // A soft light that fades out from its center
        Glow = Create(device, distance => MathF.Pow(Math.Clamp(1 - distance, 0, 1), 2.2f));
        // A disc with a smooth edge
        Disc = Create(device, distance => Math.Clamp((1 - distance) * Size / 2, 0, 1));
        // A ring along the edge of the disc
        Ring = Create(device, distance => Math.Clamp((1 - distance) * Size / 2, 0, 1) * Math.Clamp((distance - 0.86f) * Size / 2, 0, 1));
    }

    public Texture Glow { get; }

    public Texture Disc { get; }

    public Texture Ring { get; }

    public void Dispose()
    {
        Glow.Dispose();
        Disc.Dispose();
        Ring.Dispose();
    }

    /// <summary>
    /// Creates a white texture whose coverage is a function of the distance to its center (1 at its edge), premultiplied.
    /// </summary>
    private static Texture Create(GraphicsDevice device, Func<float, float> coverage)
    {
        var pixels = new uint[Size * Size];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var dx = (x + 0.5f) / Size * 2 - 1;
                var dy = (y + 0.5f) / Size * 2 - 1;
                var value = (uint)MathF.Round(coverage(MathF.Sqrt(dx * dx + dy * dy)) * 255);
                pixels[y * Size + x] = value | value << 8 | value << 16 | value << 24;
            }
        }
        return Texture.New2D(device, Size, Size, PixelFormat.R8G8B8A8_UNorm, pixels);
    }
}
