using System.Diagnostics;
using ColorRise.Tessera.Assets;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.Services;
using Stride.Core.Quantum;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// The chunks of a map asset are edited through the asset's graph, with undo, as the map editor of step 2 will edit them.
/// </summary>
public sealed class TileMapAssetGraphTests
{
    private const string Location = "Maps/Town";
    private const int ChunkCount = 1000;

    [Fact]
    public async Task The_text_of_a_chunk_is_replaced_through_its_node_and_undone()
    {
        await using var session = await TesseraTest.OpenAsync((Location, BigMap()));
        var asset = session.Asset<AssetViewModel>(Location);
        var undoRedo = UndoRedo(session);
        var steps = Steps(undoRedo);
        var map = (TileMapAsset)asset.Asset;
        var old = map.Layers[0].Chunks[500].Cells;
        var stamped = StampedText();

        session.OnUiThread(() =>
        {
            using var transaction = undoRedo.CreateTransaction();
            Chunks(asset).IndexedTarget(new NodeIndex(500))![nameof(TileChunkAsset.Cells)].Update(stamped);
            undoRedo.SetName(transaction, "Stamp");
        });

        Assert.Equal(stamped, map.Layers[0].Chunks[500].Cells);
        Assert.True(asset.IsDirty);
        Assert.Equal(["Stamp"], steps);
        session.OnUiThread(undoRedo.Undo);
        Assert.Equal(old, map.Layers[0].Chunks[500].Cells);
        Assert.False(asset.IsDirty);
        session.OnUiThread(undoRedo.Redo);
        Assert.Equal(stamped, map.Layers[0].Chunks[500].Cells);
    }

    [Fact]
    public async Task A_chunk_is_added_and_removed_through_the_graph_and_undone()
    {
        await using var session = await TesseraTest.OpenAsync((Location, BigMap()));
        var asset = session.Asset<AssetViewModel>(Location);
        var undoRedo = UndoRedo(session);
        var chunks = ((TileMapAsset)asset.Asset).Layers[0].Chunks;
        var added = new TileChunkAsset { Position = new Int2(100, 100), Cells = StampedText() };

        session.OnUiThread(() =>
        {
            using var transaction = undoRedo.CreateTransaction();
            Chunks(asset).Add(added);
            undoRedo.SetName(transaction, "Paint");
        });
        Assert.Equal(ChunkCount + 1, chunks.Count);
        Assert.Equal(new Int2(100, 100), chunks[^1].Position);

        session.OnUiThread(() =>
        {
            using var transaction = undoRedo.CreateTransaction();
            Chunks(asset).Remove(chunks[3], new NodeIndex(3));
            undoRedo.SetName(transaction, "Erase");
        });
        Assert.Equal(ChunkCount, chunks.Count);
        Assert.Equal(new Int2(4, 0), chunks[3].Position);

        session.OnUiThread(undoRedo.Undo);
        Assert.Equal(new Int2(3, 0), chunks[3].Position);
        session.OnUiThread(undoRedo.Undo);
        Assert.Equal(ChunkCount, chunks.Count);
        Assert.DoesNotContain(chunks, chunk => chunk.Position == new Int2(100, 100));
        Assert.False(asset.IsDirty);
    }

    [Fact]
    public async Task One_transaction_over_several_chunks_is_one_step()
    {
        await using var session = await TesseraTest.OpenAsync((Location, BigMap()));
        var asset = session.Asset<AssetViewModel>(Location);
        var undoRedo = UndoRedo(session);
        var steps = Steps(undoRedo);
        var chunks = ((TileMapAsset)asset.Asset).Layers[0].Chunks;
        var old = chunks[10].Cells;

        session.OnUiThread(() =>
        {
            using var transaction = undoRedo.CreateTransaction();
            foreach (var index in new[] { 10, 11, 50, 51 })
            {
                Chunks(asset).IndexedTarget(new NodeIndex(index))![nameof(TileChunkAsset.Cells)].Update(StampedText());
            }
            undoRedo.SetName(transaction, "Fill");
        });
        Assert.Equal(["Fill"], steps);

        session.OnUiThread(undoRedo.Undo);
        Assert.All(new[] { 10, 11, 50, 51 }, index => Assert.Equal(old, chunks[index].Cells));
    }

    [Fact]
    public async Task A_map_of_a_thousand_chunks_opens_in_under_five_seconds()
    {
        var watch = Stopwatch.StartNew();
        await using var session = await TesseraTest.OpenAsync((Location, BigMap()));
        var asset = session.Asset<AssetViewModel>(Location);
        Assert.Equal(ChunkCount, Chunks(asset).ItemReferences?.Count ?? ((ICollection<TileChunkAsset>)Chunks(asset).Retrieve()!).Count);
        watch.Stop();
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"The session and the graph took {watch.Elapsed}.");
    }

    [Fact]
    public async Task The_chunks_are_not_in_the_property_grid()
    {
        await using var session = await TesseraTest.OpenAsync((Location, BigMap(chunks: 3)));
        var asset = session.Asset<AssetViewModel>(Location);
        var properties = session.Session.AssetViewProperties;
        properties.UpdateTypeAndName([asset], _ => "Tile map", x => x.Name, "Tile maps");
        var grid = await properties.GenerateSelectionPropertiesAsync([asset]);

        Assert.NotNull(grid);
        var layers = grid.RootNode.Children.Single(child => child.Name == nameof(TileMapAsset.Layers));
        var layer = Assert.Single(layers.Children);
        Assert.Contains(layer.Children, child => child.Name == nameof(TileLayerAsset.Opacity));
        Assert.DoesNotContain(layer.Children, child => child.Name == nameof(TileLayerAsset.Chunks));
        Assert.DoesNotContain(grid.RootNode.Children, child => child.Name == nameof(TileMapAsset.LastSlot));
    }

    // A map of one layer whose chunks are full of tiles, in rows of 40
    private static TileMapAsset BigMap(int chunks = ChunkCount)
    {
        var cells = new TileCell[256];
        for (var i = 0; i < cells.Length; i++)
        {
            cells[i] = new TileCell(1, TileId.FromGrid(1, i % 16, i / 16));
        }
        var text = TileChunkText.Encode(cells);
        var layer = new TileLayerAsset { Name = "Ground" };
        for (var i = 0; i < chunks; i++)
        {
            layer.Chunks.Add(new TileChunkAsset { Position = new Int2(i % 40, -(i / 40)), Cells = text });
        }
        var asset = new TileMapAsset();
        asset.AddTileSet(new TileSet()).TileSet = null;
        asset.Layers.Add(layer);
        return asset;
    }

    private static string StampedText()
    {
        var cells = new TileCell[256];
        Array.Fill(cells, new TileCell(1, TileId.FromNumber(1, 7), TileFlip.Horizontal));
        return TileChunkText.Encode(cells);
    }

    private static IObjectNode Chunks(AssetViewModel asset)
        => asset.PropertyGraph.RootNode[nameof(TileMapAsset.Layers)].Target!.IndexedTarget(new NodeIndex(0))![nameof(TileLayerAsset.Chunks)].Target!;

    private static IUndoRedoService UndoRedo(EditorTestSession session) => session.Session.ServiceProvider.Get<IUndoRedoService>();

    // The names of the steps of the history; a transaction that changed nothing is no step
    private static List<string?> Steps(IUndoRedoService undoRedo)
    {
        var steps = new List<string?>();
        undoRedo.Done += (_, e) =>
        {
            if (e.Transaction.Operations.Count > 0)
            {
                steps.Add(undoRedo.GetName(e.Transaction));
            }
        };
        return steps;
    }
}
