namespace ColorRise.Tessera.Editor.TileSetEditor;

/// <summary>
/// What a press on a tile of the tile set editor does.
/// </summary>
public enum TileSetEditorMode
{
    /// <summary>
    /// A click selects a tile, Ctrl adds one, and a drag selects the tiles it covers: the property grid shows their data.
    /// </summary>
    Select,
    /// <summary>
    /// A press sets a tile solid when it was not, and clears it when it was; the tiles the drag passes get the same value.
    /// </summary>
    Solid,
}
