using System.Globalization;
using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Palette;
using Stride.Core.Presentation.Commands;
using Stride.Core.Presentation.ViewModels;

namespace ColorRise.Tessera.Editor.FlipbookEditor;

/// <summary>
/// The order of the frames Sheet makes from a row.
/// </summary>
public enum SheetFrameOrder
{
    /// <summary>
    /// The cells of the row from the left: 1, 2, 3.
    /// </summary>
    InOrder,

    /// <summary>
    /// The cells of the row from the left, then back without the two ends: 1, 2, 3, 2.
    /// </summary>
    ThereAndBack,
}

/// <summary>
/// A row of the block of a sheet, and the direction of the clip it makes.
/// </summary>
public sealed class SheetRowViewModel : DispatcherViewModel
{
    private readonly Action changed;

    internal SheetRowViewModel(IViewModelServiceProvider services, int row, FlipbookDirection? direction, Action changed)
        : base(services)
    {
        Row = row;
        Direction = direction;
        this.changed = changed;
    }

    /// <summary>
    /// The number of the row, from 1, from the top of the block.
    /// </summary>
    public int Row { get; }

    /// <summary>
    /// The direction of the clip the row makes; null when the row is skipped.
    /// </summary>
    public FlipbookDirection? Direction
    {
        get;
        set
        {
            if (SetValue(ref field, value))
            {
                changed?.Invoke();
            }
        }
    }
}

/// <summary>
/// What Sheet proposes: a block of the palette cut into one clip a row, made in an animation when it is confirmed.
/// </summary>
public sealed class PendingSheetViewModel : DispatcherViewModel
{
    // The directions of the rows of a block of several rows, from the top; the rows after them are skipped
    private static readonly FlipbookDirection[] RowDirections = [FlipbookDirection.Down, FlipbookDirection.Left, FlipbookDirection.Right, FlipbookDirection.Up];

    private readonly FlipbookAsset flipbook;

    internal PendingSheetViewModel(IViewModelServiceProvider services, FlipbookAsset flipbook, TileStamp? block, string name, Action<PendingSheetViewModel> confirm, Action cancel)
        : base(services)
    {
        this.flipbook = flipbook;
        ConfirmCommand = new AnonymousCommand(services, () =>
        {
            if (Problem is null)
            {
                confirm(this);
            }
        });
        CancelCommand = new AnonymousCommand(services, cancel);
        AnimationName = name;
        SetBlock(block);
    }

    /// <summary>
    /// The block picked in the palette, its top row on top; null until one is.
    /// </summary>
    public TileStamp? Block
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// The columns of the block, the frames of a row.
    /// </summary>
    public int Columns
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// The rows of the block, a clip each.
    /// </summary>
    public int Rows
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// The rows of the block, from the top, each with the direction of its clip.
    /// </summary>
    public IReadOnlyList<SheetRowViewModel> RowItems
    {
        get;
        private set => SetValue(ref field, value);
    } = [];

    /// <summary>
    /// The order of the frames of a row; in order at first.
    /// </summary>
    public SheetFrameOrder Order
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// How long each frame lasts, in seconds; 0.15 s at first.
    /// </summary>
    public float Duration
    {
        get;
        set => SetValue(ref field, value, Update);
    } = 0.15f;

    /// <summary>
    /// The name of the animation the clips go to: an animation of that name, or a new one.
    /// </summary>
    public string AnimationName
    {
        get;
        set => SetValue(ref field, value ?? "", Update);
    } = "";

    /// <summary>
    /// Why the sheet cannot be made as it is; null when it can.
    /// </summary>
    public string? Problem
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// The clips with frames that making the sheet replaces; null when it replaces none, or cannot be made.
    /// </summary>
    public string? Note
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// Makes the clips; it cannot run while there is a <see cref="Problem"/>.
    /// </summary>
    public ICommandBase ConfirmCommand { get; }

    /// <summary>
    /// Closes the bar; nothing changes.
    /// </summary>
    public ICommandBase CancelCommand { get; }

    /// <inheritdoc/>
    public override void Destroy()
    {
        foreach (var row in RowItems)
        {
            row.Destroy();
        }
        base.Destroy();
    }

