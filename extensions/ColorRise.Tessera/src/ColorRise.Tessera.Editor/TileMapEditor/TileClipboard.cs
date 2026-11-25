using System.Runtime.CompilerServices;
using ColorRise.Tessera.Editor.Palette;
using Stride.Core.Assets;
using Stride.Core.Assets.Presentation.ViewModels;

namespace ColorRise.Tessera.Editor.TileMapEditor;

/// <summary>
/// A tile set named by copied cells: the id of its asset and its name.
/// </summary>
public readonly record struct CopiedTileSet(AssetId Id, string Name);

/// <summary>
/// Cells copied in a session, with the tile set of each slot they name, so that another map can take them.
/// </summary>
public sealed class TileClipboard
{
    private static readonly ConditionalWeakTable<ISessionViewModel, TileClipboard> Clipboards = [];

    private TileClipboard()
    {
    }

    /// <summary>
    /// The cells copied last, as a stamp whose holes are the empty cells; null before the first copy.
    /// </summary>
    public TileStamp? Cells { get; private set; }

    /// <summary>
    /// The tile set of each slot that the cells name, in the map they were copied from.
    /// </summary>
    public IReadOnlyDictionary<ushort, CopiedTileSet> TileSets { get; private set; } = new Dictionary<ushort, CopiedTileSet>();

    /// <summary>
    /// The clipboard of a session.
    /// </summary>
    public static TileClipboard Of(ISessionViewModel session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return Clipboards.GetValue(session, _ => new TileClipboard());
    }

    /// <summary>
    /// Keeps cells and the tile sets of their slots, in place of what was copied before.
    /// </summary>
    public void Copy(TileStamp cells, IReadOnlyDictionary<ushort, CopiedTileSet> tileSets)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(tileSets);
        Cells = cells;
        TileSets = new Dictionary<ushort, CopiedTileSet>(tileSets);
    }
}
