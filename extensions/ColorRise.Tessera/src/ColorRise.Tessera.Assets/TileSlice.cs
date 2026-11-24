using Stride.Core.IO;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// One tile of a source: its id, and its region in an image, in pixels.
/// </summary>
public readonly record struct TileSlice(TileId Tile, UFile Image, Rectangle Region);

/// <summary>
/// A sprite of a sheet: its name, and its region in an image, in pixels.
/// </summary>
public readonly record struct NamedRegion(string Name, UFile Image, Rectangle Region);
