using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.ViewModels;
using Stride.Core.Assets.Quantum;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.Services;
using Stride.Core.Quantum;
using Stride.Core.Transactions;

namespace ColorRise.Tessera.Editor.TileMapEditor;

/// <summary>
/// The cells of a map asset in memory, for one editor: each chunk is decoded once, a stroke is seen at once and written as
/// one step, and only the chunks that changed are written to the asset.
/// </summary>
/// <remarks>
/// A chunk whose text cannot be read, and a place that a layer has twice, are named in <see cref="Errors"/>: the document
/// never writes them and never removes them, and no cell of them can be painted. The document follows the changes of the
/// asset that it does not make itself, such as an undo or an edit in the property grid; such a change drops an open stroke.
/// </remarks>
public sealed class TileMapDocument : IDisposable
{
    private const int Size = TileChunk.Size;
    // The chunks whose cells all have coordinates that fit in an int, as TileMapAsset reads them
    private const int MinChunk = int.MinValue / Size;
    private const int MaxChunk = int.MaxValue / Size;

    private readonly IUndoRedoService undoRedo;
    // The cells of the chunk being read
    private readonly TileCell[] read = new TileCell[Size * Size];
    private List<LayerState> layers = [];
    private Stroke? stroke;
    // The document is writing the asset: the changes of the graph are its own
    private bool writing;
    private bool disposed;

    /// <summary>
    /// Reads the cells of a map, and follows its asset until <see cref="Dispose"/>.
    /// </summary>
    public TileMapDocument(TileMapViewModel map)
    {
        ArgumentNullException.ThrowIfNull(map);
        Map = map;
        undoRedo = map.ServiceProvider.Get<IUndoRedoService>();
        Follow(structural: true);
        map.PropertyGraph.Changed += GraphChanged;
        map.PropertyGraph.ItemChanged += GraphItemChanged;
        undoRedo.Undone += HistoryMoved;
        undoRedo.Redone += HistoryMoved;
    }

    /// <summary>
    /// Raised when cells changed: by a stroke, or by a change of the asset. The chunks are those whose cells may have changed.
    /// </summary>
    public event EventHandler<TileCellsChangedEventArgs>? CellsChanged;

    /// <summary>
    /// Raised when layers were added, removed, moved or changed, or the tile sets or the sizes of the map changed. The cells of
    /// any layer may have changed with it: no <see cref="CellsChanged"/> comes for them.
    /// </summary>
    public event EventHandler? StructureChanged;

    /// <summary>
    /// The map whose cells these are.
    /// </summary>
    public TileMapViewModel Map { get; }

    /// <summary>
    /// How many layers the map has.
    /// </summary>
    public int LayerCount => layers.Count;

    /// <summary>
    /// What cannot be read in the chunks of the map, one line per chunk: the layer, the chunk, and why.
    /// </summary>
    public IReadOnlyList<string> Errors { get; private set; } = [];

    /// <summary>
    /// Whether a stroke is open.
    /// </summary>
    public bool IsStrokeOpen => stroke is not null;

    /// <summary>
    /// The decoded cells of a layer, with the cells of an open stroke, and its looks; the index is the one of the asset.
    /// </summary>
    /// <remarks>Read it, and change it only through a stroke.</remarks>
    public TileLayer GetLayer(int layer) => LayerAt(layer).Cells;

    /// <summary>
    /// Whether the document does not paint a chunk: its text cannot be read, or the layer has its place twice.
    /// </summary>
    /// <param name="layer">The index of the layer.</param>
    /// <param name="chunk">The place of the chunk, in chunks.</param>
    public bool IsUnreadable(int layer, Int2 chunk) => (uint)layer < (uint)layers.Count && IsLocked(layers[layer], chunk);

    /// <summary>
    /// The places of the chunks of a layer that the document does not paint (see <see cref="IsUnreadable"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The map has no such layer.</exception>
    public IEnumerable<Int2> UnreadableChunks(int layer)
    {
        var state = LayerAt(layer);
        return state.Chunks.Where(pair => pair.Value.Error is not null).Select(pair => pair.Key).Union(state.Twice);
    }

