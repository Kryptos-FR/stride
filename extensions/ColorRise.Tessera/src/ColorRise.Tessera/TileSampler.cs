namespace ColorRise.Tessera;

/// <summary>
/// How the pixels of tiles are read: point for sharp pixel art, linear for smooth art.
/// </summary>
public enum TileSampler
{
    /// <summary>
    /// The nearest pixel: sharp pixel art.
    /// </summary>
    Point,
    /// <summary>
    /// A blend of the four nearest pixels: smooth art.
    /// </summary>
    Linear,
}
