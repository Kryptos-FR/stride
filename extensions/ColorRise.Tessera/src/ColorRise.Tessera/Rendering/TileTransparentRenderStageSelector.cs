using System.ComponentModel;
using Stride.Core;
using Stride.Engine;
using Stride.Rendering;

namespace ColorRise.Tessera.Rendering;

/// <summary>
/// Puts the chunks of tile maps in the transparent stage, where they are sorted by depth with the sprites: tiles have
/// edges that blend, and the order of the layers is the order of their z.
/// </summary>
[DataContract("TileTransparentRenderStageSelector")]
[Display("Tile maps in the transparent stage")]
public sealed class TileTransparentRenderStageSelector : RenderStageSelector
{
    private const string EffectName = "TileMap";

    [DefaultValue(null)]
    public RenderStage? TransparentRenderStage { get; set; }

    [DefaultValue(RenderGroupMask.All)]
    public RenderGroupMask RenderGroup { get; set; } = RenderGroupMask.All;

    public override void Process(RenderObject renderObject)
    {
        if (TransparentRenderStage is { } stage && ((RenderGroupMask)(1U << (int)renderObject.RenderGroup) & RenderGroup) != 0)
        {
            // The effect name is not read: the feature draws with its own batch
            renderObject.ActiveRenderStages[stage.Index] = new ActiveRenderStage(EffectName);
        }
    }
}
