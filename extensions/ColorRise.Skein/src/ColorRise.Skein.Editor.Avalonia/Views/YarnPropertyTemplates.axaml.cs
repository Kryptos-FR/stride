using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// Skein's templates for the property grid: the picker of a Yarn node.
/// </summary>
public sealed class YarnPropertyTemplates : ResourceDictionary
{
    /// <summary>
    /// Loads the templates of the property grid.
    /// </summary>
    public YarnPropertyTemplates()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
