using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;

namespace ColorRise.Tessera.Rendering;

/// <summary>
/// Draws the chunks of tile maps in the render stage that its selector picks, in the order of the stage: in the transparent
/// stage, by depth, so that the layers, the sprites and the other maps go one over the other by their z.
/// </summary>
[DataContract("TileRenderFeature")]
[Display("Tile maps")]
public sealed class TileRenderFeature : RootRenderFeature
{
    private readonly Lock countLock = new();
    private ThreadLocal<TileBatch>? batches;

    /// <summary>
    /// The feature draws the chunks of tile maps.
    /// </summary>
    public override Type SupportedRenderObjectType => typeof(RenderTileChunk);

    /// <summary>
    /// The chunks drawn in the last frame, after culling, for the tests.
    /// </summary>
    internal int DrawnChunks { get; private set; }

    /// <summary>
    /// Makes a batch for each thread that draws.
    /// </summary>
    protected override void InitializeCore()
    {
        base.InitializeCore();
        batches = new ThreadLocal<TileBatch>(() => new TileBatch(Context.GraphicsDevice), trackAllValues: true);
    }

    /// <summary>
    /// Disposes the batches.
    /// </summary>
    protected override void Destroy()
    {
        if (batches is not null)
        {
            foreach (var batch in batches.Values)
            {
                batch.Dispose();
            }
            batches.Dispose();
            batches = null;
        }
        base.Destroy();
    }

    /// <summary>
    /// Counts the chunks drawn again from 0 for the new frame.
    /// </summary>
    public override void Extract()
    {
        base.Extract();
        DrawnChunks = 0;
    }

    /// <summary>
    /// Draws the chunks of the stage between two indices, in the stage's order.
    /// </summary>
    public override void Draw(RenderDrawContext context, RenderView renderView, RenderViewStage renderViewStage, int startIndex, int endIndex)
    {
        base.Draw(context, renderView, renderViewStage, startIndex, endIndex);
        var run = new BatchRun(batches!.Value!, context, renderView.ViewProjection, Rasterizer(RenderSystem.RenderStages[renderViewStage.Index].Output));
        Span<Vector2> uvs = stackalloc Vector2[4];

        for (var index = startIndex; index < endIndex; index++)
        {
            var render = (RenderTileChunk)GetRenderNode(renderViewStage.SortedRenderNodes[index].RenderNode).RenderObject;
            if (render.Component.Map is not { } map)
            {
                continue;
            }
            DrawChunk(ref run, render, map, ChunkColor(render.Layer, render.Component.Color), uvs);
        }
        run.End();

        lock (countLock)
        {
            DrawnChunks += endIndex - startIndex;
        }
    }

    // The layer's tint with its alpha times the opacity, times the component's colour
    private static Color4 ChunkColor(TileLayer layer, Color4 componentColor)
    {
        var tint = layer.Tint.ToColor4();
        tint.A *= Math.Clamp(layer.Opacity, 0, 1);
        return tint * componentColor;
    }

    // Multisampled outputs take a rasterizer of their sample count, as the sprites do
    private static RasterizerStateDescription? Rasterizer(RenderOutputDescription output)
    {
        if (output.MultisampleCount == MultisampleCount.None)
        {
            return null;
        }
        var rasterizer = RasterizerStates.CullNone;
        rasterizer.MultisampleCount = output.MultisampleCount;
        rasterizer.MultisampleAntiAliasLine = true;
        return rasterizer;
    }

