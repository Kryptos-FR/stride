using ColorRise.Tessera.Rendering;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering.Sprites;

namespace ColorRise.Tessera.Tests.Graphics;

[Collection(GraphicsCollection.Name)]
public sealed class TileMapDrawTests
{
    private static readonly Color R = new(255, 0, 0, 255);
    private static readonly Color G = new(0, 255, 0, 255);
    private static readonly Color B = new(0, 0, 255, 255);
    private static readonly Color Y = new(255, 255, 0, 255);
    private static readonly Color W = new(255, 255, 255, 255);
    private static readonly Color K = new(0, 0, 0, 255);
    private static readonly Color Magenta = new(255, 0, 255, 255);

    [Fact]
    public void A_map_is_drawn_cell_by_cell_with_y_up()
    {
        var frame = DrawMap(device => TestTiles.Map(TestTiles.Flat(device, R, G, B, Y), ["0 1", "2 3"]), new Vector2(1, 1), 2);
        Assert.Equal(R, frame[16, 16]);
        Assert.Equal(G, frame[48, 16]);
        Assert.Equal(B, frame[16, 48]);
        Assert.Equal(Y, frame[48, 48]);
    }

    [Theory]
    [InlineData("0", "RGBY")]
    [InlineData("0h", "GRYB")]
    [InlineData("0v", "BYRG")]
    [InlineData("0hv", "YBGR")]
    [InlineData("0d", "RBGY")]
    [InlineData("0dh", "BRYG")]
    [InlineData("0dv", "GYRB")]
    [InlineData("0dhv", "YGBR")]
    public void The_eight_flips_are_Tileds(string token, string expected)
    {
        var frame = DrawMap(device => TestTiles.Map(TestTiles.Quarters(device), [token]), new Vector2(0.5f, 0.5f), 1);
        var colours = new Dictionary<char, Color> { ['R'] = R, ['G'] = G, ['B'] = B, ['Y'] = Y };
        Assert.Equal(expected.Select(letter => colours[letter]), new[] { frame[16, 16], frame[48, 16], frame[16, 48], frame[48, 48] });
    }

    [Fact]
    public void An_upper_layer_covers_a_lower_one_and_shows_it_through_its_empty_cells()
    {
        var frame = DrawMap(ThreeLayers, new Vector2(1.5f, 0.5f), 3);
        frame.Save("three-layers");
        Assert.Equal([B, G, R], new[] { frame[10, 32], frame[32, 32], frame[53, 32] });
    }

    [Fact]
    public void A_hidden_layer_is_not_drawn()
    {
        var frame = DrawMap(ThreeLayers, new Vector2(1.5f, 0.5f), 3, (_, map) => map.Map!.Layers["Layer2"].Visible = false);
        Assert.Equal([G, G, R], new[] { frame[10, 32], frame[32, 32], frame[53, 32] });
    }

    [Fact]
    public void A_layer_at_half_opacity_blends()
    {
        var frame = DrawMap(device => TestTiles.Map(TestTiles.Flat(device, W, K), ["1"], ["0"]), new Vector2(0.5f, 0.5f), 1,
            (_, map) => map.Map!.Layers["Layer1"].Opacity = 0.5f);
        var pixel = frame[32, 32];
        Assert.All(new[] { pixel.R, pixel.G, pixel.B }, channel => Assert.InRange(channel, 127, 128));
    }

    // Half-transparent red whose colour is not multiplied by its alpha, over black: half red, not full red
    [Fact]
    public void A_tile_set_whose_alpha_is_not_premultiplied_blends_by_its_alpha()
    {
        var frame = DrawMap(device =>
        {
            var set = TestTiles.Flat(device, new Color(255, 0, 0, 128));
            set.PremultipliedAlpha = false;
            return TestTiles.Map(set, ["0"]);
        }, new Vector2(0.5f, 0.5f), 1, options: new HarnessOptions { Background = K });
        var pixel = frame[32, 32];
        Assert.True(pixel.R is >= 126 and <= 130 && pixel.G == 0 && pixel.B == 0, $"The pixel is {pixel}.");
    }

