using Stride.Core.Mathematics;
using Stride.Graphics;

namespace ColorRise.Tessera.Tests.Graphics;

/// <summary>
/// The pixels of one frame a test game drew, with y going down.
/// </summary>
internal sealed class Frame
{
    private readonly Color[] pixels;

    public Frame(int width, int height, Color[] pixels)
    {
        Width = width;
        Height = height;
        this.pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public Color this[int x, int y] => pixels[y * Width + x];

    public Color this[Int2 pixel] => this[pixel.X, pixel.Y];

    public int Count(Color color, Rectangle area)
    {
        var count = 0;
        for (var y = Math.Max(area.Top, 0); y < Math.Min(area.Bottom, Height); y++)
        {
            for (var x = Math.Max(area.Left, 0); x < Math.Min(area.Right, Width); x++)
            {
                if (this[x, y] == color)
                {
                    count++;
                }
            }
        }
        return count;
    }

    /// <summary>
    /// Saves the frame as a PNG in the folder <c>frames</c> beside the test assembly, for a person to look at.
    /// </summary>
    public string Save(string name)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "frames");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name + ".png");
        using var image = Image.New2D(Width, Height, 1, PixelFormat.R8G8B8A8_UNorm);
        image.PixelBuffer[0].SetPixels(pixels);
        using var stream = File.Create(path);
        image.Save(stream, ImageFileType.Png);
        return path;
    }
}
