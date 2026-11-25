using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Canvas;
using ColorRise.Tessera.Editor.Pictures;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.Palette;

/// <summary>
/// How a click or a drag over a source picks its tiles.
/// </summary>
public enum TilePickMode
{
    /// <summary>
    /// A click selects the tile under it.
    /// </summary>
    One,

    /// <summary>
    /// A drag selects the tiles of the rectangle of grid positions between the press and the release; a click, a block of one.
    /// </summary>
    Block,

    /// <summary>
    /// A click selects one tile, Control and a click adds or removes one, a drag selects a rectangle, a click on no tile clears.
    /// </summary>
    Many,

    /// <summary>
    /// Each tile the pointer passes while the left button is down is painted, once per drag.
    /// </summary>
    Paint,
}

/// <summary>
/// One source of a tile set on a canvas: its picture, an outline per tile, its marks and its selection.
/// </summary>
public sealed class TileSourceScene : ICanvasScene, ICanvasInput
{
    private static readonly Color TileOutline = new(255, 255, 255, 40);
    private static readonly Color MissingCross = new(230, 50, 50, 255);
    private static readonly Color MarkColor = new(255, 170, 30, 220);
    private static readonly Color SelectedFill = new(80, 160, 255, 80);
    private static readonly Color SelectedOutline = new(130, 200, 255, 255);

    // The tiles painted since the left button went down, in Paint mode
    private readonly HashSet<TileId> painted = [];
    // Where the left button went down and where the pointer is now, while it is held
    private (Vector2 Start, Vector2 Current)? press;
    // The grid positions the selection was picked in: the stamp of a grid source has this size
    private Rectangle? block;

