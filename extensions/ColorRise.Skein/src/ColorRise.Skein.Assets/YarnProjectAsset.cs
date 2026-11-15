using ColorRise.Skein.Assets.Compilation;
using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.IO;

namespace ColorRise.Skein.Assets;

/// <summary>
/// A Yarn project: the scripts that compile together into one <see cref="YarnProject"/>, their languages, and how they are
/// compiled. It is what a dialogue runner plays.
/// </summary>
[DataContract("YarnProjectAsset")]
[AssetDescription(FileExtension)]
[AssetContentType(typeof(YarnProject))]
[AssetFormatVersion(SkeinAssetFormat.Name, SkeinAssetFormat.CurrentVersion)]
[Display(1090, "Yarn project")]
public sealed class YarnProjectAsset : Asset
{
    /// <summary>
    /// The extension of Yarn project assets.
    /// </summary>
    public const string FileExtension = ".sdyarn";

    /// <summary>
    /// The pattern of a new project: every script of the project's folder and of the folders under it.
    /// </summary>
    public const string AllScripts = "**/*.yarn";

    /// <summary>
    /// The scripts of the project, as glob patterns relative to the folder of this asset, with the meaning they have in a
    /// <c>.yarnproject</c> file.
    /// </summary>
    /// <userdoc>The scripts of the project: glob patterns relative to the folder of this asset. **/*.yarn takes every script of the folder and of the folders under it; Chatter/*.yarn takes the scripts of one folder.</userdoc>
    [DataMember(10)]
    [Display("Source files")]
    public List<string> SourceFiles { get; } = [];

    /// <summary>
    /// The scripts the patterns of <see cref="SourceFiles"/> match that the project leaves out.
    /// </summary>
    /// <userdoc>Glob patterns of the scripts to leave out of the project, relative to the folder of this asset.</userdoc>
    [DataMember(20)]
    [Display("Excluded files")]
    public List<string> ExcludedFiles { get; } = [];

    /// <summary>
    /// The language the scripts are written in.
    /// </summary>
    /// <userdoc>The language the scripts are written in, as a language code (en, fr, pt-BR).</userdoc>
    [DataMember(30)]
    [Display("Base language")]
    public string BaseLanguage { get; set; } = "en";

    /// <summary>
    /// The other languages of the project, each with its strings file.
    /// </summary>
    /// <userdoc>The other languages of the project. Each has a strings file (CSV, Yarn Spinner's format) with the translation of every line.</userdoc>
    [DataMember(40)]
    [Display("Localisations")]
    public List<YarnLocalisation> Localisations { get; } = [];

    /// <summary>
    /// How "Add line ids" names the ids it adds.
    /// </summary>
    /// <userdoc>How "Add line ids" names the ids it adds to the lines: random (line:0a1b2c3) or descriptive (line:Tavern_Odile_1).</userdoc>
    [DataMember(50)]
    [Display("Line id style")]
    public YarnLineIdStyle LineIdStyle { get; set; }

    /// <summary>
    /// How the runners of the project choose among the node groups and line groups that can run, unless a runner says its own.
    /// </summary>
    /// <userdoc>How the game picks among the lines of a line group, or the nodes of a node group, that can run. A dialogue runner can choose another.</userdoc>
    [DataMember(60)]
    [Display("Saliency")]
    public YarnSaliency Saliency { get; set; } = YarnSaliency.RandomBestLeastRecentlyViewed;

    /// <summary>
    /// The severity of the compiler's messages, by their code (<c>YS0012</c>), where the project wants another than
    /// Yarn Spinner's.
    /// </summary>
    /// <userdoc>Changes how serious a message of the Yarn compiler is, by its code (YS0012). Error fails the build, None hides the message.</userdoc>
    [DataMember(70)]
    [Display("Severities")]
    public Dictionary<string, YarnSeverity> DiagnosticSeverities { get; } = [];

    /// <summary>
    /// Whether Skein keeps a <c>.yarnproject</c> file and a definitions file beside this asset, for Yarn Spinner's own tools.
    /// </summary>
    /// <userdoc>Keeps a .yarnproject file and a definitions file (.ysls.json) beside this asset, so that Yarn Spinner's VS Code extension and command line see the same project.</userdoc>
    [DataMember(80)]
    [Display("Yarn project file")]
    public bool WriteYarnProjectFile { get; set; } = true;
}

/// <summary>
/// A language of a Yarn project other than its base language.
/// </summary>
[DataContract("YarnLocalisation")]
public sealed class YarnLocalisation
{
    /// <summary>
    /// The language, as a language code (<c>fr</c>, <c>pt-BR</c>).
    /// </summary>
    /// <userdoc>The language, as a language code (fr, pt-BR).</userdoc>
    [DataMember(10)]
    public string Language { get; set; } = "";

    /// <summary>
    /// The strings file of the language: a CSV file in Yarn Spinner's format.
    /// </summary>
    /// <userdoc>The strings file of the language: a CSV file in Yarn Spinner's format, made by "Export strings".</userdoc>
    [DataMember(20)]
    [SourceFileMember(false)]
    [Display("Strings file")]
    public UFile? Strings { get; set; }
}

/// <summary>
/// How the ids of lines are named when they are added.
/// </summary>
[DataContract("YarnLineIdStyle")]
public enum YarnLineIdStyle
{
    /// <summary>
    /// Short random ids (<c>line:0a1b2c3</c>).
    /// </summary>
    Random,

    /// <summary>
    /// Ids made of the node, the speaker and a number (<c>line:Tavern_Odile_1</c>).
    /// </summary>
    Descriptive,
}
