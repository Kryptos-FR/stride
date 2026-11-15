using System.Text;
using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Assets.TextAccessors;

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

    private static ReadOnlySpan<byte> Utf8ByteOrderMark => [0xEF, 0xBB, 0xBF];

    private bool? hasByteOrderMark;

    /// <summary>
    /// Whether the file starts with a UTF-8 byte order mark. A file keeps what it came with, and a new file has none, as
    /// Yarn Spinner's tools write them.
    /// </summary>
    [DataMember(10)]
    [Display(Browsable = false)]
    public bool HasByteOrderMark
    {
        get => hasByteOrderMark ??= FileStartsWithByteOrderMark();
        set => hasByteOrderMark = value;
    }

    /// <summary>
    /// Writes the text as UTF-8, with a byte order mark only when the file had one.
    /// </summary>
    public override void Save(Stream stream)
    {
        // Read before anything is written: the file being saved may be the one the answer comes from
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: HasByteOrderMark);
        using var writer = new StreamWriter(stream, encoding, bufferSize: 1024, leaveOpen: true);
        writer.Write(Text);
    }

    private bool FileStartsWithByteOrderMark()
    {
        if ((TextAccessor as DefaultTextAccessor)?.FilePath is not { } path)
            return false;

        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            Span<byte> start = stackalloc byte[3];
            return file.ReadAtLeast(start, start.Length, throwOnEndOfStream: false) == start.Length && start.SequenceEqual(Utf8ByteOrderMark);
        }
        catch (IOException)
        {
            return false;
        }
    }
}
