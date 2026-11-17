using Stride.Core;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;

namespace ColorRise.Skein;

/// <summary>
/// The voice-over clips of a Yarn project in one language: the URL of the sound of each line that has one.
/// </summary>
/// <remarks>
/// The clips are URLs and not references, so that a game without audio can load its project: Skein's voice-over presenter
/// loads them as sounds.
/// </remarks>
[DataContract("YarnVoiceTable")]
[ContentSerializer(typeof(DataContentSerializer<YarnVoiceTable>))]
[ReferenceSerializer, DataSerializerGlobal(typeof(ReferenceSerializer<YarnVoiceTable>), Profile = "Content")]
public sealed class YarnVoiceTable
{
    /// <summary>
    /// The language of the clips (<c>en</c>, <c>fr</c>, <c>pt-BR</c>).
    /// </summary>
    public string Language { get; set; } = "";

    /// <summary>
    /// The URL of the sound of each line, by line id.
    /// </summary>
    public Dictionary<string, string> Clips { get; } = [];
}
