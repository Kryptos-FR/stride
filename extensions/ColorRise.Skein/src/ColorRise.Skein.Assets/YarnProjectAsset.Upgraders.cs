using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Reflection;
using Stride.Core.Yaml;
using Stride.Core.Yaml.Serialization;

namespace ColorRise.Skein.Assets;

public sealed partial class YarnProjectAsset
{
    /// <summary>
    /// Makes the character colours of a project of format 1.0, a list of names with their colour, a dictionary from the name
    /// to the colour. A name that is empty, or that an earlier item has already in another case, is left out, as Game Studio
    /// left it out before.
    /// </summary>
    private sealed class CharacterColorsToDictionaryUpgrader : AssetUpgraderBase
    {
        protected override void UpgradeAsset(AssetMigrationContext context, PackageVersion currentVersion, PackageVersion targetVersion, dynamic asset, PackageLoadingAssetFile assetFile, OverrideUpgraderHint overrideHint)
        {
            if (asset.CharacterColors is not DynamicYamlMapping list)
                return;

            var colors = new YamlMappingNode();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in list.Node.Children.Values.OfType<YamlMappingNode>())
            {
                var name = (item.Children.TryGetValue(new YamlScalarNode("Name"), out var value) ? (value as YamlScalarNode)?.Value : null)?.Trim();
                if (string.IsNullOrEmpty(name) || !names.Add(name))
                    continue;

                // An item that never had its colour changed has none written: it is the default colour of an item
                var color = item.Children.TryGetValue(new YamlScalarNode("Color"), out var written)
                    ? written
                    : new YamlMappingNode { { "R", "0.56" }, { "G", "0.78" }, { "B", "0.91" } };
                colors.Children.Add(new YamlScalarNode($"{ItemId.New()}~{name}"), color);
            }

            asset.CharacterColors = new DynamicYamlMapping(colors);
        }
    }
}
