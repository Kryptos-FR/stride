using System.Collections.Specialized;
using ColorRise.Tessera.Editor.Canvas;
using ColorRise.Tessera.Editor.Palette;
using ColorRise.Tessera.Editor.Pictures;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.TileMapEditor;

/// <summary>
/// A map as a canvas draws it: one picture per chunk and layer, kept until its cells change, and what the tools put over it.
/// </summary>
/// <remarks>
/// The canvas has Y down and the map Y up: the cell (0, 0) is just above the origin of the canvas. A picture is made at the
/// scale 1, 1/2, 1/4 or 1/8, the smallest that is at least the zoom, from the tiles. Used on the session's dispatcher.
/// </remarks>
public sealed class TileMapScene : ICanvasScene, IDisposable
{
    private const int Size = TileChunk.Size;
    private const int Levels = 4;
    private const float GhostOpacity = 0.6f;
    private static readonly Color UnreadableColor = new(230, 40, 40, 255);
    private static readonly Color FrameColor = new(255, 255, 255, 160);

    private readonly TileMapDocument document;
    private readonly TilePictureService pictures;
    private readonly ISessionViewModel session;
    private readonly Dictionary<PictureKey, Composed> composed = [];
    // The pictures of the tile set of each slot of the map; null for a slot whose tile set is not in the session
    private readonly Dictionary<ushort, TileSetPictures?> slots = [];
    // The chunks the document does not paint, by layer
    private readonly List<(int Layer, Int2 Chunk)> unreadable = [];
    // How many chunks the largest tile reaches into, beyond its own, to the right and up
    private Int2 reach;
    private long frameNumber;
    private TileStamp? ghost;
    private Int2 ghostOrigin;
    private TilePicture? ghostPicture;
    private bool disposed;

