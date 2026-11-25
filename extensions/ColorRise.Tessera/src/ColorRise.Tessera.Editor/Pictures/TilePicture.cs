using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.Pictures;

/// <summary>
/// Pixels a view shows: 8 bits per channel, straight alpha, row by row from the top.
/// </summary>
public sealed class TilePicture
{
    /// <summary>
    /// Makes a transparent picture.
    /// </summary>
    public TilePicture(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        Width = width;
        Height = height;
        Pixels = new Color[width * height];
    }

    /// <summary>
    /// The width, in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// The height, in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// The pixels, row by row from the top. Call <see cref="Touch"/> after writing them.
    /// </summary>
    public Color[] Pixels { get; }

    /// <summary>
    /// The pixel at (x, y), from the top left.
    /// </summary>
    public Color this[int x, int y]
    {
        get => Pixels[y * Width + x];
        set => Pixels[y * Width + x] = value;
    }

    /// <summary>
    /// A number that changes when the pixels do: a view makes its bitmap again when this is not the one it drew.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>
    /// Says that <see cref="Pixels"/> were written.
    /// </summary>
    public void Touch() => Version++;
}
