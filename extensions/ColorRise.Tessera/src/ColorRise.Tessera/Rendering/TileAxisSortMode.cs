using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Rendering;

namespace ColorRise.Tessera.Rendering;

/// <summary>
/// Sorts a stage back to front along one axis of the world, whatever the tilt of the camera: flat things (tile layers,
/// sprites, flipbooks) that lie across the axis keep the order of their positions along it.
/// </summary>
/// <remarks>
/// The key has the layout of <see cref="BackToFrontSortMode"/>: the render feature's key in the top 8 bits, the depth in the
/// next 32, and the top 24 bits of the render object's state key last, so that the other features and the state key keep
/// their meaning. Everything in the stage sorts along the axis, 3D objects too.
/// </remarks>
[DataContract("TileAxisSortMode")]
[Display("Back to front along an axis")]
public sealed class TileAxisSortMode : SortMode
{
    /// <summary>
    /// The axis the flat things are stacked along. Z by default: maps and sprites in the XY plane. A zero axis is taken as Z.
    /// </summary>
    /// <userdoc>The axis the flat things of the stage are stacked along: Z for maps and sprites in the XY plane.</userdoc>
    [DataMember(10)]
    public Vector3 Axis { get; set; } = Vector3.UnitZ;

    /// <summary>
    /// Gives each render node a key that draws the node farthest along the axis, as seen from the camera, first.
    /// </summary>
    public override unsafe void GenerateSortKey(RenderView renderView, RenderViewStage renderViewStage, SortKey* sortKeys)
    {
        Matrix.Invert(ref renderView.View, out var viewInverse);
        var axis = Axis.LengthSquared() > 0 ? Vector3.Normalize(Axis) : Vector3.UnitZ;
        // The side the camera looks at: the depth grows away from the camera
        var away = Vector3.Dot(axis, viewInverse.Forward) >= 0 ? axis : -axis;

        var renderNodes = renderViewStage.RenderNodes;
        for (var i = 0; i < renderNodes.Count; i++)
        {
            var renderNode = renderNodes[i];
            var renderObject = renderNode.RenderObject;
            var depth = ~SortModeDistance.ComputeDistance(Vector3.Dot(renderObject.BoundingBox.Center, away));
            sortKeys[i] = new SortKey
            {
                Value = ((ulong)renderNode.RootRenderFeature.SortKey << 56) | ((ulong)depth << 24) | (renderObject.StateSortKey >> 8),
                Index = i,
                StableIndex = renderObject.Index,
            };
        }
    }
}
