using Stride.Core.Mathematics;
using Stride.Graphics;

namespace ColorRise.Tessera.Tests.Assets;

/// <summary>
/// Image files made in the tests, as a person's tile sheets would be.
/// </summary>
internal static class TestImages
{
    /// <summary>
    /// The bytes of a PNG file of the given size, whose pixel at (x, y), from the top left, is <paramref name="pixel"/>(x, y).
    /// </summary>
    public static byte[] Png(int width, int height, Func<int, int, Color> pixel)
    {
        using var image = Image.New2D(width, height, 1, PixelFormat.R8G8B8A8_UNorm);
        var buffer = image.PixelBuffer[0];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                buffer.SetPixel(x, y, pixel(x, y));
            }
        }
        using var stream = new MemoryStream();
        image.Save(stream, ImageFileType.Png);
        return stream.ToArray();
    }
}
