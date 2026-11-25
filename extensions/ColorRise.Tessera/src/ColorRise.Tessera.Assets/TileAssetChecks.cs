using System.Globalization;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// What is wrong in an asset that still loads, as a merge or an edit by hand can leave it: the lines the build warns with,
/// and the editors show.
/// </summary>
public static class TileAssetChecks
{
    /// <summary>
    /// The pixels per unit the build uses when the asset's is not above 0.
    /// </summary>
    public const float DefaultPixelsPerUnit = 16;

    /// <summary>
    /// Two sources with one id (the first is kept), and a PixelsPerUnit that is not above 0 (16 is used).
    /// </summary>
    public static IReadOnlyList<string> Of(TileSetAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var lines = new List<string>();
        foreach (var group in asset.Sources.GroupBy(source => source.Id).Where(group => group.Count() > 1))
        {
            var names = string.Join(" and ", group.Select(source => $"'{source.Name}'"));
            lines.Add($"The sources {names} have the same id, {group.Key.ToString(CultureInfo.InvariantCulture)}: the first is kept, and the others give no tile.");
        }
        if (!HasPixelsPerUnit(asset))
        {
            lines.Add($"PixelsPerUnit is {asset.PixelsPerUnit.ToString(CultureInfo.InvariantCulture)}, and it must be a number above 0: {DefaultPixelsPerUnit.ToString(CultureInfo.InvariantCulture)} is used.");
        }
        return lines;
    }

    /// <summary>
    /// A slot given twice (the first is kept), and a slot out of the range 1 to 65535.
    /// </summary>
    public static IReadOnlyList<string> Of(TileMapAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var lines = new List<string>();
        foreach (var slot in asset.TileSets.Where(slot => !IsInRange(slot.Slot)).Select(slot => slot.Slot).Distinct())
        {
            lines.Add($"The slot {slot.ToString(CultureInfo.InvariantCulture)} is out of range: a slot is from 1 to {ushort.MaxValue.ToString(CultureInfo.InvariantCulture)}.");
        }
        lines.AddRange(SlotsGivenTwice(asset));
        return lines;
    }

    // The slots given twice or more; the build warns with these, and fails on a slot out of range (TileMapAsset.ToTileMap)
    internal static IEnumerable<string> SlotsGivenTwice(TileMapAsset asset)
        => asset.TileSets.Where(slot => IsInRange(slot.Slot)).GroupBy(slot => slot.Slot).Where(group => group.Count() > 1)
            .Select(group => $"The slot {group.Key.ToString(CultureInfo.InvariantCulture)} is given twice or more: the first tile set is kept.");

    /// <summary>
    /// Whether the asset's pixels per unit is a number above 0, which the build uses as it is.
    /// </summary>
    public static bool HasPixelsPerUnit(TileSetAsset asset) => asset.PixelsPerUnit > 0 && float.IsFinite(asset.PixelsPerUnit);

    /// <summary>
    /// Whether a slot is one a cell can name.
    /// </summary>
    public static bool IsInRange(int slot) => slot is >= 1 and <= ushort.MaxValue;
}
