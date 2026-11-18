// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

namespace Stride.Assets.Editor.PlayMode;

/// <summary>
/// The rate limit of the live property grid: an entity whose snapshots take longer than a budget to show is refreshed less
/// often, down to <see cref="MaximumInterval"/>, and comes back to <see cref="BaseInterval"/> when it gets cheap again.
/// </summary>
/// <remarks>
/// The interval of an entity grows with its cost: at <see cref="Budget"/> per apply it stays at <see cref="BaseInterval"/>, at
/// twice the budget it doubles. So an entity takes about the same share of the UI thread whatever its size. Not thread-safe:
/// the grid uses it on the UI thread.
/// </remarks>
[Experimental("STREXP012")]
public sealed class LiveRefreshThrottle
{
    private readonly Dictionary<int, State> states = [];

    /// <summary>
    /// The time an entity's apply may take at <see cref="BaseInterval"/>. 20 ms by default: a tenth of the UI thread at 5 Hz.
    /// </summary>
    public TimeSpan Budget { get; init; } = TimeSpan.FromMilliseconds(20);

    /// <summary>
    /// The interval the budget is for: 200 ms (5 Hz, the rate the grid asks the game for) by default. An entity within its
    /// budget is applied at every snapshot; a slower one at an interval that grows from this one.
    /// </summary>
    public TimeSpan BaseInterval { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// The longest interval between two applies of a slow entity: 1 s by default.
    /// </summary>
    public TimeSpan MaximumInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Whether any entity is refreshed less often than <see cref="BaseInterval"/>.
    /// </summary>
    public bool IsThrottled => states.Values.Any(x => x.Interval > BaseInterval);

    /// <summary>
    /// The longest interval of the entities, <see cref="BaseInterval"/> when none is throttled.
    /// </summary>
    public TimeSpan LongestInterval => states.Values.Select(x => x.Interval).DefaultIfEmpty(BaseInterval).Max();

    /// <summary>
    /// Whether a snapshot of the entity received at <paramref name="now"/> should be applied.
    /// </summary>
    public bool ShouldApply(int handle, TimeSpan now)
    {
        // An entity within its budget is applied at every snapshot, whatever their rate
        return !states.TryGetValue(handle, out var state) || state.Interval <= BaseInterval || now - state.LastApply >= state.Interval - Tolerance(state.Interval);
    }

    /// <summary>
    /// The interval at which the entity is refreshed.
    /// </summary>
    public TimeSpan GetInterval(int handle) => states.TryGetValue(handle, out var state) ? state.Interval : BaseInterval;

    /// <summary>
    /// Records that the entity's snapshot was applied at <paramref name="now"/> in <paramref name="cost"/>, and adapts its
    /// interval.
    /// </summary>
    /// <returns>Whether the entity's interval changed.</returns>
    public bool Record(int handle, TimeSpan cost, TimeSpan now)
    {
        if (!states.TryGetValue(handle, out var state))
        {
            // The first apply also pays for the first layout of the editors (and the JIT): it counts at most as the budget
            state = new State { Interval = BaseInterval, AverageCost = cost < Budget ? cost : Budget };
            states.Add(handle, state);
        }
        else
        {
            // A moving average, so that one slow frame (a garbage collection, a layout) does not slow an entity down
            state.AverageCost = TimeSpan.FromTicks((state.AverageCost.Ticks * 2 + cost.Ticks) / 3);
        }
        state.LastApply = now;

        var wanted = TimeSpan.FromTicks((long)(BaseInterval.Ticks * (state.AverageCost.Ticks / (double)Budget.Ticks)));
        var interval = wanted < BaseInterval ? BaseInterval : wanted > MaximumInterval ? MaximumInterval : wanted;
        // Round to tenths of a second, so that the interval (and what the grid says about it) does not change at every apply
        interval = TimeSpan.FromMilliseconds(Math.Ceiling(interval.TotalMilliseconds / 100) * 100);
        if (interval < BaseInterval)
            interval = BaseInterval;
        if (interval == state.Interval)
            return false;
        state.Interval = interval;
        return true;
    }

    /// <summary>
    /// Forgets the entities that are not in <paramref name="handles"/>.
    /// </summary>
    public void Keep(IReadOnlyCollection<int> handles)
    {
        foreach (var handle in states.Keys.Where(x => !handles.Contains(x)).ToList())
            states.Remove(handle);
    }

    /// <summary>
    /// Forgets every entity.
    /// </summary>
    public void Reset() => states.Clear();

    // Snapshots arrive at the game's frames, a little before or after the interval: a tenth of it is close enough
    private static TimeSpan Tolerance(TimeSpan interval) => interval / 10;

    private sealed class State
    {
        public TimeSpan Interval { get; set; }

        public TimeSpan AverageCost { get; set; }

        public TimeSpan LastApply { get; set; }
    }
}
