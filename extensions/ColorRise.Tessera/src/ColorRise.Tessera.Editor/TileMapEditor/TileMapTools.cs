using ColorRise.Tessera.Editor.Canvas;
using ColorRise.Tessera.Editor.Palette;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.TileMapEditor;

/// <summary>
/// The input of the map editor's canvas: the cell under the pointer, and the presses, moves and releases of one button at a
/// time, given to the current tool.
/// </summary>
internal sealed class TileMapInput(TileMapEditorViewModel editor) : ICanvasInput
{
    // The button of the press in progress; a second button pressed meanwhile, and a release without a press, do nothing
    private CanvasButton? pressed;

    public void PointerPressed(Vector2 position, CanvasButton button, CanvasModifiers modifiers)
    {
        if (pressed is not null)
        {
            return;
        }
        pressed = button;
        var cell = editor.Scene.CellAt(position);
        editor.Hover(cell);
        editor.CurrentTool.Press(cell, button, modifiers);
    }

    public void PointerMoved(Vector2 position, CanvasModifiers modifiers)
    {
        var cell = editor.Scene.CellAt(position);
        editor.Hover(cell);
        editor.CurrentTool.Move(cell);
    }

    public void PointerReleased(Vector2 position, CanvasButton button, CanvasModifiers modifiers)
    {
        if (pressed != button)
        {
            return;
        }
        pressed = null;
        editor.CurrentTool.Release(editor.Scene.CellAt(position), button);
    }

    public void PointerLeft() => editor.Hover(null);

    public void PointerCaptureLost()
    {
        if (pressed is null)
        {
            return;
        }
        pressed = null;
        editor.CurrentTool.Cancel();
    }

    public bool KeyPressed(CanvasKey key, CanvasModifiers modifiers) => editor.KeyPressed(key, modifiers);
}

/// <summary>
/// A tool of the map editor: what a press, a move and a release of the pointer do in the current layer.
/// </summary>
internal abstract class TileTool(TileMapEditorViewModel editor)
{
    /// <summary>
    /// What the ghost shows at the cursor; null for nothing.
    /// </summary>
    public virtual TileStamp? Ghost => null;

    protected TileMapEditorViewModel Editor { get; } = editor;

    protected TileMapDocument Document => Editor.Document;

    /// <summary>
    /// A button went down over a cell.
    /// </summary>
    public virtual void Press(Int2 cell, CanvasButton button, CanvasModifiers modifiers)
    {
    }

    /// <summary>
    /// The pointer is over a cell, with or without a button down.
    /// </summary>
    public virtual void Move(Int2 cell)
    {
    }

    /// <summary>
    /// The button of the press went up over a cell.
    /// </summary>
    public virtual void Release(Int2 cell, CanvasButton button)
    {
    }

    /// <summary>
    /// Drops the drag in progress, and gives the cells of its stroke their value back; true when there was one.
    /// </summary>
    public virtual bool Cancel() => false;
}

/// <summary>
/// A tool whose drag is a stroke of the document: it shows at once and is written at the release, as one step.
/// </summary>
internal abstract class TileStrokeTool(TileMapEditorViewModel editor) : TileTool(editor)
{
    private bool drawing;

    /// <summary>
    /// Whether the stroke of the drag is still open: the document drops it on an undo, or a change of the map it did not make.
    /// </summary>
    protected bool IsOpen
    {
        get
        {
            drawing &= Document.IsStrokeOpen;
            return drawing;
        }
    }

    public override bool Cancel()
    {
        if (!drawing)
        {
            return false;
        }
        drawing = false;
        Document.CancelStroke();
        return true;
    }

    /// <summary>
    /// Opens the stroke of a drag in the current layer, when the layer can be painted.
    /// </summary>
    protected bool Begin(bool needsStamp)
    {
        if (!Editor.CanPaint(needsStamp))
        {
            return false;
        }
        Document.BeginStroke(Editor.CurrentLayer);
        drawing = true;
        return true;
    }

    /// <summary>
    /// Writes the stroke as one step.
    /// </summary>
    protected void Commit(string name)
    {
        if (IsOpen)
        {
            drawing = false;
            Document.CommitStroke(name);
        }
    }
}

/// <summary>
/// Paints the stamp under the cursor and along a drag; Shift paints along the line from the last painted cell. A right drag
/// takes the cells under it as the stamp, and a stamp of one tile shows it in the palette.
/// </summary>
internal sealed class StampTool(TileMapEditorViewModel editor) : TileStrokeTool(editor)
{
    private Int2 last;
    private Int2? lastPainted;
    private Int2? captureStart;

    public override TileStamp? Ghost => captureStart is null ? Editor.Stamp : null;

