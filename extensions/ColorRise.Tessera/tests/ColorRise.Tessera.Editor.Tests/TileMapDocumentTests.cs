using System.Diagnostics;
using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.TileMapEditor;
using ColorRise.Tessera.Editor.ViewModels;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.Mathematics;
using Stride.Core.Quantum;
using static ColorRise.Tessera.Editor.Tests.TestTown;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// The cells of a map in memory for its editor: read once, changed by strokes that are seen at once and written as one step,
/// and written only where they changed.
/// </summary>
public sealed class TileMapDocumentTests
{
    private const string Location = "Maps/Town";

    [Fact]
    public async Task The_document_reads_the_cells_of_the_asset()
    {
        await using var map = await OpenAsync(["0,0 1,0", ". 2,1h"]);
        var layer = map.Document.GetLayer(0);

        Assert.Equal(1, map.Document.LayerCount);
        Assert.Equal(Cell(0, 0), layer.GetCell(new Int2(0, 1)));
        Assert.Equal(Cell(1, 0), layer.GetCell(new Int2(1, 1)));
        Assert.Equal(Cell(2, 1, TileFlip.Horizontal), layer.GetCell(new Int2(1, 0)));
        Assert.Equal(TileCell.Empty, layer.GetCell(new Int2(0, 0)));
        Assert.Equal(new Rectangle(0, 0, 2, 2), map.Document.UsedBounds(0));
        Assert.Equal(3, map.Document.CountCells(1));
        Assert.Equal(0, map.Document.CountCells(2));
        Assert.Empty(map.Document.Errors);
    }

    [Fact]
    public async Task A_layer_without_a_tile_has_no_bounds()
    {
        await using var map = await OpenAsync([]);

        Assert.Null(map.Document.UsedBounds(0));
    }

    [Fact]
    public async Task A_stroke_shows_at_once_and_is_one_step_that_writes_only_its_chunks()
    {
        await using var map = await OpenAsync(Rows((0, 0, "0,0"), (48, 0, "1,0")));
        var chunks = map.Asset.Layers[0].Chunks;
        var (first, far) = (chunks.Single(chunk => chunk.Position == new Int2(0, 0)), chunks.Single(chunk => chunk.Position == new Int2(3, 0)));
        var (firstText, farText) = (first.Cells, far.Cells);
        var changes = Changes(map.Document);

        map.Document.BeginStroke(0);
        map.Document.AddStrokeCells(new Dictionary<Int2, TileCell> { [new Int2(1, 1)] = Cell(1, 0) });

        Assert.True(map.Document.IsStrokeOpen);
        Assert.Equal(Cell(1, 0), map.Document.GetLayer(0).GetCell(new Int2(1, 1)));
        Assert.Equal(["0 (0, 0)"], changes);
        Assert.Empty(map.Session.Steps);
        Assert.Same(firstText, first.Cells);

        Assert.True(map.Document.CommitStroke("Stamp"));

        Assert.False(map.Document.IsStrokeOpen);
        Assert.Equal(["Stamp"], map.Session.Steps);
        Assert.NotEqual(firstText, first.Cells);
        Assert.Equal(Cell(1, 0), CellOfAsset(map.Asset, 0, new Int2(1, 1)));
        Assert.Same(far, chunks.Single(chunk => chunk.Position == new Int2(3, 0)));
        Assert.Same(farText, far.Cells);
    }

    [Fact]
    public async Task Undo_and_redo_bring_the_cells_back_and_forth()
    {
        await using var map = await OpenAsync(["0,0"]);
        Assert.True(map.Document.Write(0, new Dictionary<Int2, TileCell> { [new Int2(2, 2)] = Cell(1, 0) }, "Stamp"));
        var changes = Changes(map.Document);

        map.Session.UndoRedo.Undo();

        Assert.Equal(TileCell.Empty, map.Document.GetLayer(0).GetCell(new Int2(2, 2)));
        Assert.Equal(Cell(0, 0), map.Document.GetLayer(0).GetCell(new Int2(0, 0)));
        Assert.Equal(["0 (0, 0)"], changes);

        map.Session.UndoRedo.Redo();

        Assert.Equal(Cell(1, 0), map.Document.GetLayer(0).GetCell(new Int2(2, 2)));
        Assert.Equal(["0 (0, 0)", "0 (0, 0)"], changes);
    }

