using Stride.Core.Mathematics;
using Stride.Graphics;

namespace ColorRise.Tessera.Tests.Graphics;

/// <summary>
/// The pixels of one frame a test game drew, with y going down, and the ids of its picking stage when it has one.
/// </summary>
internal sealed class Frame
{
    private readonly Color[] pixels;
    private readonly float[]? pickedIds;

    public Frame(int width, int height, Color[] pixels, float[]? pickedIds = null)
    {
        Width = width;
        Height = height;
        this.pixels = pixels;
        this.pickedIds = pickedIds;
    }

    public int Width { get; }

    public int Height { get; }

    public Color this[int x, int y] => pixels[y * Width + x];

    public Color this[Int2 pixel] => this[pixel.X, pixel.Y];

    /// <summary>
    /// The id the picking stage drew at a pixel, 0 where it drew nothing: the whole part of the red channel, as the scene
    /// editor reads it.
    /// </summary>
    public int PickedId(int x, int y)
    {
        if (pickedIds is null)
        {
            throw new InvalidOperationException("The frame has no picking stage: set HarnessOptions.Picking.");
        }
        return (int)MathF.Floor(pickedIds[y * Width + x]);
    }

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
