using ColorRise.Tessera.Rendering;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace ColorRise.Tessera.Tests.Graphics;

[Collection(GraphicsCollection.Name)]
public sealed class TileBatchTests
{
    private static readonly Color Magenta = new(255, 0, 255, 255);
    private static readonly float[] Zooms = [8f, 9.37f, 11.113f, 23.5f];
    private const int GridSize = 8;

    [Fact]
    public void A_quad_shows_its_texture_with_y_up()
    {
        var frame = Draw(64, Magenta, TestTextures.Quarters, Color4.White,
            (batch, texture) => batch.Draw(texture, Square(0, 0, 2)),
            (new Vector2(1, 1), 2))[0];
        Assert.Equal(new Color(255, 0, 0, 255), frame[16, 16]);     // top-left
        Assert.Equal(new Color(0, 255, 0, 255), frame[48, 16]);     // top-right
        Assert.Equal(new Color(0, 0, 255, 255), frame[16, 48]);     // bottom-left
        Assert.Equal(new Color(255, 255, 0, 255), frame[48, 48]);   // bottom-right
    }

    [Fact]
    public void The_batch_colour_tints_every_quad()
    {
        var frame = Draw(64, Magenta, device => TestTextures.Flat(device, Color.White), new Color4(1, 0, 0, 1),
            (batch, texture) => batch.Draw(texture, Square(0, 0, 2)),
            (new Vector2(1, 1), 2))[0];
        Assert.Equal(new Color(255, 0, 0, 255), frame[32, 32]);
    }

    [Fact]
    public void A_half_transparent_colour_blends_with_the_background()
    {
        var frame = Draw(64, new Color(0, 0, 0, 255), device => TestTextures.Flat(device, Color.White), new Color4(1, 1, 1, 0.5f),
            (batch, texture) => batch.Draw(texture, Square(0, 0, 2)),
            (new Vector2(1, 1), 2))[0];
        var pixel = frame[32, 32];
        Assert.All(new[] { pixel.R, pixel.G, pixel.B }, channel => Assert.InRange(channel, 127, 128));
    }

    [Fact]
    public void Neighbouring_quads_leave_no_gap()
    {
        var views = Views();
        var frames = DrawGrid(views, rotation: 0);
        for (var i = 0; i < frames.Length; i++)
        {
            if (views[i].UnitsHigh == 9.37f && i % 32 == 0)
            {
                frames[i].Save($"grid-{i}");
            }
            Assert.True(0 == frames[i].Count(Magenta, InsideGrid(views[i].Center, views[i].UnitsHigh, 128)),
                $"A gap shows with the view on {views[i].Center}, {views[i].UnitsHigh} units high.");
        }
    }

    [Fact]
    public void A_rotated_grid_leaves_no_gap()
    {
        var views = Views();
        var rotation = MathUtil.DegreesToRadians(17);
        var frames = DrawGrid(views, rotation);
        for (var i = 0; i < frames.Length; i++)
        {
            if (views[i].UnitsHigh == 9.37f && i % 32 == 0)
            {
                frames[i].Save($"rotated-grid-{i}");
            }
            Assert.True(0 == CountInsideRotatedGrid(frames[i], Magenta, views[i].Center, views[i].UnitsHigh, rotation),
                $"A gap shows with the view on {views[i].Center}, {views[i].UnitsHigh} units high.");
        }
    }

    // Four zooms, each at 32 positions of the view that do not fall on whole pixels
    private static (Vector2 Center, float UnitsHigh)[] Views()
        => [.. Zooms.SelectMany(unitsHigh => Enumerable.Range(0, 32).Select(i => (new Vector2(4 + i / 32f * 0.3f, 4 + i / 32f * 0.7f), unitsHigh)))];

