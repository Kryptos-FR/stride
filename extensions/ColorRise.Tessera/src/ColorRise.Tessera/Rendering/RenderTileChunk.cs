using Stride.Core.Mathematics;
using Stride.Rendering;

namespace ColorRise.Tessera.Rendering;

/// <summary>
/// One chunk of a visible layer, as the render system sees it: its bounding box lets the chunks out of view be culled, and
/// its centre, at the layer's z, orders it among the layers, the sprites and the other maps of the transparent stage.
/// </summary>
public sealed class RenderTileChunk : RenderObject
{
    internal RenderTileChunk(TileMapComponent component, TileLayer layer, TileChunk chunk)
    {
        Component = component;
        Layer = layer;
        Chunk = chunk;
        Source = component;
    }

    /// <summary>
    /// The component whose map the chunk is in.
    /// </summary>
    public TileMapComponent Component { get; }

    /// <summary>
    /// The layer of the chunk.
    /// </summary>
    public TileLayer Layer { get; }

    /// <summary>
    /// The index of the layer in its map, which sets its z.
    /// </summary>
    public int LayerIndex { get; internal set; }

    /// <summary>
    /// The chunk of cells to draw.
    /// </summary>
    public TileChunk Chunk { get; internal set; }

    /// <summary>
    /// The entity's world matrix in the frame being drawn.
    /// </summary>
    internal Matrix World;

    /// <summary>
    /// The frame in which the processor last saw the chunk, to remove the render objects of chunks that are gone.
    /// </summary>
    internal int Frame;
}
