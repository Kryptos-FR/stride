using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Canvas;
using ColorRise.Tessera.Editor.Palette;
using ColorRise.Tessera.Editor.Pictures;
using ColorRise.Tessera.Editor.ViewModels;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.Commands;
using Stride.Core.Serialization;
using Stride.Core.Transactions;

namespace ColorRise.Tessera.Editor.TileMapEditor;

/// <summary>
/// The editor of a map: its cells on a canvas, painted with the tools in the current layer.
/// </summary>
[AssetEditorViewModel<TileMapViewModel>]
public sealed class TileMapEditorViewModel : AssetEditorViewModel<TileMapViewModel>
{
    private static readonly Color SelectionColor = new(80, 170, 255, 255);

    private readonly Dictionary<TileToolKind, TileTool> tools;
    // The layers locked in this editor
    private readonly HashSet<TileLayerAsset> lockedLayers = new(ReferenceEqualityComparer.Instance);
    // The layer of CurrentLayer, followed when the layers change
    private TileLayerAsset? currentLayerAsset;
    // The cells a right drag of the stamp tool is taking, shown over the map
    private Rectangle? captureCells;

    /// <summary>
    /// Makes the editor of a map; <see cref="Initialize"/> opens it.
    /// </summary>
    public TileMapEditorViewModel(TileMapViewModel asset)
        : base(asset)
    {
        Document = new TileMapDocument(asset);
        Scene = new TileMapScene(Document, TilePictureService.Of(asset.Session));
        Input = new TileMapInput(this);
        tools = new Dictionary<TileToolKind, TileTool>
        {
            [TileToolKind.Stamp] = new StampTool(this),
            [TileToolKind.Rectangle] = new RectangleTool(this),
            [TileToolKind.Fill] = new FillTool(this),
            [TileToolKind.Erase] = new EraseTool(this),
            [TileToolKind.Pick] = new PickTool(this),
            [TileToolKind.Select] = new SelectTool(this),
        };
        CopyCommand = new AnonymousCommand(ServiceProvider, Copy);
        CutCommand = new AnonymousCommand(ServiceProvider, Cut);
        PasteCommand = new AnonymousCommand(ServiceProvider, Paste);
        DeleteCommand = new AnonymousCommand(ServiceProvider, Delete);
        CurrentLayer = Document.LayerCount - 1;
    }

    /// <summary>
    /// The cells of the map, as the editor paints them.
    /// </summary>
    public TileMapDocument Document { get; }

    /// <summary>
    /// The map on the canvas.
    /// </summary>
    public TileMapScene Scene { get; }

    /// <summary>
    /// What the canvas tells about the pointer and the keys; it goes to the current tool.
    /// </summary>
    public ICanvasInput Input { get; }

    /// <summary>
    /// The zoom and the pan of the canvas.
    /// </summary>
    public CanvasView View { get; } = new();

    /// <summary>
    /// The tool the pointer uses; the stamp at first. Choosing another drops the drag in progress.
    /// </summary>
    public TileToolKind Tool
    {
        get;
        set
        {
            if (field != value)
            {
                tools[field].Cancel();
                SetValue(ref field, value);
                ShowGhost();
            }
        }
    }

    /// <summary>
    /// The cells the stamp, rectangle and fill tools paint; null before a tile is picked.
    /// </summary>
    public TileStamp? Stamp
    {
        get;
        set
        {
            if (SetValue(ref field, value))
            {
                ShowGhost();
            }
        }
    }

