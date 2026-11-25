using System.Collections.Specialized;
using System.Globalization;
using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Canvas;
using ColorRise.Tessera.Editor.Palette;
using ColorRise.Tessera.Editor.Pictures;
using ColorRise.Tessera.Editor.ViewModels;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Extensions;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.Collections;
using Stride.Core.Presentation.Commands;
using Stride.Core.Presentation.Services;
using Stride.Core.Serialization;
using Stride.Core.Transactions;

namespace ColorRise.Tessera.Editor.TileMapEditor;

/// <summary>
/// The editor of a map: its cells on a canvas, painted with the tools in the current layer, its layers and its tile sets.
/// </summary>
[AssetEditorViewModel<TileMapViewModel>]
public sealed class TileMapEditorViewModel : AssetEditorViewModel<TileMapViewModel>
{
    private static readonly Color SelectionColor = new(80, 170, 255, 255);

    private readonly Dictionary<TileToolKind, TileTool> tools;
    // The layers of the list, the top one first
    private readonly ObservableList<TileLayerItemViewModel> layerItems = [];
    // An item per layer the editor has seen, kept while the layer is out of the map, so that a moved layer keeps its item
    private readonly Dictionary<TileLayerAsset, TileLayerItemViewModel> knownLayers = new(ReferenceEqualityComparer.Instance);
    // The name each layer had when the editor last looked: a renamed layer takes its lock with it
    private readonly Dictionary<TileLayerAsset, string> layerNames = new(ReferenceEqualityComparer.Instance);
    // The slots of the list, by slot
    private readonly ObservableList<TileSetSlotItemViewModel> tileSetItems = [];
    // The layer of CurrentLayer, followed when the layers change
    private TileLayerAsset? currentLayerAsset;
    // The current layer went out of the map, and the current layer is the nearest one until it comes back (a move, an undo)
    private TileLayerAsset? lostLayer;
    // The editor follows a change of the layers: the current layer moves, and the selection of the list stays as the user left it
    private bool followingStructure;
    // The cells a right drag of the stamp tool is taking, shown over the map
    private Rectangle? captureCells;
    // The zoom changed since the editor opened: its setting is written to the file when the editor closes
    private bool zoomChanged;
    private bool initialized;

