using Stride.Core.Mathematics;

namespace ColorRise.Skein.Editor.Services;

/// <summary>
/// The colours of the characters of a project in Game Studio: the one the project chose for a name, or one made from the
/// name, the same at each run.
/// </summary>
public sealed class YarnCharacterColors
{
    /// <summary>
    /// The colours of a project that chose none.
    /// </summary>
    public static readonly YarnCharacterColors Default = new(new Dictionary<string, Color3>());

    private readonly Dictionary<string, Color3> chosen;

    /// <summary>
    /// The colours of a project.
    /// </summary>
    /// <param name="chosen">The colours the project chose, by the name of the character.</param>
    public YarnCharacterColors(IReadOnlyDictionary<string, Color3> chosen)
    {
        ArgumentNullException.ThrowIfNull(chosen);
        this.chosen = new Dictionary<string, Color3>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, color) in chosen)
        {
            this.chosen[name.Trim()] = color;
        }
    }

    /// <summary>
    /// The colour of a character: the one the project chose, whatever the case of the name, or the one made from it.
    /// </summary>
    public Color3 Of(string speaker)
    {
        ArgumentNullException.ThrowIfNull(speaker);
        var name = speaker.Trim();
        return chosen.TryGetValue(name, out var color) ? color : Made(name);
    }

    /// <summary>
    /// A colour that reads on a background: lighter on a dark one, darker on a light one, as little as it takes.
    /// </summary>
    /// <param name="color">The colour.</param>
    /// <param name="onDark">Whether the background is dark.</param>
    public static Color3 Readable(Color3 color, bool onDark)
    {
        var luminance = Luminance(color);
        if (onDark && luminance < 0.5f)
            return Color3.Lerp(color, new Color3(1, 1, 1), (0.5f - luminance) / (1 - luminance));

        if (!onDark && luminance > 0.4f)
            return color * (0.4f / luminance);

        return color;
    }

    /// <summary>
    /// A colour that reads on a light and on a dark background, between the two.
    /// </summary>
    public static Color3 ReadableOnBoth(Color3 color)
    {
        var luminance = Luminance(color);
        if (luminance < 0.3f)
            return Color3.Lerp(color, new Color3(1, 1, 1), (0.3f - luminance) / (1 - luminance));

        return luminance > 0.5f ? color * (0.5f / luminance) : color;
    }

    /// <summary>
    /// Whether two sets of colours say the same.
    /// </summary>
    public bool SameAs(YarnCharacterColors other)
        => other.chosen.Count == chosen.Count && chosen.All(c => other.chosen.TryGetValue(c.Key, out var color) && color == c.Value);

    // A hue from the name, lowered so that Odile and ODILE are one; a light tone that reads on the dark cards
    private static Color3 Made(string name)
    {
        var hash = 2166136261u;
        foreach (var c in name.ToLowerInvariant())
        {
            hash = unchecked((hash ^ c) * 16777619u);
        }

        var hue = hash % 360;
        var color = new ColorHSV(hue, 0.5f, 0.92f, 1).ToColor();
        return new Color3(color.R, color.G, color.B);
    }

    private static float Luminance(Color3 color) => (0.2126f * color.R) + (0.7152f * color.G) + (0.0722f * color.B);
}
