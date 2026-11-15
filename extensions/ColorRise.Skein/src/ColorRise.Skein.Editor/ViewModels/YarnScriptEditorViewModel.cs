using ColorRise.Skein.Editor.Text;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Assets.Editor.ViewModels;

namespace ColorRise.Skein.Editor.ViewModels;

/// <summary>
/// The editor of a Yarn script.
/// </summary>
[AssetEditorViewModel<YarnScriptViewModel>]
public sealed class YarnScriptEditorViewModel(YarnScriptViewModel script) : AssetEditorViewModel<YarnScriptViewModel>(script)
{
    /// <summary>
    /// The script's text.
    /// </summary>
    public YarnTextBuffer TextBuffer => Asset.TextBuffer;
}
