using Stride.Assets;
using Stride.Assets.Sprite;
using Stride.Assets.Textures;
using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Assets.Analysis;
using Stride.Core.Assets.Compiler;
using Stride.Core.BuildEngine;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using Stride.Graphics;
using Stride.TextureConverter;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// Compiles a tile set asset to a <see cref="TileSet"/> at the asset's URL, whose tiles are packed into textures of its own
/// at <c>&lt;url&gt;__ATLAS_TEXTURE__&lt;n&gt;</c>, each tile with a border of two pixels that repeats its edge.
/// </summary>
/// <remarks>
/// An image that cannot be read fails the build. A source without a tile, sprites that are bound and missing, sprites that are
/// not bound, a sprite name used twice, and an animation frame of a tile the set does not have are warnings.
/// </remarks>
[AssetCompiler(typeof(TileSetAsset), typeof(AssetCompilationContext))]
public sealed class TileSetAssetCompiler : AssetCompilerBase
{
    /// <summary>
    /// The largest width and height of a texture of tiles.
    /// </summary>
    public const int MaxTextureSize = 2048;

    /// <summary>
    /// The sprite sheets of sheet sources: the compiler reads their sprites, not their compiled content.
    /// </summary>
    public override IEnumerable<BuildDependencyInfo> GetInputTypes(AssetItem assetItem)
    {
        yield return new BuildDependencyInfo(typeof(SpriteSheetAsset), typeof(AssetCompilationContext), BuildDependencyType.CompileAsset);
    }

    protected override void Prepare(AssetCompilerContext context, AssetItem assetItem, string targetUrlInStorage, AssetCompilerResult result)
    {
        var asset = (TileSetAsset)assetItem.Asset;
        var settings = context.GetGameSettingsAsset();
        var parameters = new TileSetBuildParameters
        {
            Asset = asset,
            Platform = context.Platform,
            GraphicsPlatform = context.GetGraphicsPlatform(assetItem.Package),
            GraphicsProfile = settings.GetOrCreate<RenderingSettings>().DefaultGraphicsProfile,
            TextureQuality = settings.GetOrCreate<TextureSettings>().TextureQuality,
            ColorSpace = context.GetColorSpace(),
        };

        // Paths on disk, as sprite sheets resolve theirs: relative to the folder of the asset that names them
        var folder = assetItem.FullPath.GetParent();
        foreach (var source in asset.Sources)
        {
            switch (source)
            {
                case GridTileSetSource { Image: { } image } grid when !string.IsNullOrEmpty(image.FullPath):
                    parameters.GridImages[grid.Id] = UPath.Combine(folder, image);
                    break;
                case SpriteSheetTileSetSource { Sheet: { } sheet } sheetSource:
                    if (assetItem.Package?.FindAssetFromProxyObject(sheet) is not { Asset: SpriteSheetAsset sheetAsset } sheetItem)
                    {
                        result.Warning($"The sprite sheet of the source '{sheetSource.Name}' is not in the project.");
                        break;
                    }
                    var sheetFolder = sheetItem.FullPath.GetParent();
                    foreach (var sprite in sheetAsset.Sprites.Where(sprite => sprite.Source is not null && !string.IsNullOrEmpty(sprite.Source.FullPath)))
                    {
                        parameters.Sprites.Add(new TileSetBuildSprite
                        {
                            Source = sheetSource.Id,
                            Name = sprite.Name,
                            Image = UPath.Combine(sheetFolder, sprite.Source),
                            Region = sprite.TextureRegion,
                        });
                    }
                    break;
            }
        }

        result.BuildSteps = new AssetBuildStep(assetItem);
        result.BuildSteps.Add(new TileSetCommand(targetUrlInStorage, parameters, assetItem.Package!));
    }

