using ColorRise.Skein.Assets;
using Stride.Assets.Editor.Templates;
using Stride.Core.Assets;
using Stride.Core.Assets.Templates;
using Stride.Core.IO;

namespace ColorRise.Skein.Editor.Templates;

/// <summary>
/// Creates a Yarn project and, in a folder where the project would have no script, a first script with a node
/// <c>Start</c>: a new project then has something to play.
/// </summary>
public sealed class YarnProjectTemplateGenerator : AssetFactoryTemplateGenerator
{
    /// <summary>
    /// The name of the first script.
    /// </summary>
    public const string FirstScriptName = "Start";

    /// <inheritdoc/>
    public override bool IsSupportingTemplate(TemplateDescription templateDescription)
    {
        ArgumentNullException.ThrowIfNull(templateDescription);
        return templateDescription is TemplateAssetFactoryDescription { FactoryTypeName: nameof(YarnProjectAssetFactory) };
    }

    /// <inheritdoc/>
    protected override IEnumerable<AssetItem> CreateAssets(AssetTemplateGeneratorParameters parameters)
    {
        foreach (var item in base.CreateAssets(parameters))
        {
            yield return item;

            if (item.Asset is YarnProjectAsset project && YarnProjectFiles.ScriptsOf(parameters.Package, item.Location, project).Count == 0)
            {
                var folder = item.Location.GetFullDirectory();
                yield return new AssetItem(UPath.Combine(folder ?? new UDirectory(""), new UFile(FirstScriptName)), new YarnScriptAssetFactory().New());
            }
        }
    }
}