    public override void Press(Int2 cell, CanvasButton button, CanvasModifiers modifiers)
    {
        if (button == CanvasButton.Right)
        {
            if (Editor.HasLayer())
            {
                captureStart = cell;
                Editor.ShowCapture(TilePainting.Between(cell, cell));
                Editor.ShowGhost();
            }
            return;
        }
        if (Begin(needsStamp: true))
        {
            Paint((modifiers & CanvasModifiers.Shift) != 0 && lastPainted is { } from ? from : cell, cell);
        }
    }

    public override void Move(Int2 cell)
    {
        if (captureStart is { } start)
        {
            Editor.ShowCapture(TilePainting.Between(start, cell));
        }
        else if (IsOpen)
        {
            Paint(last, cell);
        }
    }

    public override void Release(Int2 cell, CanvasButton button)
    {
        if (button == CanvasButton.Right && captureStart is { } start)
        {
            EndCapture();
            var stamp = TilePainting.Capture(Editor.Read, start, cell);
            if (!stamp.IsEmpty)
            {
                Editor.Stamp = stamp;
                if (stamp.Size == Int2.One)
                {
                    Editor.ShowInPalette(stamp[0, 0]);
                }
            }
        }
        else if (button == CanvasButton.Left && IsOpen)
        {
            Paint(last, cell);
            Commit("Stamp");
        }
    }

    public override bool Cancel()
    {
        if (captureStart is not null)
        {
            EndCapture();
            return true;
        }
        return base.Cancel();
    }

    private void EndCapture()
    {
        captureStart = null;
        Editor.ShowCapture(null);
        Editor.ShowGhost();
    }

    // The stamp at each cell of the line
    private void Paint(Int2 from, Int2 to)
    {
        if (Editor.Stamp is not { } stamp)
        {
            return;
        }
        var cells = new Dictionary<Int2, TileCell>();
        foreach (var cell in TilePainting.Line(from, to))
        {
            TilePainting.Stamp(stamp, cell, cells);
        }
        Document.AddStrokeCells(cells);
        (last, lastPainted) = (to, to);
    }
}

/// <summary>
/// Fills the dragged rectangle with the stamp, repeated from its top-left cell.
/// </summary>
internal sealed class RectangleTool(TileMapEditorViewModel editor) : TileStrokeTool(editor)
{
    private Int2 start;

    public override void Press(Int2 cell, CanvasButton button, CanvasModifiers modifiers)
    {
        if (button == CanvasButton.Left && Begin(needsStamp: true))
        {
            start = cell;
            Show(cell);
        }
    }

    public override void Move(Int2 cell)
    {
        if (IsOpen)
        {
            Show(cell);
        }
    }

    public override void Release(Int2 cell, CanvasButton button)
    {
        if (IsOpen)
        {
            Show(cell);
            Commit("Rectangle");
        }
    }

    private void Show(Int2 corner)
    {
        if (Editor.Stamp is { } stamp)
        {
            Document.SetStrokeCells(TilePainting.Rectangle(stamp, start, corner));
        }
    }
}

/// <summary>
/// Fills the cells that touch the clicked one through a side and have its cell, inside the bounds of a fill.
/// </summary>
internal sealed class FillTool(TileMapEditorViewModel editor) : TileTool(editor)
{
    public override void Press(Int2 cell, CanvasButton button, CanvasModifiers modifiers)
    {
        if (button != CanvasButton.Left || !Editor.CanPaint(needsStamp: true))
        {
            return;
        }
        var layer = Editor.CurrentLayer;
        var bounds = TilePainting.FillBounds(Document.Map.Asset.Size, Document.UsedBounds(layer), cell);
        if (TilePainting.Fill(Editor.Read, cell, bounds, Editor.Stamp!) is not { } cells)
        {
            Editor.Message = "This fill would change more than 512 by 512 cells: close the area first, or fill it in parts.";
            return;
        }
        Document.Write(layer, cells, "Fill");
    }
}

/// <summary>
/// Empties the cells along a drag; with the right button, the cells of the dragged rectangle.
/// </summary>
internal sealed class EraseTool(TileMapEditorViewModel editor) : TileStrokeTool(editor)
{
    private CanvasButton button;
    private Int2 start;
    private Int2 last;

    public override void Press(Int2 cell, CanvasButton button, CanvasModifiers modifiers)
    {
        if (Begin(needsStamp: false))
        {
            (this.button, start, last) = (button, cell, cell);
            Erase(cell);
        }
    }

    public override void Move(Int2 cell)
    {
        if (IsOpen)
        {
            Erase(cell);
        }
    }

    public override void Release(Int2 cell, CanvasButton button)
    {
        if (IsOpen)
        {
            Erase(cell);
            Commit("Erase");
        }
    }

