using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Rendering;

/// <summary>
/// A quad given by its four corners in world space, with the texture coordinates of each corner, from 0 to 1.
/// </summary>
/// <remarks>Two quads that share corners of the same value leave no gap between them on screen.</remarks>
public struct TileQuad
{
    public Vector3 BottomLeft;
    public Vector3 BottomRight;
    public Vector3 TopRight;
    public Vector3 TopLeft;
    public Vector2 UvBottomLeft;
    public Vector2 UvBottomRight;
    public Vector2 UvTopRight;
    public Vector2 UvTopLeft;
}
