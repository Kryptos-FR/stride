using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace ColorRise.Tessera.Tests.Graphics;

/// <summary>
/// The maps in the scene editor's picking stage: each drawn cell gives the id of its component, which the editor turns
/// into the entity under the click.
/// </summary>
[Collection(GraphicsCollection.Name)]
public sealed class TilePickingTests
{
    private static readonly Color R = new(255, 0, 0, 255);
    private static readonly Color G = new(0, 255, 0, 255);
    private static readonly Color B = new(0, 0, 255, 255);

    // The view of these tests: two units wide and high, from (0, -0.5); the cell (0, 0) is the pixels 0 to 32 across and
    // 16 to 48 down, the cell (1, 0) is 32 to 64 across
    private static readonly Int2 Cell0 = new(16, 32);
    private static readonly Int2 Cell1 = new(48, 32);
    private static readonly Int2 BelowTheMap = new(16, 56);

    [Fact]
    public void A_cell_with_a_tile_gives_the_id_of_its_map()
    {
        TileMapComponent? map = null;
        var frame = Draw(scene => map = AddMap(scene, ["0 ."]));
        Assert.NotEqual(0, RuntimeIdHelper.ToRuntimeId(map!));
        Assert.Equal(RuntimeIdHelper.ToRuntimeId(map!), frame.PickedId(Cell0.X, Cell0.Y));
    }

    [Fact]
    public void A_cell_without_a_tile_gives_nothing()
    {
        var frame = Draw(scene => AddMap(scene, ["0 ."]));
        Assert.Equal(0, frame.PickedId(Cell1.X, Cell1.Y));
        Assert.Equal(0, frame.PickedId(BelowTheMap.X, BelowTheMap.Y));
    }

    // The picking stage has no sort: the map in front wins whichever is drawn first
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Two_maps_give_their_own_ids_and_the_one_in_front_wins(bool frontFirst)
    {
        TileMapComponent? back = null;
        TileMapComponent? front = null;
        var frame = Draw(scene =>
        {
            if (frontFirst)
            {
                front = AddMap(scene, new Vector3(0, 0, 1), [". 0"]);
                back = AddMap(scene, ["0 0"]);
            }
            else
            {
                back = AddMap(scene, ["0 0"]);
                front = AddMap(scene, new Vector3(0, 0, 1), [". 0"]);
            }
        });
        Assert.Equal(RuntimeIdHelper.ToRuntimeId(back!), frame.PickedId(Cell0.X, Cell0.Y));
        Assert.Equal(RuntimeIdHelper.ToRuntimeId(front!), frame.PickedId(Cell1.X, Cell1.Y));
    }

    [Fact]
    public void A_hidden_layer_is_not_picked()
    {
        TileMapComponent? map = null;
        var frame = Draw(scene =>
        {
            map = AddMap(scene, ["0 ."], [". 0"]);
            map.Map!.Layers["Layer1"].Visible = false;
        });
        Assert.Equal(RuntimeIdHelper.ToRuntimeId(map!), frame.PickedId(Cell0.X, Cell0.Y));
        Assert.Equal(0, frame.PickedId(Cell1.X, Cell1.Y));
    }

    [Fact]
    public void The_normal_frame_is_the_same_with_and_without_the_picking_stage()
    {
        static void Build(HarnessScene scene)
        {
            scene.SetView(new Vector2(1, 0.5f), unitsHigh: 2);
            var map = AddMap(scene, ["0 1"], [". 2"]);
            map.Map!.Layers["Layer1"].Opacity = 0.5f;
        }
        var without = GraphicsHarness.Run(new HarnessOptions(), Build, _ => { })[0];
        var with = GraphicsHarness.Run(new HarnessOptions { Picking = true }, Build, _ => { })[0];

        var different = 0;
        for (var y = 0; y < without.Height; y++)
        {
            for (var x = 0; x < without.Width; x++)
            {
                different += without[x, y] == with[x, y] ? 0 : 1;
            }
        }
        Assert.Equal(0, different);
        // The frame is not empty: the map is drawn in it
        Assert.Equal(R, with[Cell0]);
    }

    private static Frame Draw(Action<HarnessScene> build)
        => GraphicsHarness.Run(new HarnessOptions { Picking = true }, scene =>
        {
            scene.SetView(new Vector2(1, 0.5f), unitsHigh: 2);
            build(scene);
        }, _ => { })[0];

    private static TileMapComponent AddMap(HarnessScene scene, params string[][] layers)
        => AddMap(scene, Vector3.Zero, layers);

    private static TileMapComponent AddMap(HarnessScene scene, Vector3 position, params string[][] layers)
    {
        var map = new TileMapComponent { Map = TestTiles.Map(TestTiles.Flat(scene.Game.GraphicsDevice, R, G, B), layers) };
        scene.Add("map", map).Transform.Position = position;
        return map;
    }
}
