using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;

namespace ColorRise.Tessera.Tests.Graphics;

/// <summary>
/// How a test game draws: the size of its frame, its background and its colour space.
/// </summary>
internal sealed class HarnessOptions
{
    public int Width { get; init; } = 64;

    public int Height { get; init; } = 64;

    public Color Background { get; init; } = new(255, 0, 255, 255);

    /// <summary>
    /// Gamma by default, so that flat colours come back exactly as they were given.
    /// </summary>
    public ColorSpace ColorSpace { get; init; } = ColorSpace.Gamma;

    public int Msaa { get; init; } = 1;

    /// <summary>
    /// The sort mode of the transparent stage; null for the engine's <see cref="BackToFrontSortMode"/>.
    /// </summary>
    public SortMode? TransparentSortMode { get; init; }

    /// <summary>
    /// Draws in place of the scene, after the clear. The matrix is the camera's view times its projection.
    /// </summary>
    public Action<RenderDrawContext, Matrix>? CustomDraw { get; init; }
}
