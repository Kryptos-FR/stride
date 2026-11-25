using ColorRise.Tessera;
using Stride.Core.Serialization;

// The asset files of Tessera and the types their compiled content loads as, for the asset URL constants of games
[assembly: AssetFileExtension(".sdtileset", typeof(TileSet))]
[assembly: AssetFileExtension(".sdtilemap", typeof(TileMap))]
[assembly: AssetFileExtension(".sdflipbook", typeof(Flipbook))]
