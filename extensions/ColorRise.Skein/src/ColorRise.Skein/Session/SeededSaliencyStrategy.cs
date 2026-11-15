using Yarn.Saliency;

namespace ColorRise.Skein;

/// <summary>
/// Yarn Spinner's four saliency strategies in one, with two additions: the random pick takes its numbers from the
/// session, so that a seeded run repeats, and every pick is told with its candidates.
/// </summary>
internal sealed class SeededSaliencyStrategy : IContentSaliencyStrategy
{
    private readonly Yarn.IVariableStorage storage;
    private readonly Func<Random> random;
    private readonly Action<IReadOnlyList<ContentSaliencyOption>, ContentSaliencyOption?> picked;

    public SeededSaliencyStrategy(Yarn.IVariableStorage storage, Func<Random> random, Action<IReadOnlyList<ContentSaliencyOption>, ContentSaliencyOption?> picked)
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

        // Yarn Spinner's first and best strategies keep no count; the two that avoid repeats do
        if (Kind is YarnSaliency.BestLeastRecentlyViewed or YarnSaliency.RandomBestLeastRecentlyViewed)
            storage.SetValue(content.ViewCountKey, ViewCountOf(content) + 1);
    }

    private ContentSaliencyOption? Pick(List<ContentSaliencyOption> candidates)
    {
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
                return mostSpecific.Count == 1 ? mostSpecific[0] : mostSpecific[random().Next(mostSpecific.Count)];
        }
    }
}
