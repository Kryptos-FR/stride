using Stride.Engine;
using Stride.Rendering;

namespace ColorRise.Tessera.Rendering;

/// <summary>
/// Keeps one <see cref="RenderFlipbook"/> in the visibility group for each enabled flipbook component that shows a frame,
/// and updates it before each frame is drawn: its bounds, and the key that orders it by its y among the flipbooks of its
/// depth.
/// </summary>
public sealed class FlipbookRenderProcessor : EntityProcessor<FlipbookComponent, RenderFlipbook>, IEntityComponentRenderProcessor
{
    /// <summary>
    /// Made by the engine.
    /// </summary>
    public FlipbookRenderProcessor()
        : base(typeof(TransformComponent))
    {
    }

    /// <summary>
    /// Where the render objects go; the engine sets it.
    /// </summary>
    public VisibilityGroup VisibilityGroup { get; set; } = null!;

    /// <summary>
    /// Takes the frame each component shows, and adds or removes its render object: it is in the visibility group while
    /// the component is enabled and its frame has a cell that can be drawn.
    /// </summary>
    public override void Draw(RenderContext context)
    {
        foreach (var (component, render) in ComponentDatas)
        {
            var shown = false;
            if (component.Enabled && component.Flipbook is { } flipbook && component.CurrentFrame is { } frame)
            {
                var world = component.Entity.Transform.WorldMatrix;
                if (FlipbookQuads.Bounds(flipbook, frame, world, component.FlipX) is { } bounds)
                {
                    (render.Flipbook, render.Frame, render.World) = (flipbook, frame, world);
                    (render.FlipX, render.Color, render.Sampler) = (component.FlipX, component.Color, component.Sampler);
                    render.RenderGroup = component.RenderGroup;
                    render.BoundingBox = bounds;
                    // Among the flipbooks of one depth, the higher is drawn first, so that the lower goes over it
                    render.StateSortKey = component.SortByY ? FlipbookSortKey.FromY(world.TranslationVector.Y) : 0;
                    shown = true;
                }
            }

            if (shown != render.IsAdded)
            {
                // Added once its render group is set: the stage selectors read it when it is added
                render.IsAdded = shown;
                if (shown)
                {
                    VisibilityGroup.RenderObjects.Add(render);
                }
                else
                {
                    VisibilityGroup.RenderObjects.Remove(render);
                }
            }
        }
    }

    /// <summary>
    /// The render object of a component.
    /// </summary>
    protected override RenderFlipbook GenerateComponentData(Entity entity, FlipbookComponent component) => new(component);

    /// <summary>
    /// Whether the render object is the one of this component.
    /// </summary>
    protected override bool IsAssociatedDataValid(Entity entity, FlipbookComponent component, RenderFlipbook associatedData)
        => associatedData.Component == component;

    /// <summary>
    /// Removes the render object of a component taken out of the scene.
    /// </summary>
    protected override void OnEntityComponentRemoved(Entity entity, FlipbookComponent component, RenderFlipbook data)
    {
        if (data.IsAdded)
        {
            data.IsAdded = false;
            VisibilityGroup.RenderObjects.Remove(data);
        }
    }
}
