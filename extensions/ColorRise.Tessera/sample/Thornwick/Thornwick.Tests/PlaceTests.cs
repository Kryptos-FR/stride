using ColorRise.Tessera;
using Stride.Core.Mathematics;
using static Thornwick.Tests.TestMaps;

namespace Thornwick.Tests;

/// <summary>
/// A place: a map of the scene, its frame, and which of its cells a walker can step on.
/// </summary>
public sealed class PlaceTests
{
    // Review Focus 1
    [Fact]
    public void A_cell_outside_the_frame_is_not_free()
    {
        var place = Place(Room(4, 3));

        Assert.True(place.IsFree(new Int2(0, 0)) && place.InFrame(new Int2(0, 0)));
        Assert.True(place.IsFree(new Int2(3, 2)) && place.InFrame(new Int2(3, 2)));
        foreach (var cell in new[] { new Int2(-1, 0), new Int2(4, 0), new Int2(0, -1), new Int2(0, 3) })
        {
            Assert.False(place.InFrame(cell), $"{cell} in the frame");
            Assert.False(place.IsFree(cell), $"{cell} free");
        }
    }

    [Fact]
    public void A_map_without_a_frame_has_every_cell_in_its_frame()
    {
        var map = Room(4, 3);
        map.Size = Int2.Zero;
        Assert.True(Place(map).InFrame(new Int2(1000, -1000)));

        var empty = Place(null);
        Assert.True(empty.InFrame(new Int2(5, 5)));
        Assert.True(empty.IsFree(new Int2(5, 5)));
    }

    [Fact]
    public void A_solid_cell_is_not_free()
    {
        var map = Room(4, 3);
        Put(map, Wall, new Int2(1, 1));
        var place = Place(map);

        Assert.False(place.IsFree(new Int2(1, 1)));
        Assert.True(place.InFrame(new Int2(1, 1)));
    }

    [Fact]
    public void The_frame_is_the_maps_size_from_where_its_entity_is()
    {
        Assert.Equal(new RectangleF(100, 0, 12, 9), Place(Room(12, 9), at: new Vector3(100, 0, 0)).Frame);
        Assert.Equal(new RectangleF(0, 0, 12, 9), Place(Room(12, 9)).Frame);

        var large = Room(12, 9);
        large.CellSize = new Vector2(2, 2);
        Assert.Equal(new RectangleF(0, 0, 24, 18), Place(large).Frame);

        var unframed = Room(12, 9);
        unframed.Size = Int2.Zero;
        Assert.Null(Place(unframed).Frame);
        Assert.Null(Place(null).Frame);
    }

    [Fact]
    public void What_walks_is_under_the_overhead_layer_or_over_every_layer()
    {
        Assert.Equal(0.15f, WalkerZ("Ground", "Details", "Overhead"), 0.0001f);
        Assert.Equal(0.15f, WalkerZ("Ground", "Details"), 0.0001f);
        Assert.Equal(0.05f, WalkerZ("Ground"), 0.0001f);
        Assert.Equal(-0.05f, WalkerZ("Overhead", "Ground"), 0.0001f);
        Assert.Equal(0f, Place(null).WalkerZ);
    }

    [Fact]
    public void A_position_in_cells_is_a_point_of_the_world()
    {
        var place = Place(Room(12, 9), at: new Vector3(100, 0, 0));

        var point = place.ToWorld(new Vector2(2.5f, 1));

        Assert.Equal(new Vector3(102.5f, 1, place.WalkerZ), point);
    }

    // The z of what walks on a map of layers of these names, 0.1 apart
    private static float WalkerZ(params string[] layers)
    {
        var map = new TileMap();
        foreach (var name in layers)
        {
            map.Layers.Add(name);
        }
        var place = Place(map);
        place.Component.LayerSpacing = 0.1f;
        return place.WalkerZ;
    }
}
