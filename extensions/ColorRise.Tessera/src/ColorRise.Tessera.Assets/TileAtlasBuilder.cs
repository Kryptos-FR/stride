using Stride.Assets.Textures.Packing;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// The textures a tile set's tiles are packed into, and where each tile is in them.
/// </summary>
internal sealed class TileAtlas : IDisposable
{
    /// <summary>
    /// The images of the textures, in R8G8B8A8.
    /// </summary>
    public List<Image> Textures { get; } = [];

    /// <summary>
    /// The texture of each tile, by its index in <see cref="Textures"/>, and its region there, without its border.
    /// </summary>
    public Dictionary<TileId, (int Texture, Rectangle Region)> Placements { get; } = [];

    public void Dispose()
    {
        foreach (var texture in Textures)
        {
            texture.Dispose();
        }
        Textures.Clear();
    }
}

/// <summary>
/// Packs the tiles of a tile set into textures, each tile with a border that repeats its edge, so that a sampler never reads
/// a neighbour's pixels at the edge of a tile.
/// </summary>
internal static class TileAtlasBuilder
{
    /// <summary>
    /// The pixels of the border around each tile.
    /// </summary>
    public const int Border = 2;

    /// <summary>
    /// Packs the tiles into as few textures as fit <paramref name="maxSize"/>, with Stride's texture packer. No tile is rotated,
    /// and a slice whose pixels are all empty is left out; where a tile is given twice, its first slice is kept.
    /// </summary>
    /// <param name="loadImage">Reads the image of a file, in R8G8B8A8; called once a file. The images stay the caller's.</param>
    /// <param name="colorKey">The color that becomes transparent, all four channels alike, or <c>null</c>.</param>
    /// <param name="maxSize">The largest width and height of a texture; a power of two, or the one below it is taken.</param>
    /// <param name="srgb">Whether the textures are in sRGB.</param>
    /// <exception cref="ArgumentException">A tile is larger than a texture with its border, or an image is not in R8G8B8A8.</exception>
    public static TileAtlas Build(IReadOnlyList<TileSlice> slices, Func<UFile, Image> loadImage, Color? colorKey, int maxSize = 2048, bool srgb = false)
    {
        var limit = MathUtil.PreviousPowerOfTwo(maxSize);
        var images = new Dictionary<UFile, (Image Image, Color[] Pixels)>();
        var tiles = new Dictionary<string, TileId>();
        var elements = new List<AtlasTextureElement>();
        foreach (var slice in slices)
        {
            if (!images.TryGetValue(slice.Image, out var image))
            {
                var read = loadImage(slice.Image);
                if (read.Description.Format is not (PixelFormat.R8G8B8A8_UNorm or PixelFormat.R8G8B8A8_UNorm_SRgb))
                {
                    throw new ArgumentException($"The image '{slice.Image}' is in {read.Description.Format}: tiles are read in R8G8B8A8.", nameof(loadImage));
                }
                image = (read, read.PixelBuffer[0].GetPixels<Color>());
                images[slice.Image] = image;
            }

            if (TileSlicer.IsEmpty(image.Pixels, image.Image.Description.Width, slice.Region, colorKey))
            {
                continue;
            }
            if (slice.Region.Width + 2 * Border > limit || slice.Region.Height + 2 * Border > limit)
            {
                throw new ArgumentException($"The tile {slice.Tile} is {slice.Region.Width} by {slice.Region.Height} pixels: with its border, it does not fit a texture of {limit} by {limit}.", nameof(slices));
            }
            // The packer knows elements by name; a tile's id is unique in a tile set
            var name = slice.Tile.ToString();
            if (tiles.TryAdd(name, slice.Tile))
            {
                elements.Add(new AtlasTextureElement(name, image.Image, new RotableRectangle(slice.Region), Border, TextureAddressMode.Clamp, TextureAddressMode.Clamp));
            }
        }

        var atlas = new TileAtlas();
        if (elements.Count == 0)
        {
            return atlas;
        }

        // One heuristic of the packer: Best would pack the tiles once with each of its five heuristics
        var packer = new TexturePacker { Algorithm = TexturePackingMethod.BestShortSideFit, AllowMultipack = true, MaxWidth = limit, MaxHeight = limit };
        if (!packer.PackTextures(elements))
        {
            throw new InvalidOperationException("The texture packer could not place tiles that each fit a texture.");
        }
        foreach (var layout in packer.AtlasTextureLayouts)
        {
            var texture = AtlasTextureFactory.CreateTextureAtlas(layout, srgb);
            if (colorKey is { } key)
            {
                MakeTransparent(texture, key);
            }
            foreach (var element in layout.Textures)
            {
                var region = new Rectangle(element.DestinationRegion.X + Border, element.DestinationRegion.Y + Border, element.SourceRegion.Width, element.SourceRegion.Height);
                atlas.Placements[tiles[element.Name]] = (atlas.Textures.Count, region);
            }
            atlas.Textures.Add(texture);
        }
        return atlas;
    }

    // As the texture build does with a color key: the pixels of that color, all four channels alike, become transparent black
    private static void MakeTransparent(Image texture, Color key)
    {
        var buffer = texture.PixelBuffer[0];
        var pixels = buffer.GetPixels<Color>();
        for (var i = 0; i < pixels.Length; i++)
        {
            if (pixels[i] == key)
            {
                pixels[i] = Color.Transparent;
            }
        }
        buffer.SetPixels(pixels);
    }
}
