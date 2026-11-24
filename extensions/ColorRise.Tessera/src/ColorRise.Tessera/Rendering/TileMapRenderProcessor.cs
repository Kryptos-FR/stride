using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering;

namespace ColorRise.Tessera.Rendering;

/// <summary>
/// Keeps one <see cref="RenderTileChunk"/> in the visibility group for each chunk of each visible layer of the enabled tile
/// map components, and updates them each frame.
/// </summary>
public sealed class TileMapRenderProcessor : EntityProcessor<TileMapComponent, TileMapRenderProcessor.MapState>, IEntityComponentRenderProcessor
{
    public TileMapRenderProcessor()
        : base(typeof(TransformComponent))
    {
    }

    public VisibilityGroup VisibilityGroup { get; set; } = null!;

    public override void Draw(RenderContext context)
    {
        foreach (var (component, state) in ComponentDatas)
        {
            state.Frame++;
            if (component.Enabled && component.Map is { } map)
            {
                Update(component, map, state);
            }
            state.RemoveUnseen(VisibilityGroup);
        }
    }

    protected override MapState GenerateComponentData(Entity entity, TileMapComponent component) => new(component);

    protected override bool IsAssociatedDataValid(Entity entity, TileMapComponent component, MapState associatedData)
        => associatedData.Component == component;

    protected override void OnEntityComponentRemoved(Entity entity, TileMapComponent component, MapState data)
        => data.RemoveAll(VisibilityGroup);

    private void Update(TileMapComponent component, TileMap map, MapState state)
    {
        var world = component.Entity.Transform.WorldMatrix;
        var cellSize = map.CellSize;
        var reach = Reach(map);
        for (var index = 0; index < map.Layers.Count; index++)
        {
            var layer = map.Layers[index];
            if (!layer.Visible)
            {
                continue;
            }

            var z = index * component.LayerSpacing;
            foreach (var chunk in layer.Chunks)
            {
                var isNew = !state.Chunks.TryGetValue((layer, chunk.Position), out var render);
                render ??= new RenderTileChunk(component, layer, chunk);
                render.Chunk = chunk;
                render.LayerIndex = index;
                render.World = world;
                render.RenderGroup = component.RenderGroup;
                render.Frame = state.Frame;
                render.BoundingBox = Bounds(world, cellSize, layer.Offset, z, chunk.Position, reach);
                if (isNew)
                {
                    // Added once its render group is set: the stage selectors read it when it is added
                    state.Chunks.Add((layer, chunk.Position), render);
                    VisibilityGroup.RenderObjects.Add(render);
                }
            }
        }
    }

    // The box of the chunk's cells at the layer's z, made larger up and to the right by how far the largest tile reaches
    // beyond its cell, then turned into the world
    private static BoundingBoxExt Bounds(in Matrix world, Vector2 cellSize, Vector2 offset, float z, Int2 chunk, Vector2 reach)
    {
        var (left, bottom) = (chunk.X * (float)TileChunk.Size * cellSize.X + offset.X, chunk.Y * (float)TileChunk.Size * cellSize.Y + offset.Y);
        var (right, top) = (left + TileChunk.Size * cellSize.X + reach.X, bottom + TileChunk.Size * cellSize.Y + reach.Y);
        var box = new BoundingBoxExt(new Vector3(left, bottom, z), new Vector3(right, top, z));
        box.Transform(world);
        return box;
    }

    // How far the largest tile of the map's tile sets goes beyond a cell, up and to the right
    private static Vector2 Reach(TileMap map)
    {
        var reach = Vector2.Zero;
        foreach (var set in map.TileSets.Values)
        {
            if (!(set.PixelsPerUnit > 0))
            {
                continue;
            }
            foreach (var tile in set.Tiles.Values)
            {
                reach = Vector2.Max(reach, new Vector2(tile.Region.Width, tile.Region.Height) / set.PixelsPerUnit - map.CellSize);
            }
        }
        return reach;
    }

    /// <summary>
    /// The render objects of the chunks of one component.
    /// </summary>
    public sealed class MapState
    {
        private readonly List<(TileLayer, Int2)> unseen = [];

        internal MapState(TileMapComponent component) => Component = component;

        internal TileMapComponent Component { get; }

        internal Dictionary<(TileLayer Layer, Int2 Position), RenderTileChunk> Chunks { get; } = [];

        internal int Frame { get; set; }

        // Removes the render objects of the chunks that this frame did not see: removed chunks and layers, hidden layers, a
        // disabled component or a map changed or gone
        internal void RemoveUnseen(VisibilityGroup visibilityGroup)
        {
            foreach (var (key, render) in Chunks)
            {
                if (render.Frame != Frame)
                {
                    unseen.Add(key);
                }
            }
            foreach (var key in unseen)
            {
                visibilityGroup.RenderObjects.Remove(Chunks[key]);
                Chunks.Remove(key);
            }
            unseen.Clear();
        }

        internal void RemoveAll(VisibilityGroup visibilityGroup)
        {
            foreach (var render in Chunks.Values)
            {
                visibilityGroup.RenderObjects.Remove(render);
            }
            Chunks.Clear();
        }
    }
}
