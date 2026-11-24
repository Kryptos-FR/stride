using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Tests.Graphics;

[Collection(GraphicsCollection.Name)]
public sealed class TileAnimationDrawTests
{
    private static readonly Color R = new(255, 0, 0, 255);
    private static readonly Color G = new(0, 255, 0, 255);
    private static readonly Color B = new(0, 0, 255, 255);

    [Fact]
    public void An_animated_tile_shows_the_frame_of_its_time()
    {
        var frames = DrawAnimated(["0"], 0, 0.3, 0.8);
        Assert.Equal([G, B, G], frames.Select(frame => frame[32, 32]));
    }

    [Fact]
    public void Two_cells_of_the_same_tile_are_in_step()
    {
        var frames = DrawAnimated(["0 0"], 0.3);
        Assert.Equal([B, B], new[] { frames[0][16, 32], frames[0][48, 32] });
    }

    [Fact]
    public void A_frame_whose_tile_the_set_does_not_have_shows_the_cells_own_tile()
    {
        TileMapComponent? component = null;
        var frame = GraphicsHarness.Run(new HarnessOptions(), scene =>
        {
            var set = TestTiles.Flat(scene.Game.GraphicsDevice, R, G);
            set.GetTile(TileId.FromGrid(1, 0, 0))!.Animation.Add(new TileAnimationFrame { Tile = TileId.FromGrid(1, 9, 9), Duration = 1 });
            component = scene.Add("map", new TileMapComponent { Map = TestTiles.Map(set, ["0"]), AnimationSpeed = 0 }).Get<TileMapComponent>();
            scene.SetView(new Vector2(0.5f, 0.5f), 1);
        }, _ => component!.AnimationTime = 0.5)[0];
        Assert.Equal(R, frame[32, 32]);
    }

    [Fact]
    public void The_clock_runs_with_the_game_at_the_components_speed()
    {
        TileMapComponent? fast = null;
        TileMapComponent? normal = null;
        GraphicsHarness.Run(new HarnessOptions(), scene =>
        {
            var set = TestTiles.Flat(scene.Game.GraphicsDevice, R);
            fast = scene.Add("fast", new TileMapComponent { Map = TestTiles.Map(set, ["0"]), AnimationSpeed = 2 }).Get<TileMapComponent>();
            normal = scene.Add("normal", new TileMapComponent { Map = TestTiles.Map(set, ["0"]) }).Get<TileMapComponent>();
        }, _ => { }, _ => { }, _ => { });
        Assert.True(normal!.AnimationTime > 0, $"The clock of the component at speed 1 is {normal.AnimationTime}.");
        Assert.InRange(fast!.AnimationTime / normal.AnimationTime, 1.98, 2.02);
    }

    // A tile set of R, G and B, where R is animated: G for 0.25 s, then B for 0.5 s; one frame for each time, a cell one unit
    // wide in a 64 by 64 frame, the view on the map's row
    private static Frame[] DrawAnimated(string[] rows, params double[] times)
    {
        TileMapComponent? component = null;
        var width = rows[0].Split(' ').Length;
        return GraphicsHarness.Run(new HarnessOptions(), scene =>
        {
            var set = TestTiles.Flat(scene.Game.GraphicsDevice, R, G, B);
            var tile = set.GetTile(TileId.FromGrid(1, 0, 0))!;
            tile.Animation.Add(new TileAnimationFrame { Tile = TileId.FromGrid(1, 1, 0), Duration = 0.25f });
            tile.Animation.Add(new TileAnimationFrame { Tile = TileId.FromGrid(1, 2, 0), Duration = 0.5f });
            component = scene.Add("map", new TileMapComponent { Map = TestTiles.Map(set, rows), AnimationSpeed = 0 }).Get<TileMapComponent>();
            scene.SetView(new Vector2(width / 2f, 0.5f), width);
        }, [.. times.Select(time => (Action<HarnessScene>)(_ => component!.AnimationTime = time))]);
    }
}
