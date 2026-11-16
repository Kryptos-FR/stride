using ColorRise.Skein.Assets;
using ColorRise.Skein;
using Stride.Core.Assets.Editor.Annotations;

[assembly: StaticThumbnail(typeof(YarnScriptAsset), "YarnScriptThumbnail.png")]
[assembly: StaticThumbnail(typeof(YarnProjectAsset), "YarnProjectThumbnail.png")]
[assembly: TypeImage(typeof(DialogueRunnerComponent), "DialogueComponent.png")]
[assembly: TypeImage(typeof(DialoguePresenter), "DialogueComponent.png")]

namespace ColorRise.Skein.Assets;

/// <summary>
/// The images Skein shows in Game Studio, declared on this assembly: the thumbnails of its assets, and the icon of the
/// dialogue runner and of its presenters. <c>Resources/draw-icons.py</c> draws them.
/// </summary>
public static class EditorImages
{
}