    /// <summary>
    /// The index, in the asset's layers, of the layer the tools work on; -1 when the map has no layer. The top layer at first,
    /// and the same layer when the layers change.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The map has no such layer.</exception>
    public int CurrentLayer
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, -1);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, Math.Max(Document.LayerCount, 0));
            if (field != value)
            {
                CancelDrag();
                SetValue(ref field, value);
            }
            currentLayerAsset = value >= 0 ? Asset.Asset.Layers[value] : null;
            Scene.CurrentLayer = value;
            ShowOverlay();
        }
    }

    /// <summary>
    /// The cells the select tool selected, with Y up; null when none are.
    /// </summary>
    public Rectangle? SelectedCells
    {
        get;
        internal set
        {
            if (SetValue(ref field, value))
            {
                ShowOverlay();
            }
        }
    }

    /// <summary>
    /// The cell of the current layer under the pointer; null when the pointer is not over the canvas.
    /// </summary>
    public Int2? HoverCell
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// Why the last press or command did nothing; null when it did something.
    /// </summary>
    public string? Message
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// Copies the selected cells of the current layer to the session's clipboard.
    /// </summary>
    public ICommandBase CopyCommand { get; }

    /// <summary>
    /// Copies the selected cells, then empties them, as one step.
    /// </summary>
    public ICommandBase CutCommand { get; }

    /// <summary>
    /// Makes the copied cells the stamp, and chooses the stamp tool.
    /// </summary>
    public ICommandBase PasteCommand { get; }

    /// <summary>
    /// Empties the selected cells, as one step.
    /// </summary>
    public ICommandBase DeleteCommand { get; }

    internal TileTool CurrentTool => tools[Tool];

    /// <summary>
    /// Follows the history and the layers of the map.
    /// </summary>
    public override Task<bool> Initialize()
    {
        UndoRedoService.Undone += HistoryMoved;
        UndoRedoService.Redone += HistoryMoved;
        Document.StructureChanged += StructureChanged;
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public override void Destroy()
    {
        UndoRedoService.Undone -= HistoryMoved;
        UndoRedoService.Redone -= HistoryMoved;
        Document.StructureChanged -= StructureChanged;
        Scene.Dispose();
        Document.Dispose();
        base.Destroy();
    }

    /// <summary>
    /// Mirrors the stamp left to right.
    /// </summary>
    public void FlipStampX() => Stamp = Stamp?.FlipX();

    /// <summary>
    /// Mirrors the stamp top to bottom.
    /// </summary>
    public void FlipStampY() => Stamp = Stamp?.FlipY();

    /// <summary>
    /// Turns the stamp a quarter, clockwise or not.
    /// </summary>
    public void RotateStamp(bool clockwise) => Stamp = clockwise ? Stamp?.RotateClockwise() : Stamp?.RotateCounterClockwise();

    /// <summary>
    /// Locks or unlocks a layer in this editor: no tool changes the cells of a locked layer.
    /// </summary>
    internal void LockLayer(int layer, bool locked)
    {
        var asset = Asset.Asset.Layers[layer];
        if (locked)
        {
            lockedLayers.Add(asset);
        }
        else
        {
            lockedLayers.Remove(asset);
        }
    }

    /// <summary>
    /// Whether a layer is locked in this editor.
    /// </summary>
    internal bool IsLayerLocked(int layer) => lockedLayers.Contains(Asset.Asset.Layers[layer]);

    /// <summary>
    /// Whether the map has a layer to work on; <see cref="Message"/> says why not.
    /// </summary>
    internal bool HasLayer()
    {
        if (CurrentLayer < 0)
        {
            Message = "The map has no layer. Add one.";
            return false;
        }
        Message = null;
        return true;
    }

    /// <summary>
    /// Whether a tool can change the cells of the current layer, with the stamp when it needs one; <see cref="Message"/> says
    /// why not.
    /// </summary>
    internal bool CanPaint(bool needsStamp)
    {
        if (!HasLayer())
        {
            return false;
        }
        var name = Asset.Asset.Layers[CurrentLayer].Name;
        Message = !Document.GetLayer(CurrentLayer).Visible ? $"The layer '{name}' is hidden."
            : IsLayerLocked(CurrentLayer) ? $"The layer '{name}' is locked."
            : needsStamp && Stamp is not { IsEmpty: false } ? "Pick a tile in the palette first."
            : null;
        return Message is null;
    }

    /// <summary>
    /// The cell of the current layer at a point of the canvas.
    /// </summary>
    internal TileCell Read(Int2 cell) => Document.GetLayer(CurrentLayer).GetCell(cell);

    /// <summary>
    /// Sets the cell under the pointer, and moves the ghost with it.
    /// </summary>
    internal void Hover(Int2? cell)
    {
        HoverCell = cell;
        ShowGhost();
    }

    /// <summary>
    /// Shows the ghost of the current tool at the cell under the pointer, or hides it.
    /// </summary>
    internal void ShowGhost()
    {
        if (HoverCell is { } cell && CurrentTool.Ghost is { } stamp)
        {
            Scene.SetGhost(stamp, TilePainting.StampOrigin(stamp, cell));
        }
        else
        {
            Scene.SetGhost(null, default);
        }
    }

    /// <summary>
    /// Shows the cells a right drag takes over the map; null shows none.
    /// </summary>
    internal void ShowCapture(Rectangle? cells)
    {
        captureCells = cells;
        ShowOverlay();
    }

    /// <summary>
    /// What a key does: choose a tool, flip or turn the stamp, empty the selection, or cancel. With Control or Alt, a key is
    /// a shortcut of Game Studio, and the editor leaves it.
    /// </summary>
    internal bool KeyPressed(CanvasKey key, CanvasModifiers modifiers)
    {
        if ((modifiers & (CanvasModifiers.Control | CanvasModifiers.Alt)) != 0)
        {
            return false;
        }
        switch (key)
        {
            case CanvasKey.B:
                Tool = TileToolKind.Stamp;
                return true;
            case CanvasKey.P:
                Tool = TileToolKind.Rectangle;
                return true;
            case CanvasKey.F:
                Tool = TileToolKind.Fill;
                return true;
            case CanvasKey.E:
                Tool = TileToolKind.Erase;
                return true;
            case CanvasKey.I:
                Tool = TileToolKind.Pick;
                return true;
            case CanvasKey.R:
                Tool = TileToolKind.Select;
                return true;
            case CanvasKey.X:
                FlipStampX();
                return Stamp is not null;
            case CanvasKey.Y:
                FlipStampY();
                return Stamp is not null;
            case CanvasKey.Z:
                RotateStamp(clockwise: (modifiers & CanvasModifiers.Shift) == 0);
                return Stamp is not null;
            case CanvasKey.Delete:
                var selected = SelectedCells is not null;
                DeleteCommand.Execute();
                return selected;
            case CanvasKey.Escape:
                if (CancelDrag())
                {
                    return true;
                }
                var cleared = SelectedCells is not null;
                SelectedCells = null;
                return cleared;
            default:
                return false;
        }
    }

    // Drops the drag of the current tool; an open stroke gives its cells their value back
    private bool CancelDrag() => CurrentTool.Cancel();

    private void ShowOverlay()
    {
        Scene.Overlay.Clear();
        foreach (var cells in (ReadOnlySpan<Rectangle?>)[SelectedCells, captureCells])
        {
            if (cells is { } rectangle)
            {
                Scene.Overlay.Add(new CanvasShape(CanvasMark.Outline, BoundsOf(rectangle), SelectionColor));
            }
        }
        Scene.Invalidate();
    }

    // A rectangle of cells of the current layer on the canvas
    private RectangleF BoundsOf(Rectangle cells)
    {
        var bottomLeft = Scene.CellBounds(new Int2(cells.X, cells.Y));
        var (width, height) = (cells.Width * Scene.CellPixels.X, cells.Height * Scene.CellPixels.Y);
        return new RectangleF(bottomLeft.X, bottomLeft.Bottom - height, width, height);
    }

    private void Copy()
    {
        if (SelectedCells is not { } selection || CurrentLayer < 0)
        {
            return;
        }
        var cells = TilePainting.Capture(Read, new Int2(selection.X, selection.Y), new Int2(selection.Right - 1, selection.Bottom - 1));
        var tileSets = new Dictionary<ushort, CopiedTileSet>();
        foreach (var slot in SlotsOf(cells))
        {
            if (Asset.Asset.TileSets.FirstOrDefault(item => item.Slot == slot)?.TileSet is { } reference
                && AttachedReferenceManager.GetAttachedReference(reference) is { } attached)
            {
                var name = Session.GetAssetById(attached.Id)?.Name ?? new UFile(attached.Url).GetFileNameWithoutExtension() ?? "";
                tileSets[slot] = new CopiedTileSet(attached.Id, name);
            }
        }
        TileClipboard.Of(Session).Copy(cells, tileSets);
    }

    private void Cut()
    {
        if (SelectedCells is null || !CanPaint(needsStamp: false))
        {
            return;
        }
        Copy();
        Clear("Cut");
    }

    private void Delete()
    {
        if (SelectedCells is null || !CanPaint(needsStamp: false))
        {
            return;
        }
        Clear("Delete");
    }

    // Empties the selected cells, as one step
    private void Clear(string name)
    {
        CancelDrag();
        var selection = SelectedCells!.Value;
        var cells = new Dictionary<Int2, TileCell>();
        for (var y = selection.Y; y < selection.Bottom; y++)
        {
            for (var x = selection.X; x < selection.Right; x++)
            {
                cells[new Int2(x, y)] = TileCell.Empty;
            }
        }
        Document.Write(CurrentLayer, cells, name);
    }

    // The copied cells as the stamp, each slot changed to the slot of this map that has the same tile set
    private void Paste()
    {
        var clipboard = TileClipboard.Of(Session);
        if (clipboard.Cells is not { } copied)
        {
            return;
        }
        var slotOf = new Dictionary<AssetId, ushort>();
        foreach (var item in Asset.Asset.TileSets)
        {
            if (item is { Slot: >= 1 and <= ushort.MaxValue, TileSet: { } reference } && AttachedReferenceManager.GetAttachedReference(reference) is { } attached)
            {
                slotOf.TryAdd(attached.Id, (ushort)item.Slot);
            }
        }
        var slots = new Dictionary<ushort, ushort>();
        foreach (var slot in SlotsOf(copied))
        {
            var known = clipboard.TileSets.TryGetValue(slot, out var tileSet);
            if (!known || !slotOf.TryGetValue(tileSet.Id, out var target))
            {
                Message = $"This map does not have the tile set '{(known ? tileSet.Name : $"slot {slot}")}'. Add it, then paste again.";
                return;
            }
            slots[slot] = target;
        }

        var cells = new TileCell[copied.Size.X * copied.Size.Y];
        for (var y = 0; y < copied.Size.Y; y++)
        {
            for (var x = 0; x < copied.Size.X; x++)
            {
                var cell = copied[x, y];
                cells[y * copied.Size.X + x] = cell.IsEmpty ? cell : new TileCell(slots[cell.Slot], cell.Tile, cell.Flip);
            }
        }
        Message = null;
        Stamp = new TileStamp(copied.Size, cells);
        Tool = TileToolKind.Stamp;
    }

    // The slots the cells of a stamp name, in their order
    private static IEnumerable<ushort> SlotsOf(TileStamp stamp)
    {
        var seen = new HashSet<ushort>();
        for (var y = 0; y < stamp.Size.Y; y++)
        {
            for (var x = 0; x < stamp.Size.X; x++)
            {
                if (stamp[x, y] is { IsEmpty: false } cell && seen.Add(cell.Slot))
                {
                    yield return cell.Slot;
                }
            }
        }
    }

    // An undo or a redo drops the drag in progress, as the document drops its stroke
    private void HistoryMoved(object? sender, TransactionEventArgs e) => CancelDrag();

    // The layers changed: the drag ends, and the current layer stays on its layer, or the nearest one when it is gone
    private void StructureChanged(object? sender, EventArgs e)
    {
        CancelDrag();
        var layers = Asset.Asset.Layers;
        var index = currentLayerAsset is null ? -1 : layers.IndexOf(currentLayerAsset);
        if (index < 0)
        {
            index = CurrentLayer < 0 ? layers.Count - 1 : Math.Min(CurrentLayer, layers.Count - 1);
        }
        CurrentLayer = index;
    }
}
