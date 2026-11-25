using System.ComponentModel;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using Stride.Engine.Design;
using Stride.Graphics;

namespace ColorRise.Tessera;

/// <summary>
/// The tiles that maps paint with, by id.
/// </summary>
[DataContract]
[ContentSerializer(typeof(DataContentSerializer<TileSet>))]
[ReferenceSerializer, DataSerializerGlobal(typeof(ReferenceSerializer<TileSet>), Profile = "Content")]
[DataSerializerGlobal(typeof(CloneSerializer<TileSet>), Profile = "Clone")]
public sealed class TileSet
{
    /// <summary>
    /// How many pixels of a tile make one unit of the world.
    /// </summary>
    public float PixelsPerUnit { get; set; } = 16;

    /// <summary>
    /// The size of a tile, in pixels.
    /// </summary>
    public Int2 TileSize { get; set; } = new(16, 16);

    /// <summary>
    /// Whether the colours of the tiles' textures are multiplied by their alpha, as Stride's textures are by default.
    /// </summary>
    [DefaultValue(true)]
    public bool PremultipliedAlpha { get; set; } = true;

    /// <summary>
    /// The tiles, by id.
    /// </summary>
    public Dictionary<TileId, Tile> Tiles { get; } = [];

    /// <summary>
    /// The tile of an id, or null when the set has none.
    /// </summary>
    public Tile? GetTile(TileId id) => Tiles.GetValueOrDefault(id);

    /// <summary>
    /// A tile set of one grid source, source 1, over a texture: a tile at each whole place of the grid, with no margin and no
    /// spacing, and empty tiles kept.
    /// </summary>
    /// <param name="pixelsPerUnit">How many pixels make a unit; 0 takes the width of a tile, so that a tile is one unit wide.</param>
    public static TileSet FromTexture(Texture texture, Int2 tileSize, float pixelsPerUnit = 0)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tileSize.X, nameof(tileSize));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tileSize.Y, nameof(tileSize));
        ArgumentOutOfRangeException.ThrowIfNegative(pixelsPerUnit);

        var set = new TileSet { TileSize = tileSize, PixelsPerUnit = pixelsPerUnit > 0 ? pixelsPerUnit : tileSize.X };
        for (var row = 0; row < texture.Height / tileSize.Y; row++)
        {
            for (var column = 0; column < texture.Width / tileSize.X; column++)
            {
                var id = TileId.FromGrid(1, column, row);
                set.Tiles[id] = new Tile
                {
                    Id = id,
                    Texture = texture,
                    Region = new Rectangle(column * tileSize.X, row * tileSize.Y, tileSize.X, tileSize.Y),
                };
            }
        }
        return set;
    }
}
