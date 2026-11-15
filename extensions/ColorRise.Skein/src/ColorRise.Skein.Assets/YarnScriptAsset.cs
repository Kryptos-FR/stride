using Stride.Core;
using Stride.Core.Assets;

namespace ColorRise.Skein.Assets;

/// <summary>
/// A Yarn script: a <c>.yarn</c> text file of an asset folder. The file is the asset: it holds Yarn text and nothing else, so
/// that Yarn Spinner's own tools read and write it. It has no compiled content of its own; the Yarn project that includes it
/// compiles it.
/// </summary>
[DataContract("YarnScriptAsset")]
[AssetDescription(FileExtension, AllowArchetype = false)]
[Display(1100, "Yarn script")]
public sealed class YarnScriptAsset : SourceCodeAsset
{
    /// <summary>
    /// The extension of Yarn scripts, which is Yarn Spinner's own.
    /// </summary>
    public const string FileExtension = ".yarn";
}
