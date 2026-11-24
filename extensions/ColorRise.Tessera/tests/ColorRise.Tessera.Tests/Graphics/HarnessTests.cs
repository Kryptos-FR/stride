using Stride.Core.Mathematics;
using Stride.Rendering.Sprites;

namespace ColorRise.Tessera.Tests.Graphics;

[Collection(GraphicsCollection.Name)]
public sealed class HarnessTests
{
    private static readonly Color Magenta = new(255, 0, 255, 255);

    [Fact]
    public void A_frame_without_entities_is_the_background()
    {
        var frame = GraphicsHarness.Run(new HarnessOptions(), _ => { }, _ => { })[0];
        Assert.Equal(64 * 64, frame.Count(Magenta, new Rectangle(0, 0, 64, 64)));
    }

    [Fact]
    public void A_sprite_is_drawn_where_the_view_says()
    {
        var origin = default(Int2);
        var frame = GraphicsHarness.Run(new HarnessOptions(), scene =>
        {
            scene.SetView(new Vector2(0, 0), unitsHigh: 4);
            origin = scene.ToPixel(Vector2.Zero);
            AddWhiteSquare(scene, Vector2.Zero);
        }, _ => { })[0];
        Assert.Equal(new Int2(32, 32), origin);
        Assert.Equal(Color.White, frame[32, 32]);
        Assert.Equal(Magenta, frame[4, 4]);
    }

    [Fact]
    public void Each_frame_action_gets_its_own_frame()
    {
        Stride.Engine.Entity? square = null;
        var frames = GraphicsHarness.Run(new HarnessOptions(), scene =>
        {
            scene.SetView(new Vector2(0, 0), unitsHigh: 4);
            square = AddWhiteSquare(scene, Vector2.Zero);
        }, _ => { }, _ => square!.Transform.Position = new Vector3(1.5f, 0, 0));
        Assert.Equal(2, frames.Length);
        Assert.Equal(Color.White, frames[0][32, 32]);
        Assert.Equal(Magenta, frames[1][32, 32]);
        Assert.Equal(Color.White, frames[1][56, 32]);
    }

    [Fact]
    public void An_exception_in_an_action_comes_out_of_Run()
    {
        var error = Assert.Throws<InvalidOperationException>(() => GraphicsHarness.Run(new HarnessOptions(), _ => throw new InvalidOperationException("from the test")));
        Assert.Equal("from the test", error.Message);
        Assert.Single(GraphicsHarness.Run(new HarnessOptions(), _ => { }, _ => { }));
    }

    // A white sprite one unit wide and high, centred on a point
    private static Stride.Engine.Entity AddWhiteSquare(HarnessScene scene, Vector2 center)
    {
        var white = TestTextures.Flat(scene.Game.GraphicsDevice, Color.White);
        var entity = scene.Add("square", new Stride.Engine.SpriteComponent { SpriteProvider = new SpriteFromTexture { Texture = white, PixelsPerUnit = 4 } });
        entity.Transform.Position = new Vector3(center, 0);
        return entity;
    }
}
