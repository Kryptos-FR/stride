using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.ViewModels;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Quantum;
using Stride.Graphics;

namespace ColorRise.Tessera.Editor.TileSetEditor;

/// <summary>
/// Every change the editors make to a tile set asset, through its graph, so that the history can undo it. The caller owns
/// the transaction.
/// </summary>
internal static class TileSetGraph
{
    /// <summary>
    /// Adds a source over an image, with a new id. A tile size equal to the tile set's is stored as (0, 0): the source then
    /// follows the tile set's.
    /// </summary>
    public static GridTileSetSource AddImage(TileSetViewModel tileSet, string name, UFile image, Int2 tileSize, Int2 margin, Int2 spacing)
    {
        var source = new GridTileSetSource
        {
            Name = name,
            Image = image,
            TileSize = tileSize == tileSet.Asset.TileSize ? Int2.Zero : tileSize,
            Margin = margin,
            Spacing = spacing,
        };
        AddSource(tileSet, source);
        return source;
    }

    /// <summary>
    /// Adds a source over a sprite sheet, with a new id, and each sprite name bound to a number in the order given.
    /// </summary>
    public static SpriteSheetTileSetSource AddSheet(TileSetViewModel tileSet, string name, SpriteSheet reference, IEnumerable<string> spriteNames)
    {
        var source = new SpriteSheetTileSetSource { Name = name, Sheet = reference };
        // Not in the asset yet: the bindings go in with the source, in its step
        TileSlicer.Bind(source, spriteNames);
        AddSource(tileSet, source);
        return source;
    }

    /// <summary>
    /// Removes a source and the definitions of its tiles; the definitions stay when another source has the same id.
    /// </summary>
    public static void RemoveSource(TileSetViewModel tileSet, TileSetSource source)
    {
        var asset = tileSet.Asset;
        var index = asset.Sources.IndexOf(source);
        if (index < 0)
        {
            return;
        }
        if (!asset.Sources.Any(other => other != source && other.Id == source.Id))
        {
            var tiles = Root(tileSet)[nameof(TileSetAsset.Tiles)].Target!;
            // From the end, so that the indices that remain are still right
            for (var definition = asset.Tiles.Count - 1; definition >= 0; definition--)
            {
                if (asset.Tiles[definition].Tile.Source == source.Id)
                {
                    tiles.Remove(asset.Tiles[definition], new NodeIndex(definition));
                }
            }
        }
        Root(tileSet)[nameof(TileSetAsset.Sources)].Target!.Remove(source, new NodeIndex(index));
    }

    /// <summary>
    /// Binds each sprite name that the source does not bind to a new number, in the order given; gives how many were bound.
    /// </summary>
    public static int BindSprites(TileSetViewModel tileSet, SpriteSheetTileSetSource source, IEnumerable<string> spriteNames)
    {
        var index = tileSet.Asset.Sources.IndexOf(source);
        if (index < 0)
        {
            return 0;
        }
        var node = Root(tileSet)[nameof(TileSetAsset.Sources)].Target!.IndexedTarget(new NodeIndex(index))!;
        var bindings = node[nameof(SpriteSheetTileSetSource.Bindings)].Target!;
        var bound = source.Bindings.Select(binding => binding.SpriteName).ToHashSet(StringComparer.Ordinal);
        var number = source.LastNumber;
        var count = 0;
        foreach (var name in spriteNames)
        {
            if (!bound.Add(name))
            {
                continue;
            }
            if (number >= TileId.MaxNumber)
            {
                throw new InvalidOperationException($"The source '{source.Name}' has given all {TileId.MaxNumber} numbers of tiles.");
            }
            bindings.Add(new SpriteTileBinding { Number = ++number, SpriteName = name });
            count++;
        }
        if (count > 0)
        {
            node[nameof(SpriteSheetTileSetSource.LastNumber)].Update(number);
        }
        return count;
    }

    // Gives the source the id after the last one given, and writes that id, so that an undo gives it back
    private static void AddSource(TileSetViewModel tileSet, TileSetSource source)
    {
        var id = tileSet.Asset.LastSourceId + 1;
        if (id > TileId.MaxSource)
        {
            throw new InvalidOperationException($"The tile set has given all {TileId.MaxSource} ids of sources: make another tile set for more images.");
        }
        source.Id = id;
        Root(tileSet)[nameof(TileSetAsset.LastSourceId)].Update(id);
        Root(tileSet)[nameof(TileSetAsset.Sources)].Target!.Add(source);
    }

    private static IObjectNode Root(TileSetViewModel tileSet) => tileSet.PropertyGraph.RootNode;
}
