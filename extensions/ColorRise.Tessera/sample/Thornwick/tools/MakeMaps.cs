// Writes the maps of the Thornwick sample from the text pictures in tools/maps: each picture becomes the file of a tile map
// asset, as Game Studio writes it. A .NET file-based app; from the folder of the sample:
//
//     dotnet run tools/MakeMaps.cs -- Thornwick.Game
//
// Ids are stable (UUID v5 of a name), so a run over the same pictures writes the same files. The maps are written whole:
// a change made to them in Game Studio is lost when this runs again, so change the pictures here.
//
// A picture, in order:
//
//     map <url> <width> <height>     first: the URL of the asset and the size of its frame, in cells
//     tileset <slot> = <id>:<url>    a tile set of the map, its id copied from the line "Id:" of the tile set's file
//     tile <character> = <cell>      what a character stands for: a cell as the map's file writes it, such as 1/1:0,0 or
//                                    1/1:8,6h (the flips d, h and v after the tile id, in that order)
//     layer <name>                   a layer, then <height> lines of <width> characters, the top row first; a dot or a space
//                                    is an empty cell. The layers go from the bottom one up.
//
// A line that starts with # is a remark, and so is the rest of a map, tileset, tile or layer line from a # after a space. A
// line that starts with "layer " always starts a layer. The tool reads no asset: whether a tile exists is for the sample's
// tests to say.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

var game = args.Length > 0 ? args[0] : "Thornwick.Game";
var folder = Path.Combine("tools", "maps");
if (!Directory.Exists(folder))
{
    Console.Error.WriteLine($"There is no folder {folder} here: run the tool from the folder of the sample.");
    return 1;
}
var pictures = Directory.GetFiles(folder, "*.txt").Order(StringComparer.Ordinal).ToList();

// Every picture is read before any map is written, so that a mistake leaves every map as it was
var maps = new List<Picture>();
var failed = false;
foreach (var file in pictures)
{
    try
    {
        maps.Add(Picture.Read(file));
    }
    catch (PictureException error)
    {
        Console.Error.WriteLine(error.Message);
        failed = true;
    }
}
if (failed)
{
    return 1;
}

foreach (var map in maps)
{
    var path = Path.Combine(game, "Assets", map.Url + ".sdtilemap");
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, map.Write(), new UTF8Encoding(false));
    Console.WriteLine($"{Path.GetFileName(map.File)} -> {path}");
}
return 0;

/// <summary>
/// A mistake in a picture, with its file and its line.
/// </summary>
internal sealed class PictureException(string file, int line, string reason)
    : Exception(line > 0 ? $"{file}({line}): {reason}" : $"{file}: {reason}");

/// <summary>
/// A layer of a picture: its name, and its rows from the top, each a cell text or null for each column.
/// </summary>
internal sealed record PictureLayer(string Name, int Line, List<string?[]> Rows);

/// <summary>
/// A text picture of a map, read from its file.
/// </summary>
internal sealed partial class Picture
{
    private static readonly Guid Namespace = Guid.Parse("e97f36ba-18ac-4d1f-a3c1-d4d112d5b489");

    private const int ChunkSize = 16;

    private Picture(string file) => File = file;

    public string File { get; }

    public string Url { get; private set; } = "";

    public int Width { get; private set; }

    public int Height { get; private set; }

    public SortedDictionary<int, string> TileSets { get; } = [];

    public List<PictureLayer> Layers { get; } = [];

