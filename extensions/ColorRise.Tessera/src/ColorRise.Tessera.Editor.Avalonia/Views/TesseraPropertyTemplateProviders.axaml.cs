using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ColorRise.Tessera.Editor.Avalonia.Views;

/// <summary>
/// The templates Tessera gives the property grid: its own editor of <see cref="TileFlip"/> and
/// <see cref="TileCollision"/>, a check box for each flag.
/// </summary>
public sealed class TesseraPropertyTemplateProviders : ResourceDictionary
{
    /// <summary>
    /// Loads the templates.
    /// </summary>
    public TesseraPropertyTemplateProviders()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