    [Fact]
    public async Task A_stroke_that_changes_nothing_makes_no_step()
    {
        await using var map = await OpenAsync(["0,0"]);

        map.Document.BeginStroke(0);
        map.Document.AddStrokeCells(new Dictionary<Int2, TileCell> { [new Int2(0, 0)] = Cell(0, 0) });
        Assert.False(map.Document.CommitStroke("Stamp"));

        // A cell changed, then given its value back
        map.Document.BeginStroke(0);
        map.Document.AddStrokeCells(new Dictionary<Int2, TileCell> { [new Int2(0, 0)] = Cell(1, 0) });
        map.Document.AddStrokeCells(new Dictionary<Int2, TileCell> { [new Int2(0, 0)] = Cell(0, 0) });
        Assert.False(map.Document.CommitStroke("Stamp"));

        Assert.False(map.Document.IsStrokeOpen);
        Assert.False(map.Document.CommitStroke("Stamp"));
        Assert.Empty(map.Session.Steps);
    }

    [Fact]
    public async Task A_cancelled_stroke_leaves_the_layer_as_it_was()
    {
        await using var map = await OpenAsync(["0,0"]);
        var changes = Changes(map.Document);

        map.Document.BeginStroke(0);
        map.Document.AddStrokeCells(new Dictionary<Int2, TileCell> { [new Int2(0, 0)] = Cell(1, 0), [new Int2(20, 0)] = Cell(1, 0) });
        changes.Clear();
        map.Document.CancelStroke();

        var layer = map.Document.GetLayer(0);
        Assert.False(map.Document.IsStrokeOpen);
        Assert.Equal(Cell(0, 0), layer.GetCell(new Int2(0, 0)));
        Assert.Equal(TileCell.Empty, layer.GetCell(new Int2(20, 0)));
        Assert.Null(layer.GetChunk(new Int2(1, 0)));
        Assert.Equal(["0 (0, 0), (1, 0)"], changes);
        Assert.Single(map.Asset.Layers[0].Chunks);
        Assert.Empty(map.Session.Steps);
    }

    [Fact]
    public async Task Set_replaces_the_cells_of_the_stroke_and_add_keeps_them()
    {
        await using var map = await OpenAsync(["0,0"]);
        var (a, b, c) = (new Int2(0, 0), new Int2(1, 0), new Int2(2, 0));
        var layer = map.Document.GetLayer(0);

        map.Document.BeginStroke(0);
        map.Document.SetStrokeCells(new Dictionary<Int2, TileCell> { [a] = Cell(1, 0) });
        Assert.Equal(Cell(1, 0), layer.GetCell(a));
        map.Document.SetStrokeCells(new Dictionary<Int2, TileCell> { [b] = Cell(1, 0) });
        Assert.Equal(Cell(0, 0), layer.GetCell(a));
        Assert.Equal(Cell(1, 0), layer.GetCell(b));
        map.Document.AddStrokeCells(new Dictionary<Int2, TileCell> { [c] = Cell(2, 0) });
        map.Document.AddStrokeCells(new Dictionary<Int2, TileCell> { [c] = Cell(3, 0) });
        Assert.Equal(Cell(1, 0), layer.GetCell(b));
        Assert.Equal(Cell(3, 0), layer.GetCell(c));

        Assert.True(map.Document.CommitStroke("Stamp"));

        Assert.Equal(Cell(0, 0), CellOfAsset(map.Asset, 0, a));
        Assert.Equal(Cell(1, 0), CellOfAsset(map.Asset, 0, b));
        Assert.Equal(Cell(3, 0), CellOfAsset(map.Asset, 0, c));
    }

