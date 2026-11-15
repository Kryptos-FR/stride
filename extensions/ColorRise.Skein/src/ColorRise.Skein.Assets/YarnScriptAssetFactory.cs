using Stride.Core.Assets;

namespace ColorRise.Skein.Assets;

/// <summary>
/// Makes the script a template creates: one node to start from.
/// </summary>
public sealed class YarnScriptAssetFactory : AssetFactory<YarnScriptAsset>
{
    /// <summary>
    /// The text of a new script.
    /// </summary>
    public const string StarterText = "title: Start\nposition: 0,0\n---\nNarrator: Write your first line here.\n===\n";

    /// <inheritdoc />
    public override YarnScriptAsset New() => new() { Text = StarterText };
}
