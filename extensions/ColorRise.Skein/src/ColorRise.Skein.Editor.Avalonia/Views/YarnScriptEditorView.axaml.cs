using Avalonia.Controls;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Assets.Editor.Editors;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// The view of the Yarn script editor.
/// </summary>
[AssetEditorView<YarnScriptEditorViewModel>]
public sealed partial class YarnScriptEditorView : UserControl, IAssetEditorView
{
    public YarnScriptEditorView()
    {
        InitializeComponent();
    }
}
