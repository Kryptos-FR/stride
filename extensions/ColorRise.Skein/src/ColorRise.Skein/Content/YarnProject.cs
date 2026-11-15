using Stride.Core;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;

namespace ColorRise.Skein;

/// <summary>
/// A compiled Yarn project, what a game loads and a dialogue runner runs: the program, what the nodes and the lines say of
/// themselves, and the languages. The text of each language is a <see cref="YarnStringTable"/> of its own, loaded when the
/// language is the current one.
/// </summary>
[DataContract("YarnProject")]
[ContentSerializer(typeof(DataContentSerializer<YarnProject>))]
[ReferenceSerializer, DataSerializerGlobal(typeof(ReferenceSerializer<YarnProject>), Profile = "Content")]
public sealed class YarnProject
{
    private Yarn.Program? program;

    /// <summary>
    /// The program Yarn Spinner's compiler made, in its own binary format.
    /// </summary>
    public byte[] Program { get; set; } = [];

    /// <summary>
    /// The nodes, in the order of the scripts.
    /// </summary>
    public List<YarnNodeInfo> Nodes { get; } = [];

    /// <summary>
    /// The hashtags of the lines that have some (<c>#shout</c>), by line id.
    /// </summary>
    public Dictionary<string, List<string>> LineMetadata { get; } = [];

    /// <summary>
    /// The lines that repeat another line (<c>#shadow:</c>): from their id to the id of the line whose text they take.
    /// </summary>
    public Dictionary<string, string> ShadowLines { get; } = [];

    /// <summary>
    /// The language the scripts are written in.
    /// </summary>
    public string BaseLanguage { get; set; } = "en";

    /// <summary>
    /// The languages that have a string table, the base language first.
    /// </summary>
    public List<string> Languages { get; } = [];

    /// <summary>
    /// How the runners of the project pick among the nodes of a group, unless a runner says otherwise.
    /// </summary>
    public YarnSaliency Saliency { get; set; } = YarnSaliency.RandomBestLeastRecentlyViewed;

    /// <summary>
    /// The program, read once from <see cref="Program"/>.
    /// </summary>
    public Yarn.Program GetProgram() => program ??= Yarn.Program.Parser.ParseFrom(Program);

    /// <summary>
    /// The URL of the string table of a language, from the URL of its project.
    /// </summary>
    public static string StringTableUrl(string projectUrl, string language) => $"{projectUrl}/strings/{language}";
}
