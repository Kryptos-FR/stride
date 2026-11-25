using System.Reflection;
using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.ViewModels;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// The two assets have view models of their own, templates in "Add asset", and images in Game Studio.
/// </summary>
public sealed class TemplateTests
{
    [Fact]
    public async Task A_tile_set_and_a_map_have_view_models_of_their_own()
    {
        await using var session = await TesseraSession.OpenAsync(("Tiles/Town", new TileSetAsset()), ("Maps/Town", new TileMapAssetFactory().New()));
        Assert.IsType<TileSetViewModel>(session.Session.AllAssets.Single(a => a.Name == "Town" && a.Asset is TileSetAsset));
        Assert.IsType<TileMapViewModel>(session.Session.AllAssets.Single(a => a.Asset is TileMapAsset));
    }

    [Fact]
    public async Task The_tile_map_template_makes_a_map_with_one_layer()
    {
        await using var session = await TesseraSession.OpenAsync();
        var template = session.Template("TileMap");
        Assert.Equal(("Tile map", "Sprite", "TileMap"), (template.Name, template.Group, template.DefaultOutputName));

        var first = Assert.IsType<TileMapViewModel>(Assert.Single(await session.RunTemplateAsync(template, "Maps")));
        Assert.Equal("/TestGame/Maps/TileMap", first.Url);
        Assert.Equal(["Ground"], first.Asset.Layers.Select(layer => layer.Name));
        Assert.Empty(first.Asset.TileSets);

        var second = Assert.Single(await session.RunTemplateAsync(template, "Maps"));
        Assert.Equal("/TestGame/Maps/TileMap_2", second.Url);
    }

    [Fact]
    public async Task The_tile_set_template_makes_an_empty_tile_set()
    {
        await using var session = await TesseraSession.OpenAsync();
        var template = session.Template("TileSet");
        Assert.Equal(("Tile set", "Sprite", "TileSet"), (template.Name, template.Group, template.DefaultOutputName));

        var tileSet = Assert.IsType<TileSetViewModel>(Assert.Single(await session.RunTemplateAsync(template, "Tiles")));
        Assert.Equal("/TestGame/Tiles/TileSet", tileSet.Url);
        Assert.Empty(tileSet.Asset.Sources);
        Assert.Equal(new Int2(16, 16), tileSet.Asset.TileSize);
        Assert.Equal(16, tileSet.Asset.PixelsPerUnit);
    }

    [Fact]
    public void The_package_file_lists_both_templates()
    {
        var package = File.ReadAllText(TesseraSession.SourceFile("src/ColorRise.Tessera.Assets/ColorRise.Tessera.Assets.sdpkg"));
        Assert.Contains("Templates/Assets/TileSet.sdtpl", package, StringComparison.Ordinal);
        Assert.Contains("Templates/Assets/TileMap.sdtpl", package, StringComparison.Ordinal);
        foreach (var file in (string[])["TileSet.sdtpl", "TileMap.sdtpl", ".sdtpl/TileSet.png", ".sdtpl/TileMap.png"])
        {
            Assert.True(File.Exists(TesseraSession.SourceFile($"src/ColorRise.Tessera.Assets/Templates/Assets/{file}")), file);
        }
    }

    [Fact]
    public void The_images_are_declared_and_embedded()
    {
        var assembly = typeof(TileMapAsset).Assembly;
        var thumbnails = assembly.GetCustomAttributes<StaticThumbnailAttribute>().ToDictionary(a => a.AssetType, a => a.ResourceName);
        Assert.Equal("TileSetThumbnail.png", thumbnails[typeof(TileSetAsset)]);
        Assert.Equal("TileMapThumbnail.png", thumbnails[typeof(TileMapAsset)]);
        var icon = Assert.Single(assembly.GetCustomAttributes<TypeImageAttribute>(), a => a.Type == typeof(TileMapComponent));
        foreach (var name in (string[])["TileSetThumbnail.png", "TileMapThumbnail.png", icon.ResourceName])
        {
            Assert.Contains(assembly.GetManifestResourceNames(), resource => resource.EndsWith(name, StringComparison.Ordinal));
        }
    }
}
