using Stride.Core;

namespace ColorRise.Skein;

/// <summary>
/// How a runner picks among the nodes of a node group, or the lines of a line group, whose conditions hold: Yarn Spinner's
/// four strategies.
/// </summary>
[DataContract("YarnSaliency")]
public enum YarnSaliency
{
    /// <summary>
    /// The first in the order of the scripts.
    /// </summary>
    First,

    /// <summary>
    /// The most specific: the one with the most conditions.
    /// </summary>
    Best,

    /// <summary>
    /// Among the ones seen the least often, the most specific.
    /// </summary>
    BestLeastRecentlyViewed,

    /// <summary>
    /// Among the ones seen the least often, one of the most specific at random. Yarn Spinner's default.
    /// </summary>
    RandomBestLeastRecentlyViewed,
}