    [Fact]
    public async Task A_new_chunk_goes_to_its_place_in_the_file_and_an_empty_one_leaves()
    {
        await using var map = await OpenAsync(Rows((0, 0, "0,0"), (0, 32, "1,0")));
        var chunks = map.Asset.Layers[0].Chunks;
        Assert.Equal([new Int2(0, 2), new Int2(0, 0)], chunks.Select(chunk => chunk.Position));

        Assert.True(map.Document.Write(0, new Dictionary<Int2, TileCell> { [new Int2(20, 20)] = Cell(1, 0) }, "Stamp"));
        Assert.Equal([new Int2(0, 2), new Int2(1, 1), new Int2(0, 0)], chunks.Select(chunk => chunk.Position));

        Assert.True(map.Document.Write(0, new Dictionary<Int2, TileCell> { [new Int2(0, 32)] = TileCell.Empty }, "Erase"));
        Assert.Equal([new Int2(1, 1), new Int2(0, 0)], chunks.Select(chunk => chunk.Position));
        Assert.Equal(["Stamp", "Erase"], map.Session.Steps);

        map.Session.UndoRedo.Undo();
        Assert.Equal([new Int2(0, 2), new Int2(1, 1), new Int2(0, 0)], chunks.Select(chunk => chunk.Position));
        Assert.Equal(Cell(1, 0), map.Document.GetLayer(0).GetCell(new Int2(0, 32)));
    }

    // Review Focus 4
    [Fact]
    public async Task A_stroke_on_the_negative_side_makes_negative_chunks()
    {
        var map = await OpenAsync([]);
        var cells = new Dictionary<Int2, TileCell>
        {
            [new Int2(-1, -1)] = Cell(0, 0),
            [new Int2(-16, -17)] = Cell(1, 0),
            [new Int2(0, 0)] = Cell(2, 0),
        };
        try
        {
            Assert.True(map.Document.Write(0, cells, "Stamp"));
            Assert.Equal([new Int2(0, 0), new Int2(-1, -1), new Int2(-1, -2)], map.Asset.Layers[0].Chunks.Select(chunk => chunk.Position));
            map = await ReopenAsync(map);

            Assert.Equal([new Int2(0, 0), new Int2(-1, -1), new Int2(-1, -2)], map.Asset.Layers[0].Chunks.Select(chunk => chunk.Position));
            Assert.All(cells, cell => Assert.Equal(cell.Value, map.Document.GetLayer(0).GetCell(cell.Key)));
            Assert.Equal(3, map.Document.CountCells(1));
            Assert.Equal(new Rectangle(-16, -17, 17, 18), map.Document.UsedBounds(0));
        }
        finally
        {
            await map.DisposeAsync();
        }
    }

    // Review Focus 2
    [Fact]
    public async Task A_cell_of_a_tile_set_the_map_does_not_have_is_kept_bit_for_bit()
    {
        await using var map = await OpenAsync(Rows((3, 3, "0,0")), asset => Replace(asset, "1/1:0,0", "7/9:3,3dv"));
        Assert.Empty(map.Document.Errors);
        Assert.Equal(new TileCell(7, TileId.Parse("9:3,3"), TileFlip.Diagonal | TileFlip.Vertical), map.Document.GetLayer(0).GetCell(new Int2(3, 3)));
        var before = ChunkLines(map, 0);

        Assert.True(map.Document.Write(0, new Dictionary<Int2, TileCell> { [new Int2(5, 5)] = Cell(1, 0) }, "Stamp"));

        var after = await SavedChunkLinesAsync(map, 0);
        Assert.Equal(1, DifferentTokens(before, after));
        Assert.Contains(after, line => line.Split(' ').Contains("7/9:3,3dv"));
    }

    [Fact]
    public async Task A_cell_of_a_tile_the_set_does_not_have_is_kept_too()
    {
        await using var map = await OpenAsync(Rows((3, 3, "0,0")), asset => Replace(asset, "1/1:0,0", "1/1:9,9"));
        var before = ChunkLines(map, 0);

        Assert.True(map.Document.Write(0, new Dictionary<Int2, TileCell> { [new Int2(5, 5)] = Cell(1, 0) }, "Stamp"));

        var after = await SavedChunkLinesAsync(map, 0);
        Assert.Equal(1, DifferentTokens(before, after));
        Assert.Contains(after, line => line.Split(' ').Contains("1/1:9,9"));
    }