    /// <summary>
    /// Makes the scene of a document, whose tiles come from the pictures of the session's tile sets.
    /// </summary>
    public TileMapScene(TileMapDocument document, TilePictureService pictures)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(pictures);
        this.document = document;
        this.pictures = pictures;
        session = document.Map.Session;
        Resolve();
        FindUnreadable();
        document.CellsChanged += CellsChanged;
        document.StructureChanged += StructureChanged;
        session.DeletedAssetsChanged += DeletedAssetsChanged;
    }

    /// <inheritdoc/>
    public event EventHandler? Changed;

    /// <summary>
    /// The size of a cell in canvas units: the map's cell size times the largest pixels per unit of its tile sets (16 for a
    /// map without one), rounded, at least 1.
    /// </summary>
    public Int2 CellPixels { get; private set; }

    /// <summary>
    /// The layer the tools work on: its offset places the cells of <see cref="CellBounds"/> and <see cref="CellAt"/>, and
    /// the other layers are dimmed when <see cref="DimOthers"/> is set.
    /// </summary>
    public int CurrentLayer
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                ghostPicture = null;
                RaiseChanged();
            }
        }
    }

    /// <summary>
    /// Whether the layers other than the current one are drawn with <see cref="DimOpacity"/>.
    /// </summary>
    public bool DimOthers
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                RaiseChanged();
            }
        }
    }

    /// <summary>
    /// The opacity the other layers are multiplied by when they are dimmed; 0.35 at first.
    /// </summary>
    public float DimOpacity
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                RaiseChanged();
            }
        }
    } = 0.35f;

    /// <summary>
    /// The shapes of the tools, drawn over the map; call <see cref="Invalidate"/> after a change.
    /// </summary>
    public IList<CanvasShape> Overlay { get; } = new List<CanvasShape>();

    /// <summary>
    /// How many bytes the pictures kept may take, counting 4 per pixel; 64 MB at first. Beyond it, the pictures drawn the
    /// longest ago are dropped, but never one of the last frame.
    /// </summary>
    public long PictureBudget
    {
        get;
        set
        {
            field = value;
            Trim();
        }
    } = 64L * 1024 * 1024;

    /// <summary>
    /// How many bytes the pictures kept take.
    /// </summary>
    public long PictureBytes { get; private set; }

    /// <summary>
    /// How many pictures of chunks were made since the scene was made.
    /// </summary>
    public int ComposedCount { get; private set; }

    /// <inheritdoc/>
    /// <remarks>The map's frame; else the cells of all the layers that have a tile; else the chunk at the origin.</remarks>
    public RectangleF? Bounds
    {
        get
        {
            var (width, height) = (CellPixels.X, CellPixels.Y);
            var size = document.Map.Asset.Size;
            if (size.X > 0 && size.Y > 0)
            {
                return new RectangleF(0, -size.Y * height, size.X * width, size.Y * height);
            }
            Rectangle? used = null;
            for (var layer = 0; layer < document.LayerCount; layer++)
            {
                if (document.UsedBounds(layer) is { } bounds)
                {
                    used = used is { } known ? Rectangle.Union(known, bounds) : bounds;
                }
            }
            return used is { } cells
                ? new RectangleF(cells.X * width, -(cells.Y + cells.Height) * height, cells.Width * width, cells.Height * height)
                : new RectangleF(0, -Size * height, Size * width, Size * height);
        }
    }

    /// <inheritdoc/>
    public Vector2 GridSize => new(CellPixels.X, CellPixels.Y);

    /// <summary>
    /// Whether the tile of a cell is known to be wider than tall or taller than wide; a turn of a quarter would squash it,
    /// as the diagonal flip swaps the axes inside the tile's own rectangle.
    /// </summary>
    public bool IsNotSquare(TileCell cell)
        => slots.GetValueOrDefault(cell.Slot) is { } set && set.Find(cell.Tile) is { IsMissing: false } found && found.Region.Width != found.Region.Height;

    /// <summary>
    /// Where a cell of the current layer is on the canvas: <c>(x * w, -(y + 1) * h, w, h)</c>, moved by the layer's offset.
    /// </summary>
    public RectangleF CellBounds(Int2 cell)
    {
        var offset = CurrentOffset();
        return new RectangleF(cell.X * CellPixels.X + offset.X, -(cell.Y + 1) * CellPixels.Y + offset.Y, CellPixels.X, CellPixels.Y);
    }

    /// <summary>
    /// The cell of the current layer at a point of the canvas.
    /// </summary>
    public Int2 CellAt(Vector2 position)
    {
        var point = position - CurrentOffset();
        return new Int2((int)MathF.Floor(point.X / CellPixels.X), (int)MathF.Ceiling(-point.Y / CellPixels.Y) - 1);
    }

    /// <summary>
    /// Shows what a tool would paint, with its bottom-left cell at <paramref name="origin"/> of the current layer, at 60% of
    /// opacity; null shows nothing.
    /// </summary>
    public void SetGhost(TileStamp? stamp, Int2 origin)
    {
        if (Equals(stamp, ghost) && origin == ghostOrigin)
        {
            return;
        }
        if (!Equals(stamp, ghost))
        {
            ghostPicture = null;
        }
        (ghost, ghostOrigin) = (stamp, origin);
        RaiseChanged();
    }

    /// <summary>
    /// Asks the canvas to draw the scene again, after a change of the <see cref="Overlay"/>.
    /// </summary>
    public void Invalidate() => RaiseChanged();

    /// <inheritdoc/>
    public void Collect(RectangleF visible, float zoom, CanvasFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (disposed)
        {
            return;
        }
        frameNumber++;
        if (SlotsMoved())
        {
            Resolve();
            DropAll();
        }

        var level = LevelOf(zoom);
        for (var index = 0; index < document.LayerCount; index++)
        {
            var layer = document.GetLayer(index);
            if (!layer.Visible)
            {
                continue;
            }
            var opacity = Math.Clamp(layer.Opacity, 0, 1) * (DimOthers && index != CurrentLayer ? DimOpacity : 1);
            var offset = OffsetOf(layer);
            foreach (var chunk in ChunksSeen(layer, new RectangleF(visible.X - offset.X, visible.Y - offset.Y, visible.Width, visible.Height)))
            {
                var picture = PictureOf(index, chunk, level);
                frame.Sprites.Add(new CanvasSprite(picture, new Rectangle(0, 0, picture.Width, picture.Height), ChunkBounds(chunk, offset), opacity));
            }
        }

        if (ghost is { } stamp)
        {
            ghostPicture ??= ComposeGhost(stamp);
            var origin = CellBounds(ghostOrigin);
            var bounds = new RectangleF(origin.X, origin.Bottom - stamp.Size.Y * CellPixels.Y, stamp.Size.X * CellPixels.X, stamp.Size.Y * CellPixels.Y);
            frame.Sprites.Add(new CanvasSprite(ghostPicture, new Rectangle(0, 0, ghostPicture.Width, ghostPicture.Height), bounds, GhostOpacity));
        }

        foreach (var (layer, chunk) in unreadable)
        {
            var bounds = ChunkBounds(chunk, OffsetOf(document.GetLayer(layer)));
            if (bounds.Intersects(visible))
            {
                frame.Shapes.Add(new CanvasShape(CanvasMark.Hatch, bounds, UnreadableColor));
            }
        }
        var size = document.Map.Asset.Size;
        if (size.X > 0 && size.Y > 0)
        {
            frame.Shapes.Add(new CanvasShape(CanvasMark.Outline, new RectangleF(0, -size.Y * CellPixels.Y, size.X * CellPixels.X, size.Y * CellPixels.Y), FrameColor));
        }
        frame.Shapes.AddRange(Overlay);
        Trim();
    }

    /// <summary>
    /// Stops following the document and the session, and lets go of the pictures.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        document.CellsChanged -= CellsChanged;
        document.StructureChanged -= StructureChanged;
        session.DeletedAssetsChanged -= DeletedAssetsChanged;
        foreach (var set in slots.Values.OfType<TileSetPictures>().Distinct())
        {
            set.Changed -= TileSetChanged;
        }
        DropAll();
    }

    // The smallest scale that is at least the zoom, as a level: 0 for 1, 3 for 1/8
    private static int LevelOf(float zoom)
    {
        var level = 0;
        while (level < Levels - 1 && 1f / (1 << (level + 1)) >= zoom)
        {
            level++;
        }
        return level;
    }

    private Vector2 CurrentOffset() => (uint)CurrentLayer < (uint)document.LayerCount ? OffsetOf(document.GetLayer(CurrentLayer)) : Vector2.Zero;

    // A layer's offset in canvas units, which have Y down
    private Vector2 OffsetOf(TileLayer layer)
    {
        var cellSize = CellSize();
        return new Vector2(layer.Offset.X * CellPixels.X / cellSize.X, -layer.Offset.Y * CellPixels.Y / cellSize.Y);
    }

    private Vector2 CellSize()
    {
        var size = document.Map.Asset.CellSize;
        return new Vector2(size.X > 0 ? size.X : 1, size.Y > 0 ? size.Y : 1);
    }

    private RectangleF ChunkBounds(Int2 chunk, Vector2 offset)
    {
        var (width, height) = (Size * CellPixels.X, Size * CellPixels.Y);
        return new RectangleF(chunk.X * width + offset.X, -(chunk.Y + 1) * height + offset.Y, width, height);
    }

    // The chunks of a layer to draw in a rectangle of the canvas: those with tiles, and those a large tile reaches into
    private IEnumerable<Int2> ChunksSeen(TileLayer layer, RectangleF seen)
    {
        var (width, height) = (Size * CellPixels.X, Size * CellPixels.Y);
        var (left, right) = ((int)MathF.Floor(seen.Left / width), (int)MathF.Ceiling(seen.Right / width) - 1);
        var (bottom, top) = ((int)MathF.Ceiling(-seen.Bottom / height) - 1, (int)MathF.Ceiling(-seen.Top / height) - 1);
        var places = (long)(right - left + 1) * (top - bottom + 1);
        var around = (reach.X + 1) * (reach.Y + 1);
        if (places <= (long)layer.Chunks.Count * around)
        {
            for (var y = bottom; y <= top; y++)
            {
                for (var x = left; x <= right; x++)
                {
                    if (HasTiles(layer, new Int2(x, y)))
                    {
                        yield return new Int2(x, y);
                    }
                }
            }
            yield break;
        }

        var found = new HashSet<Int2>();
        foreach (var chunk in layer.Chunks)
        {
            for (var dy = 0; dy <= reach.Y; dy++)
            {
                for (var dx = 0; dx <= reach.X; dx++)
                {
                    var place = new Int2(chunk.Position.X + dx, chunk.Position.Y + dy);
                    if (place.X >= left && place.X <= right && place.Y >= bottom && place.Y <= top && found.Add(place))
                    {
                        yield return place;
                    }
                }
            }
        }
    }

    // Whether a chunk has tiles, or a chunk to its left or below has a tile that may reach into it
    private bool HasTiles(TileLayer layer, Int2 place)
    {
        for (var dy = 0; dy <= reach.Y; dy++)
        {
            for (var dx = 0; dx <= reach.X; dx++)
            {
                if (layer.GetChunk(new Int2(place.X - dx, place.Y - dy)) is not null)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private TilePicture PictureOf(int layer, Int2 chunk, int level)
    {
        var key = new PictureKey(layer, chunk, level);
        if (!composed.TryGetValue(key, out var entry))
        {
            entry = new Composed(Compose(document.GetLayer(layer), chunk, level));
            composed.Add(key, entry);
            PictureBytes += entry.Bytes;
            ComposedCount++;
        }
        entry.LastFrame = frameNumber;
        return entry.Picture;
    }

    // The picture of a chunk: its cells and the large tiles of the chunks to its left and below that reach into it, in the
    // order of the runtime: row by row from the bottom, each from the left
    private TilePicture Compose(TileLayer layer, Int2 chunk, int level)
    {
        var scale = 1f / (1 << level);
        var picture = new TilePicture(Math.Max(1, (int)MathF.Round(Size * CellPixels.X * scale)), Math.Max(1, (int)MathF.Round(Size * CellPixels.Y * scale)));
        var (firstRow, lastRow) = ((chunk.Y - reach.Y) * Size, chunk.Y * Size + Size - 1);
        for (var row = firstRow; row <= lastRow; row++)
        {
            for (var x = chunk.X - reach.X; x <= chunk.X; x++)
            {
                if (layer.GetChunk(new Int2(x, row >> 4)) is not { } source)
                {
                    continue;
                }
                var cells = source.Cells;
                var start = (row & (Size - 1)) * Size;
                for (var column = 0; column < Size; column++)
                {
                    var cell = cells[start + column];
                    if (!cell.IsEmpty)
                    {
                        var corner = new Vector2((x * Size + column - chunk.X * Size) * CellPixels.X, (Size - (row - chunk.Y * Size)) * CellPixels.Y);
                        DrawCell(picture, cell, corner, scale, layer.Tint);
                    }
                }
            }
        }
        picture.Touch();
        return picture;
    }

    private TilePicture ComposeGhost(TileStamp stamp)
    {
        var picture = new TilePicture(stamp.Size.X * CellPixels.X, stamp.Size.Y * CellPixels.Y);
        var tint = (uint)CurrentLayer < (uint)document.LayerCount ? document.GetLayer(CurrentLayer).Tint : Color.White;
        for (var y = 0; y < stamp.Size.Y; y++)
        {
            for (var x = 0; x < stamp.Size.X; x++)
            {
                if (!stamp[x, y].IsEmpty)
                {
                    DrawCell(picture, stamp[x, y], new Vector2(x * CellPixels.X, (stamp.Size.Y - y) * CellPixels.Y), 1, tint);
                }
            }
        }
        picture.Touch();
        return picture;
    }

    // Draws the tile of a cell from the bottom-left corner of the cell, in the pixels of the scale 1, to the right and up
    private void DrawCell(TilePicture picture, TileCell cell, Vector2 corner, float scale, Color tint)
    {
        var size = new Vector2(CellPixels.X, CellPixels.Y);
        TileSlot? tile = null;
        if (slots.GetValueOrDefault(cell.Slot) is { } set && set.TileSet.Asset.PixelsPerUnit is var unit && unit > 0
            && set.Find(cell.Tile) is { IsMissing: false } found)
        {
            tile = found;
            var cellSize = CellSize();
            var units = new Vector2(found.Region.Width / unit, found.Region.Height / unit);
            if (!Fits(units, cellSize))
            {
                size = new Vector2(found.Region.Width * CellPixels.X / (cellSize.X * unit), found.Region.Height * CellPixels.Y / (cellSize.Y * unit));
            }
        }

        var (left, right) = ((int)MathF.Round(corner.X * scale), (int)MathF.Round((corner.X + size.X) * scale));
        var (top, bottom) = ((int)MathF.Round((corner.Y - size.Y) * scale), (int)MathF.Round(corner.Y * scale));
        if (right <= 0 || bottom <= 0 || left >= picture.Width || top >= picture.Height)
        {
            return;
        }
        var destination = new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
        if (tile is { Picture: { } source })
        {
            TileComposer.Draw(picture, destination, source, tile.Region, cell.Flip, tint);
        }
        else
        {
            TileComposer.DrawMissing(picture, destination);
        }
    }

    // As the runtime: a tile whose size in units is the cell's fills the cell
    private static bool Fits(Vector2 tileSize, Vector2 cellSize)
        => MathF.Abs(tileSize.X - cellSize.X) <= cellSize.X * 1e-5f && MathF.Abs(tileSize.Y - cellSize.Y) <= cellSize.Y * 1e-5f;

    // Finds the pictures of the tile set of each slot, the size of a cell and the reach of the largest tile
    private void Resolve()
    {
        foreach (var set in slots.Values.OfType<TileSetPictures>().Distinct())
        {
            set.Changed -= TileSetChanged;
        }
        slots.Clear();
        foreach (var slot in document.Map.Asset.TileSets)
        {
            if (slot is { Slot: >= 1 and <= ushort.MaxValue } && !slots.ContainsKey((ushort)slot.Slot))
            {
                slots.Add((ushort)slot.Slot, pictures.Get(slot.TileSet));
            }
        }
        var sets = slots.Values.OfType<TileSetPictures>().Distinct().ToList();
        foreach (var set in sets)
        {
            set.Changed += TileSetChanged;
        }

        var cellSize = CellSize();
        var units = sets.Select(set => set.TileSet.Asset.PixelsPerUnit).Where(unit => unit > 0).DefaultIfEmpty(16).Max();
        CellPixels = new Int2(Math.Max(1, (int)MathF.Round(cellSize.X * units)), Math.Max(1, (int)MathF.Round(cellSize.Y * units)));

        reach = Int2.Zero;
        foreach (var set in sets.Where(set => set.TileSet.Asset.PixelsPerUnit > 0))
        {
            var unit = set.TileSet.Asset.PixelsPerUnit;
            var width = set.LargestTile.X * CellPixels.X / (cellSize.X * unit);
            var height = set.LargestTile.Y * CellPixels.Y / (cellSize.Y * unit);
            reach = new Int2(
                Math.Max(reach.X, (int)MathF.Ceiling((width - CellPixels.X) / (Size * CellPixels.X))),
                Math.Max(reach.Y, (int)MathF.Ceiling((height - CellPixels.Y) / (Size * CellPixels.Y))));
        }
    }

    // Whether a slot of the map names another tile set, or one that came or went in the session
    private bool SlotsMoved()
    {
        // A slot given twice keeps its first tile set, as in Resolve
        var seen = new HashSet<ushort>();
        foreach (var slot in document.Map.Asset.TileSets)
        {
            if (slot is not { Slot: >= 1 and <= ushort.MaxValue } || !seen.Add((ushort)slot.Slot))
            {
                continue;
            }
            if (!slots.TryGetValue((ushort)slot.Slot, out var known) || known != pictures.Get(slot.TileSet))
            {
                return true;
            }
        }
        return seen.Count != slots.Count;
    }

    private void FindUnreadable()
    {
        unreadable.Clear();
        var layers = document.Map.Asset.Layers;
        for (var index = 0; index < layers.Count && index < document.LayerCount; index++)
        {
            foreach (var place in layers[index].Chunks.Select(chunk => chunk.Position).Distinct())
            {
                if (document.IsUnreadable(index, place))
                {
                    unreadable.Add((index, place));
                }
            }
        }
    }

    private void Remove(int layer, Int2 chunk)
    {
        for (var level = 0; level < Levels; level++)
        {
            if (composed.Remove(new PictureKey(layer, chunk, level), out var entry))
            {
                PictureBytes -= entry.Bytes;
            }
        }
    }

    private void DropAll()
    {
        composed.Clear();
        PictureBytes = 0;
        ghostPicture = null;
    }

    // Drops the pictures drawn the longest ago until the cache is under its budget, keeping those of the last frame
    private void Trim()
    {
        if (PictureBytes <= PictureBudget)
        {
            return;
        }
        foreach (var (key, entry) in composed.Where(pair => pair.Value.LastFrame < frameNumber).OrderBy(pair => pair.Value.LastFrame).ToList())
        {
            composed.Remove(key);
            PictureBytes -= entry.Bytes;
            if (PictureBytes <= PictureBudget)
            {
                return;
            }
        }
    }

    private void CellsChanged(object? sender, TileCellsChangedEventArgs e)
    {
        // A large tile of a changed chunk may reach into the chunks to its right and above
        foreach (var chunk in e.Chunks)
        {
            for (var dy = 0; dy <= reach.Y; dy++)
            {
                for (var dx = 0; dx <= reach.X; dx++)
                {
                    Remove(e.Layer, new Int2(chunk.X + dx, chunk.Y + dy));
                }
            }
        }
        FindUnreadable();
        RaiseChanged();
    }

    private void StructureChanged(object? sender, EventArgs e)
    {
        Resolve();
        DropAll();
        FindUnreadable();
        RaiseChanged();
    }

    private void TileSetChanged(object? sender, EventArgs e)
    {
        Resolve();
        DropAll();
        RaiseChanged();
    }

    // A tile set of the map may be gone or back; the next frame finds out
    private void DeletedAssetsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => _ = document.Map.Dispatcher.InvokeAsync(() =>
        {
            if (!disposed)
            {
                RaiseChanged();
            }
        });

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private readonly record struct PictureKey(int Layer, Int2 Chunk, int Level);

    private sealed class Composed(TilePicture picture)
    {
        public TilePicture Picture { get; } = picture;

        public long Bytes { get; } = 4L * picture.Width * picture.Height;

        public long LastFrame { get; set; }
    }
}