    private void DrawChunk(ref BatchRun run, RenderTileChunk render, TileMap map, Color4 color, Span<Vector2> uvs)
    {
        ref readonly var world = ref render.World;
        var cellSize = map.CellSize;
        var offset = render.Layer.Offset;
        var z = render.LayerIndex * render.Component.LayerSpacing;
        var (originX, originY) = (render.Chunk.Position.X * TileChunk.Size, render.Chunk.Position.Y * TileChunk.Size);
        var cells = render.Chunk.Cells;
        var time = render.Component.AnimationTime;
        Texture? lastTexture = null;

        for (var i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            if (cell.IsEmpty || !map.TileSets.TryGetValue(cell.Slot, out var set) || !(set.PixelsPerUnit > 0)
                || set.GetTile(cell.Tile) is not { } own)
            {
                continue;
            }
            // An animated tile shows the frame of the time, from the same set; a frame the set does not have shows the tile
            var tile = own.Animation.Count > 0 && set.GetTile(own.FrameAt(time)) is { } frame ? frame : own;
            if (tile.Texture is not { } texture)
            {
                continue;
            }
            if (texture != lastTexture)
            {
                Context.StreamingManager?.StreamResources(texture);
                lastTexture = texture;
            }

            var (x, y) = (originX + i % TileChunk.Size, originY + i / TileChunk.Size);
            var region = tile.Region;
            var size = new Vector2(region.Width, region.Height) / set.PixelsPerUnit;
            var quad = new TileQuad { BottomLeft = TileQuads.Corner(world, cellSize, offset, z, x, y) };
            if (Fits(size, cellSize))
            {
                quad.BottomRight = TileQuads.Corner(world, cellSize, offset, z, x + 1, y);
                quad.TopRight = TileQuads.Corner(world, cellSize, offset, z, x + 1, y + 1);
                quad.TopLeft = TileQuads.Corner(world, cellSize, offset, z, x, y + 1);
            }
            else
            {
                // A tile of another size than the cell: from the cell's bottom-left corner, up and to the right
                var (left, bottom) = (x * cellSize.X + offset.X, y * cellSize.Y + offset.Y);
                quad.BottomRight = Vector3.TransformCoordinate(new Vector3(left + size.X, bottom, z), world);
                quad.TopRight = Vector3.TransformCoordinate(new Vector3(left + size.X, bottom + size.Y, z), world);
                quad.TopLeft = Vector3.TransformCoordinate(new Vector3(left, bottom + size.Y, z), world);
            }

            // The region is in the pixels of the texture at full quality, which a streamed texture may not have yet
            var fullSize = texture.FullQualitySize;
            TileQuads.Uvs(region, fullSize.Width, fullSize.Height, cell.Flip, uvs);
            (quad.UvBottomLeft, quad.UvBottomRight, quad.UvTopRight, quad.UvTopLeft) = (uvs[0], uvs[1], uvs[2], uvs[3]);
            run.Use(color, render.Component.Sampler, set.PremultipliedAlpha).Draw(texture, quad);
        }
    }

    // One batch for each run of quads of the same colour, sampler and alpha
    private struct BatchRun(TileBatch batch, RenderDrawContext context, Matrix viewProjection, RasterizerStateDescription? rasterizer)
    {
        private bool begun;
        private Color4 color;
        private TileSampler sampler;
        private bool premultipliedAlpha;

        public TileBatch Use(Color4 color, TileSampler sampler, bool premultipliedAlpha)
        {
            if (!begun || color != this.color || sampler != this.sampler || premultipliedAlpha != this.premultipliedAlpha)
            {
                End();
                (this.color, this.sampler, this.premultipliedAlpha) = (color, sampler, premultipliedAlpha);
                var states = context.GraphicsDevice.SamplerStates;
                batch.Begin(context.GraphicsContext, viewProjection, color, sampler == TileSampler.Linear ? states.LinearClamp : states.PointClamp,
                    rasterizer: rasterizer, premultipliedAlpha: premultipliedAlpha);
                begun = true;
            }
            return batch;
        }

        public void End()
        {
            if (begun)
            {
                batch.End();
                begun = false;
            }
        }
    }

    private static bool Fits(Vector2 tileSize, Vector2 cellSize)
        => MathF.Abs(tileSize.X - cellSize.X) <= cellSize.X * 1e-5f && MathF.Abs(tileSize.Y - cellSize.Y) <= cellSize.Y * 1e-5f;
}
