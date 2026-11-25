using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Rendering;

/// <summary>
/// A quad given by its four corners in world space, with the texture coordinates of each corner, from 0 to 1.
/// </summary>
/// <remarks>Two quads that share corners of the same value leave no gap between them on screen.</remarks>
public struct TileQuad
{
    /// <summary>
    /// The bottom-left corner, in the world.
    /// </summary>
    public Vector3 BottomLeft;
    /// <summary>
    /// The bottom-right corner, in the world.
    /// </summary>
    public Vector3 BottomRight;
    /// <summary>
    /// The top-right corner, in the world.
    /// </summary>
    public Vector3 TopRight;
    /// <summary>
    /// The top-left corner, in the world.
    /// </summary>
    public Vector3 TopLeft;
    /// <summary>
    /// The texture coordinates of the bottom-left corner.
    /// </summary>
    public Vector2 UvBottomLeft;
    /// <summary>
    /// The texture coordinates of the bottom-right corner.
    /// </summary>
    public Vector2 UvBottomRight;
    /// <summary>
    /// The texture coordinates of the top-right corner.
    /// </summary>
    public Vector2 UvTopRight;
    /// <summary>
    /// The texture coordinates of the top-left corner.
    /// </summary>
    public Vector2 UvTopLeft;
}