    [Fact]
    public void A_tint_multiplies_the_colours()
    {
        var frame = DrawMap(device => TestTiles.Map(TestTiles.Flat(device, W), ["0"]), new Vector2(0.5f, 0.5f), 1,
            (_, map) => map.Map!.Layers[0].Tint = new Color(255, 0, 0));
        Assert.Equal(R, frame[32, 32]);
    }

    [Fact]
    public void The_component_colour_multiplies_the_layers()
    {
        var frame = DrawMap(device => TestTiles.Map(TestTiles.Flat(device, W), ["0"]), new Vector2(0.5f, 0.5f), 1,
            (_, map) => map.Color = new Color4(0, 1, 0, 1));
        Assert.Equal(G, frame[32, 32]);
    }

    [Fact]
    public void A_layer_offset_moves_the_layer_only()
    {
        var frame = DrawMap(device => TestTiles.Map(TestTiles.Flat(device, R, G), ["0 0"], ["1 ."]), new Vector2(1, 0.5f), 2,
            (_, map) => map.Map!.Layers["Layer1"].Offset = new Vector2(1, 0));
        Assert.Equal(R, frame[16, 32]);
        Assert.Equal(G, frame[48, 32]);
    }

    [Fact]
    public void A_sprite_goes_between_two_layers_by_its_z()
    {
        var frame = DrawMap(device => TestTiles.Map(TestTiles.Flat(device, K, R), ["0 0"], ["1 ."]), new Vector2(1, 0.5f), 2, (scene, map) =>
        {
            // A white sprite of 4 units, over the whole map, under the layer Details
            var white = TestTextures.Flat(scene.Game.GraphicsDevice, Color.White);
            var sprite = scene.Add("sprite", new SpriteComponent { SpriteProvider = new SpriteFromTexture { Texture = white, PixelsPerUnit = 1 } });
            sprite.Transform.Position = new Vector3(1, 0.5f, map.GetLayerZ("Layer1") - 0.05f);
        });
        Assert.Equal(R, frame[16, 32]);
        Assert.Equal(W, frame[48, 32]);
    }

    [Fact]
    public void A_tilted_camera_keeps_the_order_of_the_layers()
    {
        var frame = DrawMap(ThreeLayers, new Vector2(1.5f, 0.5f), 3, (scene, _) =>
        {
            var tilt = Quaternion.RotationX(MathUtil.DegreesToRadians(30));
            scene.SetView(new Vector2(1.5f, 0.5f), 3, tilt);
            // Keeps the map in the middle of the frame: the camera looks at it from 10 units away
            var forward = Vector3.Transform(-Vector3.UnitZ, tilt);
            scene.Camera.Transform.Position = new Vector3(1.5f, 0.5f, 0) - forward * 10;
        }, setView: false);
        Assert.Equal([B, G, R], new[] { frame[10, 32], frame[32, 32], frame[53, 32] });
    }

    [Fact]
    public void Two_maps_order_by_their_entities_z()
    {
        var frame = DrawMap(device => TestTiles.Map(TestTiles.Flat(device, R), ["0"]), new Vector2(0.5f, 0.5f), 1, (scene, map) =>
        {
            map.Entity.Transform.Position = new Vector3(0, 0, 0.5f);
            var set = TestTiles.Flat(scene.Game.GraphicsDevice, G);
            scene.Add("lower map", new TileMapComponent { Map = TestTiles.Map(set, ["0"]) });
        });
        Assert.Equal(R, frame[32, 32]);
    }