    /// <summary>
    /// Reads a picture.
    /// </summary>
    /// <exception cref="PictureException">The picture has a mistake.</exception>
    public static Picture Read(string file)
    {
        var name = Path.GetFileName(file);
        var picture = new Picture(file);
        var tiles = new Dictionary<char, (string Cell, int Slot, int Line)>();
        var lines = System.IO.File.ReadAllLines(file);
        var hasMap = false;
        PictureLayer? layer = null;

        for (var index = 0; index < lines.Length; index++)
        {
            var number = index + 1;
            var line = lines[index];
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var isLayerLine = line.StartsWith("layer ", StringComparison.Ordinal);
            if (layer is not null && layer.Rows.Count < picture.Height && !isLayerLine)
            {
                layer.Rows.Add(ReadRow(line, picture.Width, tiles, name, number));
                continue;
            }
            if (layer is not null)
            {
                CheckRows(layer, picture.Height, name, number);
            }

            var text = WithoutRemark(line);
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                continue;
            }
            if (!hasMap && words[0] != "map")
            {
                throw new PictureException(name, number, "a picture starts with its line 'map <url> <width> <height>'.");
            }
            switch (words[0])
            {
                case "map" when hasMap:
                    throw new PictureException(name, number, "a picture has one map line.");
                case "map":
                    if (words.Length != 4 || !IsUrl(words[1])
                        || !TryCount(words[2], out var width) || !TryCount(words[3], out var height))
                    {
                        throw new PictureException(
                            name, number,
                            "a map line is 'map <url> <width> <height>', the sizes whole numbers above 0.");
                    }
                    (picture.Url, picture.Width, picture.Height, hasMap) = (words[1], width, height, true);
                    break;
                case "tileset":
                    var tileSet = TileSetLine().Match(text);
                    if (!tileSet.Success
                        || !int.TryParse(tileSet.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var slot)
                        || slot is < 1 or > ushort.MaxValue
                        || !Guid.TryParse(tileSet.Groups[2].Value, out _)
                        || !IsUrl(tileSet.Groups[3].Value))
                    {
                        throw new PictureException(
                            name, number,
                            "a tileset line is 'tileset <slot> = <id>:<url>', the slot from 1 to 65535.");
                    }
                    if (!picture.TileSets.TryAdd(slot, $"{tileSet.Groups[2].Value}:{tileSet.Groups[3].Value}"))
                    {
                        throw new PictureException(name, number, $"the slot {slot} has a tileset line already.");
                    }
                    break;
                case "tile":
                    var tile = TileLine().Match(text);
                    if (!tile.Success)
                    {
                        throw new PictureException(
                            name, number,
                            "a tile line is 'tile <character> = <cell>', such as 'tile g = 1/1:0,0' or 'tile ] = 1/1:8,6h'.");
                    }
                    var character = tile.Groups[1].Value[0];
                    if (character is '.' or '#')
                    {
                        throw new PictureException(
                            name, number,
                            $"'{character}' cannot be given a tile: a dot is an empty cell, and # starts a remark.");
                    }
                    var cell = CellText().Match(tile.Groups[2].Value);
                    if (!cell.Success)
                    {
                        throw new PictureException(
                            name, number,
                            $"'{tile.Groups[2].Value}' is not a cell; write slot/id with its flips d, h and v in that order, such as 1/1:3,4 or 1/2:1,1dh.");
                    }
                    var tileSlot = int.Parse(cell.Groups[1].Value, CultureInfo.InvariantCulture);
                    if (!tiles.TryAdd(character, (tile.Groups[2].Value, tileSlot, number)))
                    {
                        throw new PictureException(
                            name, number,
                            $"'{character}' has a tile line already, line {tiles[character].Line}.");
                    }
                    break;
                case "layer":
                    var layerName = text["layer".Length..].Trim();
                    if (!LayerName().IsMatch(layerName))
                    {
                        throw new PictureException(
                            name, number,
                            "a layer line is 'layer <name>', the name of letters, digits, spaces, - and _, starting with a letter.");
                    }
                    if (picture.Layers.Any(other => other.Name == layerName))
                    {
                        throw new PictureException(name, number, $"the layer '{layerName}' is named twice.");
                    }
                    layer = new PictureLayer(layerName, number, []);
                    picture.Layers.Add(layer);
                    break;
                default:
                    throw new PictureException(
                        name, number,
                        $"'{words[0]}' does not start a line of a picture: map, tileset, tile or layer.");
            }
        }

