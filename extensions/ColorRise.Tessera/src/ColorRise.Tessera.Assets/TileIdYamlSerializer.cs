using Stride.Core.Assets.Serializers;
using Stride.Core.Yaml;
using Stride.Core.Yaml.Events;
using Stride.Core.Yaml.Serialization;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// Writes a tile id in an asset as its text, such as <c>1:3,4</c> or <c>2:#17</c>.
/// </summary>
[YamlSerializerFactory(YamlAssetProfile.Name)]
internal sealed class TileIdYamlSerializer : AssetScalarSerializerBase
{
    public override bool CanVisit(Type type) => type == typeof(TileId);

    public override object ConvertFrom(ref ObjectContext context, Scalar fromScalar)
    {
        if (fromScalar.Value.Length == 0)
        {
            return default(TileId);
        }
        return TileId.TryParse(fromScalar.Value, out var id)
            ? id
            : throw new YamlException(fromScalar.Start, fromScalar.End, $"'{fromScalar.Value}' is not a tile id: write source:column,row or source:#number, such as 1:3,4 or 2:#17.");
    }

    public override string ConvertTo(ref ObjectContext objectContext) => ((TileId)objectContext.Instance).ToString();
}