    // The build engine serializes a command only to run it in another process, which this one never asks for
#pragma warning disable STRDIAG010
    private sealed class TileSetCommand : AssetCommand<TileSetBuildParameters>
#pragma warning restore STRDIAG010
    {
        public TileSetCommand(string url, TileSetBuildParameters parameters, IAssetFinder assetFinder)
            : base(url, parameters, assetFinder)
        {
            // To raise when the content of a tile set changes for the same asset
            Version = 2;
        }

        /// <summary>
        /// The images, whose bytes the build compares to know whether to run the command again.
        /// </summary>
        public override IEnumerable<ObjectUrl> GetInputFiles()
        {
            var images = Parameters.GridImages.Values.Concat(Parameters.Sprites.Select(sprite => sprite.Image));
            foreach (var image in images.Distinct().Where(image => File.Exists(image)))
            {
                yield return new ObjectUrl(UrlType.File, image);
            }
        }

        protected override Task<ResultStatus> DoCommandOverride(ICommandContext commandContext)
        {
            var asset = Parameters.Asset;
            var logger = commandContext.Logger;
            var srgb = Import(Url).IsSRgb;
            using var tool = new TextureTool();
            var images = new Dictionary<UFile, Image>();
            try
            {
                var unreadable = false;
                Image? Read(UFile path, string sourceName)
                {
                    if (!images.TryGetValue(path, out var image))
                    {
                        try
                        {
                            image = LoadImage(tool, path, srgb);
                        }
                        catch (Exception exception)
                        {
                            logger.Error($"{Url}: the image '{path}' of the source '{sourceName}' cannot be read: {exception.Message}");
                            unreadable = true;
                            return null;
                        }
                        images[path] = image;
                    }
                    return image;
                }

                var slices = new List<TileSlice>();
                foreach (var source in asset.Sources)
                {
                    switch (source)
                    {
                        case GridTileSetSource grid when Parameters.GridImages.TryGetValue(grid.Id, out var path):
                            if (Read(path, grid.Name) is { } image)
                            {
                                slices.AddRange(TileSlicer.Grid(grid, asset.TileSize, new Int2(image.Description.Width, image.Description.Height)));
                            }
                            break;
                        case SpriteSheetTileSetSource sheet:
                            slices.AddRange(SheetSlices(sheet, Read, logger));
                            break;
                    }
                }
                if (unreadable)
                {
                    return Task.FromResult(ResultStatus.Failed);
                }

                var colorKey = asset.ColorKeyEnabled ? asset.ColorKeyColor : (Color?)null;
                TileAtlas atlas;
                try
                {
                    atlas = TileAtlasBuilder.Build(slices, path => images[path], colorKey, MaxTextureSize, srgb);
                }
                catch (ArgumentException exception)
                {
                    logger.Error($"{Url}: {exception.Message}");
                    return Task.FromResult(ResultStatus.Failed);
                }

                using (atlas)
                {
                    foreach (var source in asset.Sources.Where(source => !atlas.Placements.Keys.Any(tile => tile.Source == source.Id)))
                    {
                        logger.Warning($"{Url}: the source '{source.Name}' gives no tile.");
                    }
                    return Task.FromResult(Save(atlas, tool, logger));
                }
            }
            finally
            {
                foreach (var image in images.Values)
                {
                    image.Dispose();
                }
            }
        }

        // The tiles of a sheet source, from the sprites the compiler read in its sheet, and the warnings of its bindings
        private IEnumerable<TileSlice> SheetSlices(SpriteSheetTileSetSource sheet, Func<UFile, string, Image?> read, ILogger logger)
        {
            var sprites = Parameters.Sprites
                .Where(sprite => sprite.Source == sheet.Id && read(sprite.Image, sheet.Name) is not null)
                .Select(sprite => new NamedRegion(sprite.Name, sprite.Image, sprite.Region))
                .ToList();
            var slices = TileSlicer.Sheet(sheet, sprites, out var missing, out var duplicates, out _);

            if (missing.Count > 0)
            {
                logger.Warning($"{Url}: {Count(missing.Count, "sprite")} of the source '{sheet.Name}' {(missing.Count == 1 ? "is" : "are")} bound to tiles and missing from its sprite sheet: {string.Join(", ", missing.Select(binding => binding.SpriteName))}.");
            }
            var unbound = sprites.Select(sprite => sprite.Name).Distinct().Where(name => !sheet.Bindings.Any(binding => binding.SpriteName == name)).ToList();
            if (unbound.Count > 0)
            {
                logger.Warning($"{Url}: {Count(unbound.Count, "sprite")} of the source '{sheet.Name}' {(unbound.Count == 1 ? "is" : "are")} not bound to a tile: {string.Join(", ", unbound)}.");
            }
            if (duplicates.Count > 0)
            {
                logger.Warning($"{Url}: {Count(duplicates.Count, "sprite name")} of the source '{sheet.Name}' {(duplicates.Count == 1 ? "is" : "are")} used twice or more, and each gives its first sprite: {string.Join(", ", duplicates)}.");
            }
            return slices;
        }

        // The textures of the atlas, then the tile set that names them
        private ResultStatus Save(TileAtlas atlas, TextureTool tool, Logger logger)
        {
            var content = new ContentManager(MicrothreadLocalDatabases.ProviderService);
            var textures = new List<Texture>();
            for (var i = 0; i < atlas.Textures.Count; i++)
            {
                var import = Import($"{Url}__ATLAS_TEXTURE__{i}");
                using var texImage = tool.Load(atlas.Textures[i], import.IsSRgb);
                var status = TextureHelper.ImportTextureImage(content, tool, texImage, import, CancellationToken, logger);
                if (status != ResultStatus.Successful)
                {
                    return status;
                }
                textures.Add(AttachedReferenceManager.CreateProxyObject<Texture>(AssetId.Empty, import.OutputUrl));
            }

            var asset = Parameters.Asset;
            // The definitions by tile, in any order: the first of a tile is kept
            var definitions = new Dictionary<TileId, TileDefinition>();
            var twice = new SortedSet<TileId>();
            foreach (var definition in asset.Tiles)
            {
                if (!definitions.TryAdd(definition.Tile, definition))
                {
                    twice.Add(definition.Tile);
                }
            }
            if (twice.Count > 0)
            {
                logger.Warning($"{Url}: {Count(twice.Count, "tile")} {(twice.Count == 1 ? "has" : "have")} data twice or more, and the first is kept: {string.Join(", ", twice)}.");
            }
            var absent = definitions.Values.Where(definition => !definition.IsDefault && !atlas.Placements.ContainsKey(definition.Tile))
                .Select(definition => definition.Tile).Order().ToList();
            if (absent.Count > 0)
            {
                logger.Warning($"{Url}: the data of {Count(absent.Count, "tile")} is left out, as the set does not have {(absent.Count == 1 ? "it" : "them")}: {string.Join(", ", absent)}.");
            }

            var set = new TileSet { PixelsPerUnit = asset.PixelsPerUnit, TileSize = asset.TileSize, PremultipliedAlpha = asset.PremultiplyAlpha };
            foreach (var (id, (texture, region)) in atlas.Placements.OrderBy(placement => placement.Key))
            {
                var tile = new Tile { Id = id, Texture = textures[texture], Region = region };
                if (definitions.TryGetValue(id, out var definition))
                {
                    (tile.Collision, tile.Kind) = (definition.Collision, string.IsNullOrEmpty(definition.Kind) ? null : definition.Kind);
                    foreach (var (name, property) in definition.Properties)
                    {
                        tile.Properties[name] = property.GetValue();
                    }
                    tile.Animation.AddRange(definition.Animation);
                }
                set.Tiles[id] = tile;
            }

            var gone = set.Tiles.Values
                .SelectMany(tile => tile.Animation.Where(frame => !set.Tiles.ContainsKey(frame.Tile)).Select(frame => $"{frame.Tile} (in {tile.Id})"))
                .ToList();
            if (gone.Count > 0)
            {
                logger.Warning($"{Url}: {Count(gone.Count, "animation frame")} {(gone.Count == 1 ? "names a tile" : "name tiles")} the set does not have: {string.Join(", ", gone)}.");
            }

            content.Save(Url, set);
            return ResultStatus.Successful;
        }

        // How the build writes a texture of tiles: the asset's own texture settings, without mipmaps, which would mix
        // neighbouring tiles, and without the color key, which the atlas has already applied
        private TextureHelper.ImportParameters Import(string url)
        {
            var asset = Parameters.Asset;
            var texture = new TextureAsset
            {
                Id = AssetId.Empty,
                IsCompressed = asset.IsCompressed,
                GenerateMipmaps = false,
                IsStreamable = false,
                Type = new ColorTextureType
                {
                    Alpha = asset.Alpha,
                    PremultiplyAlpha = asset.PremultiplyAlpha,
                    UseSRgbSampling = asset.UseSRgbSampling,
                    ColorKeyEnabled = false,
                },
            };
            var convert = new TextureConvertParameters(new UFile(url), texture, Parameters.Platform, Parameters.GraphicsPlatform,
                Parameters.GraphicsProfile, Parameters.TextureQuality, Parameters.ColorSpace);
            return new TextureHelper.ImportParameters(convert) { OutputUrl = url };
        }

        // An image of a source, in R8G8B8A8 as the atlas reads it
        private static Image LoadImage(TextureTool tool, UFile path, bool srgb)
        {
            using var texImage = tool.Load(path, srgb);
            tool.Decompress(texImage, srgb);
            if (texImage.Format is PixelFormat.B8G8R8A8_UNorm or PixelFormat.B8G8R8A8_UNorm_SRgb)
            {
                tool.SwitchChannel(texImage);
            }
            if (texImage.Format is not (PixelFormat.R8G8B8A8_UNorm or PixelFormat.R8G8B8A8_UNorm_SRgb))
            {
                tool.Convert(texImage, srgb ? PixelFormat.R8G8B8A8_UNorm_SRgb : PixelFormat.R8G8B8A8_UNorm);
            }
            return tool.ConvertToStrideImage(texImage);
        }

        private static string Count(int count, string thing) => count == 1 ? $"1 {thing}" : $"{count} {thing}s";
    }
}

