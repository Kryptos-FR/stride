using System.Runtime.CompilerServices;
using System.Text;
using ColorRise.Tessera.Assets;
using Stride.Core.Assets;
using Stride.Core.Assets.Serializers;
using Stride.Core.Yaml;
using Stride.Core.IO;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Tests.Assets;

public sealed class TileMapAssetYamlTests
{
    private static readonly Guid SmallMapId = new("6f1c2a52-6d7e-4f0b-9a37-3d1e2b9c4a10");

    [Fact]
    public void A_map_saved_loaded_and_saved_again_has_the_same_bytes()
    {
        var yaml = Write(SmallMap());
        Assert.Equal(yaml, Write(Read(yaml)));
    }

    [Fact]
    public void A_small_map_is_the_golden_file()
    {
        var golden = Path.Combine(GoldenFolder(), "small.sdtilemap");
        if (Environment.GetEnvironmentVariable("TESSERA_WRITE_GOLDEN") == "1")
        {
            File.WriteAllText(golden, Write(SmallMap()));
        }

        // Ids aside: the golden file reads as SmallMap, and writes back to itself
        var text = File.ReadAllText(golden).ReplaceLineEndings("\n");
        var read = Read(text);
        Assert.Equal(Describe(SmallMap()), Describe(read));
        Assert.Equal(text, Write(read));
    }

    [Fact]
    public void The_cells_of_a_chunk_are_a_literal_block()
        => Assert.Contains(Write(SmallMap()).Split('\n'), line => line.EndsWith("Cells: |", StringComparison.Ordinal));

    [Fact]
    public void A_change_of_one_cell_changes_one_line()
    {
        // As Game Studio edits it: a map read from its file, whose items keep the ids of the file
        var asset = Read(Write(SmallMap()));
        var before = Write(asset).Split('\n');

        var chunk = asset.Layers[0].Chunks[0];
        var cells = new TileCell[256];
        TileChunkText.Decode(chunk.Cells, cells);
        cells[7 * 16 + 7] = new TileCell(1, TileId.FromGrid(1, 5, 5), TileFlip.Vertical);
        chunk.Cells = TileChunkText.Encode(cells);
        var after = Write(asset).Split('\n');

        Assert.Equal(before.Length, after.Length);
        Assert.Equal(1, before.Zip(after).Count(pair => pair.First != pair.Second));
    }

    [Fact]
    public void Chunks_are_written_top_to_bottom_then_left_to_right()
    {
        var layer = new TileLayerAsset { Name = "Ground" };
        foreach (var position in new[] { new Int2(0, 0), new Int2(1, 1), new Int2(-1, 1), new Int2(0, -1) })
        {
            layer.Chunks.Add(new TileChunkAsset { Position = position, Cells = TileChunkText.Encode(new TileCell[256]) });
        }
        layer.SortChunks();
        Assert.Equal([new Int2(-1, 1), new Int2(1, 1), new Int2(0, 0), new Int2(0, -1)], layer.Chunks.Select(chunk => chunk.Position));
    }

    [Fact]
    public void A_map_goes_to_a_tile_map_and_back()
    {
        var asset = SmallMap();
        var set = new TileSet();
        var errors = new List<string>();

        var map = asset.ToTileMap(_ => set, errors);
        Assert.Empty(errors);
        Assert.Same(set, map.TileSets[1]);
        Assert.Equal(new TileCell(1, TileId.FromGrid(1, 3, 4), TileFlip.Diagonal | TileFlip.Horizontal), map.Layers["Details"].GetCell(new Int2(5, 6)));

        var back = TileMapAsset.FromTileMap(map);
        Assert.Same(set, back.TileSets[0].TileSet);
        back.TileSets[0].TileSet = null;
        Assert.Equal(Describe(asset), Describe(back));
    }

    [Fact]
    public void A_bad_chunk_is_reported_and_the_others_load()
    {
        var asset = SmallMap();
        var ground = asset.Layers[0];
        var bad = ground.Chunks.Single(chunk => chunk.Position == new Int2(-1, 0));
        bad.Cells = bad.Cells.Replace(". . . .", ". . x .");

        var errors = new List<string>();
        var map = asset.ToTileMap(_ => null, errors);

        var error = Assert.Single(errors);
        Assert.Contains("Ground", error);
        Assert.Contains("(-1, 0)", error);
        Assert.Contains("line", error);
        Assert.Equal(new TileCell(1, TileId.FromGrid(1, 0, 0)), map.Layers["Ground"].GetCell(new Int2(0, 0)));
        Assert.Null(map.Layers["Ground"].GetChunk(new Int2(-1, 0)));
    }

