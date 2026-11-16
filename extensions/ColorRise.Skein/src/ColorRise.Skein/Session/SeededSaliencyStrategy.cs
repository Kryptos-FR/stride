using Yarn.Saliency;

namespace ColorRise.Skein;

/// <summary>
/// Yarn Spinner's four saliency strategies in one, with two additions: every pick is told with its candidates, and when
/// the session has a seed the pick takes its numbers from the session, so that a seeded run repeats.
/// </summary>
/// <remarks>
/// Without a seed, Yarn Spinner's own strategies pick. With one, the same rules pick with the session's numbers.
/// </remarks>
internal sealed class SeededSaliencyStrategy : IContentSaliencyStrategy
{
    private readonly Yarn.IVariableStorage storage;
    private readonly Func<Random?> random;
    private readonly Action<IReadOnlyList<ContentSaliencyOption>, ContentSaliencyOption?> picked;
    private readonly Dictionary<YarnSaliency, IContentSaliencyStrategy> stock = [];

    /// <param name="random">The numbers of the session when it has a seed; null for Yarn Spinner's own strategies.</param>
    public SeededSaliencyStrategy(Yarn.IVariableStorage storage, Func<Random?> random, Action<IReadOnlyList<ContentSaliencyOption>, ContentSaliencyOption?> picked)
    {
        this.storage = storage;
        this.random = random;
        this.picked = picked;
    }

    /// <summary>
    /// Which of the four strategies.
    /// </summary>
    public YarnSaliency Kind { get; set; }

    /// <summary>
    /// While true, a query is a question (<c>has_any_content</c>), not a pick: nothing is told.
    /// </summary>
    public bool IsAsking { get; set; }

    public int ViewCountOf(ContentSaliencyOption content)
        => storage.TryGetValue<float>(content.ViewCountKey, out var count) ? (int)count : 0;

    public ContentSaliencyOption? QueryBestContent(IEnumerable<ContentSaliencyOption> content)
    {
        var candidates = content.ToList();
        var best = Pick(candidates);
        if (!IsAsking)
            picked(candidates, best);
        return best;
    }

    public void ContentWasSelected(ContentSaliencyOption content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (random() is null)
        {
            Stock().ContentWasSelected(content);
            return;
        }

        // Yarn Spinner's first and best strategies keep no count; the two that avoid repeats do
        if (Kind is YarnSaliency.BestLeastRecentlyViewed or YarnSaliency.RandomBestLeastRecentlyViewed)
            storage.SetValue(content.ViewCountKey, ViewCountOf(content) + 1);
    }

    private ContentSaliencyOption? Pick(List<ContentSaliencyOption> candidates)
    {
        if (random() is not { } numbers)
            return Stock().QueryBestContent(candidates);

        var passing = candidates.Where(c => c.FailingConditionValueCount == 0).ToList();
        if (passing.Count == 0)
            return null;

        switch (Kind)
        {
            case YarnSaliency.First:
                return passing[0];

            case YarnSaliency.Best:
                return passing.OrderByDescending(c => c.ComplexityScore).First();

            case YarnSaliency.BestLeastRecentlyViewed:
                return passing.OrderBy(ViewCountOf).ThenByDescending(c => c.ComplexityScore).First();

            default:
                var leastViewed = passing.GroupBy(ViewCountOf).OrderBy(g => g.Key).First();
                var mostSpecific = leastViewed.GroupBy(c => c.ComplexityScore).OrderByDescending(g => g.Key).First().ToList();
                return mostSpecific.Count == 1 ? mostSpecific[0] : mostSpecific[numbers.Next(mostSpecific.Count)];
        }
    }

    private IContentSaliencyStrategy Stock()
    {
        if (!stock.TryGetValue(Kind, out var strategy))
        {
            strategy = Kind switch
            {
                YarnSaliency.First => new FirstSaliencyStrategy(),
                YarnSaliency.Best => new BestSaliencyStrategy(),
                YarnSaliency.BestLeastRecentlyViewed => new BestLeastRecentlyViewedSaliencyStrategy(storage),
                _ => new RandomBestLeastRecentlyViewedSaliencyStrategy(storage),
            };
            stock[Kind] = strategy;
        }

        return strategy;
    }
}
