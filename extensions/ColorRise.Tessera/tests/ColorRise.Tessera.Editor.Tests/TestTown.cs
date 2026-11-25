using ColorRise.Tessera.Assets;
using Stride.Assets.Sprite;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// The tile set and maps of the editor tests: "town.png", 64 by 32 pixels, four by two tiles of 16 pixels.
/// </summary>
internal static class TestTown
{
    /// <summary>
    /// The colour of the flat tile (c, r).
    /// </summary>
    public static Color Flat(int column, int row) => new((byte)(40 + 60 * column), (byte)(80 + 100 * row), 200, 255);

    /// <summary>
    /// A pixel of "town.png", from the top left. The tile (c, r) is flat, of the colour <see cref="Flat"/>, but (0, 0) has
    /// four quarters (top-left red, top-right green, bottom-left blue, bottom-right yellow) and (3, 1) is fully transparent.
    /// </summary>
    public static Color Pixel(int x, int y)
    {
        var (column, row) = (x / 16, y / 16);
        return (column, row) switch
        {
            (0, 0) => (x % 16 < 8, y % 16 < 8) switch
            {
                (true, true) => Color.Red,
                (false, true) => Color.Lime,
                (true, false) => Color.Blue,
                _ => Color.Yellow,
            },
            (3, 1) => Color.Transparent,
            _ => Flat(column, row),
        };
    }

    /// <summary>
    /// A tile set with one grid source "Town", id 1, over the image, and tiles of 16 by 16 pixels.
    /// </summary>
    public static TileSetAsset TileSet(string image = "town.png")
    {
        var asset = new TileSetAsset { TileSize = new Int2(16, 16) };
        asset.AddSource<GridTileSetSource>("Town").Image = new UFile(image);
        return asset;
    }

    /// <summary>
    /// A sprite sheet over "town.png" with the sprites "grass" (0, 0), "water" (16, 0) and "wall" (32, 0), of one tile each.
    /// </summary>
    public static SpriteSheetAsset Objects() => new()
    {
        Sprites =
        {
            new SpriteInfo { Name = "grass", Source = new UFile("town.png"), TextureRegion = new Rectangle(0, 0, 16, 16) },
            new SpriteInfo { Name = "water", Source = new UFile("town.png"), TextureRegion = new Rectangle(16, 0, 16, 16) },
            new SpriteInfo { Name = "wall", Source = new UFile("town.png"), TextureRegion = new Rectangle(32, 0, 16, 16) },
        },
    };

    /// <summary>
    /// A map whose slot 1 is the tile set, with one layer per array ("Layer0", "Layer1", ...). A layer is rows of tokens,
    /// the top row first, down to the cell (0, 0) at the start of the last row: "." for no tile, or "c,r" and flip letters
    /// (h, v, d) for the tile 1:c,r of the slot 1, as "2,1h".
    /// </summary>
    public static TileMapAsset Map(TileSetAsset tileSet, string tileSetLocation, params string[][] layers)
    {
        var map = new TileMap();
        for (var index = 0; index < layers.Length; index++)
        {
            var layer = map.Layers.Add($"Layer{index}");
            var rows = layers[index];
            for (var row = 0; row < rows.Length; row++)
            {
                var tokens = rows[row].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (var x = 0; x < tokens.Length; x++)
                {
                    if (tokens[x] != ".")
                    {
                        layer.SetCell(new Int2(x, rows.Length - 1 - row), Token(tokens[x]));
                    }
                }
            }
        }
        var asset = TileMapAsset.FromTileMap(map);
        asset.TileSets.Add(new TileSetSlot { Slot = 1, TileSet = AttachedReferenceManager.CreateProxyObject<TileSet>(tileSet.Id, tileSetLocation) });
        asset.LastSlot = 1;
        return asset;
    }

