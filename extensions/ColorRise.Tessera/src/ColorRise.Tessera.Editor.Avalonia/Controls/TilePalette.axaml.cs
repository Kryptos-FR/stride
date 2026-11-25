using Avalonia.Controls;

namespace ColorRise.Tessera.Editor.Avalonia.Controls;

/// <summary>
/// The palette of a map editor: a source of a tile set, where a click or a drag picks the cells a tool paints. Its data
/// context is a <see cref="Palette.TilePaletteViewModel"/>.
/// </summary>
public sealed partial class TilePalette : UserControl
{
    /// <summary>
    /// Made by the map editor's view.
    /// </summary>
    public TilePalette()
    {
        InitializeComponent();
    }
}