    [Fact]
    public void Only_the_chunks_in_view_are_drawn()
    {
        var drawn = -1;
        GraphicsHarness.Run(new HarnessOptions(), scene =>
        {
            var set = TestTiles.Flat(scene.Game.GraphicsDevice, W);
            var map = new TileMap();
            map.AddTileSet(set);
            var layer = map.Layers.Add("Ground");
            for (var y = 0; y < 160; y++)
            {
                for (var x = 0; x < 160; x++)
                {
                    layer.SetCell(new Int2(x, y), set, TileId.FromGrid(1, 0, 0));
                }
            }
            scene.Add("map", new TileMapComponent { Map = map });
            scene.SetView(new Vector2(8, 8), 16);
        }, _ => { }, scene => drawn = Feature(scene).DrawnChunks);
        Assert.InRange(drawn, 1, 4);
    }

    [Fact]
    public void A_cell_changed_between_two_frames_shows_in_the_second()
    {
        TileMap? map = null;
        TileSet? set = null;
        var frames = GraphicsHarness.Run(new HarnessOptions(), scene =>
        {
            set = TestTiles.Flat(scene.Game.GraphicsDevice, R, G);
            map = TestTiles.Map(set, ["0"]);
            scene.Add("map", new TileMapComponent { Map = map });
            scene.SetView(new Vector2(0.5f, 0.5f), 1);
        }, _ => { }, _ => map!.Layers[0].SetCell(Int2.Zero, set!, TileId.FromGrid(1, 1, 0)));
        Assert.Equal(R, frames[0][32, 32]);
        Assert.Equal(G, frames[1][32, 32]);
    }

    // The layer drawn later is the one added later, so only its z can put it under the others
    [Fact]
    public void A_layer_inserted_under_the_others_in_a_later_frame_is_drawn_under_them()
    {
        TileMap? map = null;
        TileSet? set = null;
        var frames = GraphicsHarness.Run(new HarnessOptions(), scene =>
        {
            set = TestTiles.Flat(scene.Game.GraphicsDevice, R, G);
            map = TestTiles.Map(set, ["0"]);
            scene.Add("map", new TileMapComponent { Map = map });
            scene.SetView(new Vector2(0.5f, 0.5f), 1);
        }, _ => { }, _ =>
        {
            var below = new TileLayer { Name = "Below" };
            map!.Layers.Insert(0, below);
            below.SetCell(Int2.Zero, set!, TileId.FromGrid(1, 1, 0));
        });
        Assert.Equal(R, frames[1][32, 32]);
    }

    [Fact]
    public void A_cell_whose_tile_is_gone_draws_nothing_and_does_not_throw()
    {
        var frame = DrawMap(device =>
        {
            var map = TestTiles.Map(TestTiles.Flat(device, R), [". ."]);
            map.Layers[0].SetCell(new Int2(0, 0), new TileCell(9, TileId.FromGrid(1, 0, 0)));
            map.Layers[0].SetCell(new Int2(1, 0), new TileCell(1, TileId.FromGrid(1, 9, 9)));
            return map;
        }, new Vector2(1, 0.5f), 2);
        Assert.Equal(64 * 64, frame.Count(Magenta, new Rectangle(0, 0, 64, 64)));
    }

    [Fact]
    public void A_tile_larger_than_a_cell_goes_up_and_to_the_right()
    {
        var frame = DrawMap(device =>
        {
            var tall = TileSet.FromTexture(TestTextures.FromPixels(device, 8, 16, (_, _) => Color.White), new Int2(8, 16), pixelsPerUnit: 8);
            return TestTiles.Map(tall, ["0"]);
        }, new Vector2(0.5f, 1), 2);
        Assert.Equal(W, frame[32, 16]);   // cell (0, 1)
        Assert.Equal(W, frame[32, 48]);   // cell (0, 0)
        Assert.Equal(Magenta, frame[4, 32]);
    }

