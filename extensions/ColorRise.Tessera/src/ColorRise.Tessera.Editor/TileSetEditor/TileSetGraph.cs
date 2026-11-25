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

    /// <summary>
    /// Puts the data of a tile in the asset: it replaces the tile's definition, goes in at its place in the order of the tiles
    /// when the tile has none, or removes the tile's definition when it says nothing. The definition goes in the asset as it
    /// is, so it is one that nothing else holds. Nothing changes when the asset has the same data.
    /// </summary>
    public static void SetDefinition(TileSetViewModel tileSet, TileDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var asset = tileSet.Asset;
        var tiles = Root(tileSet)[nameof(TileSetAsset.Tiles)].Target!;
        var index = asset.Tiles.FindIndex(existing => existing.Tile == definition.Tile);
        if (index >= 0)
        {
            if (definition.IsDefault)
            {
                tiles.Remove(asset.Tiles[index], new NodeIndex(index));
            }
            else if (!HaveSameData(asset.Tiles[index], definition))
            {
                tiles.Update(definition, new NodeIndex(index));
            }
        }
        else if (!definition.IsDefault)
        {
            tiles.Add(definition, new NodeIndex(PlaceOf(asset, definition.Tile)));
        }
    }

    /// <summary>
    /// Writes the frames of a tile and keeps its other data. A definition left with nothing is removed; a tile without one
    /// gets one, at its place in the order of the tiles. Only the frames that differ are written: the frames the old and the
    /// new list share at their start and at their end stay as they are.
    /// </summary>
    public static void SetAnimation(TileSetViewModel tileSet, TileId tile, IReadOnlyList<TileAnimationFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        var asset = tileSet.Asset;
        var tiles = Root(tileSet)[nameof(TileSetAsset.Tiles)].Target!;
        var index = asset.Tiles.FindIndex(existing => existing.Tile == tile);
        if (index < 0)
        {
            if (frames.Count > 0)
            {
                var added = new TileDefinition { Tile = tile };
                added.Animation.AddRange(frames);
                tiles.Add(added, new NodeIndex(PlaceOf(asset, tile)));
            }
            return;
        }
        var definition = asset.Tiles[index];
        if (frames.Count == 0 && definition.Collision == TileCollision.None && string.IsNullOrEmpty(definition.Kind) && definition.Properties.Count == 0)
        {
            tiles.Remove(definition, new NodeIndex(index));
            return;
        }
        var old = definition.Animation;
        var animation = tiles.IndexedTarget(new NodeIndex(index))![nameof(TileDefinition.Animation)].Target!;
        var start = 0;
        while (start < old.Count && start < frames.Count && old[start].Equals(frames[start]))
        {
            start++;
        }
        var end = 0;
        while (end < old.Count - start && end < frames.Count - start && old[old.Count - 1 - end].Equals(frames[frames.Count - 1 - end]))
        {
            end++;
        }
        var (oldMiddle, newMiddle) = (old.Count - start - end, frames.Count - start - end);
        var updated = Math.Min(oldMiddle, newMiddle);
        for (var i = 0; i < updated; i++)
        {
            animation.Update(frames[start + i], new NodeIndex(start + i));
        }
        for (var i = updated; i < newMiddle; i++)
        {
            animation.Add(frames[start + i], new NodeIndex(start + i));
        }
        // From the end, so that the indices that remain are still right
        for (var i = oldMiddle - 1; i >= updated; i--)
        {
            animation.Remove(old[start + i], new NodeIndex(start + i));
        }
    }

    /// <summary>
    /// Sets or clears the solid flag of a tile, and keeps its other data; false when the tile already was so. A definition
    /// that says nothing more goes away.
    /// </summary>
    public static bool SetSolid(TileSetViewModel tileSet, TileId tile, bool solid)
    {
        var asset = tileSet.Asset;
        var index = asset.Tiles.FindIndex(existing => existing.Tile == tile);
        var collision = index >= 0 ? asset.Tiles[index].Collision : TileCollision.None;
        if (collision.HasFlag(TileCollision.Solid) == solid)
        {
            return false;
        }
        var tiles = Root(tileSet)[nameof(TileSetAsset.Tiles)].Target!;
        collision = solid ? collision | TileCollision.Solid : collision & ~TileCollision.Solid;
        if (index < 0)
        {
            tiles.Add(new TileDefinition { Tile = tile, Collision = collision }, new NodeIndex(PlaceOf(asset, tile)));
            return true;
        }
        var definition = asset.Tiles[index];
        if (collision == TileCollision.None && string.IsNullOrEmpty(definition.Kind) && definition.Properties.Count == 0 && definition.Animation.Count == 0)
        {
            tiles.Remove(definition, new NodeIndex(index));
        }
        else
        {
            tiles.IndexedTarget(new NodeIndex(index))![nameof(TileDefinition.Collision)].Update(collision);
        }
        return true;
    }

    /// <summary>
    /// Gives the sprite of the tile <paramref name="fromNumber"/> to the tile <paramref name="missingNumber"/> of a sheet
    /// source: the binding of the missing tile names the sprite, and the other binding and the data of its tile go away.
    /// </summary>
    public static void Rebind(TileSetViewModel tileSet, SpriteSheetTileSetSource source, int missingNumber, int fromNumber)
    {
        var asset = tileSet.Asset;
        var sourceIndex = asset.Sources.IndexOf(source);
        var missing = source.Bindings.FindIndex(binding => binding.Number == missingNumber);
        var from = source.Bindings.FindIndex(binding => binding.Number == fromNumber);
        if (sourceIndex < 0 || missing < 0 || from < 0 || missing == from)
        {
            return;
        }
        var bindings = Root(tileSet)[nameof(TileSetAsset.Sources)].Target!.IndexedTarget(new NodeIndex(sourceIndex))![nameof(SpriteSheetTileSetSource.Bindings)].Target!;
        bindings.IndexedTarget(new NodeIndex(missing))![nameof(SpriteTileBinding.SpriteName)].Update(source.Bindings[from].SpriteName);
        bindings.Remove(source.Bindings[from], new NodeIndex(from));
        var gone = TileId.FromNumber(source.Id, fromNumber);
        var tiles = Root(tileSet)[nameof(TileSetAsset.Tiles)].Target!;
        // From the end, so that the indices that remain are still right
        for (var definition = asset.Tiles.Count - 1; definition >= 0; definition--)
        {
            if (asset.Tiles[definition].Tile == gone)
            {
                tiles.Remove(asset.Tiles[definition], new NodeIndex(definition));
            }
        }
    }

    /// <summary>
    /// Whether two definitions give a tile the same data: collision, kind, properties of the same types and values, and
    /// animation. The tile they are for is not compared.
    /// </summary>
    public static bool HaveSameData(TileDefinition first, TileDefinition second)
    {
        if (first.Collision != second.Collision || (first.Kind ?? "") != (second.Kind ?? "") || first.Properties.Count != second.Properties.Count)
        {
            return false;
        }
        foreach (var (name, property) in first.Properties)
        {
            if (!second.Properties.TryGetValue(name, out var other) || property?.GetType() != other?.GetType() || !Equals(property?.GetValue(), other?.GetValue()))
            {
                return false;
            }
        }
        return first.Animation.SequenceEqual(second.Animation);
    }

    // Where a definition of the tile goes in the list: before the first definition of a tile that comes after it
    private static int PlaceOf(TileSetAsset asset, TileId tile)
    {
        var place = asset.Tiles.FindIndex(existing => existing.Tile.CompareTo(tile) > 0);
        return place < 0 ? asset.Tiles.Count : place;
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
