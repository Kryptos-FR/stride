using System.Text.RegularExpressions;

namespace ColorRise.Skein;

/// <summary>
/// How a <see cref="YarnNodeAttribute"/> filters the nodes it offers, as with Yarn Spinner for Unity.
/// </summary>
public enum YarnNodeFilter
{
    /// <summary>
    /// Every node.
    /// </summary>
    None,

    /// <summary>
    /// The nodes whose header contains the filter text, whatever its case.
    /// </summary>
    Contains,

    /// <summary>
    /// The nodes whose header starts with the filter text, whatever its case.
    /// </summary>
    StartsWith,

    /// <summary>
    /// The nodes whose header ends with the filter text, whatever its case.
    /// </summary>
    EndsWith,

    /// <summary>
    /// The nodes whose header the filter text matches, as a regular expression.
    /// </summary>
    MatchesRegex,
}

/// <summary>
/// Says that a string member holds the title of a node, so that Game Studio offers the nodes of the project to pick from.
/// </summary>
/// <remarks>
/// <see cref="Filter"/> narrows the list: <c>[YarnNode(nameof(Story), Filter = "lamp", FilterHeader = "quest")]</c> offers
/// the nodes with a <c>quest:</c> header that contains <c>lamp</c>. A node group is offered when one of its nodes passes.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class YarnNodeAttribute : Attribute
{
    /// <summary>
    /// Ties the member to the project its nodes come from.
    /// </summary>
    /// <param name="projectMember">The name of the member of the same object that holds the <see cref="YarnProject"/>.</param>
    public YarnNodeAttribute(string projectMember)
    {
        ProjectMember = projectMember;
    }

    /// <summary>
    /// The name of the member of the same object that holds the <see cref="YarnProject"/>.
    /// </summary>
    public string ProjectMember { get; }

    /// <summary>
    /// The text a node's header is compared with; null or empty for every node.
    /// </summary>
    public string? Filter { get; set; }

    /// <summary>
    /// How the header is compared with <see cref="Filter"/>.
    /// </summary>
    public YarnNodeFilter FilterType { get; set; } = YarnNodeFilter.Contains;

    /// <summary>
    /// The header that is compared: <c>title</c>, or any header of the nodes (<c>quest</c>, <c>tags</c>, <c>when</c>).
    /// </summary>
    public string FilterHeader { get; set; } = "title";

    /// <summary>
    /// Whether the attribute filters the nodes at all.
    /// </summary>
    public bool Filters => !string.IsNullOrEmpty(Filter) && FilterType != YarnNodeFilter.None;

    /// <summary>
    /// Whether a value of the header passes the filter. A regular expression that does not read matches nothing.
    /// </summary>
    public bool Matches(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!Filters)
            return true;

        var filter = Filter!;
        switch (FilterType)
        {
            case YarnNodeFilter.Contains:
                return value.Contains(filter, StringComparison.OrdinalIgnoreCase);
            case YarnNodeFilter.StartsWith:
                return value.StartsWith(filter, StringComparison.OrdinalIgnoreCase);
            case YarnNodeFilter.EndsWith:
                return value.EndsWith(filter, StringComparison.OrdinalIgnoreCase);
            case YarnNodeFilter.MatchesRegex:
                try
                {
                    return Regex.IsMatch(value, filter, RegexOptions.None, TimeSpan.FromSeconds(1));
                }
                catch (Exception exception) when (exception is ArgumentException or RegexMatchTimeoutException)
                {
                    return false;
                }

            default:
                return true;
        }
    }
}