    private void Erase(Int2 cell)
    {
        if (button == CanvasButton.Right)
        {
            var bounds = TilePainting.Between(start, cell);
            var cells = new Dictionary<Int2, TileCell>();
            for (var y = bounds.Y; y < bounds.Bottom; y++)
            {
                for (var x = bounds.X; x < bounds.Right; x++)
                {
                    cells[new Int2(x, y)] = TileCell.Empty;
                }
            }
            Document.SetStrokeCells(cells);
        }
        else
        {
            Document.AddStrokeCells(TilePainting.Line(last, cell).ToDictionary(place => place, _ => TileCell.Empty));
            last = cell;
        }
    }
}

/// <summary>
/// Takes the clicked cell as the stamp, shows its tile in the palette, and goes back to the stamp tool; an empty cell does
/// nothing.
/// </summary>
internal sealed class PickTool(TileMapEditorViewModel editor) : TileTool(editor)
{
    public override void Press(Int2 cell, CanvasButton button, CanvasModifiers modifiers)
    {
        if (button != CanvasButton.Left || !Editor.HasLayer())
        {
            return;
        }
        var picked = Editor.Read(cell);
        if (!picked.IsEmpty)
        {
            Editor.Stamp = TileStamp.Single(picked);
            Editor.ShowInPalette(picked);
            Editor.Tool = TileToolKind.Stamp;
        }
    }
}

/// <summary>
/// Selects the dragged rectangle of cells; a drag that starts in the selection moves its tiles, and the selection with them.
/// </summary>
/// <remarks>
/// The tiles of a move leave their cells and replace what is where they land; the empty cells of the selection are holes,
/// which leave what is under them.
/// </remarks>
internal sealed class SelectTool(TileMapEditorViewModel editor) : TileStrokeTool(editor)
{
    private Int2 start;
    // The selection before the drag
    private Rectangle? before;
    private bool selecting;
    // The tiles a move takes, from the bottom-left cell of the selection; null when no move is open
    private TileStamp? moving;

    public override void Press(Int2 cell, CanvasButton button, CanvasModifiers modifiers)
    {
        if (button != CanvasButton.Left || !Editor.HasLayer())
        {
            return;
        }
        (start, before) = (cell, Editor.SelectedCells);
        if (before is { } selection && TilePainting.Contains(selection, cell))
        {
            if (Begin(needsStamp: false))
            {
                moving = TilePainting.Capture(Editor.Read, new Int2(selection.X, selection.Y), new Int2(selection.Right - 1, selection.Bottom - 1));
            }
            return;
        }
        selecting = true;
        Editor.SelectedCells = TilePainting.Between(cell, cell);
    }

    public override void Move(Int2 cell)
    {
        if (selecting)
        {
            Editor.SelectedCells = TilePainting.Between(start, cell);
        }
        else if (moving is not null)
        {
            if (IsOpen)
            {
                MoveTo(cell);
            }
            else
            {
                // The document dropped the stroke: the selection goes back
                moving = null;
                Editor.SelectedCells = before;
            }
        }
    }

    public override void Release(Int2 cell, CanvasButton button)
    {
        Move(cell);
        if (moving is not null)
        {
            moving = null;
            Commit("Move");
        }
        selecting = false;
    }

    public override bool Cancel()
    {
        if (!selecting && moving is null)
        {
            return false;
        }
        (selecting, moving) = (false, null);
        base.Cancel();
        Editor.SelectedCells = before;
        return true;
    }

    // Whether the document refusing a cell of the move would lose a tile there, or leave a tile that should have gone
    private bool Loses(Rectangle from, Int2 cell, TileCell value)
    {
        if (Document.CanWrite(Editor.CurrentLayer, cell, value))
        {
            return false;
        }
        return !value.IsEmpty || (TilePainting.Contains(from, cell) && !moving![cell.X - from.X, cell.Y - from.Y].IsEmpty);
    }

    // The stroke of a move by the offset from the press to the cell, and the selection where the tiles are. A move that would
    // lose a tile stays where the last one put the tiles, with a message
    private void MoveTo(Int2 cell)
    {
        var from = before!.Value;
        var offset = new Int2(cell.X - start.X, cell.Y - start.Y);
        var cells = new Dictionary<Int2, TileCell>();
        for (var y = from.Y; y < from.Bottom; y++)
        {
            for (var x = from.X; x < from.Right; x++)
            {
                cells[new Int2(x, y)] = TileCell.Empty;
            }
        }
        for (var y = 0; y < from.Height; y++)
        {
            for (var x = 0; x < from.Width; x++)
            {
                if (moving![x, y] is { IsEmpty: false } tile)
                {
                    cells[new Int2(from.X + x + offset.X, from.Y + y + offset.Y)] = tile;
                }
            }
        }
        if (cells.Any(pair => Loses(from, pair.Key, pair.Value)))
        {
            Editor.Message = "The tiles cannot go there: a cell is outside the map, or in a chunk the editor does not paint.";
            return;
        }
        Editor.Message = null;
        Document.SetStrokeCells(cells);
        Editor.SelectedCells = new Rectangle(from.X + offset.X, from.Y + offset.Y, from.Width, from.Height);
    }
}
