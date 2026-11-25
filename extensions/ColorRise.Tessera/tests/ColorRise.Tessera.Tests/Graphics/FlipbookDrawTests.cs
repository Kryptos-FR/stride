using ColorRise.Tessera.Rendering;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace ColorRise.Tessera.Tests.Graphics;

/// <summary>
/// A flipbook drawn by its render feature, in a 64 by 64 frame whose view is 4 units high: 16 pixels a unit, the entity in
/// the middle of the frame unless a test moves the view.
/// </summary>
[Collection(GraphicsCollection.Name)]
public sealed class FlipbookDrawTests
{
    private static readonly Color R = new(255, 0, 0, 255);
    private static readonly Color G = new(0, 255, 0, 255);
    private static readonly Color B = new(0, 0, 255, 255);
    private static readonly Color Y = new(255, 255, 0, 255);
    private static readonly Color W = new(255, 255, 255, 255);
    private static readonly Color Magenta = new(255, 0, 255, 255);

    [Fact]
    public void A_flipbook_draws_its_frame_with_its_pivot_on_the_entity()
    {
        // A tile 1 unit wide and 1.5 high, its pivot (0.5, 0) on the entity at (0, 0)
        HarnessScene? view = null;
        var frame = Draw(device => Book(TestTiles.Tall(device, R), Frame(0.1f, Cell(0))), (scene, _) => view = scene);
        frame.Save("flipbook-pivot");

        Assert.Equal(R, frame[view!.ToPixel(new Vector2(0, 0.05f))]);
        Assert.Equal(Magenta, frame[view.ToPixel(new Vector2(0, -0.05f))]);
        Assert.Equal(R, frame[view.ToPixel(new Vector2(0, 1.45f))]);
        Assert.Equal(Magenta, frame[view.ToPixel(new Vector2(0, 1.55f))]);
        Assert.Equal(R, frame[view.ToPixel(new Vector2(-0.45f, 0.5f))]);
        Assert.Equal(Magenta, frame[view.ToPixel(new Vector2(-0.55f, 0.5f))]);
        Assert.Equal(R, frame[view.ToPixel(new Vector2(0.45f, 0.5f))]);
        Assert.Equal(Magenta, frame[view.ToPixel(new Vector2(0.55f, 0.5f))]);
    }

    [Fact]
    public void The_frame_of_the_time_is_drawn()
    {
        FlipbookComponent? component = null;
        var frames = GraphicsHarness.Run(new HarnessOptions(), scene =>
        {
            var book = Book(TestTiles.Flat(scene.Game.GraphicsDevice, R, G, B), Frame(0.1f, Cell(0)), Frame(0.1f, Cell(1)), Frame(0.1f, Cell(2)));
            component = new FlipbookComponent { Flipbook = book, Animation = "show", PlayOnStart = false };
            scene.Add("flipbook", component);
            scene.SetView(new Vector2(0, 0.5f), 1);
        }, _ => component!.Time = 0, _ => component!.Time = 0.15, _ => component!.Time = 0.25);

        Assert.Equal([R, G, B], frames.Select(frame => frame[32, 32]));
    }

    [Fact]
    public void The_game_plays_a_flipbook_that_plays_on_start()
    {
        FlipbookComponent? component = null;
        GraphicsHarness.Run(new HarnessOptions(), scene =>
        {
            component = new FlipbookComponent { Flipbook = Book(TestTiles.Flat(scene.Game.GraphicsDevice, R), Frame(0.1f, Cell(0))), Animation = "show" };
            scene.Add("flipbook", component);
        }, _ => { }, _ => { }, _ => { }, _ => { });

        Assert.True(component!.IsPlaying);
        Assert.True(component.Time > 0, $"The time of the flipbook is {component.Time}.");
    }

    [Fact]
    public void FlipX_draws_the_frame_mirrored()
    {
        var plain = DrawQuarters(flipX: false, TileFlip.None);
        var mirrored = DrawQuarters(flipX: true, TileFlip.None);
        mirrored.Save("flipbook-flipx");

        Assert.Equal([R, G, B, Y], Quarters(plain));
        Assert.Equal([G, R, Y, B], Quarters(mirrored));
    }

    [Fact]
    public void A_cell_flip_turns_the_tile()
    {
        // With the pivot in the middle of the tile, a cell flipped horizontally is the frame mirrored
        var flipped = DrawQuarters(flipX: false, TileFlip.Horizontal);
        var mirrored = DrawQuarters(flipX: true, TileFlip.None);

        Assert.Equal([G, R, Y, B], Quarters(flipped));
        Assert.Equal(Quarters(mirrored), Quarters(flipped));
    }

    [Fact]
    public void A_frame_of_two_cells_draws_both()
    {
        // A body and, 4 pixels over it, a head: the view 2 units high on the frame
        var frame = Draw(device => Book(TestTiles.Flat(device, R, G), Frame(0.1f, Cell(0), Cell(1, new Int2(0, 4)))),
            (scene, _) => scene.SetView(new Vector2(0, 1), 2));
        frame.Save("flipbook-two-cells");

        Assert.Equal(G, frame[32, 16]);
        Assert.Equal(R, frame[32, 48]);
        Assert.Equal(Magenta, frame[4, 16]);
        Assert.Equal(Magenta, frame[60, 48]);
    }