    // Review Focus 3
    [Fact]
    public async Task A_chunk_that_cannot_be_read_is_named_and_never_written()
    {
        await using var map = await OpenAsync(Rows((0, 0, "0,0")), asset =>
        {
            var chunk = asset.Layers[0].Chunks[0];
            var lines = chunk.Cells.Split('\n');
            lines[0] = string.Join(' ', lines[0].Split(' ')[..15]);
            chunk.Cells = string.Join('\n', lines);
        });
        var chunks = map.Asset.Layers[0].Chunks;
        var bad = chunks[0].Cells;
        Assert.False(TileChunkText.TryDecode(bad, new TileCell[256], out var reason));

        var error = Assert.Single(map.Document.Errors);
        Assert.Contains("Layer0", error);
        Assert.Contains("0, 0", error);
        Assert.Contains(reason, error);
        Assert.True(map.Document.IsUnreadable(0, new Int2(0, 0)));
        Assert.False(map.Document.IsUnreadable(0, new Int2(1, 0)));
        Assert.Equal(TileCell.Empty, map.Document.GetLayer(0).GetCell(new Int2(0, 0)));
        Assert.False(map.Document.CanWrite(0, new Int2(3, 3), Cell(1, 0)));
        Assert.False(map.Document.CanWrite(0, new Int2(3, 3), TileCell.Empty));

        map.Document.BeginStroke(0);
        map.Document.AddStrokeCells(new Dictionary<Int2, TileCell> { [new Int2(3, 3)] = Cell(1, 0), [new Int2(17, 3)] = Cell(1, 0) });
        Assert.True(map.Document.CommitStroke("Stamp"));

        Assert.Same(bad, chunks.Single(chunk => chunk.Position == new Int2(0, 0)).Cells);
        Assert.Equal(Cell(1, 0), CellOfAsset(map.Asset, 0, new Int2(17, 3)));
        Assert.Equal(TileCell.Empty, map.Document.GetLayer(0).GetCell(new Int2(3, 3)));
    }

    [Fact]
    public async Task A_conflict_marker_in_a_chunk_is_unreadable_and_the_other_chunks_load()
    {
        await using var map = await OpenAsync(Rows((0, 0, "0,0"), (16, 0, "1,0")), asset =>
        {
            var chunk = asset.Layers[0].Chunks.Single(chunk => chunk.Position == new Int2(0, 0));
            var lines = chunk.Cells.Split('\n');
            lines[2] = "<<<<<<< ours";
            chunk.Cells = string.Join('\n', lines);
        });

        Assert.Contains("merge", Assert.Single(map.Document.Errors));
        Assert.True(map.Document.IsUnreadable(0, new Int2(0, 0)));
        Assert.Equal(Cell(1, 0), map.Document.GetLayer(0).GetCell(new Int2(16, 0)));
    }

    [Fact]
    public async Task A_chunk_given_twice_keeps_the_first_and_touches_neither()
    {
        TileChunkAsset? second = null;
        await using var map = await OpenAsync(Rows((0, 0, "0,0")), asset =>
        {
            var cells = new TileCell[256];
            cells[1 * 16 + 1] = Cell(3, 0);
            second = new TileChunkAsset { Position = new Int2(0, 0), Cells = TileChunkText.Encode(cells) };
            asset.Layers[0].Chunks.Add(second);
        });
        var chunks = map.Asset.Layers[0].Chunks;
        var (firstText, secondText) = (chunks[0].Cells, chunks[1].Cells);

        Assert.Contains("twice", Assert.Single(map.Document.Errors));
        Assert.Equal(Cell(0, 0), map.Document.GetLayer(0).GetCell(new Int2(0, 0)));
        Assert.Equal(TileCell.Empty, map.Document.GetLayer(0).GetCell(new Int2(1, 1)));
        Assert.False(map.Document.CanWrite(0, new Int2(2, 2), Cell(1, 0)));

        Assert.False(map.Document.Write(0, new Dictionary<Int2, TileCell> { [new Int2(2, 2)] = Cell(1, 0), [new Int2(0, 0)] = TileCell.Empty }, "Stamp"));

        Assert.Equal(2, chunks.Count);
        Assert.Same(firstText, chunks[0].Cells);
        Assert.Same(secondText, chunks[1].Cells);
        Assert.Empty(map.Session.Steps);
    }

