using Stride.Core;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;

namespace ColorRise.Skein;

/// <summary>
/// The lines of a Yarn project in one language.
/// </summary>
[DataContract("YarnStringTable")]
[ContentSerializer(typeof(DataContentSerializer<YarnStringTable>))]
[ReferenceSerializer, DataSerializerGlobal(typeof(ReferenceSerializer<YarnStringTable>), Profile = "Content")]
public sealed class YarnStringTable
{
    /// <summary>
    /// The language of the texts (<c>en</c>, <c>fr</c>, <c>pt-BR</c>).
    /// </summary>
    public string Language { get; set; } = "";

    /// <summary>
    /// The text of each line, by line id, with its markup and its <c>{0}</c> placeholders.
    /// </summary>
    public Dictionary<string, string> Strings { get; } = [];
}
