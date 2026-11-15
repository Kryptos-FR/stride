using ColorRise.Skein.Assets.Compilation;
using Stride.Core.Assets;

namespace ColorRise.Skein.Assets;

/// <summary>
/// Makes the project a template creates: every script of its folder, and a jump to a node that does not exist is an error.
/// </summary>
public sealed class YarnProjectAssetFactory : AssetFactory<YarnProjectAsset>
{
    /// <summary>
    /// The code Yarn Spinner gives a jump to a node that does not exist; a warning for Yarn Spinner.
    /// </summary>
    public const string MissingNodeCode = "YS0012";

    /// <inheritdoc />
    public override YarnProjectAsset New() => new()
    {
        SourceFiles = { YarnProjectAsset.AllScripts },
        DiagnosticSeverities = { [MissingNodeCode] = YarnSeverity.Error },
    };
}