    [Fact]
    public async Task A_chunk_made_readable_again_by_hand_is_read()
    {
        await using var map = await OpenAsync(Rows((0, 0, "0,0")), asset => asset.Layers[0].Chunks[0].Cells = "1/1:0,0\n");
        Assert.True(map.Document.IsUnreadable(0, new Int2(0, 0)));
        var changes = Changes(map.Document);
        var cells = new TileCell[256];
        cells[5 * 16 + 5] = Cell(1, 0);

        SetCellsThroughTheGraph(map, 0, 0, TileChunkText.Encode(cells));

        Assert.Empty(map.Document.Errors);
        Assert.False(map.Document.IsUnreadable(0, new Int2(0, 0)));
        Assert.Equal(Cell(1, 0), map.Document.GetLayer(0).GetCell(new Int2(5, 5)));
        Assert.Equal(["0 (0, 0)"], changes);
    }

    // Review Focus 5
    [Fact]
    public async Task An_undo_while_a_stroke_is_open_drops_the_stroke()
    {
        await using var map = await OpenAsync(["0,0"]);
        Assert.True(map.Document.Write(0, new Dictionary<Int2, TileCell> { [new Int2(1, 1)] = Cell(1, 0) }, "Stamp"));

        map.Document.BeginStroke(0);
        map.Document.AddStrokeCells(new Dictionary<Int2, TileCell> { [new Int2(2, 2)] = Cell(2, 0) });
        map.Session.UndoRedo.Undo();

        var layer = map.Document.GetLayer(0);
        Assert.False(map.Document.IsStrokeOpen);
        Assert.Equal(TileCell.Empty, layer.GetCell(new Int2(1, 1)));
        Assert.Equal(TileCell.Empty, layer.GetCell(new Int2(2, 2)));
        Assert.Equal(Cell(0, 0), layer.GetCell(new Int2(0, 0)));
        Assert.False(map.Document.CommitStroke("Stamp"));
        Assert.Equal(["Stamp"], map.Session.Steps);
    }

    [Fact]
    public async Task A_change_from_outside_is_followed()
    {
        await using var map = await OpenAsync(Rows((0, 0, "0,0"), (16, 0, "1,0")));
        var changes = Changes(map.Document);
        var structure = 0;
        map.Document.StructureChanged += (_, _) => structure++;
        var cells = new TileCell[256];
        cells[2] = Cell(3, 1);

        SetCellsThroughTheGraph(map, 0, 0, TileChunkText.Encode(cells));

        Assert.Equal(Cell(3, 1), map.Document.GetLayer(0).GetCell(new Int2(2, 0)));
        Assert.Equal(TileCell.Empty, map.Document.GetLayer(0).GetCell(new Int2(0, 0)));
        Assert.Equal(Cell(1, 0), map.Document.GetLayer(0).GetCell(new Int2(16, 0)));
        Assert.Equal(["0 (0, 0)"], changes);
        Assert.Equal(0, structure);

        var added = new TileLayerAsset { Name = "Top" };
        added.Chunks.Add(new TileChunkAsset { Position = new Int2(-1, 0), Cells = TileChunkText.Encode(cells) });
        using (var transaction = map.Session.UndoRedo.CreateTransaction())
        {
            map.Map.PropertyGraph.RootNode[nameof(TileMapAsset.Layers)].Target!.Add(added);
            map.Session.UndoRedo.SetName(transaction, "Add layer");
        }

        Assert.Equal(1, structure);
        Assert.Equal(2, map.Document.LayerCount);
        Assert.Equal("Top", map.Document.GetLayer(1).Name);
        Assert.Equal(Cell(3, 1), map.Document.GetLayer(1).GetCell(new Int2(-14, 0)));

        using (var transaction = map.Session.UndoRedo.CreateTransaction())
        {
            LayerNode(map, 0)[nameof(TileLayerAsset.Opacity)].Update(0.5f);
            map.Session.UndoRedo.SetName(transaction, "Opacity");
        }

        Assert.Equal(2, structure);
        Assert.Equal(0.5f, map.Document.GetLayer(0).Opacity);

        map.Session.UndoRedo.Undo();
        map.Session.UndoRedo.Undo();
        Assert.Equal(1, map.Document.LayerCount);
        Assert.Equal(1f, map.Document.GetLayer(0).Opacity);
    }

