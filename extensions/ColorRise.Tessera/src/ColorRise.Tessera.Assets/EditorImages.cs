using Stride.Core.Assets.Editor.Annotations;
using ColorRise.Tessera;
using ColorRise.Tessera.Assets;

[assembly: StaticThumbnail(typeof(TileSetAsset), "TileSetThumbnail.png")]
[assembly: StaticThumbnail(typeof(TileMapAsset), "TileMapThumbnail.png")]
[assembly: StaticThumbnail(typeof(FlipbookAsset), "FlipbookThumbnail.png")]
[assembly: TypeImage(typeof(TileMapComponent), "TileMapComponent.png")]
[assembly: TypeImage(typeof(FlipbookComponent), "FlipbookComponent.png")]

namespace ColorRise.Tessera.Assets;

/// <summary>
/// The images Tessera shows in Game Studio, declared on this assembly: the thumbnails of its assets, and the icons of the
/// tile map component and the flipbook component. <c>Resources/draw-icons.py</c> draws them.
/// </summary>
public static class EditorImages
{
}