    // Draws a grid of 8 by 8 white quads of one unit, from (0, 0) to (8, 8), turned about its centre, once for each view
    private static Frame[] DrawGrid((Vector2 Center, float UnitsHigh)[] views, float rotation)
    {
        var turn = Matrix.RotationZ(rotation);
        Vector3 Corner(int x, int y)
            => Vector3.TransformCoordinate(new Vector3(x - GridSize / 2, y - GridSize / 2, 0), turn) + new Vector3(GridSize / 2, GridSize / 2, 0);

        return Draw(128, Magenta, device => TestTextures.Flat(device, Color.White), Color4.White, (batch, texture) =>
        {
            for (var y = 0; y < GridSize; y++)
            {
                for (var x = 0; x < GridSize; x++)
                {
                    batch.Draw(texture, new TileQuad
                    {
                        BottomLeft = Corner(x, y),
                        BottomRight = Corner(x + 1, y),
                        TopRight = Corner(x + 1, y + 1),
                        TopLeft = Corner(x, y + 1),
                        UvBottomLeft = new Vector2(0, 1),
                        UvBottomRight = new Vector2(1, 1),
                        UvTopRight = new Vector2(1, 0),
                        UvTopLeft = new Vector2(0, 0),
                    });
                }
            }
        }, views);
    }

    // The pixels that the grid covers, one pixel in from its edges
    private static Rectangle InsideGrid(Vector2 center, float unitsHigh, int size)
    {
        var pixelsPerUnit = size / unitsHigh;
        var left = (int)MathF.Ceiling((0 - center.X) * pixelsPerUnit + size / 2f) + 1;
        var right = (int)MathF.Floor((GridSize - center.X) * pixelsPerUnit + size / 2f) - 1;
        var top = (int)MathF.Ceiling(size / 2f - (GridSize - center.Y) * pixelsPerUnit) + 1;
        var bottom = (int)MathF.Floor(size / 2f - (0 - center.Y) * pixelsPerUnit) - 1;
        return new Rectangle(left, top, right - left, bottom - top);
    }

    // Counts the pixels of a colour whose centre is in the turned grid, a pixel and a half in from its edges
    private static int CountInsideRotatedGrid(Frame frame, Color color, Vector2 center, float unitsHigh, float rotation)
    {
        var pixelsPerUnit = frame.Height / unitsHigh;
        var margin = 1.5f / pixelsPerUnit;
        var back = Matrix.RotationZ(-rotation);
        var count = 0;
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                var world = new Vector3(center.X + (x + 0.5f - frame.Width / 2f) / pixelsPerUnit, center.Y - (y + 0.5f - frame.Height / 2f) / pixelsPerUnit, 0);
                var grid = Vector3.TransformCoordinate(world - new Vector3(GridSize / 2, GridSize / 2, 0), back) + new Vector3(GridSize / 2, GridSize / 2, 0);
                if (grid.X > margin && grid.X < GridSize - margin && grid.Y > margin && grid.Y < GridSize - margin && frame[x, y] == color)
                {
                    count++;
                }
            }
        }
        return count;
    }

    private static TileQuad Square(float left, float bottom, float size) => new()
    {
        BottomLeft = new Vector3(left, bottom, 0),
        BottomRight = new Vector3(left + size, bottom, 0),
        TopRight = new Vector3(left + size, bottom + size, 0),
        TopLeft = new Vector3(left, bottom + size, 0),
        UvBottomLeft = new Vector2(0, 1),
        UvBottomRight = new Vector2(1, 1),
        UvTopRight = new Vector2(1, 0),
        UvTopLeft = new Vector2(0, 0),
    };

    // One game: a batch and a texture made once, then one frame for each view, each drawn by the batch alone
    private static Frame[] Draw(int size, Color background, Func<GraphicsDevice, Texture> makeTexture, Color4 color,
        Action<TileBatch, Texture> draw, params (Vector2 Center, float UnitsHigh)[] views)
    {
        TileBatch? batch = null;
        Texture? texture = null;
        var options = new HarnessOptions
        {
            Width = size,
            Height = size,
            Background = background,
            CustomDraw = (context, viewProjection) =>
            {
                batch!.Begin(context.GraphicsContext, viewProjection, color);
                draw(batch, texture!);
                batch.End();
            },
        };
        return GraphicsHarness.Run(options,
            scene =>
            {
                batch = new TileBatch(scene.Game.GraphicsDevice);
                texture = makeTexture(scene.Game.GraphicsDevice);
            },
            [.. views.Select(view => (Action<HarnessScene>)(scene => scene.SetView(view.Center, view.UnitsHigh)))]);
    }
}
