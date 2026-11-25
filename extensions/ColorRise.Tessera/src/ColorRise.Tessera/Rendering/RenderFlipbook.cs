using Stride.Core.Mathematics;
using Stride.Rendering;

namespace ColorRise.Tessera.Rendering;

/// <summary>
/// The frame a flipbook component shows, as the render system sees it: its bounding box lets it be culled, and its centre, at
/// the entity's z, orders it among the layers, the sprites and the other flipbooks of the transparent stage.
/// </summary>
public sealed class RenderFlipbook : RenderObject
{
    internal RenderFlipbook(FlipbookComponent component)
    {
        Component = component;
        Source = component;
    }

    /// <summary>
    /// The component whose frame is drawn.
    /// </summary>
    public FlipbookComponent Component { get; }

    /// <summary>
    /// The flipbook of the frame drawn, as the processor saw it before the frame.
    /// </summary>
    internal Flipbook? Flipbook;

    /// <summary>
    /// The frame drawn.
    /// </summary>
    internal FlipbookFrame? Frame;

    /// <summary>
    /// The entity's world matrix in the frame being drawn.
    /// </summary>
    internal Matrix World;

    /// <summary>
    /// The component's mirror, colour and sampler in the frame being drawn.
    /// </summary>
    internal bool FlipX;

    /// <inheritdoc cref="FlipX"/>
    internal Color4 Color;

    /// <inheritdoc cref="FlipX"/>
    internal TileSampler Sampler;

    /// <summary>
    /// Whether the render object is in the visibility group.
    /// </summary>
    internal bool IsAdded;
}