    [Fact]
    public async Task A_frame_keeps_tiles_inside_and_lets_erase_work_outside()
    {
        await using var map = await OpenAsync(Rows((9, 9, "0,0")), asset => asset.Size = new Int2(4, 4));
        Assert.Equal(Cell(0, 0), map.Document.GetLayer(0).GetCell(new Int2(9, 9)));

        Assert.True(map.Document.Write(0, new Dictionary<Int2, TileCell> { [new Int2(3, 3)] = Cell(1, 0), [new Int2(4, 4)] = Cell(1, 0) }, "Stamp"));

        Assert.Equal(Cell(1, 0), CellOfAsset(map.Asset, 0, new Int2(3, 3)));
        Assert.Equal(TileCell.Empty, CellOfAsset(map.Asset, 0, new Int2(4, 4)));
        Assert.False(map.Document.CanWrite(0, new Int2(4, 4), Cell(1, 0)));
        Assert.False(map.Document.CanWrite(0, new Int2(-1, 0), Cell(1, 0)));
        Assert.True(map.Document.CanWrite(0, new Int2(9, 9), TileCell.Empty));

        Assert.True(map.Document.Write(0, new Dictionary<Int2, TileCell> { [new Int2(9, 9)] = TileCell.Empty }, "Erase"));
        Assert.Equal(TileCell.Empty, CellOfAsset(map.Asset, 0, new Int2(9, 9)));
    }

