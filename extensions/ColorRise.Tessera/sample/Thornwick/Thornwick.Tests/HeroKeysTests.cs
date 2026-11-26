using ColorRise.Tessera;

namespace Thornwick.Tests;

/// <summary>
/// The side the hero's keys ask for.
/// </summary>
public sealed class HeroKeysTests
{
    [Theory]
    [InlineData(true, false, false, false, FlipbookDirection.Left)]
    [InlineData(false, true, false, false, FlipbookDirection.Right)]
    [InlineData(false, false, true, false, FlipbookDirection.Up)]
    [InlineData(false, false, false, true, FlipbookDirection.Down)]
    [InlineData(false, false, false, false, FlipbookDirection.None)]
    [InlineData(true, true, false, false, FlipbookDirection.None)]
    [InlineData(true, true, true, false, FlipbookDirection.Up)]
    [InlineData(true, false, true, false, FlipbookDirection.Left)]
    [InlineData(false, false, true, true, FlipbookDirection.None)]
    [InlineData(true, true, true, true, FlipbookDirection.None)]
    public void The_keys_ask_for_one_side(bool left, bool right, bool up, bool down, FlipbookDirection side)
        => Assert.Equal(side, HeroKeys.Wanted(left, right, up, down));
}
