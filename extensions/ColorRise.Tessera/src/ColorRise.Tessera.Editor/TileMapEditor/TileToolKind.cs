namespace ColorRise.Tessera.Editor.TileMapEditor;

/// <summary>
/// A tool of the map editor.
/// </summary>
public enum TileToolKind
{
    /// <summary>
    /// Paints the stamp under the cursor, and along a drag (B).
    /// </summary>
    Stamp,

    /// <summary>
    /// Fills a dragged rectangle with the stamp (P).
    /// </summary>
    Rectangle,

    /// <summary>
    /// Fills the cells that touch the clicked one and have its tile (F).
    /// </summary>
    Fill,

    /// <summary>
    /// Empties cells along a drag, or in a rectangle with the right button (E).
    /// </summary>
    Erase,

    /// <summary>
    /// Takes the tile of a cell as the stamp (I).
    /// </summary>
    Pick,

    /// <summary>
    /// Selects a rectangle of cells, and moves it (R).
    /// </summary>
    Select,
}
