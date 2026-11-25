using ColorRise.Tessera.Rendering;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering;
using Stride.Rendering.Sprites;
using static ColorRise.Tessera.Tests.Graphics.FlipbookDrawTests;

namespace ColorRise.Tessera.Tests.Graphics;

/// <summary>
/// The order of flipbooks of one depth by their y (risk R2), and among the layers and the sprites by their z. Each frame
/// reads the pixel in its middle, where the camera looks: straight down -Z with the engine's sort, or tilted 30 degrees
/// around X with the axis sort, from 10 units away, 4 units high in 64 pixels.
/// </summary>
/// <remarks>
/// The flipbooks are one unit wide and high, their pivot in the middle of their bottom edge. <c>high</c> (red) is at
/// (0, 0.5) and <c>low</c> (green) at (0, 0): the band from 0.5 to 1 is in both.
/// </remarks>
[Collection(GraphicsCollection.Name)]
public sealed class FlipbookOrderTests
{
    private static readonly Color R = new(255, 0, 0, 255);
    private static readonly Color G = new(0, 255, 0, 255);
    private static readonly Color B = new(0, 0, 255, 255);
    private static readonly Color K = new(0, 0, 0, 255);
    private static readonly Color W = new(255, 255, 255, 255);

    private static readonly Vector2 SharedBand = new(0, 0.75f);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Of_two_flipbooks_of_one_z_the_lower_is_drawn_over(bool lowFirst)
    {
        var pixel = DrawHighAndLow(lowFirst, tilted: false);
        Assert.Equal(G, pixel);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_order_by_y_holds_under_a_tilted_camera_with_the_axis_sort(bool lowFirst)
    {
        var pixel = DrawHighAndLow(lowFirst, tilted: true);
        Assert.Equal(G, pixel);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_flipbook_in_front_wins_whatever_its_y(bool lowFirst)
    {
        var pixel = DrawHighAndLow(lowFirst, tilted: false, setup: (high, _) => high.Entity.Transform.Position.Z = 0.05f);
        Assert.Equal(R, pixel);
    }

    [Fact]
    public void Without_sort_by_y_a_flipbook_is_drawn_under_those_that_sort()
    {
        // High is added last and does not sort: its key is 0, under any y's
        var pixel = DrawHighAndLow(lowFirst: true, tilted: false, setup: (high, _) => high.SortByY = false);
        Assert.Equal(G, pixel);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_flipbook_at_the_z_of_a_layer_is_drawn_over_that_layer(bool flipbookFirst)
    {
        // Two cells: Layer0 red on both, Layer1 green on the first. The flipbook, blue, stands on (1, 0) at the z of Layer0:
        // its left half is over the first cell, its right half over the second
        var frames = Run(false, scene =>
        {
            var map = new TileMapComponent { Map = TestTiles.Map(TestTiles.Flat(scene.Game.GraphicsDevice, R, G), ["0 0"], ["1 ."]) };
            var place = new Vector3(1, 0, map.GetLayerZ("Layer0"));
            if (flipbookFirst)
            {
                AddFlipbook(scene, "flipbook", TestTiles.Flat(scene.Game.GraphicsDevice, B), place);
            }
            scene.Add("map", map);
            if (!flipbookFirst)
            {
                AddFlipbook(scene, "flipbook", TestTiles.Flat(scene.Game.GraphicsDevice, B), place);
            }
        }, new Vector2(0.75f, 0.5f), new Vector2(1.25f, 0.5f), new Vector2(0.25f, 0.5f), new Vector2(1.75f, 0.5f));

        Assert.Equal([G, B, G, R], frames);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_flipbook_between_two_layers_keeps_its_place(bool axisSort)
    {
        // R2's scene with a flipbook in the sprite's place: a map of 16 by 16 cells, Layer0 black, Layer1 red on the columns 0
        // to 7; a white flipbook 2 units wide and high, centred on (8, 2) between the two layers. Half a unit left of its
        // centre Layer1 covers it; half a unit right, it covers Layer0
        var centre = new Vector2(8, 2);
        var frames = Run(axisSort, scene =>
        {
            var set = TestTiles.Flat(scene.Game.GraphicsDevice, K, R);
            var ground = Enumerable.Repeat(string.Join(' ', Enumerable.Repeat("0", 16)), 16).ToArray();
            var details = Enumerable.Repeat(string.Join(' ', Enumerable.Repeat("1", 8).Concat(Enumerable.Repeat(".", 8))), 16).ToArray();
            var map = new TileMapComponent { Map = TestTiles.Map(set, ground, details) };
            scene.Add("map", map);

            var white = TileSet.FromTexture(TestTextures.Flat(scene.Game.GraphicsDevice, Color.White), new Int2(4, 4), pixelsPerUnit: 2);
            var flipbook = AddFlipbook(scene, "flipbook", white, new Vector3(centre, map.GetLayerZ("Layer1") - 0.05f));
            flipbook.Flipbook!.Pivot = new Vector2(0.5f, 0.5f);
        }, centre - new Vector2(0.5f, 0), centre + new Vector2(0.5f, 0));

        Assert.Equal([R, W], frames);
    }

    [Fact]
    public void A_sprite_at_the_z_of_flipbooks_is_drawn_under_them()
    {
        // A white sprite of 2 units centred on the flipbook's middle, added after it, at its z
        var frames = Run(false, scene =>
        {
            AddFlipbook(scene, "flipbook", TestTiles.Flat(scene.Game.GraphicsDevice, B), Vector3.Zero);
            var white = TestTextures.Flat(scene.Game.GraphicsDevice, Color.White);
            var sprite = scene.Add("sprite", new SpriteComponent { SpriteProvider = new SpriteFromTexture { Texture = white, PixelsPerUnit = 2 } });
            sprite.Transform.Position = new Vector3(0, 0.5f, 0);
        }, new Vector2(0, 0.5f), new Vector2(0.75f, 0.5f));

        Assert.Equal([B, W], frames);
    }

    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(2, 1, 0)]
    [InlineData(1, 2, 0)]
    public void Three_flipbooks_of_one_z_are_drawn_from_the_highest_to_the_lowest(int first, int second, int third)
    {
        // Section 11's test of R2: red at y 1, green at 0.5, blue at 0, each overlapping the next by half; the bands 1 to 1.5
        // and 0.5 to 1 are in two each
        var colours = new[] { R, G, B };
        var places = new[] { 1f, 0.5f, 0f };
        var points = new[] { new Vector2(0, 1.75f), new Vector2(0, 1.25f), new Vector2(0, 0.75f), new Vector2(0, 0.25f) };
        foreach (var tilted in new[] { false, true })
        {
            var frames = Run(tilted, scene =>
            {
                var set = TestTiles.Flat(scene.Game.GraphicsDevice, R, G, B);
                foreach (var index in new[] { first, second, third })
                {
                    var flipbook = AddFlipbook(scene, $"flipbook {index}", set, new Vector3(0, places[index], 0));
                    flipbook.Flipbook!.Animations[0].Clips[0].Frames[0].Cells[0].Tile = TileId.FromGrid(1, index, 0);
                }
            }, points);

            Assert.True(new[] { R, G, B, B }.SequenceEqual(frames), $"Tilted {tilted}: {string.Join(", ", frames)}.");
        }
    }

    // High and low, added in the order given, the frame on their shared band
    private static Color DrawHighAndLow(bool lowFirst, bool tilted, Action<FlipbookComponent, FlipbookComponent>? setup = null)
        => Run(tilted, scene =>
        {
            var set = TestTiles.Flat(scene.Game.GraphicsDevice, R, G);
            FlipbookComponent high;
            FlipbookComponent low;
            if (lowFirst)
            {
                low = AddFlipbook(scene, "low", set, Vector3.Zero, column: 1);
                high = AddFlipbook(scene, "high", set, new Vector3(0, 0.5f, 0), column: 0);
            }
            else
            {
                high = AddFlipbook(scene, "high", set, new Vector3(0, 0.5f, 0), column: 0);
                low = AddFlipbook(scene, "low", set, Vector3.Zero, column: 1);
            }
            setup?.Invoke(high, low);
        }, SharedBand)[0];

    // A flipbook of one frame of the set's tile in the given column, standing on a place
    private static FlipbookComponent AddFlipbook(HarnessScene scene, string name, TileSet set, Vector3 place, int column = 0)
    {
        var component = new FlipbookComponent { Flipbook = Book(set, Frame(0.1f, Cell(column))), Animation = "show", PlayOnStart = false };
        scene.Add(name, component).Transform.Position = place;
        return component;
    }

    // Builds the scene, then draws one frame per point, the camera looking at it; the colour in the middle of each frame
    private static Color[] Run(bool tilted, Action<HarnessScene> build, params Vector2[] points)
    {
        var options = new HarnessOptions { TransparentSortMode = tilted ? new TileAxisSortMode() : null };
        var frames = GraphicsHarness.Run(options, build, [.. points.Select(point => (Action<HarnessScene>)(scene => LookAt(scene, point, tilted)))]);
        return [.. frames.Select(frame => frame[32, 32])];
    }

    // As AxisSortTests: tilted, the camera looks at the point from 10 units away
    private static void LookAt(HarnessScene scene, Vector2 point, bool tilted)
    {
        if (!tilted)
        {
            scene.SetView(point, 4);
            return;
        }
        var tilt = Quaternion.RotationX(MathUtil.DegreesToRadians(30));
        scene.SetView(point, 4, tilt);
        var forward = Vector3.Transform(-Vector3.UnitZ, tilt);
        scene.Camera.Transform.Position = new Vector3(point, 0) - forward * 10;
    }
}