    /// <summary>
    /// Takes another block; its rows get their directions again.
    /// </summary>
    internal void SetBlock(TileStamp? block)
    {
        foreach (var row in RowItems)
        {
            row.Destroy();
        }
        Block = block;
        (Columns, Rows) = block is null ? (0, 0) : (block.Size.X, block.Size.Y);
        var rows = new List<SheetRowViewModel>();
        for (var index = 0; index < Rows; index++)
        {
            FlipbookDirection? direction = Rows == 1 ? FlipbookDirection.None : index < RowDirections.Length ? RowDirections[index] : null;
            rows.Add(new SheetRowViewModel(ServiceProvider, index + 1, direction, Update));
        }
        RowItems = rows;
        Update();
    }

    /// <summary>
    /// The clips the sheet makes, in the order of the rows: one for each row with a direction and a tile, with new frames.
    /// </summary>
    internal List<(FlipbookDirection Direction, List<FlipbookFrame> Frames)> MakeClips()
    {
        var clips = new List<(FlipbookDirection, List<FlipbookFrame>)>();
        foreach (var row in RowItems)
        {
            if (row.Direction is { } direction && CellsOf(row) is { Count: > 0 } cells)
            {
                clips.Add((direction, [.. Ordered(cells).Select(MakeFrame)]));
            }
        }
        return clips;
    }

    /// <summary>
    /// Says again what the sheet replaces, after a change of the asset.
    /// </summary>
    internal void Refresh() => Update();

    // The animation the clips go to: the first one of the name, or none for a new one
    private FlipbookAnimation? Target(string name) => flipbook.Animations.FirstOrDefault(animation => animation is not null && animation.Name == name);

    // The cells of a row that have a tile, from the left; the block counts its rows from the bottom
    private List<TileCell> CellsOf(SheetRowViewModel row)
    {
        var cells = new List<TileCell>();
        if (Block is { } block)
        {
            var y = block.Size.Y - row.Row;
            for (var x = 0; x < block.Size.X; x++)
            {
                if (block[x, y] is { IsEmpty: false } cell)
                {
                    cells.Add(cell);
                }
            }
        }
        return cells;
    }

    // There and back adds the frames between the two ends, from the last: 1, 2, 3 then 2
    private IEnumerable<TileCell> Ordered(List<TileCell> cells)
        => Order == SheetFrameOrder.ThereAndBack && cells.Count > 2 ? cells.Concat(Enumerable.Range(1, cells.Count - 2).Reverse().Select(index => cells[index])) : cells;

    private FlipbookFrame MakeFrame(TileCell cell)
    {
        var frame = new FlipbookFrame { Duration = Duration };
        frame.Cells.Add(new FlipbookCell { Tile = cell.Tile, Flip = cell.Flip });
        return frame;
    }

    private void Update()
    {
        var name = AnimationName.Trim();
        // A row of holes makes no clip, as a skipped row
        var written = RowItems.Where(row => row.Direction is not null && CellsOf(row).Count > 0).ToList();
        var twice = RowItems.Where(row => row.Direction is not null).GroupBy(row => row.Direction).FirstOrDefault(group => group.Count() > 1);
        Problem = Block is null ? "Drag over the frames in the palette."
            : name.Length == 0 ? "The animation needs a name."
            : !(Duration > 0) ? "The duration must be above 0."
            : written.Count == 0 ? "Every row is skipped."
            : twice is not null ? $"Two rows have the direction {twice.Key}."
            : null;

        Note = null;
        if (Problem is null && Target(name) is { } animation)
        {
            var replaced = written
                .Select(row => row.Direction!.Value)
                .Where(direction => animation.Clips.FirstOrDefault(clip => clip is not null && clip.Direction == direction) is { Frames.Count: > 0 })
                .ToList();
            Note = replaced.Count switch
            {
                0 => null,
                1 => string.Create(CultureInfo.InvariantCulture, $"Replaces the clip {replaced[0]} of '{name}'."),
                _ => string.Create(CultureInfo.InvariantCulture, $"Replaces the clips {string.Join(", ", replaced)} of '{name}'."),
            };
        }
        ConfirmCommand.IsEnabled = Problem is null;
    }
}