    /// <summary>
    /// The smallest rectangle of cells that holds every tile of a layer; null for a layer without a tile.
    /// </summary>
    public Rectangle? UsedBounds(int layer)
    {
        var (minX, minY, maxX, maxY) = (int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
        foreach (var chunk in LayerAt(layer).Cells.Chunks)
        {
            var cells = chunk.Cells;
            for (var index = 0; index < cells.Length; index++)
            {
                if (!cells[index].IsEmpty)
                {
                    var cell = CellOf(chunk.Position, index);
                    (minX, minY) = (Math.Min(minX, cell.X), Math.Min(minY, cell.Y));
                    (maxX, maxY) = (Math.Max(maxX, cell.X), Math.Max(maxY, cell.Y));
                }
            }
        }
        return minX > maxX ? null : new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    /// <summary>
    /// How many cells of all the layers name a slot.
    /// </summary>
    public int CountCells(ushort slot)
    {
        var count = 0;
        foreach (var layer in layers)
        {
            foreach (var chunk in layer.Cells.Chunks)
            {
                foreach (var cell in chunk.Cells)
                {
                    if (cell.Slot == slot && !cell.IsEmpty)
                    {
                        count++;
                    }
                }
            }
        }
        return count;
    }

    /// <summary>
    /// Whether a stroke can give a cell a value: not in a chunk the document does not paint, and a tile only inside the frame
    /// of the map, when it has one. Emptying a cell outside the frame is allowed.
    /// </summary>
    public bool CanWrite(int layer, Int2 cell, TileCell value)
    {
        if ((uint)layer >= (uint)layers.Count || IsLocked(layers[layer], TileLayer.ChunkOf(cell)))
        {
            return false;
        }
        var size = Map.Asset.Size;
        return value.IsEmpty || size == Int2.Zero || (cell.X >= 0 && cell.Y >= 0 && cell.X < size.X && cell.Y < size.Y);
    }

    /// <summary>
    /// Opens a stroke on a layer; a stroke that is open is cancelled first.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The map has no such layer.</exception>
    public void BeginStroke(int layer)
    {
        LayerAt(layer);
        CancelStroke();
        stroke = new Stroke(layer);
    }

    /// <summary>
    /// Makes the stroke these cells and no other: the cells it had before and does not have now get their value back. The
    /// layer shows them at once; the cells that <see cref="CanWrite"/> refuses are left out.
    /// </summary>
    /// <exception cref="InvalidOperationException">No stroke is open.</exception>
    public void SetStrokeCells(IReadOnlyDictionary<Int2, TileCell> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        var current = OpenStroke();
        var layer = layers[current.Layer].Cells;
        var changed = new HashSet<Int2>();
        foreach (var (cell, first) in current.FirstValues.Where(pair => !cells.ContainsKey(pair.Key)).ToList())
        {
            Set(layer, cell, first, changed);
            current.FirstValues.Remove(cell);
        }
        Paint(current, cells, changed);
        RaiseCellsChanged(current.Layer, changed);
    }

    /// <summary>
    /// Adds cells to the stroke; a cell given again takes its last value. The layer shows them at once; the cells that
    /// <see cref="CanWrite"/> refuses are left out.
    /// </summary>
    /// <exception cref="InvalidOperationException">No stroke is open.</exception>
    public void AddStrokeCells(IReadOnlyDictionary<Int2, TileCell> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        var current = OpenStroke();
        var changed = new HashSet<Int2>();
        Paint(current, cells, changed);
        RaiseCellsChanged(current.Layer, changed);
    }

    /// <summary>
    /// Writes the chunks whose cells the stroke changed, as one step of the given name, and closes the stroke.
    /// </summary>
    /// <returns>False, with no step, when no stroke is open or no cell has another value than before the stroke.</returns>
    public bool CommitStroke(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (stroke is not { } current)
        {
            return false;
        }
        stroke = null;

        var state = layers[current.Layer];
        var places = current.FirstValues.Where(pair => state.Cells.GetCell(pair.Key) != pair.Value).Select(pair => TileLayer.ChunkOf(pair.Key)).ToHashSet();
        // The item of each place, the first one where the layer has a place twice
        var items = new Dictionary<Int2, TileChunkAsset>();
        foreach (var item in state.Asset.Chunks)
        {
            if (item is not null)
            {
                items.TryAdd(item.Position, item);
            }
        }

        var writes = new List<ChunkWrite>();
        foreach (var place in places)
        {
            if (IsLocked(state, place))
            {
                continue;
            }
            var item = items.GetValueOrDefault(place);
            var chunk = state.Cells.GetChunk(place);
            var text = chunk is null ? null : TileChunkText.Encode(chunk.Cells);
            if (item is not null ? item.Cells != text : text is not null)
            {
                writes.Add(new ChunkWrite(place, item, text));
            }
        }
        if (writes.Count == 0)
        {
            return false;
        }
        Write(current.Layer, writes, name);
        return true;
    }

    /// <summary>
    /// Gives the cells of the stroke their value back, and closes it; nothing is written.
    /// </summary>
    public void CancelStroke()
    {
        if (stroke is not { } current)
        {
            return;
        }
        stroke = null;
        var layer = layers[current.Layer].Cells;
        var changed = new HashSet<Int2>();
        foreach (var (cell, first) in current.FirstValues)
        {
            Set(layer, cell, first, changed);
        }
        RaiseCellsChanged(current.Layer, changed);
    }

    /// <summary>
    /// Gives cells of a layer their values as one step: a stroke begun, set and committed.
    /// </summary>
    /// <returns>False, with no step, when no cell has another value.</returns>
    public bool Write(int layer, IReadOnlyDictionary<Int2, TileCell> cells, string name)
    {
        BeginStroke(layer);
        SetStrokeCells(cells);
        return CommitStroke(name);
    }

    /// <summary>
    /// Stops following the asset.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        stroke = null;
        Map.PropertyGraph.Changed -= GraphChanged;
        Map.PropertyGraph.ItemChanged -= GraphItemChanged;
        undoRedo.Undone -= HistoryMoved;
        undoRedo.Redone -= HistoryMoved;
    }

    private LayerState LayerAt(int layer)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(layer);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(layer, layers.Count);
        return layers[layer];
    }

