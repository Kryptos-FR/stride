using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace ColorRise.Tessera;

/// <summary>
/// The layers of a map, from the bottom one, drawn first, to the top one.
/// </summary>
public sealed class TileLayerCollection : IReadOnlyList<TileLayer>
{
    private readonly List<TileLayer> layers = [];
    private readonly TileMap map;

    internal TileLayerCollection(TileMap map) => this.map = map;

    /// <summary>
    /// How many layers the map has.
    /// </summary>
    public int Count => layers.Count;

    /// <summary>
    /// The layer at an index, the bottom one at 0.
    /// </summary>
    public TileLayer this[int index] => layers[index];

    /// <summary>
    /// The first layer of a name.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The map has no layer of that name.</exception>
    public TileLayer this[string name]
        => TryGet(name, out var layer) ? layer : throw new KeyNotFoundException($"The map has no layer named '{name}'.");

    /// <summary>
    /// Finds a layer by its name.
    /// </summary>
    public bool TryGet(string name, [MaybeNullWhen(false)] out TileLayer layer)
    {
        layer = layers.Find(candidate => candidate.Name == name);
        return layer is not null;
    }

    /// <summary>
    /// The index of a layer, or -1 when the map does not have it.
    /// </summary>
    public int IndexOf(TileLayer layer) => layers.IndexOf(layer);

    /// <summary>
    /// Adds a new layer on top of the others.
    /// </summary>
    public TileLayer Add(string name)
    {
        var layer = new TileLayer { Name = name };
        Insert(layers.Count, layer);
        return layer;
    }

    /// <summary>
    /// Puts a layer at an index, 0 for the bottom.
    /// </summary>
    /// <exception cref="ArgumentException">The layer is already in a map.</exception>
    public void Insert(int index, TileLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (layer.Map is not null)
        {
            throw new ArgumentException($"The layer '{layer.Name}' is already in a map: remove it from that map first.", nameof(layer));
        }
        layers.Insert(index, layer);
        layer.Map = map;
    }

    /// <summary>
    /// Removes a layer from the map; false when the map does not have it.
    /// </summary>
    public bool Remove(TileLayer layer)
    {
        if (!layers.Remove(layer))
        {
            return false;
        }
        layer.Map = null;
        return true;
    }

    /// <summary>
    /// The layers, the bottom one first.
    /// </summary>
    public IEnumerator<TileLayer> GetEnumerator() => layers.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
