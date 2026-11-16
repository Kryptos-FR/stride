using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Assets.Editor.Editors;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// The view of the overview of a Yarn project: its scripts, its variables, its languages with the commands on the lines,
/// and what the compiler says.
/// </summary>
[AssetEditorView<YarnProjectEditorViewModel>]
public sealed partial class YarnProjectEditorView : UserControl, IAssetEditorView
{
    /// <summary>
    /// Makes the view of the overview of a Yarn project.
    /// </summary>
    public YarnProjectEditorView()
    {
        InitializeComponent();
    }

    // A script, a variable or a message: its place in the script's editor
    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is YarnProjectEditorViewModel editor && sender is ListBox { SelectedItem: { } item })
        {
            editor.GoToCommand.Execute(item);
        }
    }

    private void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is YarnProjectEditorViewModel editor && sender is Button { DataContext: YarnProjectLanguageItem language })
        {
            editor.ExportStringsCommand.Execute(language.Language);
        }
    }
}