    private Stroke OpenStroke() => stroke ?? throw new InvalidOperationException("No stroke is open: call BeginStroke first.");

    private void Paint(Stroke current, IReadOnlyDictionary<Int2, TileCell> cells, HashSet<Int2> changed)
    {
        var layer = layers[current.Layer].Cells;
        foreach (var (cell, value) in cells)
        {
            if (CanWrite(current.Layer, cell, value))
            {
                current.FirstValues.TryAdd(cell, layer.GetCell(cell));
                Set(layer, cell, value, changed);
            }
        }
    }

    // Sets a cell, and adds its chunk to the changed ones when its value changed
    private static void Set(TileLayer layer, Int2 cell, TileCell value, HashSet<Int2> changed)
    {
        if (layer.GetCell(cell) != value)
        {
            layer.SetCell(cell, value);
            changed.Add(TileLayer.ChunkOf(cell));
        }
    }

    private void RaiseCellsChanged(int layer, IReadOnlyCollection<Int2> chunks)
    {
        if (chunks.Count > 0)
        {
            CellsChanged?.Invoke(this, new TileCellsChangedEventArgs(layer, chunks));
        }
    }

    // One transaction: the texts first, then the removed items, then the new ones, each at its place in the file's order
    private void Write(int layer, List<ChunkWrite> writes, string name)
    {
        var state = layers[layer];
        var list = state.Asset.Chunks;
        var node = Map.PropertyGraph.RootNode[nameof(TileMapAsset.Layers)].Target!.IndexedTarget(new NodeIndex(layer))![nameof(TileLayerAsset.Chunks)].Target!;
        writing = true;
        try
        {
            using var transaction = undoRedo.CreateTransaction();
            foreach (var write in writes.Where(write => write is { Item: not null, Text: not null }))
            {
                node.IndexedTarget(new NodeIndex(list.IndexOf(write.Item!)))![nameof(TileChunkAsset.Cells)].Update(write.Text);
            }
            foreach (var write in writes.Where(write => write is { Item: not null, Text: null }))
            {
                node.Remove(write.Item, new NodeIndex(list.IndexOf(write.Item!)));
            }
            foreach (var write in writes.Where(write => write.Item is null).OrderByDescending(write => write.Place.Y).ThenBy(write => write.Place.X))
            {
                node.Add(new TileChunkAsset { Position = write.Place, Cells = write.Text! }, new NodeIndex(PlaceOf(list, write.Place)));
            }
            undoRedo.SetName(transaction, name);
        }
        finally
        {
            writing = false;
        }

        foreach (var write in writes)
        {
            if (write.Text is null)
            {
                state.Chunks.Remove(write.Place);
            }
            else
            {
                state.Chunks[write.Place] = new ChunkState(write.Text, null);
            }
        }
    }

