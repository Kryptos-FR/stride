using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.ViewModels;
using Stride.Assets.Sprite;
using Stride.Core.Assets;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;

namespace ColorRise.Tessera.Editor.Pictures;

/// <summary>
/// The sources of one tile set asset, made again when the asset, a sprite sheet it uses, or (on <see cref="Refresh"/>) a
/// file changes. Read and refreshed on the session's dispatcher.
/// </summary>
public sealed class TileSetPictures
{
    // The width of a row of a sheet's boxes, and the gap between two boxes, in canvas units
    private const int SheetRowWidth = 256;
    private const int SheetGap = 2;

    private readonly TilePictureService service;
    // Replaced, never changed: read on the threads that raise the session's asset changes
    private volatile IReadOnlySet<AssetId> sheets = new HashSet<AssetId>();
    private bool detached;

    internal TileSetPictures(TilePictureService service, TileSetViewModel tileSet)
    {
        this.service = service;
        TileSet = tileSet;
        Build();
        tileSet.Session.AssetPropertiesChanged += AssetPropertiesChanged;
    }

    /// <summary>
    /// The tile set asset.
    /// </summary>
    public TileSetViewModel TileSet { get; }

    /// <summary>
    /// Its sources, in the order of the asset.
    /// </summary>
    public IReadOnlyList<TileSourceLayout> Sources { get; private set; } = [];

    /// <summary>
    /// The largest tile, in pixels; the tile size of the set when it has no tile with a picture.
    /// </summary>
    public Int2 LargestTile { get; private set; }

    /// <summary>
    /// Raised when the pictures were made again.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// The slot of a tile, in the first source that has it, or null.
    /// </summary>
    public TileSlot? Find(TileId tile)
    {
        foreach (var layout in Sources)
        {
            if (layout.SlotOf(tile) is { } slot)
            {
                return slot;
            }
        }
        return null;
    }