    /// <summary>
    /// A flipbook on the tile set (none when null): "walk", which loops, with the clips Down (1:0,0, 1:1,0, 1:2,0) and Left
    /// (1:0,1, 1:1,1), and "idle", with one clip for any direction (1:3,0). Every frame has one cell and lasts 0.1 s.
    /// </summary>
    public static FlipbookAsset Flipbook(TileSetAsset? tileSet, string tileSetLocation)
    {
        var asset = new FlipbookAsset();
        if (tileSet is not null)
        {
            asset.TileSet = AttachedReferenceManager.CreateProxyObject<TileSet>(tileSet.Id, tileSetLocation);
        }
        var walk = new FlipbookAnimation { Name = "walk", LoopMode = FlipbookLoopMode.Loop };
        walk.Clips.Add(Clip(FlipbookDirection.Down, "1:0,0", "1:1,0", "1:2,0"));
        walk.Clips.Add(Clip(FlipbookDirection.Left, "1:0,1", "1:1,1"));
        var idle = new FlipbookAnimation { Name = "idle" };
        idle.Clips.Add(Clip(FlipbookDirection.None, "1:3,0"));
        asset.Animations.Add(walk);
        asset.Animations.Add(idle);
        return asset;
    }

    /// <summary>
    /// A clip of frames of one cell each, of the given tiles, that last 0.1 s.
    /// </summary>
    public static FlipbookClip Clip(FlipbookDirection direction, params string[] tiles)
    {
        var clip = new FlipbookClip { Direction = direction };
        foreach (var tile in tiles)
        {
            clip.Frames.Add(Frame(tile));
        }
        return clip;
    }

    /// <summary>
    /// A frame of one cell, with no offset, that lasts 0.1 s.
    /// </summary>
    public static FlipbookFrame Frame(string tile, TileFlip flip = TileFlip.None)
    {
        var frame = new FlipbookFrame { Duration = 0.1f };
        frame.Cells.Add(new FlipbookCell { Tile = TileId.Parse(tile), Flip = flip });
        return frame;
    }

    /// <summary>
    /// The rows of a layer for <see cref="Map"/> with the given tokens at the given cells, and "." elsewhere, from (0, 0).
    /// </summary>
    public static string[] Rows(params (int X, int Y, string Token)[] cells)
    {
        var (width, height) = (cells.Max(cell => cell.X) + 1, cells.Max(cell => cell.Y) + 1);
        var rows = new string[height];
        for (var row = 0; row < height; row++)
        {
            var y = height - 1 - row;
            rows[row] = string.Join(' ', Enumerable.Range(0, width).Select(x => cells.FirstOrDefault(cell => cell.X == x && cell.Y == y).Token ?? "."));
        }
        return rows;
    }

    /// <summary>
    /// A map of <paramref name="side"/> by <paramref name="side"/> cells from (0, 0), a multiple of 16, in
    /// <paramref name="layers"/> layers, every cell with the tile 1:(x mod 4),(y mod 2) of the slot 1. Its chunks are written as
    /// text directly, which is quicker than through a runtime map.
    /// </summary>
    public static TileMapAsset LargeMap(TileSetAsset tileSet, string tileSetLocation, int side, int layers)
    {
        var asset = Map(tileSet, tileSetLocation);
        var cells = new TileCell[TileChunk.Size * TileChunk.Size];
        for (var i = 0; i < cells.Length; i++)
        {
            cells[i] = Cell(i % TileChunk.Size % 4, i / TileChunk.Size % 2);
        }
        var text = TileChunkText.Encode(cells);
        var chunks = side / TileChunk.Size;
        for (var index = 0; index < layers; index++)
        {
            var layer = new TileLayerAsset { Name = $"Layer{index}" };
            for (var y = chunks - 1; y >= 0; y--)
            {
                for (var x = 0; x < chunks; x++)
                {
                    layer.Chunks.Add(new TileChunkAsset { Position = new Int2(x, y), Cells = text });
                }
            }
            asset.Layers.Add(layer);
        }
        return asset;
    }

    /// <summary>
    /// The cell of the tile 1:c,r of the slot 1.
    /// </summary>
    public static TileCell Cell(int column, int row, TileFlip flip = TileFlip.None) => new(1, TileId.Parse($"1:{column},{row}"), flip);

    // "2,1h": the tile 1:2,1, flipped horizontally
    private static TileCell Token(string token)
    {
        var letters = token.AsSpan().TrimStart("0123456789,");
        var flip = TileFlip.None;
        foreach (var letter in letters)
        {
            flip |= letter switch
            {
                'h' => TileFlip.Horizontal,
                'v' => TileFlip.Vertical,
                'd' => TileFlip.Diagonal,
                _ => throw new FormatException($"'{letter}' is no flip, in '{token}'."),
            };
        }
        var place = token[..^letters.Length].Split(',');
        return Cell(int.Parse(place[0]), int.Parse(place[1]), flip);
    }
}