    // The index of a new chunk: before the first chunk that the file keeps after it, top to bottom, then left to right
    private static int PlaceOf(List<TileChunkAsset> chunks, Int2 place)
    {
        var index = chunks.FindIndex(chunk => chunk is not null && (chunk.Position.Y < place.Y || (chunk.Position.Y == place.Y && chunk.Position.X > place.X)));
        return index < 0 ? chunks.Count : index;
    }

    private void GraphChanged(object? sender, AssetMemberNodeChangeEventArgs e) => AssetChanged(e.Member.Parent.Type != typeof(TileChunkAsset));

    private void GraphItemChanged(object? sender, AssetItemNodeChangeEventArgs e) => AssetChanged(e.Collection.Type != typeof(List<TileChunkAsset>));

    // A change the document did not make: an open stroke is dropped, then the asset is read again where it changed
    private void AssetChanged(bool structural)
    {
        if (writing || disposed)
        {
            return;
        }
        CancelStroke();
        Follow(structural);
    }

    // An undo or a redo drops an open stroke, even one that did not change this map
    private void HistoryMoved(object? sender, TransactionEventArgs e) => CancelStroke();

    // Reads what changed in the asset since the document last read or wrote it
    private void Follow(bool structural)
    {
        var assets = Map.Asset.Layers;
        var layersChanged = layers.Count != assets.Count || layers.Where((layer, index) => !ReferenceEquals(layer.Asset, assets[index])).Any();
        if (layersChanged)
        {
            var known = new Dictionary<TileLayerAsset, LayerState>(ReferenceEqualityComparer.Instance);
            foreach (var layer in layers)
            {
                known.TryAdd(layer.Asset, layer);
            }
            layers = [.. assets.Select(asset => known.Remove(asset, out var layer) ? layer : new LayerState(asset))];
        }

        var changes = new List<(int Layer, List<Int2> Chunks)>();
        for (var index = 0; index < layers.Count; index++)
        {
            var layer = layers[index];
            layer.CopyLooks();
            var chunks = Read(layer);
            if (chunks.Count > 0)
            {
                changes.Add((index, chunks));
            }
        }
        Errors = [.. layers.SelectMany(layer => layer.Errors)];

        if (structural || layersChanged)
        {
            StructureChanged?.Invoke(this, EventArgs.Empty);
            return;
        }
        foreach (var (layer, chunks) in changes)
        {
            RaiseCellsChanged(layer, chunks);
        }
    }