    [Fact]
    public async Task A_stroke_on_a_large_layer_writes_one_item()
    {
        await using var map = await OpenAsync([], asset =>
        {
            var cells = new TileCell[256];
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i] = Cell(i % 4, i / 4 % 2);
            }
            var text = TileChunkText.Encode(cells);
            for (var i = 0; i < 1000; i++)
            {
                asset.Layers[0].Chunks.Add(new TileChunkAsset { Position = new Int2(i % 40, -(i / 40)), Cells = text });
            }
        });
        var graphChanges = 0;
        map.Map.PropertyGraph.Changed += (_, _) => graphChanges++;
        map.Map.PropertyGraph.ItemChanged += (_, _) => graphChanges++;

        var watch = Stopwatch.StartNew();
        Assert.True(map.Document.Write(0, new Dictionary<Int2, TileCell> { [new Int2(100, -100)] = Cell(3, 1, TileFlip.Horizontal) }, "Stamp"));
        watch.Stop();

        Assert.Equal(1, graphChanges);
        Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(50), $"The stroke took {watch.Elapsed.TotalMilliseconds} ms.");
    }

    // The town's tile set, and a map of it with one layer per array, as TestTown.Map makes it; a change of the asset before
    // the session opens
    private static async DispatcherTask<OpenMap> OpenAsync(string[] layer, Action<TileMapAsset>? change = null)
    {
        var tileSet = TileSet();
        var asset = Map(tileSet, "Tiles/Town", layer);
        change?.Invoke(asset);
        return await OpenAsync(tileSet, asset);
    }

    private static async DispatcherTask<OpenMap> OpenAsync(TileSetAsset tileSet, TileMapAsset asset)
    {
        var session = await TesseraSession.OpenAsync(("Tiles/Town", tileSet), (Location, asset));
        return new OpenMap(session, new TileMapDocument(session.MapAt(Location)));
    }

    // Saves the map, closes the session, and opens the saved file in a new session
    private static async DispatcherTask<OpenMap> ReopenAsync(OpenMap map)
    {
        Assert.True(await map.Session.Session.SaveSessionAsync());
        var file = map.Session.FileOf(Location, TileMapAsset.FileExtension);
        var asset = AssetFileSerializer.Load<TileMapAsset>(file).Asset;
        var tileSet = AssetFileSerializer.Load<TileSetAsset>(map.Session.FileOf("Tiles/Town", TileSetAsset.FileExtension)).Asset;
        await map.DisposeAsync();
        return await OpenAsync(tileSet, asset);
    }

    private static void Replace(TileMapAsset asset, string token, string by)
    {
        var chunk = asset.Layers[0].Chunks[0];
        Assert.Equal(1, chunk.Cells.Split(token).Length - 1);
        chunk.Cells = chunk.Cells.Replace(token, by, StringComparison.Ordinal);
    }

    // The cell as the asset has it now
    private static TileCell CellOfAsset(TileMapAsset asset, int layer, Int2 cell)
    {
        var position = TileLayer.ChunkOf(cell);
        var chunk = asset.Layers[layer].Chunks.SingleOrDefault(chunk => chunk.Position == position);
        if (chunk is null)
        {
            return TileCell.Empty;
        }
        var cells = new TileCell[256];
        TileChunkText.Decode(chunk.Cells, cells);
        return cells[(cell.Y & 15) * 16 + (cell.X & 15)];
    }

    // The changes of cells the document tells, as "layer (x, y), (x, y)"
    private static List<string> Changes(TileMapDocument document)
    {
        var changes = new List<string>();
        document.CellsChanged += (_, e) => changes.Add($"{e.Layer} {string.Join(", ", e.Chunks.OrderBy(chunk => chunk.Y).ThenBy(chunk => chunk.X).Select(chunk => $"({chunk.X}, {chunk.Y})"))}");
        return changes;
    }

    private static IObjectNode LayerNode(OpenMap map, int layer)
        => map.Map.PropertyGraph.RootNode[nameof(TileMapAsset.Layers)].Target!.IndexedTarget(new NodeIndex(layer))!;

    // As the property grid or another editor would change the text of a chunk, in a step of its own
    private static void SetCellsThroughTheGraph(OpenMap map, int layer, int chunk, string text)
    {
        using var transaction = map.Session.UndoRedo.CreateTransaction();
        LayerNode(map, layer)[nameof(TileLayerAsset.Chunks)].Target!.IndexedTarget(new NodeIndex(chunk))![nameof(TileChunkAsset.Cells)].Update(text);
        map.Session.UndoRedo.SetName(transaction, "Cells");
    }

    // The lines of the file of the map inside the chunks of a layer, trimmed
    private static List<string> ChunkLines(OpenMap map, int layer)
    {
        var lines = File.ReadAllLines(map.Session.FileOf(Location, TileMapAsset.FileExtension));
        var start = Array.FindIndex(lines, line => line.Trim().TrimStart('-').Trim() == $"Name: Layer{layer}");
        Assert.True(start >= 0, string.Join('\n', lines));
        var chunks = Array.FindIndex(lines, start, line => line.Trim() == "Chunks:");
        Assert.True(chunks >= 0, string.Join('\n', lines));
        var indent = Indent(lines[chunks]);
        return [.. lines.Skip(chunks + 1).TakeWhile(line => line.Trim().Length == 0 || Indent(line) > indent).Select(line => line.Trim())];
    }

    private static async Task<List<string>> SavedChunkLinesAsync(OpenMap map, int layer)
    {
        Assert.True(await map.Session.Session.SaveSessionAsync());
        return ChunkLines(map, layer);
    }

    private static int Indent(string line) => line.Length - line.TrimStart().Length;

    // How many cells differ between two versions of the same lines
    private static int DifferentTokens(List<string> before, List<string> after)
    {
        Assert.Equal(before.Count, after.Count);
        return before.Zip(after).Sum(pair =>
        {
            var (old, now) = (pair.First.Split(' '), pair.Second.Split(' '));
            Assert.Equal(old.Length, now.Length);
            return old.Zip(now).Count(tokens => tokens.First != tokens.Second);
        });
    }

    private sealed class OpenMap(TesseraSession session, TileMapDocument document) : IAsyncDisposable
    {
        public TesseraSession Session { get; } = session;

        public TileMapDocument Document { get; } = document;

        public TileMapViewModel Map => Session.MapAt(Location);

        public TileMapAsset Asset => Map.Asset;

        public async ValueTask DisposeAsync()
        {
            Document.Dispose();
            await Session.DisposeAsync();
        }
    }
}