    [Fact]
    public void A_bad_chunk_throws_when_no_list_of_errors_is_given()
    {
        var asset = SmallMap();
        asset.Layers[0].Chunks[0].Cells = "x";
        Assert.Throws<FormatException>(() => asset.ToTileMap(_ => null));
    }

    [Fact]
    public void A_tile_set_added_takes_a_slot_never_given_before()
    {
        var asset = new TileMapAsset();
        Assert.Equal(1, asset.AddTileSet(new TileSet()).Slot);
        asset.TileSets.Clear();
        Assert.Equal(2, asset.AddTileSet(new TileSet()).Slot);
        Assert.Equal(2, asset.LastSlot);
    }

    [Fact]
    public void A_slot_out_of_range_is_reported()
    {
        var asset = new TileMapAsset();
        asset.TileSets.Add(new TileSetSlot { Slot = 70000 });
        var errors = new List<string>();
        var map = asset.ToTileMap(_ => new TileSet(), errors);
        Assert.Contains("70000", Assert.Single(errors));
        Assert.Empty(map.TileSets);
    }

    [Fact]
    public void A_tile_id_is_written_as_its_text()
    {
        var yaml = WriteObject(new TileAnimationFrame { Tile = TileId.FromGrid(1, 3, 4), Duration = 0.25f });
        Assert.Contains("Tile: 1:3,4", yaml);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
        var read = (TileAnimationFrame)AssetYamlSerializer.Default.Deserialize(stream, typeof(TileAnimationFrame));
        Assert.Equal(TileId.FromGrid(1, 3, 4), read.Tile);
    }

    // CellSize (1, 1), Size (0, 0), a slot 1 without a tile set, two layers "Ground" and "Details". Ground has cells in the
    // chunks (0, 0) and (-1, 0); Details has one cell with the flips dh and an opacity of 0.5.
    internal static TileMapAsset SmallMap()
    {
        var map = new TileMap();
        var set = new TileSet();
        map.SetTileSet(1, set);
        var ground = map.Layers.Add("Ground");
        ground.SetCell(new Int2(0, 0), set, TileId.FromGrid(1, 0, 0));
        ground.SetCell(new Int2(1, 0), set, TileId.FromGrid(1, 1, 0));
        ground.SetCell(new Int2(0, 15), set, TileId.FromNumber(1, 17), TileFlip.Horizontal);
        ground.SetCell(new Int2(-1, 0), set, TileId.FromGrid(1, 2, 0));
        var details = map.Layers.Add("Details");
        details.Opacity = 0.5f;
        details.SetCell(new Int2(5, 6), set, TileId.FromGrid(1, 3, 4), TileFlip.Diagonal | TileFlip.Horizontal);

        var asset = TileMapAsset.FromTileMap(map);
        asset.Id = (AssetId)SmallMapId;
        asset.TileSets[0].TileSet = null;
        return asset;
    }

    // The values of a map asset, ids aside
    private static string Describe(TileMapAsset asset)
    {
        var text = new StringBuilder();
        text.Append($"{asset.CellSize} {asset.Size} last {asset.LastSlot}\n");
        foreach (var slot in asset.TileSets)
        {
            text.Append($"slot {slot.Slot} {slot.TileSet is not null}\n");
        }
        foreach (var layer in asset.Layers)
        {
            text.Append($"layer {layer.Name} {layer.Visible} {layer.Opacity} {layer.Tint} {layer.Offset} {layer.Collision}\n");
            foreach (var chunk in layer.Chunks)
            {
                text.Append($"chunk {chunk.Position}\n{chunk.Cells}");
            }
        }
        return text.ToString();
    }

    private static string Write(TileMapAsset asset)
    {
        using var stream = new MemoryStream();
        AssetFileSerializer.Save(stream, asset, null);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string WriteObject(object value)
    {
        using var stream = new MemoryStream();
        AssetYamlSerializer.Default.Serialize(stream, value);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static TileMapAsset Read(string yaml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
        return (TileMapAsset)AssetFileSerializer.Load<Asset>(stream, new UFile("Town.sdtilemap")).Asset;
    }

    private static string GoldenFolder([CallerFilePath] string path = "") => Path.Combine(Path.GetDirectoryName(path)!, "Golden");
}