    [Fact]
    public void The_colour_of_the_component_tints_the_frame()
    {
        var frame = Draw(device => Book(TestTiles.Flat(device, W), Frame(0.1f, Cell(0))), (scene, component) =>
        {
            component.Color = new Color4(0, 1, 0, 1);
            scene.SetView(new Vector2(0, 0.5f), 1);
        });

        Assert.Equal(G, frame[32, 32]);
    }

    [Fact]
    public void A_disabled_component_is_not_drawn()
    {
        // Each action reads the count of the frame drawn before it
        FlipbookComponent? component = null;
        var drawn = new List<int>();
        var frames = GraphicsHarness.Run(new HarnessOptions(), scene =>
        {
            component = new FlipbookComponent { Flipbook = Book(TestTiles.Flat(scene.Game.GraphicsDevice, R), Frame(0.1f, Cell(0))), Animation = "show" };
            scene.Add("flipbook", component);
        }, _ => { }, scene =>
        {
            drawn.Add(Feature(scene).DrawnFlipbooks);
            component!.Enabled = false;
        }, scene => drawn.Add(Feature(scene).DrawnFlipbooks));

        Assert.Equal(R, frames[0][32, 31]);
        Assert.Equal(64 * 64, frames[1].Count(Magenta, new Rectangle(0, 0, 64, 64)));
        Assert.Equal([1, 0], drawn);
    }

    // Review Focus 1
    [Fact]
    public void A_cell_whose_tile_is_gone_draws_nothing_and_the_others_are_drawn()
    {
        var frame = Draw(device => Book(TestTiles.Flat(device, R), Frame(0.1f, Cell(0), new FlipbookCell { Tile = TileId.FromGrid(1, 9, 9), Offset = new Int2(0, 4) })),
            (scene, _) => scene.SetView(new Vector2(0, 1), 2));

        Assert.Equal(R, frame[32, 48]);
        Assert.Equal(Magenta, frame[32, 16]);
    }

    [Fact]
    public void A_flipbook_without_a_tile_set_draws_nothing()
    {
        var frames = GraphicsHarness.Run(new HarnessOptions(), scene =>
        {
            var book = Book(TestTiles.Flat(scene.Game.GraphicsDevice, R), Frame(0.1f, Cell(0)));
            book.TileSet = null;
            scene.Add("flipbook", new FlipbookComponent { Flipbook = book, Animation = "show" });
        }, _ => { }, _ => { }, _ => { });

        Assert.All(frames, frame => Assert.Equal(64 * 64, frame.Count(Magenta, new Rectangle(0, 0, 64, 64))));
    }

    // The quarters tile, one unit square, its pivot in the middle of its bottom edge on the entity; the view on the tile
    private static Frame DrawQuarters(bool flipX, TileFlip flip)
        => Draw(device => Book(TestTiles.Quarters(device), Frame(0.1f, new FlipbookCell { Tile = TileId.FromGrid(1, 0, 0), Flip = flip })),
            (scene, component) =>
            {
                component.FlipX = flipX;
                scene.SetView(new Vector2(0, 0.5f), 1);
            });

    // Top-left, top-right, bottom-left, bottom-right
    private static Color[] Quarters(Frame frame) => [frame[16, 16], frame[48, 16], frame[16, 48], frame[48, 48]];

    // One frame of a flipbook on an entity at the origin, playing its animation "show", which a test sets up further
    private static Frame Draw(Func<GraphicsDevice, Flipbook> makeFlipbook, Action<HarnessScene, FlipbookComponent>? setup = null)
        => GraphicsHarness.Run(new HarnessOptions(), scene =>
        {
            var component = new FlipbookComponent { Flipbook = makeFlipbook(scene.Game.GraphicsDevice), Animation = "show", PlayOnStart = false };
            scene.Add("flipbook", component);
            setup?.Invoke(scene, component);
        }, _ => { })[0];

    // A flipbook of one animation, "show", a loop whose one clip None has the given frames
    internal static Flipbook Book(TileSet set, params FlipbookFrame[] frames)
    {
        var clip = new FlipbookClip();
        clip.Frames.AddRange(frames);
        var animation = new FlipbookAnimation { Name = "show" };
        animation.Clips.Add(clip);
        var flipbook = new Flipbook { TileSet = set };
        flipbook.Animations.Add(animation);
        return flipbook;
    }

    internal static FlipbookFrame Frame(float duration, params FlipbookCell[] cells)
    {
        var frame = new FlipbookFrame { Duration = duration };
        frame.Cells.AddRange(cells);
        return frame;
    }

    internal static FlipbookCell Cell(int column, Int2 offset = default) => new() { Tile = TileId.FromGrid(1, column, 0), Offset = offset };

    private static FlipbookRenderFeature Feature(HarnessScene scene)
        => scene.Game.SceneSystem.GraphicsCompositor.RenderFeatures.OfType<FlipbookRenderFeature>().Single();
}
