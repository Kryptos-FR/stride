using System.Globalization;
using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Canvas;
using ColorRise.Tessera.Editor.Pictures;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.Commands;
using Stride.Core.Presentation.ViewModels;

namespace ColorRise.Tessera.Editor.TileSetEditor;

/// <summary>
/// An image the user is about to add: its grid can be changed and seen before anything is in the asset.
/// </summary>
public sealed class PendingImageSourceViewModel : DispatcherViewModel
{
    /// <summary>
    /// Above this many tiles the grid is counted, and its tiles are not outlined.
    /// </summary>
    public const int OutlineLimit = 16384;

    private static readonly Color TileOutline = new(255, 255, 255, 160);

    internal PendingImageSourceViewModel(IViewModelServiceProvider services, UFile image, TilePicture? picture, string? problem, string name, Int2 tileSize, Action<PendingImageSourceViewModel> confirm, Action cancel)
        : base(services)
    {
        Image = image;
        Picture = picture;
        ImageProblem = problem;
        ImageSize = picture is null ? Int2.Zero : new Int2(picture.Width, picture.Height);
        Scene = new GridScene(this);
        ConfirmCommand = new AnonymousCommand(services, () =>
        {
            if (Problem is null)
            {
                confirm(this);
            }
        });
        CancelCommand = new AnonymousCommand(services, cancel);
        Name = name;
        TileWidth = tileSize.X;
        TileHeight = tileSize.Y;
        Update();
    }

    /// <summary>
    /// The image file, an absolute path.
    /// </summary>
    public UFile Image { get; }

    /// <summary>
    /// The size of the image in pixels; (0, 0) when the file cannot be read.
    /// </summary>
    public Int2 ImageSize { get; }

    /// <summary>
    /// The name of the source; the file's name without its extension at first.
    /// </summary>
    public string Name
    {
        get;
        set => SetValue(ref field, value);
    } = "";

    /// <summary>
    /// The width of a tile, in pixels; the tile set's at first.
    /// </summary>
    public int TileWidth
    {
        get;
        set => SetValue(ref field, value, Update);
    }

    /// <summary>
    /// The height of a tile, in pixels; the tile set's at first.
    /// </summary>
    public int TileHeight
    {
        get;
        set => SetValue(ref field, value, Update);
    }

    /// <summary>
    /// The pixels left of the first column.
    /// </summary>
    public int MarginX
    {
        get;
        set => SetValue(ref field, value, Update);
    }

    /// <summary>
    /// The pixels above the first row.
    /// </summary>
    public int MarginY
    {
        get;
        set => SetValue(ref field, value, Update);
    }

    /// <summary>
    /// The pixels between two columns.
    /// </summary>
    public int SpacingX
    {
        get;
        set => SetValue(ref field, value, Update);
    }

    /// <summary>
    /// The pixels between two rows.
    /// </summary>
    public int SpacingY
    {
        get;
        set => SetValue(ref field, value, Update);
    }

    /// <summary>
    /// The columns of tiles the numbers cut in the image.
    /// </summary>
    public int Columns
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// The rows of tiles the numbers cut in the image.
    /// </summary>
    public int Rows
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// Why the image cannot be added as it is; null when it can.
    /// </summary>
    public string? Problem
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// The image, with an outline per tile of the grid.
    /// </summary>
    public ICanvasScene Scene { get; }

    /// <summary>
    /// Adds the image to the tile set as a source; it cannot run while there is a <see cref="Problem"/>.
    /// </summary>
    public ICommandBase ConfirmCommand { get; }

    /// <summary>
    /// Drops the image; nothing is added.
    /// </summary>
    public ICommandBase CancelCommand { get; }

    /// <summary>
    /// The tile size of the numbers.
    /// </summary>
    internal Int2 TileSize => new(TileWidth, TileHeight);

    /// <summary>
    /// The margin of the numbers.
    /// </summary>
    internal Int2 Margin => new(MarginX, MarginY);

    /// <summary>
    /// The spacing of the numbers.
    /// </summary>
    internal Int2 Spacing => new(SpacingX, SpacingY);

    private TilePicture? Picture { get; }

    private string? ImageProblem { get; }

    // The regions of the tiles, as the build cuts them
    private List<Rectangle> Tiles { get; set; } = [];

    // The grid as the build cuts it, and what keeps it from being added. The columns and the rows are counted; the grid is
    // cut, for its outlines, only when it has at most OutlineLimit tiles
    private void Update()
    {
        var valid = ImageSize != Int2.Zero && TileWidth >= 1 && TileHeight >= 1 && MarginX >= 0 && MarginY >= 0 && SpacingX >= 0 && SpacingY >= 0;
        var (columns, rows) = valid
            ? (Count(ImageSize.X, MarginX, TileWidth, SpacingX, TileId.MaxColumn + 1), Count(ImageSize.Y, MarginY, TileHeight, SpacingY, TileId.MaxRow + 1))
            : (0, 0);
        // A grid without a row has no column either, and the other way
        (Columns, Rows) = columns == 0 || rows == 0 ? (0, 0) : (columns, rows);
        Tiles = (long)Columns * Rows is > 0 and <= OutlineLimit
            ? [.. TileSlicer.Grid(new GridTileSetSource { Id = 1, TileSize = TileSize, Margin = Margin, Spacing = Spacing }, TileSize, ImageSize).Select(slice => slice.Region)]
            : [];
        Problem = ImageProblem
            ?? (TileWidth < 1 || TileHeight < 1 ? "A tile is at least 1 pixel wide and 1 pixel high." : null)
            ?? (MarginX < 0 || MarginY < 0 || SpacingX < 0 || SpacingY < 0 ? "The margins and the spacings are 0 or more." : null)
            ?? (Columns * Rows == 0
                ? string.Create(CultureInfo.InvariantCulture, $"No tile of {TileWidth} by {TileHeight} pixels fits in the image of {ImageSize.X} by {ImageSize.Y} pixels.")
                : null);
        ConfirmCommand.IsEnabled = Problem is null;
        ((GridScene)Scene).RaiseChanged();
    }

    // How many tiles of a size fit along a side after the margin, a spacing between two, as TileSlicer.Grid cuts them
    private static int Count(int side, int margin, int size, int spacing, int max)
    {
        var room = (long)side - margin - size;
        return room < 0 ? 0 : (int)Math.Min(room / ((long)size + spacing) + 1, max);
    }

    // The image and the outlines of its tiles
    private sealed class GridScene(PendingImageSourceViewModel pending) : ICanvasScene
    {
        public RectangleF? Bounds => pending.ImageSize == Int2.Zero ? null : new RectangleF(0, 0, pending.ImageSize.X, pending.ImageSize.Y);

        public Vector2 GridSize => Vector2.Zero;

        public event EventHandler? Changed;

        public void Collect(RectangleF visible, float zoom, CanvasFrame frame)
        {
            if (pending.Picture is not { } picture)
            {
                return;
            }
            frame.Sprites.Add(new CanvasSprite(picture, new Rectangle(0, 0, picture.Width, picture.Height), new RectangleF(0, 0, picture.Width, picture.Height)));
            foreach (var tile in pending.Tiles)
            {
                RectangleF bounds = tile;
                if (bounds.Intersects(visible))
                {
                    frame.Shapes.Add(new CanvasShape(CanvasMark.Outline, bounds, TileOutline));
                }
            }
        }

        public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
    }
}
