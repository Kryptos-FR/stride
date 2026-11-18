// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Assets.Editor.PlayMode;

namespace Stride.Assets.Editor.Tests.PlayMode;

/// <summary>
/// Tests <see cref="LiveRefreshThrottle"/>, the rate limit of the live property grid.
/// </summary>
public sealed class LiveRefreshThrottleTests
{
    private static readonly TimeSpan Ms = TimeSpan.FromMilliseconds(1);
    private readonly LiveRefreshThrottle throttle = new();

    [Fact]
    public void AnEntityWithinItsBudgetIsAppliedAtEverySnapshot()
    {
        for (var i = 0; i < 10; i++)
        {
            var now = i * 50 * Ms;
            Assert.True(throttle.ShouldApply(1, now));
            throttle.Record(1, 5 * Ms, now);
        }
        Assert.False(throttle.IsThrottled);
        Assert.Equal(throttle.BaseInterval, throttle.GetInterval(1));
    }

    [Fact]
    public void ASlowEntityIsRefreshedLessOftenInProportionToItsCost()
    {
        // The first apply counts at most as the budget; then the average grows to the cost
        var now = TimeSpan.Zero;
        for (var i = 0; i < 20; i++, now += TimeSpan.FromSeconds(1))
            throttle.Record(1, 60 * Ms, now);

        // Three times the budget: three times the base interval
        Assert.True(throttle.IsThrottled);
        Assert.Equal(600 * Ms, throttle.GetInterval(1));
        var last = now - TimeSpan.FromSeconds(1);
        Assert.False(throttle.ShouldApply(1, last + 200 * Ms));
        Assert.True(throttle.ShouldApply(1, last + 550 * Ms));
        Assert.Equal(600 * Ms, throttle.LongestInterval);
    }

    [Fact]
    public void TheIntervalStopsAtTheMaximumAndComesBackWhenTheEntityGetsCheap()
    {
        var now = TimeSpan.Zero;
        for (var i = 0; i < 10; i++, now += TimeSpan.FromSeconds(1))
            throttle.Record(1, 500 * Ms, now);
        Assert.Equal(throttle.MaximumInterval, throttle.GetInterval(1));

        for (var i = 0; i < 20; i++, now += TimeSpan.FromSeconds(1))
            throttle.Record(1, Ms, now);
        Assert.False(throttle.IsThrottled);
    }

    [Fact]
    public void EachEntityHasItsOwnInterval()
    {
        var now = TimeSpan.Zero;
        for (var i = 0; i < 10; i++, now += TimeSpan.FromSeconds(1))
        {
            throttle.Record(1, 500 * Ms, now);
            throttle.Record(2, Ms, now);
        }
        Assert.Equal(throttle.MaximumInterval, throttle.GetInterval(1));
        Assert.Equal(throttle.BaseInterval, throttle.GetInterval(2));
        Assert.True(throttle.ShouldApply(2, now));

        // An entity no longer shown is forgotten
        throttle.Keep([2]);
        Assert.False(throttle.IsThrottled);
        throttle.Reset();
        Assert.Equal(throttle.BaseInterval, throttle.LongestInterval);
    }
}
