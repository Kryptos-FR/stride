using ColorRise.Tessera.Rendering;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering;
using Stride.Rendering.Sprites;

namespace ColorRise.Tessera.Tests.Graphics;

/// <summary>
/// A sprite between two layers of a map keeps its place under a tilted camera once the transparent stage sorts along an axis.
/// </summary>
[Collection(GraphicsCollection.Name)]
public sealed class AxisSortTests
{
    private static readonly Color R = new(255, 0, 0, 255);
    private static readonly Color K = new(0, 0, 0, 255);
    private static readonly Color W = new(255, 255, 255, 255);

    // A map of 16 by 16 cells, so one chunk whose centre is (8, 8). Layer0 is black everywhere. Layer1 is red on the
    // columns 0 to 7 and empty on the columns 8 to 15. A white sprite, 2 units wide and high, is centred on (8, y), at a z
    // between the two layers. The camera is tilted 30 degrees around X and looks at the sprite's centre from 10 units
    // away, 4 units high in a frame of 64 pixels: 16 pixels a unit, and the row 32 of the frame crosses the sprite.
    [Theory]
    [InlineData(2f)]    // nearer to the camera's plane than the chunk's centre: the default sort draws the sprite last
    [InlineData(14f)]   // farther: the default sort draws it first
    public void A_sprite_between_two_layers_keeps_its_place_under_a_tilted_camera(float y)
    {
        var frame = Draw(y, new TileAxisSortMode());
        Assert.Equal(R, frame[24, 32]);   // half a unit left of the sprite's centre: Layer1 covers the sprite
        Assert.Equal(W, frame[40, 32]);   // half a unit right: Layer1 is empty there, and the sprite covers Layer0
    }

    [Fact]
    public void The_layers_keep_their_order_from_behind()
    {
        // The same scene, the camera turned 180 degrees around Y and placed behind the map, looking at (8, 8): Layer0 is now
        // in front, so where both layers have tiles the pixel is black
        var frame = Draw(8f, new TileAxisSortMode(), Quaternion.RotationY(MathF.PI), target: new Vector3(8, 8, 0));
        // Seen from behind, x is mirrored: the columns 0 to 7 of the map are on the right half of the frame
        Assert.Equal(K, frame[40, 32]);
    }

    [Fact]
    public void An_axis_of_zero_sorts_as_Z()
    {
        var frame = Draw(2f, new TileAxisSortMode { Axis = Vector3.Zero });
        Assert.Equal(R, frame[24, 32]);
        Assert.Equal(W, frame[40, 32]);
    }

    private static Frame Draw(float y, SortMode? sortMode, Quaternion? rotation = null, Vector3? target = null)
        => GraphicsHarness.Run(new HarnessOptions { TransparentSortMode = sortMode }, scene =>
        {
            var set = TestTiles.Flat(scene.Game.GraphicsDevice, K, R);
            var ground = Enumerable.Repeat(string.Join(' ', Enumerable.Repeat("0", 16)), 16).ToArray();
            var details = Enumerable.Repeat(string.Join(' ', Enumerable.Repeat("1", 8).Concat(Enumerable.Repeat(".", 8))), 16).ToArray();
            var map = new TileMapComponent { Map = TestTiles.Map(set, ground, details) };
            scene.Add("map", map);

            var white = TestTextures.Flat(scene.Game.GraphicsDevice, Color.White);
            var sprite = scene.Add("sprite", new SpriteComponent { SpriteProvider = new SpriteFromTexture { Texture = white, PixelsPerUnit = 2 } });
            var spriteCentre = new Vector3(8, y, map.GetLayerZ("Layer1") - 0.05f);
            sprite.Transform.Position = spriteCentre;

            var look = target ?? spriteCentre;
            var tilt = rotation ?? Quaternion.RotationX(MathUtil.DegreesToRadians(30));
            scene.SetView(new Vector2(look.X, look.Y), 4, tilt);
            // The camera looks at the point from 10 units away
            var forward = Vector3.Transform(-Vector3.UnitZ, tilt);
            scene.Camera.Transform.Position = look - forward * 10;
        }, _ => { })[0];
}