    /// <summary>
    /// The source shown; the selected tiles it does not have are dropped when it changes.
    /// </summary>
    public TileSourceLayout? Layout
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }
            field = value;
            press = null;
            List<TileId> kept = value is null ? [] : Selection.Where(tile => value.SlotOf(tile) is not null).ToList();
            if (kept.Count != Selection.Count)
            {
                Selection = kept;
                block = BlockOf(kept);
            }
            RaiseChanged();
        }
    }

    /// <summary>
    /// How the pointer picks.
    /// </summary>
    public TilePickMode Mode
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                press = null;
                RaiseChanged();
            }
        }
    }

    /// <summary>
    /// The selected tiles, in the order of their ids.
    /// </summary>
    public IReadOnlyList<TileId> Selection { get; private set; } = [];

    /// <summary>
    /// A mark the owner adds on a tile, or null for none.
    /// </summary>
    public Func<TileId, CanvasMark?>? MarkOf
    {
        get;
        set
        {
            field = value;
            RaiseChanged();
        }
    }

    /// <inheritdoc/>
    public RectangleF? Bounds => Layout is { Size: { X: > 0, Y: > 0 } size } ? new RectangleF(0, 0, size.X, size.Y) : null;

    /// <inheritdoc/>
    /// <remarks>Margins and spacings make the grid of a source uneven: the outlines of the tiles are its grid.</remarks>
    public Vector2 GridSize => Vector2.Zero;

    /// <inheritdoc/>
    public event EventHandler? Changed;

    /// <summary>
    /// Raised when a click or a drag of the person picked: at its release, when it selected a tile, or cleared in
    /// <see cref="TilePickMode.Many"/> mode. <see cref="Select"/> does not raise it.
    /// </summary>
    public event EventHandler? SelectionChanged;

    /// <summary>
    /// Raised in <see cref="TilePickMode.Paint"/> mode for a tile under the pressed pointer, once per drag.
    /// </summary>
    public event EventHandler<TileId>? Painted;

    /// <summary>
    /// Raised in <see cref="TilePickMode.Paint"/> mode when the left button goes up.
    /// </summary>
    public event EventHandler? PaintEnded;

    /// <summary>
    /// Selects tiles of the source; the others are left out.
    /// </summary>
    public void Select(IEnumerable<TileId> tiles)
    {
        List<TileId> selected = Layout is { } layout ? tiles.Distinct().Where(tile => layout.SlotOf(tile) is not null).Order().ToList() : [];
        Selection = selected;
        block = BlockOf(selected);
        RaiseChanged();
    }

    /// <summary>
    /// The selection as a block of cells of a slot, or null when nothing is selected. For a grid source, the block is the
    /// rectangle of grid positions picked, its top row on top, and a position without a selected tile is an empty cell; for a
    /// sheet source, it is the first selected tile.
    /// </summary>
    public TileStamp? ToStamp(ushort slot)
    {
        if (Selection.Count == 0)
        {
            return null;
        }
        if (block is not { } positions || Layout is not { IsGrid: true } layout)
        {
            return TileStamp.Single(new TileCell(slot, Selection[0]));
        }
        var cells = new TileCell[positions.Width * positions.Height];
        for (var y = 0; y < positions.Height; y++)
        {
            for (var x = 0; x < positions.Width; x++)
            {
                // The stamp counts its rows from the bottom, and the image from the top
                var tile = TileId.FromGrid(layout.Source.Id, positions.X + x, positions.Bottom - 1 - y);
                cells[y * positions.Width + x] = Selection.Contains(tile) ? new TileCell(slot, tile) : TileCell.Empty;
            }
        }
        return new TileStamp(new Int2(positions.Width, positions.Height), cells);
    }

    /// <inheritdoc/>
    public void Collect(RectangleF visible, float zoom, CanvasFrame frame)
    {
        if (Layout is not { } layout)
        {
            return;
        }
        if (layout is { IsGrid: true, Image: { } image })
        {
            frame.Sprites.Add(new CanvasSprite(image, new Rectangle(0, 0, image.Width, image.Height), new RectangleF(0, 0, image.Width, image.Height)));
        }

        var shown = Shown();
        foreach (var slot in layout.Slots)
        {
            var bounds = (RectangleF)slot.Bounds;
            if (!bounds.Intersects(visible))
            {
                continue;
            }
            if (!layout.IsGrid && slot.Picture is { } picture)
            {
                frame.Sprites.Add(new CanvasSprite(picture, slot.Region, bounds));
            }
            frame.Shapes.Add(new CanvasShape(CanvasMark.Outline, bounds, TileOutline));
            if (slot.IsMissing)
            {
                frame.Shapes.Add(new CanvasShape(CanvasMark.Cross, bounds, MissingCross));
            }
            if (MarkOf?.Invoke(slot.Tile) is { } mark)
            {
                frame.Shapes.Add(new CanvasShape(mark, bounds, MarkColor));
            }
            if (shown.Contains(slot.Tile))
            {
                frame.Shapes.Add(new CanvasShape(CanvasMark.Fill, bounds, SelectedFill));
                frame.Shapes.Add(new CanvasShape(CanvasMark.Outline, bounds, SelectedOutline));
            }
        }
    }

    /// <inheritdoc/>
    public void PointerPressed(Vector2 position, CanvasButton button, CanvasModifiers modifiers)
    {
        if (button != CanvasButton.Left || Layout is null)
        {
            return;
        }
        press = (position, position);
        if (Mode == TilePickMode.Paint)
        {
            painted.Clear();
            Paint(position);
        }
        RaiseChanged();
    }

    /// <inheritdoc/>
    public void PointerMoved(Vector2 position, CanvasModifiers modifiers)
    {
        if (press is not { } held)
        {
            return;
        }
        press = (held.Start, position);
        if (Mode == TilePickMode.Paint)
        {
            Paint(position);
        }
        else
        {
            RaiseChanged();
        }
    }

    /// <inheritdoc/>
    public void PointerReleased(Vector2 position, CanvasButton button, CanvasModifiers modifiers)
    {
        if (button != CanvasButton.Left || press is not { } held || Layout is not { } layout)
        {
            return;
        }
        press = null;
        if (Mode == TilePickMode.Paint)
        {
            PaintEnded?.Invoke(this, EventArgs.Empty);
            return;
        }
        if (Mode == TilePickMode.Many)
        {
            PickMany(layout, held.Start, position, modifiers);
        }
        else if (Mode == TilePickMode.Block && layout.IsGrid)
        {
            // A block with no tile picks nothing
            if (BlockBetween(layout, held.Start, position) is { Tiles.Count: > 0 } picked)
            {
                Pick(picked.Tiles, picked.Positions);
            }
        }
        else if (layout.SlotAt(held.Start) is { } slot)
        {
            // One tile, the one under the press, whatever the drag
            Pick([slot.Tile], BlockOf([slot.Tile]));
        }
        RaiseChanged();
    }

    /// <inheritdoc/>
    public void PointerLeft()
    {
    }

    /// <inheritdoc/>
    public bool KeyPressed(CanvasKey key, CanvasModifiers modifiers) => false;

    private void PickMany(TileSourceLayout layout, Vector2 start, Vector2 end, CanvasModifiers modifiers)
    {
        var adds = (modifiers & CanvasModifiers.Control) != 0;
        var under = layout.SlotAt(start);
        if (under == layout.SlotAt(end))
        {
            // A click
            if (under is null)
            {
                if (adds)
                {
                    return;
                }
                Pick([], null);
            }
            else if (adds)
            {
                var tiles = Selection.Contains(under.Tile) ? Selection.Where(tile => tile != under.Tile) : Selection.Append(under.Tile);
                Pick(tiles.Order().ToList(), null);
            }
            else
            {
                Pick([under.Tile], null);
            }
            return;
        }
        var dragged = Covered(layout, start, end);
        Pick((adds ? Selection.Union(dragged) : dragged).Order().ToList(), null);
    }

    // The selection a person picked; a null block is the one the tiles span
    private void Pick(IReadOnlyList<TileId> tiles, Rectangle? positions)
    {
        Selection = tiles;
        block = positions ?? BlockOf(tiles);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Paint(Vector2 position)
    {
        if (Layout?.SlotAt(position) is { } slot && painted.Add(slot.Tile))
        {
            Painted?.Invoke(this, slot.Tile);
        }
    }

    // What is drawn as selected: what the held button would pick, or the selection
    private IReadOnlyCollection<TileId> Shown()
    {
        if (press is not { } held || Layout is not { } layout)
        {
            return Selection.ToHashSet();
        }
        return Mode switch
        {
            TilePickMode.Block when layout.IsGrid && BlockBetween(layout, held.Start, held.Current).Tiles is { Count: > 0 } tiles => tiles,
            TilePickMode.Many when layout.SlotAt(held.Start) != layout.SlotAt(held.Current) => Covered(layout, held.Start, held.Current),
            _ => Selection.ToHashSet(),
        };
    }

    // The tiles whose boxes the rectangle between two points touches
    private static List<TileId> Covered(TileSourceLayout layout, Vector2 start, Vector2 end)
    {
        var (min, max) = (Vector2.Min(start, end), Vector2.Max(start, end));
        return [.. layout.Slots.Where(slot => slot.Bounds.Left <= max.X && slot.Bounds.Right > min.X && slot.Bounds.Top <= max.Y && slot.Bounds.Bottom > min.Y).Select(slot => slot.Tile)];
    }

    // The tiles of the rectangle of grid positions between two points, and that rectangle
    private static (List<TileId> Tiles, Rectangle? Positions) BlockBetween(TileSourceLayout layout, Vector2 start, Vector2 end)
    {
        if (GridPosition(layout, start) is not { } from || GridPosition(layout, end) is not { } to)
        {
            return ([], null);
        }
        var positions = new Rectangle(Math.Min(from.X, to.X), Math.Min(from.Y, to.Y), Math.Abs(from.X - to.X) + 1, Math.Abs(from.Y - to.Y) + 1);
        var tiles = new List<TileId>();
        for (var row = positions.Top; row < positions.Bottom; row++)
        {
            for (var column = positions.Left; column < positions.Right; column++)
            {
                var tile = TileId.FromGrid(layout.Source.Id, column, row);
                if (layout.SlotOf(tile) is not null)
                {
                    tiles.Add(tile);
                }
            }
        }
        tiles.Sort();
        return (tiles, positions);
    }

    // The column and the row of the grid at a point of a grid source, kept inside the grid
    private static Int2? GridPosition(TileSourceLayout layout, Vector2 point)
    {
        if (layout.Source is not GridTileSetSource grid || layout.Slots.Count == 0)
        {
            return null;
        }
        var size = layout.Slots[0].Region;
        var step = new Int2(size.Width + grid.Spacing.X, size.Height + grid.Spacing.Y);
        if (step.X <= 0 || step.Y <= 0)
        {
            return null;
        }
        var columns = (layout.Size.X - grid.Margin.X - size.Width) / step.X + 1;
        var rows = (layout.Size.Y - grid.Margin.Y - size.Height) / step.Y + 1;
        if (columns <= 0 || rows <= 0)
        {
            return null;
        }
        var column = (int)Math.Floor((point.X - grid.Margin.X) / step.X);
        var row = (int)Math.Floor((point.Y - grid.Margin.Y) / step.Y);
        return new Int2(Math.Clamp(column, 0, columns - 1), Math.Clamp(row, 0, rows - 1));
    }

    // The grid positions that tiles of a grid source span
    private Rectangle? BlockOf(IReadOnlyCollection<TileId> tiles)
    {
        if (Layout is not { IsGrid: true } || tiles.Count == 0)
        {
            return null;
        }
        var (left, top) = (tiles.Min(tile => tile.Column), tiles.Min(tile => tile.Row));
        return new Rectangle(left, top, tiles.Max(tile => tile.Column) - left + 1, tiles.Max(tile => tile.Row) - top + 1);
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
