using Avalonia.Media;
using Avalonia.Media.Immutable;
using ColorRise.Skein.Assets.Compilation;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// The colours of Skein's views, for the dark look of Game Studio.
/// </summary>
internal static class YarnPalette
{
    public static readonly IBrush Header = Brush(0x3F, 0x58, 0x66);
    public static readonly IBrush NoteHeader = Brush(0x8A, 0x7A, 0x2B);
    public static readonly IBrush Error = Brush(0xE5, 0x53, 0x4B);
    public static readonly IBrush Warning = Brush(0xE0, 0xA8, 0x3A);
    public static readonly IBrush Info = Brush(0x5A, 0xA9, 0xE6);
    public static readonly IBrush GraphBackground = Brush(0x3A, 0x3A, 0x3F);
    public static readonly IBrush FrameLabel = Brush(0xD8, 0xD8, 0xD8);
    public static readonly Color NodeGroupFrame = Color.FromRgb(0xC8, 0xC8, 0xC8);

    // The names Yarn Spinner's editors know for the color header, in tones that keep a white title readable
    private static readonly Dictionary<string, IBrush> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["red"] = Brush(0xA8, 0x3A, 0x3A),
        ["green"] = Brush(0x3B, 0x85, 0x4A),
        ["blue"] = Brush(0x36, 0x68, 0xAD),
        ["orange"] = Brush(0xB8, 0x6A, 0x1C),
        ["yellow"] = Brush(0xA3, 0x8B, 0x1B),
        ["purple"] = Brush(0x76, 0x4A, 0xA3),
    };

    /// <summary>
    /// The names of the colours a node can be given from the graph's menu.
    /// </summary>
    public static IReadOnlyList<string> ColorNames { get; } = ["red", "orange", "yellow", "green", "blue", "purple"];

    /// <summary>
    /// The brush of the header of a card, from the <c>color:</c> header of its node: a known name, or any colour Avalonia reads.
    /// </summary>
    public static IBrush HeaderOf(string? color, bool isNote)
    {
        if (!string.IsNullOrWhiteSpace(color))
        {
            if (Named.TryGetValue(color.Trim(), out var named))
                return named;

            if (Color.TryParse(color.Trim(), out var parsed))
                return new ImmutableSolidColorBrush(parsed);
        }

        return isNote ? NoteHeader : Header;
    }

    /// <summary>
    /// A colour for a value, the same at each run: the frames and the chips of one value look alike.
    /// </summary>
    public static Color OfValue(string value)
    {
        // FNV-1a: the hash of a string of .NET changes at each run
        var hash = 2166136261u;
        foreach (var c in value)
        {
            hash = (hash ^ c) * 16777619u;
        }

        return new HslColor(1, hash % 360, 0.55, 0.62).ToRgb();
    }

    public static IBrush OfSeverity(YarnSeverity severity) => severity switch
    {
        YarnSeverity.Error => Error,
        YarnSeverity.Warning => Warning,
        _ => Info,
    };

    private static IBrush Brush(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));
}
