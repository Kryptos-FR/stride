using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// The tiles of the sources of a tile set.
/// </summary>
public static class TileSlicer
{
    /// <summary>
    /// The grid positions of a source that are wholly inside the image, by row then column. The margin moves the start of the
    /// grid and asks for nothing at the far edge, as in Tiled. No look at the pixels: an empty position is a tile too.
    /// </summary>
    /// <remarks>
    /// A size that is not above 0, a margin below 0, a step (size plus spacing) that is not above 0 or a source id that no
    /// tile can have gives no tile. The grid stops at the last column and row a tile id can hold.
    /// </remarks>
    public static List<TileSlice> Grid(GridTileSetSource source, Int2 defaultTileSize, Int2 imageSize)
    {
        ArgumentNullException.ThrowIfNull(source);
        var slices = new List<TileSlice>();
        var size = source.TileSize == Int2.Zero ? defaultTileSize : source.TileSize;
        var (stepX, stepY) = ((long)size.X + source.Spacing.X, (long)size.Y + source.Spacing.Y);
        if (size.X <= 0 || size.Y <= 0 || source.Margin.X < 0 || source.Margin.Y < 0 || stepX <= 0 || stepY <= 0
            || source.Id is < 1 or > TileId.MaxSource)
        {
            return slices;
        }

        for (var row = 0; row <= TileId.MaxRow; row++)
        {
            var y = source.Margin.Y + row * stepY;
            if (y + size.Y > imageSize.Y)
            {
                break;
            }
            for (var column = 0; column <= TileId.MaxColumn; column++)
            {
                var x = source.Margin.X + column * stepX;
                if (x + size.X > imageSize.X)
                {
                    break;
                }
                slices.Add(new TileSlice(TileId.FromGrid(source.Id, column, row), source.Image!, new Rectangle((int)x, (int)y, size.X, size.Y)));
            }
        }
        return slices;
    }

    /// <summary>
    /// Whether every pixel of a region has an alpha of 0 or is the color key, all four channels alike as the texture build compares them.
    /// </summary>
    /// <param name="pixels">The pixels of the image, row by row from the top.</param>
    public static bool IsEmpty(ReadOnlySpan<Color> pixels, int imageWidth, Rectangle region, Color? colorKey)
    {
        if (imageWidth <= 0)
        {
            return true;
        }
        var imageHeight = pixels.Length / imageWidth;
        var (left, right) = (Math.Max(region.Left, 0), Math.Min(region.Right, imageWidth));
        var (top, bottom) = (Math.Max(region.Top, 0), Math.Min(region.Bottom, imageHeight));
        for (var y = top; y < bottom; y++)
        {
            foreach (var pixel in pixels.Slice(y * imageWidth + left, Math.Max(right - left, 0)))
            {
                if (pixel.A != 0 && pixel != colorKey)
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// The tiles of a sheet source: one per binding whose sprite is in the sheet, in the order of the bindings. A name used
    /// twice in the sheet takes the first sprite of that name.
    /// </summary>
    /// <param name="missing">The bindings whose sprite is not in the sheet.</param>
    /// <param name="duplicateNames">The names that more than one sprite has, each once.</param>
    /// <param name="unbound">How many names of the sheet have no binding yet.</param>
    public static List<TileSlice> Sheet(SpriteSheetTileSetSource source, IReadOnlyList<NamedRegion> sprites,
        out List<SpriteTileBinding> missing, out List<string> duplicateNames, out int unbound)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sprites);
        var byName = new Dictionary<string, NamedRegion>(StringComparer.Ordinal);
        duplicateNames = [];
        foreach (var sprite in sprites)
        {
            if (!byName.TryAdd(sprite.Name, sprite) && !duplicateNames.Contains(sprite.Name))
            {
                duplicateNames.Add(sprite.Name);
            }
        }

        var slices = new List<TileSlice>();
        missing = [];
        var bound = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in source.Bindings)
        {
            bound.Add(binding.SpriteName);
            if (byName.TryGetValue(binding.SpriteName, out var sprite) && source.Id is >= 1 and <= TileId.MaxSource
                && binding.Number is >= 0 and <= TileId.MaxNumber)
            {
                slices.Add(new TileSlice(TileId.FromNumber(source.Id, binding.Number), sprite.Image, sprite.Region));
            }
            else
            {
                missing.Add(binding);
            }
        }
        unbound = byName.Keys.Count(name => !bound.Contains(name));
        return slices;
    }

    /// <summary>
    /// Gives a number never given before to each sprite name that has none, in the order given.
    /// </summary>
    /// <returns>Whether a binding was added.</returns>
    /// <exception cref="InvalidOperationException">The source has given every number a tile id can hold.</exception>
    public static bool Bind(SpriteSheetTileSetSource source, IEnumerable<string> spriteNames)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(spriteNames);
        var bound = source.Bindings.Select(binding => binding.SpriteName).ToHashSet(StringComparer.Ordinal);
        var added = false;
        foreach (var name in spriteNames)
        {
            if (!bound.Add(name))
            {
                continue;
            }
            if (source.LastNumber >= TileId.MaxNumber)
            {
                throw new InvalidOperationException($"The source '{source.Name}' has given all {TileId.MaxNumber} numbers of tiles.");
            }
            source.Bindings.Add(new SpriteTileBinding { Number = ++source.LastNumber, SpriteName = name });
            added = true;
        }
        return added;
    }
}