    // Reads the chunks of a layer whose text is not the one last read or written, and forgets the places that are gone;
    // gives the places whose cells may have changed
    private List<Int2> Read(LayerState layer)
    {
        var changed = new List<Int2>();
        var seen = new HashSet<Int2>();
        layer.Twice.Clear();
        layer.Errors.Clear();
        foreach (var item in layer.Asset.Chunks)
        {
            if (item is null)
            {
                layer.Errors.Add($"Layer '{layer.Asset.Name}': the list of chunks has an empty item, which the editor leaves as it is.");
                continue;
            }
            var place = item.Position;
            var where = $"Layer '{layer.Asset.Name}', chunk ({place.X}, {place.Y})";
            if (!seen.Add(place))
            {
                layer.Twice.Add(place);
                layer.Errors.Add($"{where}: the layer has this chunk twice; the editor shows the first and paints neither.");
                continue;
            }

            var text = item.Cells ?? "";
            if (layer.Chunks.TryGetValue(place, out var known) && (ReferenceEquals(known.Text, text) || known.Text == text))
            {
                // The same text: the cells are those already read, and the next compare is by reference
                layer.Chunks[place] = known with { Text = text };
            }
            else
            {
                var error = Decode(place, text);
                layer.Chunks[place] = new ChunkState(text, error);
                Put(layer.Cells, place, error is null ? read : null);
                changed.Add(place);
            }
            if (layer.Chunks[place].Error is { } reason)
            {
                layer.Errors.Add($"{where}: {reason}");
            }
        }

        foreach (var place in layer.Chunks.Keys.Where(place => !seen.Contains(place)).ToList())
        {
            layer.Chunks.Remove(place);
            Put(layer.Cells, place, null);
            changed.Add(place);
        }
        return changed;
    }

    // Reads the text of a chunk into the buffer; gives why it cannot be read, or null
    private string? Decode(Int2 place, string text)
    {
        if (place.X is < MinChunk or > MaxChunk || place.Y is < MinChunk or > MaxChunk)
        {
            return "the chunk is out of the range of cells.";
        }
        return TileChunkText.TryDecode(text, read, out var error) ? null : error;
    }

    // Gives the cells of a chunk of the layer these values, or empties them
    private static void Put(TileLayer layer, Int2 place, TileCell[]? cells)
    {
        if (cells is null && layer.GetChunk(place) is null)
        {
            return;
        }
        for (var index = 0; index < Size * Size; index++)
        {
            layer.SetCell(CellOf(place, index), cells?[index] ?? TileCell.Empty);
        }
    }

    private static Int2 CellOf(Int2 place, int index) => new(place.X * Size + index % Size, place.Y * Size + index / Size);

    private static bool IsLocked(LayerState layer, Int2 place)
        => layer.Twice.Contains(place) || (layer.Chunks.TryGetValue(place, out var chunk) && chunk.Error is not null);

    // A layer of the asset, its decoded cells, and what the document knows of its chunks
    private sealed class LayerState(TileLayerAsset asset)
    {
        public TileLayerAsset Asset { get; } = asset;

        public TileLayer Cells { get; } = new();

        // The text of the chunk of each place, as last read or written, and why it cannot be read
        public Dictionary<Int2, ChunkState> Chunks { get; } = [];

        // The places the layer has twice: the first chunk is shown, and neither is painted
        public HashSet<Int2> Twice { get; } = [];

        public List<string> Errors { get; } = [];

        public void CopyLooks()
        {
            Cells.Name = Asset.Name;
            Cells.Visible = Asset.Visible;
            Cells.Opacity = Asset.Opacity;
            Cells.Tint = Asset.Tint;
            Cells.Offset = Asset.Offset;
            Cells.Collision = Asset.Collision;
        }
    }

    private readonly record struct ChunkState(string Text, string? Error);

    // A chunk the commit writes: its text in the item, a new item when there is none, no item when the text is null
    private readonly record struct ChunkWrite(Int2 Place, TileChunkAsset? Item, string? Text);

    private sealed class Stroke(int layer)
    {
        public int Layer { get; } = layer;

        // The value each cell the stroke touched had before it
        public Dictionary<Int2, TileCell> FirstValues { get; } = [];
    }
}