    /// <summary>
    /// Makes the editor of a map; <see cref="Initialize"/> opens it.
    /// </summary>
    public TileMapEditorViewModel(TileMapViewModel asset)
        : base(asset)
    {
        Document = new TileMapDocument(asset);
        Scene = new TileMapScene(Document, TilePictureService.Of(asset.Session))
        {
            DimOthers = DimOtherLayers,
            DimOpacity = Math.Clamp(TesseraEditorSettings.DimOpacity.GetValue(), 0, 1),
        };
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
        AddLayerCommand = new AnonymousCommand(ServiceProvider, AddLayer);
        RemoveLayerCommand = new AnonymousTaskCommand(ServiceProvider, RemoveLayerAsync);
        MoveLayerUpCommand = new AnonymousCommand(ServiceProvider, () => MoveLayer(1));
        MoveLayerDownCommand = new AnonymousCommand(ServiceProvider, () => MoveLayer(-1));
        AddTileSetCommand = new AnonymousTaskCommand(ServiceProvider, AddTileSetAsync);
        RemoveTileSetCommand = new AnonymousTaskCommand(ServiceProvider, RemoveTileSetAsync);
        if (TesseraEditorSettings.LastZoom.GetValue() is > 0 and var zoom)
        {
            View.Zoom = zoom;
        }
        FollowLayerNames();
        SyncLayers();
        SyncTileSets();
        CurrentLayer = Document.LayerCount - 1;
        UpdateProblems();
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
    /// The zoom and the pan of the canvas; the zoom the last map editor had at first.
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
            if (!followingStructure)
            {
                lostLayer = null;
            }
            Scene.CurrentLayer = value;
            // The map in the property grid stays there while the layers change under it
            if (!followingStructure || SelectedLayer is not null)
            {
                SelectedLayer = currentLayerAsset is null ? null : knownLayers.GetValueOrDefault(currentLayerAsset);
            }
            ShowOverlay();
            UpdateStatus();
        }
    }

    /// <summary>
    /// The layers of the map, the top one first, as the list shows them.
    /// </summary>
    public IReadOnlyObservableList<TileLayerItemViewModel> Layers => layerItems;

    /// <summary>
    /// The layer selected in the list: the current layer, which the property grid shows; null shows the map.
    /// </summary>
    public TileLayerItemViewModel? SelectedLayer
    {
        get;
        set
        {
            if (!SetValue(ref field, value))
            {
                return;
            }
            if (value is not null && Asset.Asset.Layers.IndexOf(value.Layer) is >= 0 and var index && index != CurrentLayer)
            {
                CurrentLayer = index;
            }
            ShowProperties();
        }
    }

    /// <summary>
    /// The slots of the map with their tile sets, by slot.
    /// </summary>
    public IReadOnlyObservableList<TileSetSlotItemViewModel> TileSets => tileSetItems;

    /// <summary>
    /// The slot whose palette is shown; the first one at first.
    /// </summary>
    public TileSetSlotItemViewModel? SelectedTileSet
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Whether the canvas draws the grid of the cells; kept in the editor settings.
    /// </summary>
    public bool ShowGrid
    {
        get;
        set
        {
            if (SetValue(ref field, value))
            {
                TesseraEditorSettings.ShowGrid.SetValue(value);
                TesseraEditorSettings.Save();
            }
        }
    } = TesseraEditorSettings.ShowGrid.GetValue();

    /// <summary>
    /// Whether the layers other than the current one are dimmed; kept in the editor settings.
    /// </summary>
    public bool DimOtherLayers
    {
        get;
        set
        {
            if (SetValue(ref field, value))
            {
                Scene.DimOthers = value;
                TesseraEditorSettings.DimOtherLayers.SetValue(value);
                TesseraEditorSettings.Save();
            }
        }
    } = TesseraEditorSettings.DimOtherLayers.GetValue();

    /// <summary>
    /// The cell under the pointer, such as "cell 12, 7"; empty when the pointer is not over the canvas.
    /// </summary>
    public string StatusCell
    {
        get;
        private set => SetValue(ref field, value);
    } = "";

    /// <summary>
    /// The current layer, such as "Ground" or "Ground (locked)"; "no layer" when the map has none.
    /// </summary>
    public string StatusLayer
    {
        get;
        private set => SetValue(ref field, value);
    } = "";

    /// <summary>
    /// The tile of the current layer under the pointer, with the name of its tile set, such as "Town 1:3,4"; empty when there
    /// is none.
    /// </summary>
    public string StatusTile
    {
        get;
        private set => SetValue(ref field, value);
    } = "";

    /// <summary>
    /// The zoom of the canvas, such as "300%".
    /// </summary>
    public string StatusZoom
    {
        get;
        private set => SetValue(ref field, value);
    } = "";

    /// <summary>
    /// What is wrong in the map: the checks of the build, the chunks that cannot be read, and the tile sets that are gone, by
    /// slot.
    /// </summary>
    public IReadOnlyList<string> Problems
    {
        get;
        private set => SetValue(ref field, value);
    } = [];

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

    /// <summary>
    /// Puts a new layer over the current one, and makes it current.
    /// </summary>
    public ICommandBase AddLayerCommand { get; }

    /// <summary>
    /// Removes the current layer, after a question when it has tiles; the layer under it becomes current.
    /// </summary>
    public ICommandBase RemoveLayerCommand { get; }

    /// <summary>
    /// Moves the current layer one place up, over the layer above it.
    /// </summary>
    public ICommandBase MoveLayerUpCommand { get; }

    /// <summary>
    /// Moves the current layer one place down, under the layer below it.
    /// </summary>
    public ICommandBase MoveLayerDownCommand { get; }

    /// <summary>
    /// Picks a tile set in the session, and adds it in a new slot.
    /// </summary>
    public ICommandBase AddTileSetCommand { get; }

    /// <summary>
    /// Removes the selected slot, after a question when cells use it; the cells stay.
    /// </summary>
    public ICommandBase RemoveTileSetCommand { get; }

    internal TileTool CurrentTool => tools[Tool];

    private IEditorDialogService Dialogs => ServiceProvider.Get<IEditorDialogService>();

    /// <summary>
    /// Follows the history, the layers and the tile sets of the map, and shows the current layer in the property grid.
    /// </summary>
    public override Task<bool> Initialize()
    {
        UndoRedoService.Undone += HistoryMoved;
        UndoRedoService.Redone += HistoryMoved;
        Document.StructureChanged += StructureChanged;
        Document.CellsChanged += CellsChanged;
        Session.DeletedAssetsChanged += DeletedAssetsChanged;
        View.Changed += ViewChanged;
        initialized = true;
        ShowProperties();
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public override void Destroy()
    {
        UndoRedoService.Undone -= HistoryMoved;
        UndoRedoService.Redone -= HistoryMoved;
        Document.StructureChanged -= StructureChanged;
        Document.CellsChanged -= CellsChanged;
        Session.DeletedAssetsChanged -= DeletedAssetsChanged;
        View.Changed -= ViewChanged;
        foreach (var item in tileSetItems)
        {
            DestroySlot(item);
        }
        foreach (var item in knownLayers.Values)
        {
            item.Destroy();
        }
        if (zoomChanged)
        {
            TesseraEditorSettings.SaveInternal();
        }
        Scene.Dispose();
        Document.Dispose();
        base.Destroy();
    }

    /// <summary>
    /// Adds a tile set in a new slot, as one step, and selects the slot; false, with a <see cref="Message"/>, when the map
    /// has the tile set already or has given every slot.
    /// </summary>
    public bool AddTileSet(TileSetViewModel tileSet)
    {
        ArgumentNullException.ThrowIfNull(tileSet);
        if (Asset.Asset.TileSets.Any(slot => IdOf(slot) == tileSet.Id))
        {
            Message = $"The map already has the tile set '{tileSet.Name}'.";
            return false;
        }
        if (!TileAssetChecks.IsInRange(Asset.Asset.LastSlot + 1))
        {
            Message = "The map has given every slot: no tile set can be added.";
            return false;
        }
        TileSetSlot added;
        using (var transaction = UndoRedoService.CreateTransaction())
        {
            added = TileMapGraph.AddTileSet(Asset, tileSet);
            UndoRedoService.SetName(transaction, "Add tile set");
        }
        SelectedTileSet = tileSetItems.FirstOrDefault(item => item.Slot == added.Slot) ?? SelectedTileSet;
        Message = null;
        return true;
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
    /// Locks or unlocks a layer: no tool changes the cells of a locked layer. The lock is kept in the editor settings, by the
    /// map and the name of the layer.
    /// </summary>
    internal void LockLayer(TileLayerAsset layer, bool locked)
    {
        SetLock(layer.Name, locked);
        UpdateStatus();
    }

    /// <summary>
    /// Whether a layer is locked.
    /// </summary>
    internal bool IsLayerLocked(TileLayerAsset layer) => IsLocked(layer.Name);

    /// <summary>
    /// Gives a layer a new name, as one step; an empty name, or the name of another layer, is refused with a
    /// <see cref="Message"/>.
    /// </summary>
    internal void RenameLayer(TileLayerAsset layer, string? name)
    {
        var index = Asset.Asset.Layers.IndexOf(layer);
        name = name?.Trim() ?? "";
        if (index < 0 || name == layer.Name)
        {
            return;
        }
        if (name.Length == 0)
        {
            Message = "A layer needs a name.";
            return;
        }
        if (Asset.Asset.Layers.Any(other => other != layer && other.Name == name))
        {
            Message = $"Another layer is named '{name}'.";
            return;
        }
        using (var transaction = UndoRedoService.CreateTransaction())
        {
            TileMapGraph.RenameLayer(Asset, index, name);
            UndoRedoService.SetName(transaction, "Rename layer");
        }
        Message = null;
    }

    /// <summary>
    /// Shows or hides a layer, as one step.
    /// </summary>
    internal void SetLayerVisible(TileLayerAsset layer, bool visible)
    {
        var index = Asset.Asset.Layers.IndexOf(layer);
        if (index < 0 || layer.Visible == visible)
        {
            return;
        }
        using var transaction = UndoRedoService.CreateTransaction();
        TileMapGraph.SetVisible(Asset, index, visible);
        UndoRedoService.SetName(transaction, "Layer visibility");
    }

    /// <summary>
    /// Shows a tile in the palette of its slot, and selects the slot.
    /// </summary>
    internal void ShowInPalette(TileCell cell)
    {
        if (!cell.IsEmpty && tileSetItems.FirstOrDefault(item => item.Slot == cell.Slot) is { Palette: { } palette } item)
        {
            SelectedTileSet = item;
            palette.Show(cell.Tile);
        }
    }

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
        var layer = currentLayerAsset!;
        Message = !Document.GetLayer(CurrentLayer).Visible ? $"The layer '{layer.Name}' is hidden."
            : IsLayerLocked(layer) ? $"The layer '{layer.Name}' is locked."
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
        UpdateStatus();
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

    private void ShowProperties()
    {
        if (!initialized || IsDestroyed)
        {
            return;
        }
        if (SelectedLayer is not { } item)
        {
            ShowAssetProperties();
            return;
        }
        EditorProperties.TypeDescription = "Layer";
        EditorProperties.Name = item.Name;
        EditorProperties.GenerateSelectionPropertiesAsync([item]).Forget();
    }

    // The document tells of the cells of a stroke it drops before it reads the asset's new layers: the editor's own layer
    // is the one to name then, not the asset's at the same index
    private void UpdateStatus()
    {
        var layer = currentLayerAsset;
        StatusCell = HoverCell is { } cell ? string.Create(CultureInfo.InvariantCulture, $"cell {cell.X}, {cell.Y}") : "";
        StatusLayer = layer is null ? "no layer" : IsLayerLocked(layer) ? $"{layer.Name} (locked)" : layer.Name;
        StatusTile = HoverCell is { } hover && layer is not null && Read(hover) is { IsEmpty: false } tile
            ? $"{tileSetItems.FirstOrDefault(item => item.Slot == tile.Slot)?.Name ?? $"missing (slot {tile.Slot})"} {tile.Tile}"
            : "";
        StatusZoom = (View.Zoom * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
    }

    private void UpdateProblems()
    {
        var problems = new List<string>(TileAssetChecks.Of(Asset.Asset));
        problems.AddRange(Document.Errors);
        foreach (var item in tileSetItems.Where(item => item.IsMissing))
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"The tile set of the slot {item.Slot} is missing: its cells show as missing boxes."));
        }
        if (!problems.SequenceEqual(Problems))
        {
            Problems = problems;
        }
    }

    private void AddLayer()
    {
        var names = Asset.Asset.Layers.Select(layer => layer.Name).ToHashSet(StringComparer.Ordinal);
        var number = 1;
        while (names.Contains(LayerName(number)))
        {
            number++;
        }
        var index = CurrentLayer + 1;
        CancelDrag();
        using (var transaction = UndoRedoService.CreateTransaction())
        {
            TileMapGraph.AddLayer(Asset, index, LayerName(number));
            UndoRedoService.SetName(transaction, "Add layer");
        }
        CurrentLayer = index;
        Message = null;

        static string LayerName(int number) => string.Create(CultureInfo.InvariantCulture, $"Layer {number}");
    }

    private async Task RemoveLayerAsync()
    {
        if (!HasLayer())
        {
            return;
        }
        var layer = Asset.Asset.Layers[CurrentLayer];
        var tiles = CountTiles(CurrentLayer);
        if (tiles > 0)
        {
            var question = string.Create(CultureInfo.InvariantCulture, $"Remove the layer '{layer.Name}' and its {tiles:N0} {(tiles == 1 ? "tile" : "tiles")}?");
            if (await Dialogs.MessageBoxAsync(question, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }
        }
        // The layers may have changed while the question was open
        var index = Asset.Asset.Layers.IndexOf(layer);
        if (index < 0)
        {
            return;
        }
        CancelDrag();
        using (var transaction = UndoRedoService.CreateTransaction())
        {
            TileMapGraph.RemoveLayer(Asset, index);
            UndoRedoService.SetName(transaction, "Remove layer");
        }
        SetLock(layer.Name, false);
        CurrentLayer = Document.LayerCount == 0 ? -1 : Math.Max(index - 1, 0);
    }

    // Moves the current layer by one place: up is toward the end of the asset's layers, drawn last
    private void MoveLayer(int delta)
    {
        if (!HasLayer())
        {
            return;
        }
        var (from, to) = (CurrentLayer, CurrentLayer + delta);
        if (to < 0 || to >= Document.LayerCount)
        {
            return;
        }
        CancelDrag();
        using (var transaction = UndoRedoService.CreateTransaction())
        {
            TileMapGraph.MoveLayer(Asset, from, to);
            UndoRedoService.SetName(transaction, "Move layer");
        }
        CurrentLayer = to;
    }

    private int CountTiles(int layer)
    {
        var count = 0;
        foreach (var chunk in Document.GetLayer(layer).Chunks)
        {
            foreach (var cell in chunk.Cells)
            {
                if (!cell.IsEmpty)
                {
                    count++;
                }
            }
        }
        return count;
    }

    private async Task AddTileSetAsync()
    {
        if (await Dialogs.OpenAssetPickerAsync(Session, [typeof(TileSetAsset)]) is TileSetViewModel tileSet)
        {
            AddTileSet(tileSet);
        }
    }

    private async Task RemoveTileSetAsync()
    {
        if (SelectedTileSet is not { } selected)
        {
            return;
        }
        var cells = Document.CountCells(selected.Slot);
        if (cells > 0)
        {
            var use = cells == 1 ? "1 cell uses it" : string.Create(CultureInfo.InvariantCulture, $"{cells:N0} cells use it");
            var question = $"Remove the tile set '{selected.Name}'? {use}. They stay in the map and show as missing. This cannot be put right by adding the tile set again; use Undo.";
            if (await Dialogs.MessageBoxAsync(question, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }
        }
        // The slot may have gone while the question was open
        if (!Asset.Asset.TileSets.Contains(selected.Item))
        {
            return;
        }
        using (var transaction = UndoRedoService.CreateTransaction())
        {
            TileMapGraph.RemoveTileSet(Asset, selected.Item);
            UndoRedoService.SetName(transaction, "Remove tile set");
        }
        Message = null;
    }

    // The list shows the layers of the asset, the top one first; a layer keeps its item while the editor is open
    private void SyncLayers()
    {
        var layers = Asset.Asset.Layers;
        var wanted = new List<TileLayerItemViewModel>(layers.Count);
        for (var index = layers.Count - 1; index >= 0; index--)
        {
            if (!knownLayers.TryGetValue(layers[index], out var item))
            {
                item = new TileLayerItemViewModel(this, layers[index]);
                knownLayers[layers[index]] = item;
            }
            wanted.Add(item);
        }
        foreach (var gone in layerItems.Where(item => !wanted.Contains(item)).ToList())
        {
            layerItems.Remove(gone);
        }
        for (var index = 0; index < wanted.Count; index++)
        {
            var item = wanted[index];
            var at = layerItems.IndexOf(item);
            if (at != index)
            {
                if (at >= 0)
                {
                    layerItems.RemoveAt(at);
                }
                layerItems.Insert(index, item);
            }
            item.Refresh();
        }
    }

    // A layer renamed takes its lock with it
    private void FollowLayerNames()
    {
        foreach (var layer in Asset.Asset.Layers)
        {
            if (layerNames.TryGetValue(layer, out var before) && before != layer.Name && IsLocked(before))
            {
                SetLock(before, false);
                SetLock(layer.Name, true);
            }
            layerNames[layer] = layer.Name;
        }
    }

    private bool IsLocked(string name) => TesseraEditorSettings.LockedLayers.GetValue()?.Contains(LockKey(name)) == true;

    private void SetLock(string name, bool locked)
    {
        var key = LockKey(name);
        var keys = TesseraEditorSettings.LockedLayers.GetValue() ?? [];
        if (keys.Contains(key) == locked)
        {
            return;
        }
        // A new list: the one of the settings is never changed in place
        TesseraEditorSettings.LockedLayers.SetValue(locked ? [.. keys, key] : [.. keys.Where(other => other != key)]);
        TesseraEditorSettings.SaveInternal();
    }

    private string LockKey(string name) => $"{Asset.Id}/{name}";

    // The list shows the slots of the asset by slot, the first of each number; a slot keeps its item while its tile set does
    private void SyncTileSets()
    {
        var pictures = TilePictureService.Of(Session);
        var wanted = Asset.Asset.TileSets
            .Where(slot => TileAssetChecks.IsInRange(slot.Slot))
            .DistinctBy(slot => slot.Slot)
            .OrderBy(slot => slot.Slot)
            .Select(slot => (Item: slot, Pictures: pictures.Get(slot.TileSet)))
            .ToList();
        var items = new List<TileSetSlotItemViewModel>(wanted.Count);
        foreach (var (slot, found) in wanted)
        {
            var item = tileSetItems.FirstOrDefault(existing => existing.Item == slot && existing.Slot == slot.Slot && existing.Pictures == found);
            if (item is null)
            {
                item = new TileSetSlotItemViewModel(ServiceProvider, slot, found);
                if (item.Palette is { } palette)
                {
                    palette.PickedChanged += PalettePicked;
                }
            }
            items.Add(item);
        }
        if (items.SequenceEqual(tileSetItems))
        {
            return;
        }
        var selected = SelectedTileSet?.Slot;
        foreach (var gone in tileSetItems.Where(item => !items.Contains(item)).ToList())
        {
            DestroySlot(gone);
        }
        tileSetItems.Clear();
        tileSetItems.AddRange(items);
        SelectedTileSet = items.FirstOrDefault(item => item.Slot == selected) ?? items.FirstOrDefault();
    }

    private void DestroySlot(TileSetSlotItemViewModel item)
    {
        if (item.Palette is { } palette)
        {
            palette.PickedChanged -= PalettePicked;
        }
        item.Destroy();
    }

    // A pick in a palette is the stamp, for the stamp tool
    private void PalettePicked(object? sender, EventArgs e)
    {
        if (sender is TilePaletteViewModel { Picked: { } picked })
        {
            Stamp = picked;
            Tool = TileToolKind.Stamp;
            Message = null;
        }
    }

    private static AssetId? IdOf(TileSetSlot slot)
        => slot.TileSet is { } reference ? AttachedReferenceManager.GetAttachedReference(reference)?.Id : null;

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
            if (TileAssetChecks.IsInRange(item.Slot) && IdOf(item) is { } id)
            {
                slotOf.TryAdd(id, (ushort)item.Slot);
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

    // The layers or the tile sets changed: the drag ends, the lists follow, and the current layer stays on its layer, or the
    // nearest one while it is gone
    private void StructureChanged(object? sender, EventArgs e)
    {
        CancelDrag();
        FollowLayerNames();
        SyncLayers();
        SyncTileSets();
        var layers = Asset.Asset.Layers;
        int index;
        if (lostLayer is not null && layers.IndexOf(lostLayer) is >= 0 and var back)
        {
            index = back;
            lostLayer = null;
        }
        else if (currentLayerAsset is not null && layers.IndexOf(currentLayerAsset) is >= 0 and var same)
        {
            index = same;
        }
        else
        {
            lostLayer ??= currentLayerAsset;
            index = CurrentLayer < 0 ? layers.Count - 1 : Math.Min(CurrentLayer, layers.Count - 1);
        }
        followingStructure = true;
        try
        {
            CurrentLayer = index;
        }
        finally
        {
            followingStructure = false;
        }
        UpdateProblems();
    }

    private void CellsChanged(object? sender, TileCellsChangedEventArgs e)
    {
        UpdateStatus();
        UpdateProblems();
    }

    // A tile set deleted from the session, or brought back, changes what its slots show
    private void DeletedAssetsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Dispatcher.InvokeAsync(() =>
    {
        if (!IsDestroyed)
        {
            SyncTileSets();
            UpdateProblems();
            UpdateStatus();
        }
    });

    // The zoom is kept for the next map editor
    private void ViewChanged(object? sender, EventArgs e)
    {
        if (View.Zoom != TesseraEditorSettings.LastZoom.GetValue())
        {
            TesseraEditorSettings.LastZoom.SetValue(View.Zoom);
            zoomChanged = true;
        }
        UpdateStatus();
    }
}