        if (!hasMap)
        {
            throw new PictureException(name, 0, "the picture has no line 'map <url> <width> <height>'.");
        }
        if (layer is not null)
        {
            CheckRows(layer, picture.Height, name, lines.Length);
        }
        foreach (var (character, (_, slot, line)) in tiles)
        {
            if (!picture.TileSets.ContainsKey(slot))
            {
                throw new PictureException(
                    name, line,
                    $"the tile of '{character}' is in the slot {slot}, which has no tileset line.");
            }
        }
        return picture;
    }

    /// <summary>
    /// The text of the map's file.
    /// </summary>
    public string Write()
    {
        var text = new StringBuilder();
        text.Append("!TileMapAsset\n");
        text.Append($"Id: {Uuid5($"map/{Url}")}\n");
        text.Append("SerializedVersion: {ColorRise.Tessera: 1.0.0.0}\n");
        text.Append("Tags: []\n");
        text.Append("CellSize: {X: 1.0, Y: 1.0}\n");
        text.Append(CultureInfo.InvariantCulture, $"Size: {{X: {Width}, Y: {Height}}}\n");
        text.Append(TileSets.Count == 0 ? "TileSets: {}\n" : "TileSets:\n");
        foreach (var (slot, reference) in TileSets)
        {
            text.Append($"    {Key($"{Url}/tileset/{slot}")}:\n");
            text.Append(CultureInfo.InvariantCulture, $"        Slot: {slot}\n");
            text.Append($"        TileSet: {reference}\n");
        }
        text.Append(Layers.Count == 0 ? "Layers: {}\n" : "Layers:\n");
        foreach (var layer in Layers)
        {
            text.Append($"    {Key($"{Url}/layer/{layer.Name}")}:\n");
            text.Append($"        Name: {layer.Name}\n");
            text.Append("        Visible: true\n");
            text.Append("        Opacity: 1.0\n");
            text.Append("        Tint: {R: 255, G: 255, B: 255, A: 255}\n");
            text.Append("        Offset: {X: 0.0, Y: 0.0}\n");
            text.Append("        Collision: true\n");
            var chunks = Chunks(layer);
            text.Append(chunks.Count == 0 ? "        Chunks: []\n" : "        Chunks:\n");
            foreach (var (x, y) in chunks)
            {
                text.Append(CultureInfo.InvariantCulture, $"            -   Position: {{X: {x}, Y: {y}}}\n");
                text.Append("                Cells: |\n");
                for (var row = ChunkSize - 1; row >= 0; row--)
                {
                    text.Append("                    ");
                    for (var column = 0; column < ChunkSize; column++)
                    {
                        text.Append(column > 0 ? " " : "").Append(CellAt(layer, x * ChunkSize + column, y * ChunkSize + row) ?? ".");
                    }
                    text.Append('\n');
                }
            }
        }
        text.Append(CultureInfo.InvariantCulture, $"LastSlot: {(TileSets.Count == 0 ? 0 : TileSets.Keys.Max())}\n");
        return text.ToString();
    }

    // The chunks that hold a cell, top to bottom then left to right, as TileLayerAsset.SortChunks orders them
    private List<(int X, int Y)> Chunks(PictureLayer layer)
    {
        var chunks = new HashSet<(int X, int Y)>();
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (CellAt(layer, x, y) is not null)
                {
                    chunks.Add((x / ChunkSize, y / ChunkSize));
                }
            }
        }
        return [.. chunks.OrderByDescending(chunk => chunk.Y).ThenBy(chunk => chunk.X)];
    }

    // The cell text at (x, y), y up from the bottom row of the frame; null when empty or past the frame
    private string? CellAt(PictureLayer layer, int x, int y)
        => x < Width && y < Height ? layer.Rows[Height - 1 - y][x] : null;

    private static string?[] ReadRow(string line, int width, Dictionary<char, (string Cell, int Slot, int Line)> tiles, string name, int number)
    {
        if (line.Length != width)
        {
            throw new PictureException(name, number, $"the line has {line.Length} characters; a row of the map has {width}.");
        }
        var row = new string?[width];
        for (var x = 0; x < width; x++)
        {
            var character = line[x];
            if (character is '.' or ' ')
            {
                continue;
            }
            if (!tiles.TryGetValue(character, out var tile))
            {
                throw new PictureException(name, number, $"'{character}', column {x + 1}, has no tile line.");
            }
            row[x] = tile.Cell;
        }
        return row;
    }

    private static void CheckRows(PictureLayer layer, int height, string name, int number)
    {
        if (layer.Rows.Count < height)
        {
            throw new PictureException(
                name, number,
                $"the layer '{layer.Name}' of line {layer.Line} has {layer.Rows.Count} of its {height} rows.");
        }
    }

    private static string WithoutRemark(string line)
    {
        var remark = line.IndexOf(" #", StringComparison.Ordinal);
        return (remark < 0 ? line : line[..remark]).Trim();
    }

    private static bool TryCount(string text, out int count)
        => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out count) && count > 0;

    private static bool IsUrl(string text) => AssetUrl().IsMatch(text);

    /// <summary>
    /// The UUID v5 of a name, as Python's <c>uuid.uuid5</c> gives it.
    /// </summary>
    private static Guid Uuid5(string name)
    {
        var ns = Namespace.ToByteArray(true);
        var hash = SHA1.HashData([.. ns, .. Encoding.UTF8.GetBytes(name)]);
        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, true);
    }

    /// <summary>
    /// The key of an item of a collection: a UUID v5 without dashes.
    /// </summary>
    private static string Key(string name) => Uuid5(name).ToString("N");

    [GeneratedRegex(@"^tileset\s+(\d+)\s*=\s*([0-9a-fA-F-]+):(\S+)$")]
    private static partial Regex TileSetLine();

    [GeneratedRegex(@"^tile (\S)\s+=\s+(\S+)$")]
    private static partial Regex TileLine();

    [GeneratedRegex(@"^(\d+)/\d+:(?:\d+,\d+|#\d+)d?h?v?$")]
    private static partial Regex CellText();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9 _-]*$")]
    private static partial Regex LayerName();

    [GeneratedRegex(@"^[A-Za-z0-9_][A-Za-z0-9_./-]*$")]
    private static partial Regex AssetUrl();
}
