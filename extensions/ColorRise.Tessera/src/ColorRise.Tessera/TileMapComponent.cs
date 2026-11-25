using ColorRise.Tessera.Rendering;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.Rendering;

namespace ColorRise.Tessera;

/// <summary>
/// Shows a tile map in the scene, in the plane z = 0 of its entity with y up, and answers what is at a place of it.
/// </summary>
[DataContract("TileMapComponent")]
[Display("Tile map", Expand = ExpandRule.Once)]
[ComponentCategory("2D")]
[DefaultEntityComponentRenderer(typeof(TileMapRenderProcessor))]
public sealed class TileMapComponent : ActivableEntityComponent
{
    /// <userdoc>The tile map to show.</userdoc>
    [DataMember(10)]
    public TileMap? Map { get; set; }

    /// <summary>
    /// The colour every tile of the map is multiplied by.
    /// </summary>
    /// <userdoc>The colour every tile of the map is multiplied by.</userdoc>
    [DataMember(20)]
    public Color4 Color { get; set; } = Color4.White;

    /// <summary>
    /// How far apart the layers are along the entity's z, so that a layer is drawn over the one under it.
    /// </summary>
    /// <userdoc>How far apart the layers are along z. A layer is drawn over the one under it.</userdoc>
    [DataMember(30)]
    [Display("Layer spacing")]
    public float LayerSpacing { get; set; } = 0.1f;

    /// <userdoc>Point keeps pixel art sharp; linear smooths it.</userdoc>
    [DataMember(40)]
    public TileSampler Sampler { get; set; }

    /// <summary>
    /// How fast the animated tiles play: 1 is their own speed, 0 stops them.
    /// </summary>
    /// <userdoc>How fast the animated tiles play: 1 is their own speed, 0 stops them.</userdoc>
    [DataMember(50)]
    [Display("Animation speed")]
    public float AnimationSpeed { get; set; } = 1;

    /// <summary>
    /// The time of the animated tiles, in seconds.
    /// </summary>
    [DataMemberIgnore]
    public double AnimationTime { get; set; }

    /// <userdoc>The render group of the map, for the cameras and render stages that draw only some groups.</userdoc>
    [DataMember(60)]
    [Display("Render group")]
    public RenderGroup RenderGroup { get; set; }

    private Vector2 CellSize => Map?.CellSize ?? Vector2.One;

    private Matrix World => Entity?.Transform.WorldMatrix ?? Matrix.Identity;

    /// <summary>
    /// The cell under a point of the world, through the entity's world matrix; (0, 0) without a map.
    /// </summary>
    public Int2 WorldToCell(Vector3 world)
    {
        if (Map is null)
        {
            return Int2.Zero;
        }
        var local = Vector3.TransformCoordinate(world, Matrix.Invert(World));
        return new Int2(FloorToInt(local.X / CellSize.X), FloorToInt(local.Y / CellSize.Y));
    }

    /// <summary>
    /// The point of the world at a corner or at the centre of a cell.
    /// </summary>
    public Vector3 CellToWorld(Int2 cell, TileAnchor anchor = TileAnchor.BottomLeft)
    {
        var size = CellSize;
        var shift = anchor == TileAnchor.Center ? 0.5 : 0;
        var local = new Vector3((float)((cell.X + shift) * size.X), (float)((cell.Y + shift) * size.Y), 0);
        return Vector3.TransformCoordinate(local, World);
    }

    /// <summary>
    /// The z of a layer in the entity's space: its index times <see cref="LayerSpacing"/>.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The map has no layer of that name, or there is no map.</exception>
    public float GetLayerZ(string layerName)
    {
        var map = Map ?? throw new KeyNotFoundException($"The component has no map, so no layer named '{layerName}'.");
        return map.Layers.IndexOf(map.Layers[layerName]) * LayerSpacing;
    }

