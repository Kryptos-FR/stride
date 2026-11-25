using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Assets.Editor.Settings;
using Stride.Core.Mathematics;
using Stride.Core.Settings;

namespace ColorRise.Tessera.Editor;

/// <summary>
/// The settings of Tessera's editors: what Game Studio's settings show under "Tessera", and what the editors keep for
/// themselves between two sessions.
/// </summary>
[EditorSettings]
internal static class TesseraEditorSettings
{
    /// <summary>
    /// Whether the map editor draws the grid of the cells.
    /// </summary>
    public static SettingsKey<bool> ShowGrid { get; } = new("Tessera/ShowGrid", EditorSettings.SettingsContainer, true)
    {
        DisplayName = "Tessera/Show the grid of a map",
    };

    /// <summary>
    /// The colour of the grid of the map editor.
    /// </summary>
    public static SettingsKey<Color> GridColor { get; } = new("Tessera/GridColor", EditorSettings.SettingsContainer, new Color(255, 255, 255, 64))
    {
        DisplayName = "Tessera/Grid colour",
    };

    /// <summary>
    /// Whether the map editor dims the layers other than the current one.
    /// </summary>
    public static SettingsKey<bool> DimOtherLayers { get; } = new("Tessera/DimOtherLayers", EditorSettings.SettingsContainer, false)
    {
        DisplayName = "Tessera/Dim the other layers",
    };

    /// <summary>
    /// The opacity of the dimmed layers, from 0 to 1.
    /// </summary>
    public static SettingsKey<float> DimOpacity { get; } = new("Tessera/DimOpacity", EditorSettings.SettingsContainer, 0.35f)
    {
        DisplayName = "Tessera/Opacity of the dimmed layers",
    };

    /// <summary>
    /// Whether the map editor plays the animated tiles.
    /// </summary>
    public static SettingsKey<bool> PlayAnimations { get; } = new("Tessera/PlayAnimations", EditorSettings.SettingsContainer, true)
    {
        DisplayName = "Tessera/Play the animated tiles of a map",
    };

    /// <summary>
    /// The zoom a map editor opens at; 0 until a zoom was chosen, and the editor then fits the map. Not shown in the settings.
    /// </summary>
    public static SettingsKey<float> LastZoom { get; } = new("Tessera/LastZoom", InternalSettings.SettingsContainer, 0f);

    /// <summary>
    /// The locked layers of the maps, as "&lt;asset id&gt;/&lt;layer name&gt;". Not shown in the settings.
    /// </summary>
    public static SettingsKey<List<string>> LockedLayers { get; } = new("Tessera/LockedLayers", InternalSettings.SettingsContainer, () => new List<string>());

    /// <summary>
    /// Writes the settings shown in Game Studio's settings to their file.
    /// </summary>
    public static void Save() => EditorSettings.Save();

    /// <summary>
    /// Writes the settings the editors keep for themselves to their file.
    /// </summary>
    public static void SaveInternal() => InternalSettings.Save();
}
