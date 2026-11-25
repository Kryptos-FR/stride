using ColorRise.Tessera.Editor.Pictures;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.TileMapEditor;

/// <summary>
/// Draws tiles into pictures, on the CPU: nearest neighbour, alpha over what is there, clipped to the target.
/// </summary>
public static class TileComposer
{
    /// <summary>
    /// The colour of the box drawn for a tile that cannot be drawn: magenta. Its cross is black.
    /// </summary>
    public static Color MissingColor { get; } = new(255, 0, 255, 255);

    /// <summary>
    /// Draws a region of a picture into a rectangle of another, turned as the runtime turns a tile (diagonal first, then
    /// horizontal, then vertical), each pixel multiplied by the tint and laid over what is there.
    /// </summary>
    /// <remarks>
    /// As the runtime's quads, the rectangle is not turned: a tile that is not square and turned on its diagonal is stretched
    /// to it.
    /// </remarks>
    public static void Draw(TilePicture target, Rectangle destination, TilePicture source, Rectangle region, TileFlip flip, Color tint)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);
        if (destination.Width <= 0 || destination.Height <= 0 || region.Width <= 0 || region.Height <= 0)
        {
            return;
        }
        var (left, top, right, bottom) = Clip(target, destination);
        var (diagonal, horizontal, vertical) = ((flip & TileFlip.Diagonal) != 0, (flip & TileFlip.Horizontal) != 0, (flip & TileFlip.Vertical) != 0);
        var tinted = tint != Color.White;
        var pixels = target.Pixels;
        for (var y = top; y < bottom; y++)
        {
            // Where the centre of the pixel is in the rectangle, from 0 to 1, with y down
            var v = (y - destination.Y + 0.5f) / destination.Height;
            for (var x = left; x < right; x++)
            {
                var u = (x - destination.X + 0.5f) / destination.Width;
                // The texel of a point of the screen: the flips undone in the other order, as TileQuads.Uvs does
                var (su, sv) = (horizontal ? 1 - u : u, vertical ? 1 - v : v);
                if (diagonal)
                {
                    (su, sv) = (sv, su);
                }
                var sx = region.X + Math.Min((int)(su * region.Width), region.Width - 1);
                var sy = region.Y + Math.Min((int)(sv * region.Height), region.Height - 1);
                if ((uint)sx >= (uint)source.Width || (uint)sy >= (uint)source.Height)
                {
                    continue;
                }
                var pixel = source.Pixels[sy * source.Width + sx];
                Over(ref pixels[y * target.Width + x], tinted ? Multiply(pixel, tint) : pixel);
            }
        }
    }

    /// <summary>
    /// Fills a rectangle with <see cref="MissingColor"/> and draws its two diagonals in black.
    /// </summary>
    public static void DrawMissing(TilePicture target, Rectangle destination)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (destination.Width <= 0 || destination.Height <= 0)
        {
            return;
        }
        var (left, top, right, bottom) = Clip(target, destination);
        for (var y = top; y < bottom; y++)
        {
            Array.Fill(target.Pixels, MissingColor, y * target.Width + left, right - left);
        }

        var steps = Math.Max(destination.Width, destination.Height);
        for (var step = 0; step < steps; step++)
        {
            var x = Along(step, steps, destination.Width);
            var y = Along(step, steps, destination.Height);
            Set(target, destination.X + x, destination.Y + y, Color.Black);
            Set(target, destination.X + destination.Width - 1 - x, destination.Y + y, Color.Black);
        }
    }

    private static (int Left, int Top, int Right, int Bottom) Clip(TilePicture target, Rectangle destination)
        => (Math.Max(destination.Left, 0), Math.Max(destination.Top, 0), Math.Min(destination.Right, target.Width), Math.Min(destination.Bottom, target.Height));

    // The pixel of a line of `length` pixels at a step of a line drawn in `steps` steps
    private static int Along(int step, int steps, int length) => steps == 1 ? 0 : (int)Math.Round(step * (length - 1) / (double)(steps - 1));

    private static void Set(TilePicture target, int x, int y, Color color)
    {
        if ((uint)x < (uint)target.Width && (uint)y < (uint)target.Height)
        {
            target[x, y] = color;
        }
    }

    private static Color Multiply(Color pixel, Color tint)
        => new((byte)((pixel.R * tint.R + 127) / 255), (byte)((pixel.G * tint.G + 127) / 255), (byte)((pixel.B * tint.B + 127) / 255), (byte)((pixel.A * tint.A + 127) / 255));

    // Straight alpha: the pixel over what is under it
    private static void Over(ref Color under, Color over)
    {
        if (over.A == 255 || under.A == 0)
        {
            under = over;
            return;
        }
        if (over.A == 0)
        {
            return;
        }
        var alpha = over.A / 255f;
        var below = under.A / 255f * (1 - alpha);
        var total = alpha + below;
        under = new Color(
            (byte)((over.R * alpha + under.R * below) / total + 0.5f),
            (byte)((over.G * alpha + under.G * below) / total + 0.5f),
            (byte)((over.B * alpha + under.B * below) / total + 0.5f),
            (byte)(total * 255 + 0.5f));
    }
}