    /// <summary>
    /// Makes the pictures again, from the files as they are now.
    /// </summary>
    public void Refresh()
    {
        Build();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void Detach()
    {
        detached = true;
        TileSet.Session.AssetPropertiesChanged -= AssetPropertiesChanged;
    }

    // Raised on a thread of the pool, once the session's history recorded a change, an undo or a redo
    private void AssetPropertiesChanged(object? sender, AssetChangedEventArgs e)
    {
        var watched = sheets;
        if (e.Assets.Any(asset => asset == TileSet || watched.Contains(asset.Id)))
        {
            _ = TileSet.Dispatcher.InvokeAsync(() =>
            {
                if (!detached)
                {
                    Refresh();
                }
            });
        }
    }

    private void Build()
    {
        var asset = TileSet.Asset;
        Color? key = asset.ColorKeyEnabled ? asset.ColorKeyColor : null;
        // Paths on disk, as the build resolves them: relative to the folder of the asset that names them
        var folder = TileSet.AssetItem.FullPath.GetParent();
        var watched = new HashSet<AssetId>();
        var layouts = new List<TileSourceLayout>();
        foreach (var source in asset.Sources)
        {
            layouts.Add(source switch
            {
                GridTileSetSource grid => Grid(grid, asset, folder, key),
                SpriteSheetTileSetSource sheet => Sheet(sheet, asset, key, watched),
                _ => new TileSourceLayout(source, false, Int2.Zero, null, $"The source '{source.Name}' is of a kind the editors do not know.", [], []),
            });
        }
        sheets = watched;
        Sources = layouts;
        var drawn = layouts.SelectMany(layout => layout.Slots).Where(slot => !slot.IsMissing).ToList();
        LargestTile = drawn.Count == 0 ? asset.TileSize : new Int2(drawn.Max(slot => slot.Region.Width), drawn.Max(slot => slot.Region.Height));
    }

    private TileSourceLayout Grid(GridTileSetSource source, TileSetAsset asset, UDirectory folder, Color? key)
    {
        if (source.Image is null || string.IsNullOrEmpty(source.Image.FullPath))
        {
            return new TileSourceLayout(source, true, Int2.Zero, null, $"The source '{source.Name}' has no image.", [], []);
        }
        if (service.Picture(UPath.Combine(folder, source.Image), key, out var problem) is not { } image)
        {
            return new TileSourceLayout(source, true, Int2.Zero, null, problem, [], []);
        }
        // The tiles the build makes: the positions of the grid that are not empty once the color key is applied
        var slots = TileSlicer.Grid(source, asset.TileSize, new Int2(image.Width, image.Height))
            .Where(slice => !TileSlicer.IsEmpty(image.Pixels, image.Width, slice.Region, key))
            .Select(slice => new TileSlot(slice.Tile, slice.Region, image, slice.Region, null))
            .ToList();
        return new TileSourceLayout(source, true, new Int2(image.Width, image.Height), image, null, slots, []);
    }

    private TileSourceLayout Sheet(SpriteSheetTileSetSource source, TileSetAsset asset, Color? key, HashSet<AssetId> watched)
    {
        if (source.Sheet is null || AttachedReferenceManager.GetAttachedReference(source.Sheet) is not { } reference)
        {
            return new TileSourceLayout(source, false, Int2.Zero, null, $"The source '{source.Name}' has no sprite sheet.", [], []);
        }
        watched.Add(reference.Id);
        if (TileSet.Session.GetAssetById(reference.Id) is not { IsDeleted: false, Asset: SpriteSheetAsset sheet } sheetViewModel)
        {
            return new TileSourceLayout(source, false, Int2.Zero, null, $"The sprite sheet of the source '{source.Name}' is not in the project.", [], []);
        }

        var sheetFolder = sheetViewModel.AssetItem.FullPath.GetParent();
        var sprites = sheet.Sprites.Where(sprite => sprite.Source is not null && !string.IsNullOrEmpty(sprite.Source.FullPath))
            .Select(sprite => new NamedRegion(sprite.Name, UPath.Combine(sheetFolder, sprite.Source), sprite.TextureRegion))
            .ToList();
        var bySlice = TileSlicer.Sheet(source, sprites, out _, out _, out _).GroupBy(slice => slice.Tile).ToDictionary(group => group.Key, group => group.First());

        var problems = new List<string>();
        var boxes = new List<(TileId Tile, Int2 Size, TilePicture? Picture, Rectangle Region, string SpriteName)>();
        var seen = new HashSet<TileId>();
        foreach (var binding in source.Bindings.OrderBy(binding => binding.Number))
        {
            if (source.Id is < 1 or > TileId.MaxSource || binding.Number is < 0 or > TileId.MaxNumber)
            {
                continue;
            }
            var tile = TileId.FromNumber(source.Id, binding.Number);
            if (!seen.Add(tile))
            {
                continue;
            }
            if (bySlice.TryGetValue(tile, out var slice))
            {
                var picture = service.Picture(slice.Image, key, out var problem);
                if (problem is not null && !problems.Contains(problem))
                {
                    problems.Add(problem);
                }
                boxes.Add((tile, new Int2(Math.Max(slice.Region.Width, 1), Math.Max(slice.Region.Height, 1)), picture, slice.Region, binding.SpriteName));
            }
            else
            {
                // A sprite that is gone keeps a box of the size of a tile, where the palette marks it
                var size = new Int2(asset.TileSize.X > 0 ? asset.TileSize.X : 16, asset.TileSize.Y > 0 ? asset.TileSize.Y : 16);
                boxes.Add((tile, size, null, Rectangle.Empty, binding.SpriteName));
            }
        }

        var (slots, layoutSize) = Flow(boxes);
        var bound = source.Bindings.Select(binding => binding.SpriteName).ToHashSet(StringComparer.Ordinal);
        var unbound = sheet.Sprites.Select(sprite => sprite.Name).Where(name => !bound.Contains(name)).Distinct(StringComparer.Ordinal).ToList();
        return new TileSourceLayout(source, false, layoutSize, null, problems.Count > 0 ? string.Join(" ", problems) : null, slots, unbound);
    }

    // Puts the boxes in rows, left to right, a row as wide as the widest box when that is wider than a row
    private static (List<TileSlot> Slots, Int2 Size) Flow(List<(TileId Tile, Int2 Size, TilePicture? Picture, Rectangle Region, string SpriteName)> boxes)
    {
        var limit = Math.Max(SheetRowWidth, boxes.Count == 0 ? 0 : boxes.Max(box => box.Size.X));
        var (x, y, rowHeight, width) = (0, 0, 0, 0);
        var slots = new List<TileSlot>();
        foreach (var box in boxes)
        {
            if (x > 0 && x + box.Size.X > limit)
            {
                (x, y, rowHeight) = (0, y + rowHeight + SheetGap, 0);
            }
            var bounds = new Rectangle(x, y, box.Size.X, box.Size.Y);
            slots.Add(new TileSlot(box.Tile, bounds, box.Picture, box.Region, box.SpriteName));
            x += box.Size.X + SheetGap;
            rowHeight = Math.Max(rowHeight, box.Size.Y);
            width = Math.Max(width, bounds.Right);
        }
        return (slots, new Int2(width, y + rowHeight));
    }
}