    [Fact]
    public void A_map_at_a_fraction_of_a_pixel_has_no_gap()
    {
        var views = new[] { 8f, 9.37f, 11.113f, 23.5f }
            .SelectMany(unitsHigh => Enumerable.Range(0, 32).Select(i => (Center: new Vector2(16 + i / 32f * 0.3f, 16 + i / 32f * 0.7f), UnitsHigh: unitsHigh)))
            .ToArray();
        var frames = GraphicsHarness.Run(new HarnessOptions { Width = 128, Height = 128 }, scene =>
        {
            var set = TestTiles.Flat(scene.Game.GraphicsDevice, W);
            var map = new TileMap();
            map.AddTileSet(set);
            var layer = map.Layers.Add("Ground");
            for (var y = 0; y < 32; y++)
            {
                for (var x = 0; x < 32; x++)
                {
                    layer.SetCell(new Int2(x, y), set, TileId.FromGrid(1, 0, 0));
                }
            }
            scene.Add("map", new TileMapComponent { Map = map });
        }, [.. views.Select(view => (Action<HarnessScene>)(scene => scene.SetView(view.Center, view.UnitsHigh)))]);

        for (var i = 0; i < frames.Length; i++)
        {
            Assert.True(0 == frames[i].Count(Magenta, new Rectangle(0, 0, 128, 128)), $"A gap shows with the view on {views[i].Center}, {views[i].UnitsHigh} units high.");
        }
    }

    [Fact]
    public void Linear_colour_space_gives_the_same_tint()
    {
        var orange = new Color(255, 128, 0, 255);
        var frame = DrawMap(device => TestTiles.Map(TestTiles.Flat(device, W), ["0"]), new Vector2(0.5f, 0.5f), 1,
            (_, map) => map.Map!.Layers[0].Tint = orange, options: new HarnessOptions { ColorSpace = ColorSpace.Linear });
        var pixel = frame[32, 32];
        Assert.InRange(pixel.R, 252, 255);
        Assert.InRange(pixel.G, 125, 131);
        Assert.InRange(pixel.B, 0, 3);
    }

    [Theory]
    [InlineData(TileSampler.Point)]
    [InlineData(TileSampler.Linear)]
    public void Linear_sampling_blends_two_texels_and_point_does_not(TileSampler sampler)
    {
        var frame = DrawMap(device =>
        {
            var halves = TestTextures.FromPixels(device, 2, 2, (x, _) => x == 0 ? K : W);
            return TestTiles.Map(TileSet.FromTexture(halves, new Int2(2, 2), pixelsPerUnit: 2), ["0"]);
        }, new Vector2(0.5f, 0.5f), 1, (_, map) => map.Sampler = sampler);

        if (sampler == TileSampler.Point)
        {
            Assert.Equal(K, frame[31, 32]);
            Assert.Equal(W, frame[32, 32]);
        }
        else
        {
            Assert.InRange(frame[31, 32].R, 60, 200);
            Assert.InRange(frame[32, 32].R, 60, 200);
        }
    }

    // Layers of three cells in a row: Layer0 red everywhere, Layer1 green on the first two, Layer2 blue on the first
    private static TileMap ThreeLayers(GraphicsDevice device)
        => TestTiles.Map(TestTiles.Flat(device, R, G, B), ["0 0 0"], ["1 1 ."], ["2 . ."]);

    private static TileRenderFeature Feature(HarnessScene scene)
        => scene.Game.SceneSystem.GraphicsCompositor.RenderFeatures.OfType<TileRenderFeature>().Single();

    // One frame of a map on an entity at the origin, with the view on a point
    private static Frame DrawMap(Func<GraphicsDevice, TileMap> makeMap, Vector2 center, float unitsHigh,
        Action<HarnessScene, TileMapComponent>? setup = null, bool setView = true, HarnessOptions? options = null)
        => GraphicsHarness.Run(options ?? new HarnessOptions(), scene =>
        {
            var map = new TileMapComponent { Map = makeMap(scene.Game.GraphicsDevice) };
            scene.Add("map", map);
            if (setView)
            {
                scene.SetView(center, unitsHigh);
            }
            setup?.Invoke(scene, map);
        }, _ => { })[0];
}