    /// <summary>
    /// Whether a cell has a solid tile on a layer whose <see cref="TileLayer.Collision"/> is on.
    /// </summary>
    public bool IsSolid(Int2 cell)
    {
        if (Map is not { } map)
        {
            return false;
        }
        foreach (var layer in map.Layers)
        {
            if (layer.Collision && map.GetTile(layer.GetCell(cell)) is { IsSolid: true })
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Whether a rectangle of the map's plane, given in world units with X and Y its bottom-left corner, overlaps a solid
    /// cell. A rectangle that only touches the edge of a cell does not overlap it.
    /// </summary>
    public bool OverlapsSolid(RectangleF worldRectangle)
    {
        if (Map is not { } map)
        {
            return false;
        }

        // The cells under the rectangle, in the map's space; a turned entity takes the box around the turned rectangle
        var toLocal = Matrix.Invert(World);
        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        var (left, bottom) = (worldRectangle.X, worldRectangle.Y);
        var (right, top) = (worldRectangle.X + worldRectangle.Width, worldRectangle.Y + worldRectangle.Height);
        foreach (var corner in new[] { new Vector3(left, bottom, 0), new Vector3(right, bottom, 0), new Vector3(left, top, 0), new Vector3(right, top, 0) })
        {
            var local = Vector3.TransformCoordinate(corner, toLocal);
            min = Vector2.Min(min, new Vector2(local.X, local.Y));
            max = Vector2.Max(max, new Vector2(local.X, local.Y));
        }
        var size = map.CellSize;
        var (firstX, lastX) = (Math.Floor((double)min.X / size.X), Math.Ceiling((double)max.X / size.X) - 1);
        var (firstY, lastY) = (Math.Floor((double)min.Y / size.Y), Math.Ceiling((double)max.Y / size.Y) - 1);
        // A rectangle that is not a number, or an entity scaled to zero, covers no cell
        if (!double.IsFinite(firstX) || !double.IsFinite(lastX) || !double.IsFinite(firstY) || !double.IsFinite(lastY) || lastX < firstX || lastY < firstY)
        {
            return false;
        }
        var cells = (FirstX: (long)Math.Max(firstX, long.MinValue / 2), LastX: (long)Math.Min(lastX, long.MaxValue / 2),
            FirstY: (long)Math.Max(firstY, long.MinValue / 2), LastY: (long)Math.Min(lastY, long.MaxValue / 2));
        // The chunks under the rectangle, clamped to the chunks that cells of int can be in
        var (chunkMinX, chunkMaxX) = (Math.Max(cells.FirstX >> 4, int.MinValue >> 4), Math.Min(cells.LastX >> 4, int.MaxValue >> 4));
        var (chunkMinY, chunkMaxY) = (Math.Max(cells.FirstY >> 4, int.MinValue >> 4), Math.Min(cells.LastY >> 4, int.MaxValue >> 4));
        var chunksUnder = (double)(chunkMaxX - chunkMinX + 1) * (chunkMaxY - chunkMinY + 1);

        foreach (var layer in map.Layers)
        {
            if (!layer.Collision)
            {
                continue;
            }
            // Through the chunks under the rectangle, or through the layer's chunks when it has fewer: a huge rectangle reads
            // no more chunks than the layer has, and a small one no more than it covers
            if (chunksUnder <= layer.Chunks.Count)
            {
                for (var y = chunkMinY; y <= chunkMaxY; y++)
                {
                    for (var x = chunkMinX; x <= chunkMaxX; x++)
                    {
                        if (layer.GetChunk(new Int2((int)x, (int)y)) is { } chunk && HasSolid(map, chunk, cells))
                        {
                            return true;
                        }
                    }
                }
            }
            else
            {
                foreach (var chunk in layer.Chunks)
                {
                    if (HasSolid(map, chunk, cells))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    // Whether the cells of a chunk inside a range of cells have a solid tile
    private static bool HasSolid(TileMap map, TileChunk chunk, (long FirstX, long LastX, long FirstY, long LastY) cells)
    {
        var (chunkX, chunkY) = ((long)chunk.Position.X * TileChunk.Size, (long)chunk.Position.Y * TileChunk.Size);
        var (fromX, toX) = (Math.Max(cells.FirstX, chunkX), Math.Min(cells.LastX, chunkX + TileChunk.Size - 1));
        var (fromY, toY) = (Math.Max(cells.FirstY, chunkY), Math.Min(cells.LastY, chunkY + TileChunk.Size - 1));
        for (var y = fromY; y <= toY; y++)
        {
            for (var x = fromX; x <= toX; x++)
            {
                var cell = chunk.Cells[(int)((y - chunkY) * TileChunk.Size + (x - chunkX))];
                if (!cell.IsEmpty && map.GetTile(cell) is { IsSolid: true })
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static int FloorToInt(double value)
        => double.IsNaN(value) ? 0 : (int)Math.Clamp(Math.Floor(value), int.MinValue, int.MaxValue);
}
