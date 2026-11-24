using Stride.Core.Mathematics;
using Stride.Graphics;

namespace ColorRise.Tessera.Tests.Graphics;

/// <summary>
/// Textures made by the tests, of colours whose place is known.
/// </summary>
internal static class TestTextures
{
    public static Texture Flat(GraphicsDevice device, Color color, int size = 4)
        => FromPixels(device, size, size, (_, _) => color);

    /// <summary>
    /// 8 by 8 pixels: top-left red, top-right green, bottom-left blue, bottom-right yellow.
    /// </summary>
    public static Texture Quarters(GraphicsDevice device)
        => FromPixels(device, 8, 8, Quarter);

    public static Color Quarter(int x, int y) => (x < 4, y < 4) switch
    {
        (true, true) => new Color(255, 0, 0, 255),
        (false, true) => new Color(0, 255, 0, 255),
        (true, false) => new Color(0, 0, 255, 255),
        _ => new Color(255, 255, 0, 255),
    };

    public static Texture FromPixels(GraphicsDevice device, int width, int height, Func<int, int, Color> pixel)
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
        return Texture.New(device, image);
    }
}