/// <summary>
/// What the tile set command needs, and what it compares between two builds: the asset, the images on disk, the sprites of
/// the sheets, and the settings of the textures.
/// </summary>
[DataContract]
internal sealed class TileSetBuildParameters
{
    public TileSetAsset Asset { get; set; } = null!;

    /// <summary>
    /// The image of each grid source, by the source's id, as a path on disk.
    /// </summary>
    public Dictionary<int, UFile> GridImages { get; } = [];

    /// <summary>
    /// The sprites of the sprite sheets of sheet sources.
    /// </summary>
    public List<TileSetBuildSprite> Sprites { get; } = [];

    public PlatformType Platform { get; set; }

    public GraphicsPlatform GraphicsPlatform { get; set; }

    public GraphicsProfile GraphicsProfile { get; set; }

    public TextureQuality TextureQuality { get; set; }

    public ColorSpace ColorSpace { get; set; }
}

/// <summary>
/// A sprite of a sprite sheet, as a sheet source of a tile set reads it.
/// </summary>
[DataContract]
internal sealed class TileSetBuildSprite
{
    /// <summary>
    /// The id of the sheet source.
    /// </summary>
    public int Source { get; set; }

    public string Name { get; set; } = "";

    /// <summary>
    /// The sprite's image, as a path on disk.
    /// </summary>
    public UFile Image { get; set; } = null!;

    public Rectangle Region { get; set; }
}
