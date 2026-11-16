using System.Text;
using Yarn.Markup;

namespace ColorRise.Skein;

/// <summary>
/// Yarn Spinner's <c>plural</c> and <c>ordinal</c> markers, with <c>other</c> as the text of every case the marker does
/// not give.
/// </summary>
/// <remarks>
/// Yarn Spinner refuses a marker that lacks the case it picks, and its rules pick <c>many</c> for every French or Spanish
/// number above one: a translator who writes <c>one</c> and <c>other</c>, as the language has them, would get an error.
/// </remarks>
internal sealed class PluralMarkup : IAttributeMarkerProcessor
{
    private const string Other = "other";

    private static readonly string[] Cases = ["zero", "one", "two", "few", "many"];

    private readonly BuiltInMarkupReplacer replacer = new();

    /// <inheritdoc />
    public ReplacementMarkerResult ProcessReplacementMarker(MarkupAttribute marker, StringBuilder childBuilder, List<MarkupAttribute> childAttributes, string localeCode)
    {
        // The marker's properties are a dictionary of its own that Yarn Spinner made for this one use
        if (marker.Properties is IDictionary<string, MarkupValue> properties && properties.TryGetValue(Other, out var other))
        {
            foreach (var missing in Cases.Where(c => !properties.ContainsKey(c)))
            {
                properties[missing] = other;
            }
        }

        return replacer.ProcessReplacementMarker(marker, childBuilder, childAttributes, localeCode);
    }
}
