using Stride.Core;
using Stride.Core.Mathematics;
using static ColorRise.Tessera.Tests.Graphics.FlipbookDrawTests;

namespace ColorRise.Tessera.Tests.Graphics;

/// <summary>
/// The flipbooks in the scene editor's picking stage: each drawn cell gives the id of its component, which the editor turns
/// into the entity under the click (risk R7).
/// </summary>
[Collection(GraphicsCollection.Name)]
public sealed class FlipbookPickingTests
{
    private static readonly Color R = new(255, 0, 0, 255);
    private static readonly Color B = new(0, 0, 255, 255);

    // The view of these tests: two units wide and high, from (0, -0.5): 32 pixels a unit, the middle of the frame on (1, 0.5)
    private static readonly Int2 Middle = new(32, 32);
    private static readonly Int2 Left = new(8, 32);

    [Fact]
    public void A_frame_gives_the_id_of_its_component()
    {
        // The flipbook stands on (1, 0): its frame is the unit from 0.5 to 1.5 across, 0 to 1 up
        FlipbookComponent? flipbook = null;
        var frame = Draw(scene => flipbook = AddFlipbook(scene, new Vector3(1, 0, 0)));

        Assert.NotEqual(0, RuntimeIdHelper.ToRuntimeId(flipbook!));
        Assert.Equal(RuntimeIdHelper.ToRuntimeId(flipbook!), frame.PickedId(Middle.X, Middle.Y));
        Assert.Equal(0, frame.PickedId(Left.X, Left.Y));
    }

    // The picking stage has no sort: the one in front wins whichever is drawn first
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_flipbook_in_front_of_a_map_wins_the_click(bool flipbookFirst)
    {
        FlipbookComponent? flipbook = null;
        TileMapComponent? map = null;
        var frame = Draw(scene =>
        {
            if (flipbookFirst)
            {
                flipbook = AddFlipbook(scene, new Vector3(1, 0, 0.5f));
                map = AddMap(scene);
            }
            else
            {
                map = AddMap(scene);
                flipbook = AddFlipbook(scene, new Vector3(1, 0, 0.5f));
            }
        });

        Assert.Equal(RuntimeIdHelper.ToRuntimeId(flipbook!), frame.PickedId(Middle.X, Middle.Y));
        Assert.Equal(RuntimeIdHelper.ToRuntimeId(map!), frame.PickedId(Left.X, Left.Y));
    }

    [Fact]
    public void A_flipbook_behind_a_tile_gives_the_map()
    {
        TileMapComponent? map = null;
        var frame = Draw(scene =>
        {
            AddFlipbook(scene, new Vector3(1, 0, -0.5f));
            map = AddMap(scene);
        });

        Assert.Equal(RuntimeIdHelper.ToRuntimeId(map!), frame.PickedId(Middle.X, Middle.Y));
    }

    private static Frame Draw(Action<HarnessScene> build)
        => GraphicsHarness.Run(new HarnessOptions { Picking = true }, scene =>
        {
            scene.SetView(new Vector2(1, 0.5f), unitsHigh: 2);
            build(scene);
        }, _ => { })[0];

    // A blue flipbook one unit wide and high, standing on a place
    private static FlipbookComponent AddFlipbook(HarnessScene scene, Vector3 place)
    {
        var component = new FlipbookComponent { Flipbook = Book(TestTiles.Flat(scene.Game.GraphicsDevice, B), Frame(0.1f, Cell(0))), Animation = "show", PlayOnStart = false };
        scene.Add("flipbook", component).Transform.Position = place;
        return component;
    }

    // A map of two red cells, (0, 0) and (1, 0)
    private static TileMapComponent AddMap(HarnessScene scene)
    {
        var map = new TileMapComponent { Map = TestTiles.Map(TestTiles.Flat(scene.Game.GraphicsDevice, R), ["0 0"]) };
        scene.Add("map", map);
        return map;
    }
}
